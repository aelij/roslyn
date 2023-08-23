﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.LanguageService;
using Microsoft.CodeAnalysis.Shared.Collections;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Shared.Utilities;

namespace Microsoft.CodeAnalysis.ConvertNumericLiteral;

internal abstract class AbstractConvertNumericLiteralCodeRefactoringProvider<TNumericLiteralExpression>
    : CodeRefactoringProvider
    where TNumericLiteralExpression : SyntaxNode
{
    protected abstract (string hexPrefix, string binaryPrefix) GetNumericLiteralPrefixes();

    /// <summary>
    /// Converting numbers is a fairly uncommon task.  Put these at the end of the list after more relevant
    /// refactorings.
    /// </summary>
    protected override CodeActionRequestPriority ComputeRequestPriority()
        => CodeActionRequestPriority.Low;

    public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var (document, _, cancellationToken) = context;
        var numericToken = await GetNumericTokenAsync(context).ConfigureAwait(false);

        if (numericToken == default || numericToken.ContainsDiagnostics)
            return;

        var syntaxNode = numericToken.GetRequiredParent();
        var semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        var symbol = semanticModel.GetTypeInfo(syntaxNode, cancellationToken).Type;
        if (symbol == null)
            return;

        if (!IsIntegral(symbol.SpecialType))
            return;

        var valueOpt = semanticModel.GetConstantValue(syntaxNode);
        if (!valueOpt.HasValue)
            return;

        var root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        var value = IntegerUtilities.ToInt64(valueOpt.Value);
        var numericText = numericToken.ToString();
        var (hexPrefix, binaryPrefix) = GetNumericLiteralPrefixes();
        var (prefix, number, suffix) = GetNumericLiteralParts(numericText, hexPrefix, binaryPrefix);
        var kind = string.IsNullOrEmpty(prefix) ? NumericKind.Decimal
            : prefix.Equals(hexPrefix, StringComparison.OrdinalIgnoreCase) ? NumericKind.Hexadecimal
            : prefix.Equals(binaryPrefix, StringComparison.OrdinalIgnoreCase) ? NumericKind.Binary
            : NumericKind.Unknown;

        if (kind == NumericKind.Unknown)
            return;

        using var result = TemporaryArray<CodeAction>.Empty;

        if (kind != NumericKind.Decimal)
            result.Add(CreateCodeAction(value.ToString(), FeaturesResources.Convert_to_decimal));

        if (kind != NumericKind.Binary)
            result.Add(CreateCodeAction(binaryPrefix + Convert.ToString(value, toBase: 2), FeaturesResources.Convert_to_binary));

        if (kind != NumericKind.Hexadecimal)
            result.Add(CreateCodeAction(hexPrefix + value.ToString("X"), FeaturesResources.Convert_to_hex));

        const string DigitSeparator = "_";
        if (numericText.Contains(DigitSeparator))
        {
            result.Add(CreateCodeAction(prefix + number.Replace(DigitSeparator, string.Empty), FeaturesResources.Remove_separators));
        }
        else
        {
            result.AsRef().AddIfNotNull(kind switch
            {
                NumericKind.Decimal when number.Length > 3 => CreateCodeAction(AddSeparators(number, interval: 3), FeaturesResources.Separate_thousands),
                NumericKind.Hexadecimal when number.Length > 4 => CreateCodeAction(hexPrefix + AddSeparators(number, interval: 4), FeaturesResources.Separate_words),
                NumericKind.Binary when number.Length > 4 => CreateCodeAction(binaryPrefix + AddSeparators(number, interval: 4), FeaturesResources.Separate_nibbles),
                _ => null,
            });
        }

        if (result.Count == 1)
        {
            context.RegisterRefactoring(result[0]);
        }
        else if (result.Count > 1)
        {
            context.RegisterRefactoring(CodeAction.Create(
                FeaturesResources.Convert_number,
                result.ToImmutableAndClear(),
                isInlinable: true));
        }

        CodeAction CreateCodeAction(string text, string title)
            => CodeAction.Create(title, c => ReplaceTokenAsync(document, root, numericToken, value, text, suffix), title);
    }

    private static Task<Document> ReplaceTokenAsync(Document document, SyntaxNode root, SyntaxToken numericToken, long value, string text, string suffix)
    {
        var generator = SyntaxGenerator.GetGenerator(document);
        var updatedToken = generator.NumericLiteralToken(text + suffix, (ulong)value)
            .WithTriviaFrom(numericToken);
        var updatedRoot = root.ReplaceToken(numericToken, updatedToken);
        return Task.FromResult(document.WithSyntaxRoot(updatedRoot));
    }

    internal virtual async Task<SyntaxToken> GetNumericTokenAsync(CodeRefactoringContext context)
    {
        var syntaxFacts = context.Document.GetRequiredLanguageService<ISyntaxFactsService>();

        var literalNode = await context.TryGetRelevantNodeAsync<TNumericLiteralExpression>().ConfigureAwait(false);
        var numericLiteralExpressionNode = syntaxFacts.IsNumericLiteralExpression(literalNode)
            ? literalNode
            : null;

        return numericLiteralExpressionNode != null
            ? numericLiteralExpressionNode.GetFirstToken()    // We know that TNumericLiteralExpression has always only one token: NumericLiteralToken
            : default;
    }

    private static (string prefix, string number, string suffix) GetNumericLiteralParts(string numericText, string hexPrefix, string binaryPrefix)
    {
        // Match literal text and extract out base prefix, type suffix and the number itself.
        var groups = Regex.Match(numericText, $"({hexPrefix}|{binaryPrefix})?([_0-9a-f]+)(.*)", RegexOptions.IgnoreCase).Groups;
        return (prefix: groups[1].Value, number: groups[2].Value, suffix: groups[3].Value);
    }

    private static string AddSeparators(string numericText, int interval)
    {
        // Insert digit separators in the given interval.
        var result = Regex.Replace(numericText, $"(.{{{interval}}})", "_$1", RegexOptions.RightToLeft);
        // Fix for the case "0x_1111" that is not supported yet.
        return result[0] == '_' ? result[1..] : result;
    }

    private static bool IsIntegral(SpecialType specialType)
    {
        switch (specialType)
        {
            case SpecialType.System_Byte:
            case SpecialType.System_SByte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
                return true;

            default:
                return false;
        }
    }

    private enum NumericKind { Unknown, Decimal, Binary, Hexadecimal }
}
