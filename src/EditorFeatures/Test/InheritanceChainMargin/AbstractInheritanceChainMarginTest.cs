﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media.TextFormatting;
using ICSharpCode.Decompiler.Metadata;
using Microsoft.CodeAnalysis.Editor.InheritanceMargin;
using Microsoft.CodeAnalysis.Editor.Tagging;
using Microsoft.CodeAnalysis.Editor.UnitTests.Workspaces;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.InheritanceMargin;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Test.Utilities;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Roslyn.Test.Utilities;
using Roslyn.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.UnitTests.InheritanceChainMargin
{
    [Trait(Traits.Feature, Traits.Features.InheritanceChainMargin)]
    public abstract class AbstractInheritanceChainMarginTest
    {
        protected readonly string BaseType = nameof(BaseType);
        protected readonly string SubType = nameof(SubType);
        protected readonly string Overriding = nameof(Overriding);
        protected readonly string Overriden = nameof(Overriden);
        protected readonly string Implementing = nameof(Implementing);
        protected readonly string Implemented = nameof(Implemented);
        protected readonly string ImplementingAndOverriden = nameof(ImplementingAndOverriden);
        protected readonly string ImplementingAndOverriding = nameof(ImplementingAndOverriding);

        public Task VerifyInDifferentFileAsync(
            string membersMarkup,
            string targetsMarkup,
            bool testInSingleProject)
        {

        }

        public async Task VerifyInSameFileAsync(string markup, string languageName)
        {
            TestFileMarkupParser.GetPositionsAndSpans(
                markup,
                out var cleanMarkup,
                out var carets,
                out var selectedSpans);
            var workspaceFile = $@"
<Workspace>
   <Project Language=""{languageName}"" CommonReferences=""true"">
       <Document>
            {markup}
       </Document>
   </Project>
</Workspace>";

            using var testWorkspace = TestWorkspace.Create(
                workspaceFile,
                composition: EditorTestCompositions.EditorFeaturesWpf);

            var taggerProvider = testWorkspace.ExportProvider.GetExportedValue<InheritanceChainMarginTaggerProvider>();
            var testAccessor = taggerProvider.GetTestAccessor();
            var testHostDocument = testWorkspace.Documents.Single();
            var document = testWorkspace.CurrentSolution.GetRequiredDocument(testHostDocument.Id);

            var context = new TaggerContext<InheritanceMarginTag>(document, testHostDocument.GetTextView().TextSnapshot);
            await testAccessor.ProduceTagsAsync(context).ConfigureAwait(false);
            var tagSpans = context.tagSpans.ToImmutableArray();
        }

        private async Task VerifyTagAsync(
            Document document,
            ImmutableDictionary<string, ImmutableArray<TextSpan>> selectedSpan,
            ImmutableArray<ITagSpan<InheritanceMarginTag>> tagSpans)
        {
            var x = @"
interface {|target1 IBar|} { }
{|margin, implemented, A target1=IBar, |}class A : IBar { } ";
            var sourceText = await document.GetTextAsync().ConfigureAwait(false);
            var allExpectedMargins = selectedSpan
                .Where(kvp => kvp.Key.StartsWith("margin"))
                .ToDictionary(
                    keySelector: kvp => kvp.Key,
                    elementSelector: kvp => kvp.Value[0]);

            var targetToSpans = selectedSpan
                .Where(kvp => kvp.Key.StartsWith("target"))
                .ToImmutableDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.SelectAsArray(span => new DocumentSpan(document, span)));

            var allExpectedMarin = allExpectedMargins
                .SelectAsArray(kvp => ParseTestLineMargin(kvp.Key, kvp.Value, sourceText, targetToSpans));
        }

        private void VerifyTestLineMargin(TestLineMargin expectedMargin, ITagSpan<InheritanceMarginTag> actualTaggedSpan)
        {
            var snapshot = actualTaggedSpan.Span.Snapshot;
            var span = actualTaggedSpan.Span;
            var lineOfStart = snapshot.GetLineNumberFromPosition(span.Start);
            var lineOfEnd = snapshot.GetLineNumberFromPosition(span.End);
            // The whole line should be tagged.
            Assert.Equal(expectedMargin.LineNumber, lineOfStart);
            Assert.Equal(expectedMargin.LineNumber, lineOfEnd);

            var tag = actualTaggedSpan.Tag;
            Assert.Equal(expectedMargin.Moniker, tag.Moniker.ToString());
            Assert.Equal(expectedMargin.Members.Length, tag.Members.Length);
            for (int i = 0; i < expectedMargin.Members.Length; i++)
            {
                var expectedMember = expectedMargin.Members[i];
                var actualMember = tag.Members[i];
                Assert.Equal(expectedMember.MemberName, actualMember.DisplayContent);
                Assert.Equal(expectedMember.Targets.Length, actualMember.Targets.Length);
                for (int j = 0; j < expectedMember.Targets.Length; j++)
                {
                    var expectedTarget = expectedMember.Targets[j];
                    var actualTarget = actualMember.Targets[j];
                    Assert.Equal(expectedTarget.TargetName, actualTarget.Name);
                    Assert.Equal(expectedTarget.Definitions, actualTarget.DefinitionItems.SelectMany(d => d.SourceSpans));
                }
            }
        }

        private static TestLineMargin ParseTestLineMargin(
            string marginText,
            TextSpan marginSpan,
            SourceText sourceText,
            ImmutableDictionary<string, ImmutableArray<DocumentSpan>> targetIdToDocumentSpans)
        {
            var marginTextGroup = marginText.Split(',')
                .SelectAsArray(text => text.Trim());
            var lineNumber = sourceText.Lines.GetLineFromPosition(marginSpan.Start).LineNumber;

            var moniker = marginTextGroup[1];
            var memberToTargets = marginTextGroup
                    .Skip(1)
                    .ToDictionary(
                        keySelector: text => text.Split(' ').First().Trim(),
                        elementSelector: text => text.Split(' ')
                            .Skip(1)
                            .SelectAsArray(targetAndName => (TargetId: targetAndName.Substring(0, targetAndName.IndexOf("=", StringComparison.Ordinal)), Name: targetAndName.Substring(targetAndName.IndexOf("=", StringComparison.Ordinal) + 1))));
            using var _ = PooledObjects.ArrayBuilder<TestMemberTag>.GetInstance(out var builder);
            foreach (var (member, targets) in memberToTargets)
            {
                var testTargetTags = targets
                    .SelectAsArray(target => new TestTargetTag(target.Name, targetIdToDocumentSpans[target.TargetId]));
                var testMemberTag = new TestMemberTag(member, testTargetTags);
                builder.Add(testMemberTag);
            }

            return new TestLineMargin(moniker, lineNumber, builder.ToImmutableArray());
        }

        private class TestLineMargin
        {
            public readonly string Moniker;
            public readonly int LineNumber;
            public readonly ImmutableArray<TestMemberTag> Members;

            public TestLineMargin(string moniker, int lineNumber, ImmutableArray<TestMemberTag> members)
            {
                Moniker = moniker;
                LineNumber = lineNumber;
                Members = members;
            }
        }

        private class TestMemberTag
        {
            public readonly string MemberName;
            public readonly ImmutableArray<TestTargetTag> Targets;

            public TestMemberTag(string memberName, ImmutableArray<TestTargetTag> targets)
            {
                MemberName = memberName;
                Targets = targets;
            }
        }

        private class TestTargetTag
        {
            public readonly string TargetName;
            public readonly ImmutableArray<DocumentSpan> Definitions;

            public TestTargetTag(string targetName, ImmutableArray<DocumentSpan> definitions)
            {
                TargetName = targetName;
                Definitions = definitions;
            }
        }

    }
}
