﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Roslynator.CSharp.Syntax;

namespace Roslynator.CSharp.Analysis
{
    internal static class OptimizeMethodCallAnalysis
    {
        public static void OptimizeStringCompare(SyntaxNodeAnalysisContext context, in SimpleMemberInvocationExpressionInfo invocationInfo)
        {
            InvocationExpressionSyntax invocationExpression = invocationInfo.InvocationExpression;

            ISymbol symbol = context.SemanticModel.GetSymbol(invocationExpression, context.CancellationToken);

            if (symbol?.Kind != SymbolKind.Method)
                return;

            if (symbol.ContainingType?.SpecialType != SpecialType.System_String)
                return;

            var methodSymbol = (IMethodSymbol)symbol;

            if (!SymbolUtility.IsPublicStaticNonGeneric(methodSymbol, "Compare"))
                return;

            ImmutableArray<IParameterSymbol> parameters = methodSymbol.Parameters;

            if (parameters.Length != 3)
                return;

            if (parameters[0].Type.SpecialType != SpecialType.System_String)
                return;

            if (parameters[1].Type.SpecialType != SpecialType.System_String)
                return;

            if (!parameters[2].Type.HasMetadataName(MetadataNames.System_StringComparison))
                return;

            SyntaxNode node = invocationExpression.WalkUpParentheses();

            if (node.IsParentKind(SyntaxKind.EqualsExpression))
            {
                var equalsExpression = (BinaryExpressionSyntax)node.Parent;

                ExpressionSyntax other = (equalsExpression.Left == node)
                    ? equalsExpression.Right
                    : equalsExpression.Left;

                if (other.WalkDownParentheses().IsNumericLiteralExpression("0"))
                {
                    DiagnosticHelpers.ReportDiagnostic(context, DiagnosticDescriptors.OptimizeMethodCall, equalsExpression, "string.Compare");
                    return;
                }
            }

            DiagnosticHelpers.ReportDiagnostic(context, DiagnosticDescriptors.OptimizeMethodCall, invocationExpression, "string.Compare");
        }

        public static void OptimizeDebugAssert(SyntaxNodeAnalysisContext context, in SimpleMemberInvocationExpressionInfo invocationInfo)
        {
            InvocationExpressionSyntax invocationExpression = invocationInfo.InvocationExpression;

            if (invocationExpression.SpanContainsDirectives())
                return;

            SeparatedSyntaxList<ArgumentSyntax> arguments = invocationInfo.Arguments;

            if (arguments.Count < 1 || arguments.Count > 3)
                return;

            if (arguments[0].Expression?.Kind() != SyntaxKind.FalseLiteralExpression)
                return;

            IMethodSymbol methodSymbol = context.SemanticModel.GetMethodSymbol(invocationExpression, context.CancellationToken);

            if (!SymbolUtility.IsPublicStaticNonGeneric(methodSymbol, "Assert"))
                return;

            if (methodSymbol.ContainingType?.HasMetadataName(MetadataNames.System_Diagnostics_Debug) != true)
                return;

            if (!methodSymbol.ReturnsVoid)
                return;

            ImmutableArray<IParameterSymbol> assertParameters = methodSymbol.Parameters;

            if (assertParameters[0].Type.SpecialType != SpecialType.System_Boolean)
                return;

            int length = assertParameters.Length;

            for (int i = 1; i < length; i++)
            {
                if (assertParameters[i].Type.SpecialType != SpecialType.System_String)
                    return;
            }

            if (!ContainsFailMethod())
                return;

            DiagnosticHelpers.ReportDiagnostic(context, DiagnosticDescriptors.OptimizeMethodCall, invocationExpression, "Debug.Assert");

            bool ContainsFailMethod()
            {
                foreach (ISymbol symbol in methodSymbol.ContainingType.GetMembers("Fail"))
                {
                    if (symbol is IMethodSymbol failMethodSymbol
                        && SymbolUtility.IsPublicStaticNonGeneric(failMethodSymbol)
                        && failMethodSymbol.ReturnsVoid)
                    {
                        ImmutableArray<IParameterSymbol> failParameters = failMethodSymbol.Parameters;

                        if (failParameters.Length == ((length == 1) ? 1 : length - 1)
                            && failParameters.All(f => f.Type.SpecialType == SpecialType.System_String))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
        }

        public static void OptimizeStringJoin(SyntaxNodeAnalysisContext context, in SimpleMemberInvocationExpressionInfo invocationInfo)
        {
            InvocationExpressionSyntax invocationExpression = invocationInfo.InvocationExpression;

            ArgumentSyntax firstArgument = invocationInfo.Arguments.FirstOrDefault();

            if (firstArgument == null)
                return;

            if (invocationInfo.MemberAccessExpression.SpanOrTrailingTriviaContainsDirectives()
                || invocationInfo.ArgumentList.OpenParenToken.ContainsDirectives
                || firstArgument.ContainsDirectives)
            {
                return;
            }

            SemanticModel semanticModel = context.SemanticModel;
            CancellationToken cancellationToken = context.CancellationToken;

            IMethodSymbol methodSymbol = semanticModel.GetMethodSymbol(invocationExpression, cancellationToken);

            if (!SymbolUtility.IsPublicStaticNonGeneric(methodSymbol, "Join"))
                return;

            if (methodSymbol.ContainingType?.SpecialType != SpecialType.System_String)
                return;

            if (!methodSymbol.IsReturnType(SpecialType.System_String))
                return;

            ImmutableArray<IParameterSymbol> parameters = methodSymbol.Parameters;

            if (parameters.Length != 2)
                return;

            if (parameters[0].Type.SpecialType != SpecialType.System_String)
                return;

            if (!parameters[1].IsParameterArrayOf(SpecialType.System_String, SpecialType.System_Object)
                && !parameters[1].Type.OriginalDefinition.IsIEnumerableOfT())
            {
                return;
            }

            if (firstArgument.Expression == null)
                return;

            if (!CSharpUtility.IsEmptyStringExpression(firstArgument.Expression, semanticModel, cancellationToken))
                return;

            DiagnosticHelpers.ReportDiagnostic(context, DiagnosticDescriptors.OptimizeMethodCall, invocationExpression, "string.Join");
        }
    }
}
