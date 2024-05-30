﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.BrokeredServices;
using Microsoft.CodeAnalysis.Contracts.EditAndContinue;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.ErrorReporting;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.EditAndContinue;

[Export(typeof(IManagedHotReloadLanguageService))]
[method: ImportingConstructor]
[method: Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
internal sealed partial class ManagedHotReloadLanguageService(
    IServiceBrokerProvider serviceBrokerProvider,
    IEditAndContinueService encService,
    SolutionSnapshotRegistry solutionSnapshotRegistry) : IManagedHotReloadLanguageService
{
    private sealed class PdbMatchingSourceTextProvider : IPdbMatchingSourceTextProvider
    {
        public static readonly PdbMatchingSourceTextProvider Instance = new();

        // Returning null will check the file on disk:
        public ValueTask<string?> TryGetMatchingSourceTextAsync(string filePath, ImmutableArray<byte> requiredChecksum, SourceHashAlgorithm checksumAlgorithm, CancellationToken cancellationToken)
            => ValueTaskFactory.FromResult<string?>(null);
    }

    private static readonly ActiveStatementSpanProvider s_emptyActiveStatementProvider =
        (_, _, _) => ValueTaskFactory.FromResult(ImmutableArray<ActiveStatementSpan>.Empty);

    private readonly ManagedHotReloadServiceProxy _debuggerService = new(serviceBrokerProvider.ServiceBroker);
    private readonly SolutionSnapshotProviderProxy _solutionSnapshotProvider = new(serviceBrokerProvider.ServiceBroker);

    private bool _disabled;
    private DebuggingSessionId? _debuggingSession;
    private Solution? _committedDesignTimeSolution;
    private Solution? _pendingUpdatedDesignTimeSolution;

    private void Disable()
    {
        _disabled = true;
        _debuggingSession = null;
        _committedDesignTimeSolution = null;
        _pendingUpdatedDesignTimeSolution = null;
        solutionSnapshotRegistry.Clear();
    }

    private async ValueTask<Solution> GetCurrentDesignTimeSolutionAsync(CancellationToken cancellationToken)
    {
        // First, calls to the client to get the current snapshot id.
        // The client service calls the LSP client, which sends message to the LSP server, which in turn calls back to RegisterSolutionSnapshot.
        // Once complete the snapshot should be registered.
        var id = await _solutionSnapshotProvider.RegisterSolutionSnapshotAsync(cancellationToken).ConfigureAwait(false);

        return solutionSnapshotRegistry.GetRegisteredSolutionSnapshot(id);
    }

    private static Solution GetCurrentCompileTimeSolution(Solution currentDesignTimeSolution)
        => currentDesignTimeSolution.Services.GetRequiredService<ICompileTimeSolutionProvider>().GetCompileTimeSolution(currentDesignTimeSolution);

    public async ValueTask StartSessionAsync(CancellationToken cancellationToken)
    {
        if (_disabled)
        {
            return;
        }

        try
        {
            var currentDesignTimeSolution = await GetCurrentDesignTimeSolutionAsync(cancellationToken).ConfigureAwait(false);
            _committedDesignTimeSolution = currentDesignTimeSolution;
            var compileTimeSolution = GetCurrentCompileTimeSolution(currentDesignTimeSolution);

            // TODO: use remote proxy once we transition to pull diagnostics
            _debuggingSession = await encService.StartDebuggingSessionAsync(
                compileTimeSolution,
                _debuggerService,
                PdbMatchingSourceTextProvider.Instance,
                captureMatchingDocuments: [],
                captureAllMatchingDocuments: false,
                reportDiagnostics: true,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (FatalError.ReportAndCatchUnlessCanceled(e, cancellationToken))
        {
            // the service failed, error has been reported - disable further operations
            Disable();
        }
    }

    private ValueTask BreakStateOrCapabilitiesChangedAsync(bool? inBreakState, CancellationToken cancellationToken)
    {
        if (_disabled)
        {
            return ValueTaskFactory.CompletedTask;
        }

        try
        {
            Contract.ThrowIfNull(_debuggingSession);
            encService.BreakStateOrCapabilitiesChanged(_debuggingSession.Value, inBreakState);
        }
        catch (Exception e) when (FatalError.ReportAndCatchUnlessCanceled(e, cancellationToken))
        {
            Disable();
        }

        return ValueTaskFactory.CompletedTask;
    }

    public ValueTask EnterBreakStateAsync(CancellationToken cancellationToken)
        => BreakStateOrCapabilitiesChangedAsync(inBreakState: true, cancellationToken);

    public ValueTask ExitBreakStateAsync(CancellationToken cancellationToken)
        => BreakStateOrCapabilitiesChangedAsync(inBreakState: false, cancellationToken);

    public ValueTask OnCapabilitiesChangedAsync(CancellationToken cancellationToken)
        => BreakStateOrCapabilitiesChangedAsync(inBreakState: null, cancellationToken);

    public ValueTask CommitUpdatesAsync(CancellationToken cancellationToken)
    {
        if (_disabled)
        {
            return ValueTaskFactory.CompletedTask;
        }

        try
        {
            Contract.ThrowIfNull(_debuggingSession);
            var committedDesignTimeSolution = Interlocked.Exchange(ref _pendingUpdatedDesignTimeSolution, null);
            Contract.ThrowIfNull(committedDesignTimeSolution);

            _committedDesignTimeSolution = committedDesignTimeSolution;

            encService.CommitSolutionUpdate(_debuggingSession.Value);
        }
        catch (Exception e) when (FatalError.ReportAndCatchUnlessCanceled(e, cancellationToken))
        {
            Disable();
        }

        return ValueTaskFactory.CompletedTask;
    }

    public ValueTask DiscardUpdatesAsync(CancellationToken cancellationToken)
    {
        if (_disabled)
        {
            return ValueTaskFactory.CompletedTask;
        }

        try
        {
            Contract.ThrowIfNull(_debuggingSession);
            Contract.ThrowIfNull(Interlocked.Exchange(ref _pendingUpdatedDesignTimeSolution, null));

            encService.DiscardSolutionUpdate(_debuggingSession.Value);
        }
        catch (Exception e) when (FatalError.ReportAndCatchUnlessCanceled(e, cancellationToken))
        {
            Disable();
        }

        return ValueTaskFactory.CompletedTask;
    }

    public ValueTask EndSessionAsync(CancellationToken cancellationToken)
    {
        if (_disabled)
        {
            return ValueTaskFactory.CompletedTask;
        }

        try
        {
            Contract.ThrowIfNull(_debuggingSession);

            encService.EndDebuggingSession(_debuggingSession.Value);

            _debuggingSession = null;
            _committedDesignTimeSolution = null;
            _pendingUpdatedDesignTimeSolution = null;
        }
        catch (Exception e) when (FatalError.ReportAndCatchUnlessCanceled(e, cancellationToken))
        {
            Disable();
        }

        return ValueTaskFactory.CompletedTask;
    }

    /// <summary>
    /// Returns true if any changes have been made to the source since the last changes had been applied.
    /// For performance reasons it only implements a heuristic and may return both false positives and false negatives.
    /// If the result is a false negative the debugger will not apply the changes unless the user explicitly triggers apply change command.
    /// The background diagnostic analysis will still report rude edits for these ignored changes. It may also happen that these rude edits 
    /// will disappear once the debuggee is resumed - if they are caused by presence of active statements around the change.
    /// If the result is a false positive the debugger attempts to apply the changes, which will result in a delay but will correctly end up
    /// with no actual deltas to be applied.
    /// 
    /// If <paramref name="sourceFilePath"/> is specified checks for changes only in a document of the given path.
    /// This is not supported (returns false) for source-generated documents.
    /// </summary>
    public async ValueTask<bool> HasChangesAsync(string? sourceFilePath, CancellationToken cancellationToken)
    {
        try
        {
            var debuggingSession = _debuggingSession;
            if (debuggingSession == null)
            {
                return false;
            }

            Contract.ThrowIfNull(_committedDesignTimeSolution);
            var oldSolution = _committedDesignTimeSolution;

            var newSolution = await GetCurrentDesignTimeSolutionAsync(cancellationToken).ConfigureAwait(false);

            return (sourceFilePath != null)
                ? await EditSession.HasChangesAsync(oldSolution, newSolution, sourceFilePath, cancellationToken).ConfigureAwait(false)
                : await EditSession.HasChangesAsync(oldSolution, newSolution, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (FatalError.ReportAndCatchUnlessCanceled(e, cancellationToken))
        {
            return true;
        }
    }

    public async ValueTask<ManagedHotReloadUpdates> GetUpdatesAsync(CancellationToken cancellationToken)
    {
        if (_disabled)
        {
            return new ManagedHotReloadUpdates([], []);
        }

        try
        {
            Contract.ThrowIfNull(_debuggingSession);

            var designTimeSolution = await GetCurrentDesignTimeSolutionAsync(cancellationToken).ConfigureAwait(false);
            var solution = GetCurrentCompileTimeSolution(designTimeSolution);

            ModuleUpdates moduleUpdates;
            ImmutableArray<DiagnosticData> diagnosticData;
            ImmutableArray<(DocumentId DocumentId, ImmutableArray<RudeEditDiagnostic> Diagnostics)> rudeEdits;
            DiagnosticData? syntaxError;

            try
            {
                var results = await encService.EmitSolutionUpdateAsync(_debuggingSession.Value, solution, s_emptyActiveStatementProvider, cancellationToken).ConfigureAwait(false);

                moduleUpdates = results.ModuleUpdates;
                diagnosticData = results.Diagnostics.ToDiagnosticData(solution);
                rudeEdits = results.RudeEdits;
                syntaxError = results.GetSyntaxErrorData(solution);
            }
            catch (Exception e) when (FatalError.ReportAndCatchUnlessCanceled(e, cancellationToken))
            {
                var descriptor = EditAndContinueDiagnosticDescriptors.GetDescriptor(RudeEditKind.InternalError);

                var diagnostic = Diagnostic.Create(
                    descriptor,
                    Location.None,
                    string.Format(descriptor.MessageFormat.ToString(), "", e.Message));

                diagnosticData = [DiagnosticData.Create(designTimeSolution, diagnostic, project: null)];
                rudeEdits = [];
                moduleUpdates = new ModuleUpdates(ModuleUpdateStatus.RestartRequired, []);
                syntaxError = null;
            }

            // Only store the solution if we have any changes to apply, otherwise CommitUpdatesAsync/DiscardUpdatesAsync won't be called.
            if (moduleUpdates.Status == ModuleUpdateStatus.Ready)
            {
                _pendingUpdatedDesignTimeSolution = designTimeSolution;
            }

            var diagnostics = await EmitSolutionUpdateResults.GetAllDiagnosticsAsync(solution, diagnosticData, rudeEdits, syntaxError, moduleUpdates.Status, cancellationToken).ConfigureAwait(false);
            return new ManagedHotReloadUpdates(moduleUpdates.Updates, diagnostics);
        }
        catch (Exception e) when (FatalError.ReportAndCatchUnlessCanceled(e, cancellationToken))
        {
            Disable();
            return new ManagedHotReloadUpdates([], []);
        }
    }
}
