﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.SolutionCrawler;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Diagnostics
{
    [Shared]
    [ExportIncrementalAnalyzerProvider(WellKnownSolutionCrawlerAnalyzers.Diagnostic, workspaceKinds: [])]
    internal partial class DefaultDiagnosticAnalyzerService : IIncrementalAnalyzerProvider, IDiagnosticUpdateSource
    {
        private readonly DiagnosticAnalyzerInfoCache _analyzerInfoCache = new();
        private readonly IGlobalOptionService _globalOptions;

        public event EventHandler<ImmutableArray<DiagnosticsUpdatedArgs>>? DiagnosticsUpdated;

        [ImportingConstructor]
        [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
        public DefaultDiagnosticAnalyzerService(
            IDiagnosticUpdateSourceRegistrationService registrationService,
            IGlobalOptionService globalOptions)
        {
            _globalOptions = globalOptions;
            registrationService.Register(this);
            _globalOptions = globalOptions;
        }

        public IIncrementalAnalyzer CreateIncrementalAnalyzer(Workspace workspace)
        {
            if (_globalOptions.IsLspPullDiagnostics())
            {
                // We rely on LSP to query us for diagnostics when things have changed and poll us for changes that might
                // have happened to the project or closed files outside of VS.
                return NoOpIncrementalAnalyzer.Instance;
            }

            return new DefaultDiagnosticIncrementalAnalyzer(this, workspace);
        }

        // this only support push model, pull model will be provided by DiagnosticService by caching everything this one pushed
        public bool SupportGetDiagnostics => false;

        public event EventHandler DiagnosticsCleared { add { } remove { } }

        public ValueTask<ImmutableArray<DiagnosticData>> GetDiagnosticsAsync(Workspace workspace, ProjectId? projectId, DocumentId? documentId, object? id, bool includeSuppressedDiagnostics, CancellationToken cancellationToken)
        {
            // pull model not supported
            return new ValueTask<ImmutableArray<DiagnosticData>>([]);
        }

        internal void RaiseDiagnosticsUpdated(ImmutableArray<DiagnosticsUpdatedArgs> state)
            => DiagnosticsUpdated?.Invoke(this, state);

        private sealed class DefaultDiagnosticIncrementalAnalyzer : IIncrementalAnalyzer
        {
            private readonly DefaultDiagnosticAnalyzerService _service;
            private readonly Workspace _workspace;
            private readonly InProcOrRemoteHostAnalyzerRunner _diagnosticAnalyzerRunner;

            public DefaultDiagnosticIncrementalAnalyzer(DefaultDiagnosticAnalyzerService service, Workspace workspace)
            {
                Contract.ThrowIfNull(workspace.Kind);

                _service = service;
                _workspace = workspace;
                _diagnosticAnalyzerRunner = new InProcOrRemoteHostAnalyzerRunner(service._analyzerInfoCache);
            }

            public void Shutdown()
            {
            }

            public Task AnalyzeSyntaxAsync(Document document, InvocationReasons reasons, CancellationToken cancellationToken)
                => AnalyzeSyntaxOrNonSourceDocumentAsync(document, cancellationToken);

            public Task AnalyzeNonSourceDocumentAsync(TextDocument textDocument, InvocationReasons reasons, CancellationToken cancellationToken)
                => AnalyzeSyntaxOrNonSourceDocumentAsync(textDocument, cancellationToken);

            private async Task AnalyzeSyntaxOrNonSourceDocumentAsync(TextDocument textDocument, CancellationToken cancellationToken)
            {
                Debug.Assert(textDocument.Project.Solution.Workspace == _workspace);

                // right now, there is no way to observe diagnostics for closed file.
                if (!_workspace.IsDocumentOpen(textDocument.Id))
                {
                    return;
                }

                await AnalyzeForKindAsync(textDocument, AnalysisKind.Syntax, cancellationToken).ConfigureAwait(false);
            }

            public async Task AnalyzeDocumentAsync(Document document, SyntaxNode bodyOpt, InvocationReasons reasons, CancellationToken cancellationToken)
            {
                Debug.Assert(document.Project.Solution.Workspace == _workspace);

                if (!IsSemanticAnalysisOn())
                {
                    return;
                }

                await AnalyzeForKindAsync(document, AnalysisKind.Semantic, cancellationToken).ConfigureAwait(false);

                bool IsSemanticAnalysisOn()
                {
                    // right now, there is no way to observe diagnostics for closed file.
                    if (!_workspace.IsDocumentOpen(document.Id))
                        return false;

                    // Misc and cloud workspaces never supports semantics.
                    if (_workspace.Kind is WorkspaceKind.MiscellaneousFiles or WorkspaceKind.CloudEnvironmentClientWorkspace)
                        return false;

                    return true;
                }
            }

            private async Task AnalyzeForKindAsync(TextDocument document, AnalysisKind kind, CancellationToken cancellationToken)
            {
                Contract.ThrowIfNull(_workspace.Kind);

                var diagnosticData = await GetDiagnosticsAsync(document, kind, cancellationToken).ConfigureAwait(false);

                // TODO: Consider raising these with a batching work queue to aggregate results from analyzers that
                // complete quickly.
                _service.RaiseDiagnosticsUpdated([DiagnosticsUpdatedArgs.DiagnosticsCreated(new DefaultUpdateArgsId(_workspace.Kind, kind, document.Id),
                    _workspace, document.Project.Solution, document.Project.Id, document.Id, diagnosticData)]);
            }

            /// <summary>
            /// Get diagnostics for the given document.
            /// 
            /// This is a simple API to get all diagnostics for the given document.
            /// 
            /// The intended audience for this API is for ones that pefer simplicity over performance such as document that belong to misc project.
            /// this doesn't cache nor use cache for anything. it will re-caculate new diagnostics every time for the given document.
            /// it will not persist any data on disk nor use OOP to calculate the data.
            /// 
            /// This should never be used when performance is a big concern. for such context, use much complex API from IDiagnosticAnalyzerService
            /// that provide all kinds of knobs/cache/persistency/OOP to get better perf over simplicity.
            /// </summary>
            private async Task<ImmutableArray<DiagnosticData>> GetDiagnosticsAsync(
               TextDocument document, AnalysisKind kind, CancellationToken cancellationToken)
            {
                var loadDiagnostic = await document.State.GetLoadDiagnosticAsync(cancellationToken).ConfigureAwait(false);
                if (loadDiagnostic != null)
                    return [DiagnosticData.Create(loadDiagnostic, document)];

                var project = document.Project;
                var analyzers = GetAnalyzers(project.Solution.SolutionState.Analyzers, project);
                if (analyzers.IsEmpty)
                    return [];

                var ideOptions = _service._globalOptions.GetIdeAnalyzerOptions(project);

                var compilationWithAnalyzers = await DocumentAnalysisExecutor.CreateCompilationWithAnalyzersAsync(
                    project, ideOptions, analyzers, includeSuppressedDiagnostics: false, cancellationToken).ConfigureAwait(false);

                var analysisScope = new DocumentAnalysisScope(document, span: null, analyzers, kind);
                var executor = new DocumentAnalysisExecutor(analysisScope, compilationWithAnalyzers, _diagnosticAnalyzerRunner, isExplicit: false, logPerformanceInfo: true);

                using var _ = ArrayBuilder<DiagnosticData>.GetInstance(out var builder);
                foreach (var analyzer in analyzers)
                    builder.AddRange(await executor.ComputeDiagnosticsAsync(analyzer, cancellationToken).ConfigureAwait(false));

                return builder.ToImmutable();
            }

            private static ImmutableArray<DiagnosticAnalyzer> GetAnalyzers(HostDiagnosticAnalyzers hostAnalyzers, Project project)
            {
                // C# or VB document that supports compiler
                var compilerAnalyzer = hostAnalyzers.GetCompilerDiagnosticAnalyzer(project.Language);
                if (compilerAnalyzer != null)
                {
                    return [compilerAnalyzer];
                }

                // document that doesn't support compiler diagnostics such as FSharp or TypeScript
                return hostAnalyzers.CreateDiagnosticAnalyzersPerReference(project).Values.SelectManyAsArray(v => v);
            }

            public Task RemoveDocumentAsync(DocumentId documentId, CancellationToken cancellationToken)
            {
                // a file is removed from a solution
                //
                // here syntax and semantic indicates type of errors not where it is originated from.
                // Option.Semantic or Option.ScriptSemantic indicates what kind of document we will produce semantic errors from.
                // Option.Semantic == true means we will generate semantic errors for all document type
                // Option.ScriptSemantic == true means we will generate semantic errors only for script document type
                // both of them at the end generates semantic errors
                RaiseEmptyDiagnosticUpdated(AnalysisKind.Syntax, documentId);
                RaiseEmptyDiagnosticUpdated(AnalysisKind.Semantic, documentId);
                return Task.CompletedTask;
            }

            public Task DocumentResetAsync(Document document, CancellationToken cancellationToken)
            {
                // no closed file diagnostic and file is not opened, remove any existing diagnostics
                return RemoveDocumentAsync(document.Id, cancellationToken);
            }

            public Task NonSourceDocumentResetAsync(TextDocument textDocument, CancellationToken cancellationToken)
            {
                // no closed file diagnostic and file is not opened, remove any existing diagnostics
                return RemoveDocumentAsync(textDocument.Id, cancellationToken);
            }

            public Task DocumentCloseAsync(Document document, CancellationToken cancellationToken)
                => DocumentResetAsync(document, cancellationToken);

            public Task NonSourceDocumentCloseAsync(TextDocument textDocument, CancellationToken cancellationToken)
                => NonSourceDocumentResetAsync(textDocument, cancellationToken);

            private void RaiseEmptyDiagnosticUpdated(AnalysisKind kind, DocumentId documentId)
            {
                Contract.ThrowIfNull(_workspace.Kind);

                // TODO: Consider raising these with a batching work queue to aggregate results from analyzers that
                // complete quickly.
                _service.RaiseDiagnosticsUpdated([DiagnosticsUpdatedArgs.DiagnosticsRemoved(
                    new DefaultUpdateArgsId(_workspace.Kind, kind, documentId), _workspace, null, documentId.ProjectId, documentId)]);
            }

            public Task AnalyzeProjectAsync(Project project, bool semanticsChanged, InvocationReasons reasons, CancellationToken cancellationToken)
                => Task.CompletedTask;

            public Task DocumentOpenAsync(Document document, CancellationToken cancellationToken)
                => Task.CompletedTask;

            public Task NonSourceDocumentOpenAsync(TextDocument textDocument, CancellationToken cancellationToken)
                => Task.CompletedTask;

            public Task ActiveDocumentSwitchedAsync(TextDocument document, CancellationToken cancellationToken)
                => Task.CompletedTask;

            public Task NewSolutionSnapshotAsync(Solution solution, CancellationToken cancellationToken)
                => Task.CompletedTask;

            public Task RemoveProjectAsync(ProjectId projectId, CancellationToken cancellationToken)
                => Task.CompletedTask;

            public void LogAnalyzerCountSummary()
            {
            }

            public int Priority => 1;

            private class DefaultUpdateArgsId : BuildToolId.Base<int, DocumentId>, ISupportLiveUpdate
            {
                private readonly string _workspaceKind;

                public DefaultUpdateArgsId(string workspaceKind, AnalysisKind kind, DocumentId documentId) : base((int)kind, documentId)
                    => _workspaceKind = workspaceKind;

                public override string BuildTool => PredefinedBuildTools.Live;

                public override bool Equals(object? obj)
                {
                    if (obj is not DefaultUpdateArgsId other)
                    {
                        return false;
                    }

                    return _workspaceKind == other._workspaceKind && base.Equals(obj);
                }

                public override int GetHashCode()
                    => Hash.Combine(_workspaceKind.GetHashCode(), base.GetHashCode());
            }
        }
    }
}
