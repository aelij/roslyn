﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Extensions.ContextQuery;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Utilities;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.CodeAnalysis.CSharp.Completion.KeywordRecommenders
{
    internal class ScopedKeywordRecommender : AbstractSyntacticSingleKeywordRecommender
    {
        public ScopedKeywordRecommender()
            : base(SyntaxKind.ScopedKeyword)
        {
        }

        protected override bool IsValidContext(int position, CSharpSyntaxContext context, CancellationToken cancellationToken)
        {
            var syntaxTree = context.SyntaxTree;
            return
                syntaxTree.IsParameterModifierContext(position, context.LeftToken, includeOperators: true, out _, out _) ||
                syntaxTree.IsAnonymousMethodParameterModifierContext(position, context.LeftToken) ||
                syntaxTree.IsPossibleLambdaParameterModifierContext(position, context.LeftToken, cancellationToken) ||
                IsValidScopedLocalContext(context);
        }

        private static bool IsValidScopedLocalContext(CSharpSyntaxContext context)
        {
            // scoped ref var x ...
            if (context.IsStatementContext || context.IsGlobalStatementContext)
            {
                return true;
            }

            var token = context.TargetToken;
            switch (token.Kind())
            {
                //     for (scoped ref var x ...
                case SyntaxKind.OpenParenToken:
                    var previous = token.GetPreviousToken(includeSkipped: true);
                    return previous.IsKind(SyntaxKind.ForKeyword);

                // M(out scoped ..)
                case SyntaxKind.OutKeyword:
                    return token.Parent is ArgumentSyntax;
            }

            return false;
        }

    }
}
