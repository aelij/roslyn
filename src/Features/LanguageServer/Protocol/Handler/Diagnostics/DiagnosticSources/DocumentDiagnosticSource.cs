﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Copilot;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.EditAndContinue;

namespace Microsoft.CodeAnalysis.LanguageServer.Handler.Diagnostics;

internal sealed class DocumentDiagnosticSource(DiagnosticKind diagnosticKind, TextDocument document)
    : AbstractDocumentDiagnosticSource<TextDocument>(document)
{
    /// <summary>
    /// This is a normal document source that represents live/fresh diagnostics that should supersede everything else.
    /// </summary>
    public override bool IsLiveSource()
        => true;

    public override async Task<ImmutableArray<DiagnosticData>> GetDiagnosticsAsync(
        IDiagnosticAnalyzerService diagnosticAnalyzerService, RequestContext context, CancellationToken cancellationToken)
    {
        // We call GetDiagnosticsForSpanAsync here instead of GetDiagnosticsForIdsAsync as it has faster perf
        // characteristics. GetDiagnosticsForIdsAsync runs analyzers against the entire compilation whereas
        // GetDiagnosticsForSpanAsync will only run analyzers against the request document.
        // Also ensure we pass in "includeSuppressedDiagnostics = true" for unnecessary suppressions to be reported.
        var allSpanDiagnostics = await diagnosticAnalyzerService.GetDiagnosticsForSpanAsync(
            Document, range: null, diagnosticKind, includeSuppressedDiagnostics: true, cancellationToken: cancellationToken).ConfigureAwait(false);

        // Add cached Copilot diagnostics when computing analyzer semantic diagnostics.
        // TODO: move to a separate diagnostic source. https://github.com/dotnet/roslyn/issues/72896
        if (diagnosticKind == DiagnosticKind.AnalyzerSemantic)
        {
            var copilotDiagnostics = await Document.GetCachedCopilotDiagnosticsAsync(span: null, cancellationToken).ConfigureAwait(false);
            allSpanDiagnostics = allSpanDiagnostics.AddRange(copilotDiagnostics);
        }

        // Drop the source suppressed diagnostics.
        // https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1824321 tracks
        // adding LSP support for returning source suppressed diagnostics.
        allSpanDiagnostics = allSpanDiagnostics.WhereAsArray(diagnostic => !diagnostic.IsSuppressed);

        return allSpanDiagnostics;
    }
}
