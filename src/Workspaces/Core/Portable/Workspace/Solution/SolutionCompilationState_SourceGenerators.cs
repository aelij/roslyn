﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Remote;
using Microsoft.CodeAnalysis.Shared.Collections;
using Microsoft.CodeAnalysis.SourceGeneration;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis;

using AnalyzerReferencesToSourceGenerators = ConditionalWeakTable<IReadOnlyList<AnalyzerReference>, SolutionCompilationState.SourceGeneratorMap>;

internal partial class SolutionCompilationState
{
    internal sealed record SourceGeneratorMap(
        ImmutableArray<ISourceGenerator> SourceGenerators,
        ImmutableDictionary<ISourceGenerator, AnalyzerReference> SourceGeneratorToAnalyzerReference);

    /// <summary>
    /// Cached mapping from language (only C#/VB since those are the only languages that support analyzers) to the lists
    /// of analyzer references (see <see cref="ProjectState.AnalyzerReferences"/>) to all the <see
    /// cref="ISourceGenerator"/>s produced by those references.  This should only be created and cached on the OOP side
    /// of things so that we don't cause source generators to be loaded (and fixed) within VS (which is .net framework
    /// only).
    /// </summary>
    private static readonly ImmutableArray<(string language, AnalyzerReferencesToSourceGenerators referencesToGenerators, AnalyzerReferencesToSourceGenerators.CreateValueCallback callback)> s_languageToAnalyzerReferencesToSourceGeneratorsMap =
    [
        (LanguageNames.CSharp, new(), (static rs => ComputeSourceGenerators(rs, LanguageNames.CSharp))),
        (LanguageNames.VisualBasic, new(), (static rs => ComputeSourceGenerators(rs, LanguageNames.VisualBasic))),
    ];

    private static ConditionalWeakTable<ProjectState, AsyncLazy<bool>> s_hasSourceGeneratorsMap = new();

    private static SourceGeneratorMap ComputeSourceGenerators(IReadOnlyList<AnalyzerReference> analyzerReferences, string language)
    {
        using var generators = TemporaryArray<ISourceGenerator>.Empty;
        var generatorToAnalyzerReference = ImmutableDictionary.CreateBuilder<ISourceGenerator, AnalyzerReference>();

        foreach (var reference in analyzerReferences)
        {
            foreach (var generator in reference.GetGenerators(language).Distinct())
            {
                generators.Add(generator);
                generatorToAnalyzerReference.Add(generator, reference);
            }
        }

        return new(generators.ToImmutableAndClear(), generatorToAnalyzerReference.ToImmutable());
    }

    /// <summary>
    /// This method should only be called in a .net core host like our out of process server.
    /// </summary>
    private static ImmutableArray<ISourceGenerator> GetSourceGenerators(ProjectState projectState)
    {
        var map = GetSourceGeneratorMap(projectState);
        return map is null ? [] : map.SourceGenerators;
    }

    /// <summary>
    /// This method should only be called in a .net core host like our out of process server.
    /// </summary>
    private static AnalyzerReference GetAnalyzerReference(ProjectState projectState, ISourceGenerator sourceGenerator)
    {
        var map = GetSourceGeneratorMap(projectState);
        Contract.ThrowIfNull(map);
        return map.SourceGeneratorToAnalyzerReference[sourceGenerator];
    }

    private static SourceGeneratorMap? GetSourceGeneratorMap(ProjectState projectState)
    {
        var tupleOpt = s_languageToAnalyzerReferencesToSourceGeneratorsMap.FirstOrNull(static (t, language) => t.language == language, projectState.Language);
        if (tupleOpt is null)
            return null;

        var tuple = tupleOpt.Value;
        return tuple.referencesToGenerators.GetValue(projectState.AnalyzerReferences, tuple.callback);
    }

    public static async Task<bool> HasSourceGeneratorsAsync(ProjectState projectState, CancellationToken cancellationToken)
    {
        var lazy = s_hasSourceGeneratorsMap.GetValue(
            projectState,
            static projectState => AsyncLazy.Create(cancellationToken => ComputeHasSourceGeneratorsAsync(projectState, cancellationToken)));

        return await lazy.GetValueAsync(cancellationToken).ConfigureAwait(false);

        static async Task<bool> ComputeHasSourceGeneratorsAsync(
            ProjectState projectState, CancellationToken cancellationToken)
        {
            var client = await RemoteHostClient.TryGetClientAsync(projectState.LanguageServices.SolutionServices, cancellationToken).ConfigureAwait(false);
            if (client is null)
            {
                // In proc, just load the generators and see if we have any.
                return GetSourceGenerators(projectState).Any();
            }

            // Out of process, call to the remote to figure this out.
            await client.TryInvokeAsync<IRemoteSourceGenerationService>(
                )
        }
    }
}
