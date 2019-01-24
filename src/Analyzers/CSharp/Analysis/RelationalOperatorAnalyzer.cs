﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Roslynator.CSharp.Syntax;

namespace Roslynator.CSharp.Analysis
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class RelationalOperatorAnalyzer : BaseDiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get
            {
                return ImmutableArray.Create(
                    DiagnosticDescriptors.ExpressionIsAlwaysEqualToTrueOrFalse,
                    DiagnosticDescriptors.UnnecessaryRelationalOperator);
            }
        }

        public override void Initialize(AnalysisContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            base.Initialize(context);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(startContext =>
            {
                if (!startContext.IsAnalyzerSuppressed(DiagnosticDescriptors.UnnecessaryRelationalOperator))
                {
                    startContext.RegisterSyntaxNodeAction(AnalyzeLessThanExpression, SyntaxKind.LessThanExpression);
                    startContext.RegisterSyntaxNodeAction(AnalyzeGreaterThanExpression, SyntaxKind.GreaterThanExpression);
                }

                startContext.RegisterSyntaxNodeAction(AnalyzeLessThanOrEqualExpression, SyntaxKind.LessThanOrEqualExpression);
                startContext.RegisterSyntaxNodeAction(AnalyzeGreaterThanOrEqualExpression, SyntaxKind.GreaterThanOrEqualExpression);
            });
        }

        // x:
        // byte
        // ushort
        // uint
        // ulong
        // Array.Length
        // string.Length
        // ICollection<T>.Count
        // IReadOnlyCollection<T>.Count

        // x >= 0 >>> true
        // 0 >= x >>> 0 == x
        public static void AnalyzeGreaterThanOrEqualExpression(SyntaxNodeAnalysisContext context)
        {
            var greaterThanOrEqualExpression = (BinaryExpressionSyntax)context.Node;

            BinaryExpressionInfo info = SyntaxInfo.BinaryExpressionInfo(greaterThanOrEqualExpression);

            if (!info.Success)
                return;

            if (!context.IsAnalyzerSuppressed(DiagnosticDescriptors.ExpressionIsAlwaysEqualToTrueOrFalse)
                && IsAlwaysEqualToTrueOrFalse(greaterThanOrEqualExpression, info.Left, info.Right, context.SemanticModel, context.CancellationToken))
            {
                ReportExpressionAlwaysEqualToTrueOrFalse(context, "true");
            }
            else if (!context.IsAnalyzerSuppressed(DiagnosticDescriptors.UnnecessaryRelationalOperator)
                && IsUnnecessaryRelationalOperator(info.Right, info.Left, context.SemanticModel, context.CancellationToken))
            {
                ReportUnnecessaryRelationalOperator(context, info.OperatorToken);
            }
        }

        // 0 > x >>> false
        public static void AnalyzeGreaterThanExpression(SyntaxNodeAnalysisContext context)
        {
            var greaterThanExpression = (BinaryExpressionSyntax)context.Node;

            BinaryExpressionInfo info = SyntaxInfo.BinaryExpressionInfo(greaterThanExpression);

            if (!info.Success)
                return;

            if (!IsAlwaysEqualToTrueOrFalse(greaterThanExpression, info.Right, info.Left, context.SemanticModel, context.CancellationToken))
                return;

            ReportExpressionAlwaysEqualToTrueOrFalse(context, "false");
        }

        // 0 <= x >>> true
        // x <= 0 >>> x == 0
        public static void AnalyzeLessThanOrEqualExpression(SyntaxNodeAnalysisContext context)
        {
            var lessThanOrEqualExpression = (BinaryExpressionSyntax)context.Node;

            BinaryExpressionInfo info = SyntaxInfo.BinaryExpressionInfo(lessThanOrEqualExpression);

            if (!info.Success)
                return;

            if (!context.IsAnalyzerSuppressed(DiagnosticDescriptors.ExpressionIsAlwaysEqualToTrueOrFalse)
                && IsAlwaysEqualToTrueOrFalse(lessThanOrEqualExpression, info.Right, info.Left, context.SemanticModel, context.CancellationToken))
            {
                ReportExpressionAlwaysEqualToTrueOrFalse(context, "true");
            }
            else if (!context.IsAnalyzerSuppressed(DiagnosticDescriptors.UnnecessaryRelationalOperator)
                && IsUnnecessaryRelationalOperator(info.Left, info.Right, context.SemanticModel, context.CancellationToken))
            {
                ReportUnnecessaryRelationalOperator(context, info.OperatorToken);
            }
        }

        // x < 0 >>> false
        public static void AnalyzeLessThanExpression(SyntaxNodeAnalysisContext context)
        {
            var lessThanExpression = (BinaryExpressionSyntax)context.Node;

            BinaryExpressionInfo info = SyntaxInfo.BinaryExpressionInfo(lessThanExpression);

            if (!info.Success)
                return;

            if (!IsAlwaysEqualToTrueOrFalse(lessThanExpression, info.Left, info.Right, context.SemanticModel, context.CancellationToken))
                return;

            ReportExpressionAlwaysEqualToTrueOrFalse(context, "false");
        }

        private static bool IsAlwaysEqualToTrueOrFalse(
            BinaryExpressionSyntax binaryExpression,
            ExpressionSyntax left,
            ExpressionSyntax right,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            if (!right.IsNumericLiteralExpression("0"))
                return false;

            if (binaryExpression.IsKind(SyntaxKind.LessThanOrEqualExpression, SyntaxKind.GreaterThanOrEqualExpression)
                && IsReversedForStatement())
            {
                return false;
            }

            ITypeSymbol typeSymbol = semanticModel.GetTypeSymbol(left, cancellationToken);

            switch (typeSymbol?.SpecialType)
            {
                case SpecialType.System_Byte:
                case SpecialType.System_UInt16:
                case SpecialType.System_UInt32:
                case SpecialType.System_UInt64:
                    return true;
                case SpecialType.System_Int32:
                    return IsCountOrLengthProperty(left, semanticModel, cancellationToken);
                default:
                    return false;
            }

            bool IsReversedForStatement()
            {
                if (!(left is IdentifierNameSyntax identifierName))
                    return false;

                if (!(binaryExpression.WalkUpParentheses().Parent is ForStatementSyntax forStatement))
                    return false;

                VariableDeclarationSyntax declaration = forStatement.Declaration;

                if (declaration == null)
                    return false;

                string name = identifierName.Identifier.ValueText;

                foreach (VariableDeclaratorSyntax declarator in declaration.Variables)
                {
                    if (string.Equals(name, declarator.Identifier.ValueText, StringComparison.Ordinal))
                        return true;
                }

                return false;
            }
        }

        private static bool IsUnnecessaryRelationalOperator(
            ExpressionSyntax left,
            ExpressionSyntax right,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            if (!right.IsNumericLiteralExpression("0"))
                return false;

            if (!IsCountOrLengthProperty(left, semanticModel, cancellationToken))
                return false;

            return true;
        }

        private static bool IsCountOrLengthProperty(
            ExpressionSyntax expression,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            if (expression.IsKind(SyntaxKind.SimpleMemberAccessExpression))
            {
                var memberAccessExpression = (MemberAccessExpressionSyntax)expression;

                if (memberAccessExpression.Name is IdentifierNameSyntax identifierName)
                {
                    switch (identifierName.Identifier.ValueText)
                    {
                        case "Count":
                        case "Length":
                            {
                                if (semanticModel.GetSymbol(expression, cancellationToken) is IPropertySymbol propertySymbol
                                    && propertySymbol.Type.SpecialType == SpecialType.System_Int32)
                                {
                                    INamedTypeSymbol containingType = propertySymbol.ContainingType?.OriginalDefinition;

                                    switch (containingType?.SpecialType)
                                    {
                                        case SpecialType.System_String:
                                        case SpecialType.System_Array:
                                        case SpecialType.System_Collections_Generic_ICollection_T:
                                        case SpecialType.System_Collections_Generic_IList_T:
                                        case SpecialType.System_Collections_Generic_IReadOnlyCollection_T:
                                        case SpecialType.System_Collections_Generic_IReadOnlyList_T:
                                            {
                                                return true;
                                            }
                                        default:
                                            {
                                                if (containingType?.ImplementsAny(
                                                    SpecialType.System_Collections_Generic_ICollection_T,
                                                    SpecialType.System_Collections_Generic_IReadOnlyCollection_T,
                                                    allInterfaces: true) == true)
                                                {
                                                    return true;
                                                }

                                                break;
                                            }
                                    }
                                }

                                break;
                            }
                    }
                }
            }

            return false;
        }

        private static void ReportExpressionAlwaysEqualToTrueOrFalse(SyntaxNodeAnalysisContext context, string booleanName)
        {
            DiagnosticHelpers.ReportDiagnostic(
                context,
                DiagnosticDescriptors.ExpressionIsAlwaysEqualToTrueOrFalse,
                context.Node,
                booleanName);
        }

        private static void ReportUnnecessaryRelationalOperator(SyntaxNodeAnalysisContext context, SyntaxToken operatorToken)
        {
            DiagnosticHelpers.ReportDiagnostic(
                context,
                DiagnosticDescriptors.UnnecessaryRelationalOperator,
                Location.Create(operatorToken.SyntaxTree, new TextSpan(operatorToken.SpanStart, 1)));
        }
    }
}
