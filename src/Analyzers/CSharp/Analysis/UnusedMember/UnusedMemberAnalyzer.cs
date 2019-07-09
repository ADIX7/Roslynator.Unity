﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Roslynator.CSharp.Analysis.UnusedMember
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class UnusedMemberAnalyzer : BaseDiagnosticAnalyzer
    {
        static readonly Dictionary<string, List<string>> _unityMethodsByClasses;

        static readonly List<string> _unityLifecycleClasses;
        static readonly List<string> _unityLifecycleMethods;

        static UnusedMemberAnalyzer()
        {

            _unityMethodsByClasses = new Dictionary<string, List<string>>()
            {
                {
                    "UnityEngine.MonoBehaviour",
                    new List<string>(){
                        "Awake",
                        "FixedUpdate",
                        "LateUpdate",
                        "OnAnimatorIK",
                        "OnAnimatorMove",
                        "OnApplicationFocus",
                        "OnApplicationPause",
                        "OnApplicationQuit",
                        "OnAudioFilterRead",
                        "OnBecameInvisible",
                        "OnBecameVisible",
                        "OnCollisionEnter",
                        "OnCollisionEnter2D",
                        "OnCollisionExit",
                        "OnCollisionExit2D",
                        "OnCollisionStay",
                        "OnCollisionStay2D",
                        "OnConnectedToServer",
                        "OnControllerColliderHit",
                        "OnDestroy",
                        "OnDisable",
                        "OnDisconnectedFromServer",
                        "OnDrawGizmos",
                        "OnDrawGizmosSelected",
                        "OnEnable",
                        "OnFailedToConnect",
                        "OnFailedToConnectToMasterServer",
                        "OnGUI",
                        "OnJointBreak",
                        "OnJointBreak2D",
                        "OnMasterServerEvent",
                        "OnMouseDown",
                        "OnMouseDrag",
                        "OnMouseEnter",
                        "OnMouseExit",
                        "OnMouseOver",
                        "OnMouseUp",
                        "OnMouseUpAsButton",
                        "OnNetworkInstantiate",
                        "OnParticleCollision",
                        "OnParticleSystemStopped",
                        "OnParticleTrigger",
                        "OnPlayerConnected",
                        "OnPlayerDisconnected",
                        "OnPostRender",
                        "OnPreCull",
                        "OnPreRender",
                        "OnRenderImage",
                        "OnRenderObject",
                        "OnSerializeNetworkView",
                        "OnServerInitialized",
                        "OnTransformChildrenChanged",
                        "OnTransformParentChanged",
                        "OnTriggerEnter",
                        "OnTriggerEnter2D",
                        "OnTriggerExit",
                        "OnTriggerExit2D",
                        "OnTriggerStay",
                        "OnTriggerStay2D",
                        "OnValidate",
                        "OnWillRenderObject",
                        "Reset",
                        "Start",
                        "Update"
                    }
                }
            };

            _unityLifecycleClasses = _unityMethodsByClasses.Keys.ToList();
            _unityLifecycleMethods = _unityMethodsByClasses.SelectMany(kvp => kvp.Value).ToList();
        }
        
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get { return ImmutableArray.Create(DiagnosticDescriptors.RemoveUnusedMemberDeclaration); }
        }

        public override void Initialize(AnalysisContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            base.Initialize(context);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeTypeDeclaration, SyntaxKind.ClassDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeTypeDeclaration, SyntaxKind.StructDeclaration);
        }

        [SuppressMessage("Simplification", "RCS1180:Inline lazy initialization.", Justification = "<Pending>")]
        private static void AnalyzeTypeDeclaration(SyntaxNodeAnalysisContext context)
        {
            var typeDeclaration = (TypeDeclarationSyntax)context.Node;

            if (typeDeclaration.Modifiers.Contains(SyntaxKind.PartialKeyword))
                return;

            SemanticModel semanticModel = context.SemanticModel;
            CancellationToken cancellationToken = context.CancellationToken;

            ImmutableArray<AttributeData> attributes = default;

            if (typeDeclaration.IsKind(SyntaxKind.StructDeclaration))
            {
                INamedTypeSymbol declarationSymbol = semanticModel.GetDeclaredSymbol(typeDeclaration, cancellationToken);

                attributes = declarationSymbol.GetAttributes();

                if (attributes.Any(f => f.AttributeClass.HasMetadataName(MetadataNames.System_Runtime_InteropServices_StructLayoutAttribute)))
                    return;
            }

            SyntaxList<MemberDeclarationSyntax> members = typeDeclaration.Members;

            UnusedMemberWalker walker = null;

            foreach (MemberDeclarationSyntax member in members)
            {
                if (member.ContainsDiagnostics)
                    continue;

                if (member.ContainsUnbalancedIfElseDirectives(member.Span))
                    continue;

                switch (member.Kind())
                {
                    case SyntaxKind.DelegateDeclaration:
                        {
                            var declaration = (DelegateDeclarationSyntax)member;

                            if (SyntaxAccessibility<DelegateDeclarationSyntax>.Instance.GetAccessibility(declaration) == Accessibility.Private)
                            {
                                if (walker == null)
                                    walker = UnusedMemberWalkerCache.GetInstance();

                                walker.AddDelegate(declaration.Identifier.ValueText, declaration);
                            }

                            break;
                        }
                    case SyntaxKind.EventDeclaration:
                        {
                            var declaration = (EventDeclarationSyntax)member;

                            if (declaration.ExplicitInterfaceSpecifier == null
                                && SyntaxAccessibility<EventDeclarationSyntax>.Instance.GetAccessibility(declaration) == Accessibility.Private)
                            {
                                if (walker == null)
                                    walker = UnusedMemberWalkerCache.GetInstance();

                                walker.AddNode(declaration.Identifier.ValueText, declaration);
                            }

                            break;
                        }
                    case SyntaxKind.EventFieldDeclaration:
                        {
                            var declaration = (EventFieldDeclarationSyntax)member;

                            if (SyntaxAccessibility<EventFieldDeclarationSyntax>.Instance.GetAccessibility(declaration) == Accessibility.Private)
                            {
                                if (walker == null)
                                    walker = UnusedMemberWalkerCache.GetInstance();

                                walker.AddNodes(declaration.Declaration);
                            }

                            break;
                        }
                    case SyntaxKind.FieldDeclaration:
                        {
                            var declaration = (FieldDeclarationSyntax)member;
                            SyntaxTokenList modifiers = declaration.Modifiers;

                            if (SyntaxAccessibility<FieldDeclarationSyntax>.Instance.GetAccessibility(declaration) == Accessibility.Private)
                            {
                                if (walker == null)
                                    walker = UnusedMemberWalkerCache.GetInstance();

                                walker.AddNodes(declaration.Declaration, isConst: modifiers.Contains(SyntaxKind.ConstKeyword));
                            }

                            break;
                        }
                    case SyntaxKind.MethodDeclaration:
                        {
                            var declaration = (MethodDeclarationSyntax)member;

                            SyntaxTokenList modifiers = declaration.Modifiers;

                            if (declaration.ExplicitInterfaceSpecifier == null
                                && !declaration.AttributeLists.Any()
                                && SyntaxAccessibility<MethodDeclarationSyntax>.Instance.GetAccessibility(declaration) == Accessibility.Private)
                            {
                                string methodName = declaration.Identifier.ValueText;

                                if (!IsMainMethod(declaration, modifiers, methodName)
                                    && !IsUnityLifeCycleMethod(typeDeclaration, semanticModel, methodName))
                                {
                                    if (walker == null)
                                        walker = UnusedMemberWalkerCache.GetInstance();

                                    walker.AddNode(methodName, declaration);
                                }
                            }

                            break;
                        }
                    case SyntaxKind.PropertyDeclaration:
                        {
                            var declaration = (PropertyDeclarationSyntax)member;

                            if (declaration.ExplicitInterfaceSpecifier == null
                                && SyntaxAccessibility<PropertyDeclarationSyntax>.Instance.GetAccessibility(declaration) == Accessibility.Private)
                            {
                                if (walker == null)
                                    walker = UnusedMemberWalkerCache.GetInstance();

                                walker.AddNode(declaration.Identifier.ValueText, declaration);
                            }

                            break;
                        }
                }
            }

            if (walker == null)
                return;

            Collection<NodeSymbolInfo> nodes = walker.Nodes;

            if (ShouldAnalyzeDebuggerDisplayAttribute()
                && nodes.Any(f => f.CanBeInDebuggerDisplayAttribute))
            {
                if (attributes.IsDefault)
                    attributes = semanticModel.GetDeclaredSymbol(typeDeclaration, cancellationToken).GetAttributes();

                string value = attributes
                    .FirstOrDefault(f => f.AttributeClass.HasMetadataName(MetadataNames.System_Diagnostics_DebuggerDisplayAttribute))?
                    .ConstructorArguments
                    .SingleOrDefault(shouldThrow: false)
                    .Value?
                    .ToString();

                if (value != null)
                    RemoveMethodsAndPropertiesThatAreInDebuggerDisplayAttributeValue(value, ref nodes);

                if (nodes.Count == 0)
                {
                    UnusedMemberWalkerCache.Free(walker);
                    return;
                }
            }

            walker.SemanticModel = semanticModel;
            walker.CancellationToken = cancellationToken;

            walker.Visit(typeDeclaration);

            foreach (NodeSymbolInfo info in nodes)
            {
                SyntaxNode node = info.Node;

                if (node is VariableDeclaratorSyntax variableDeclarator)
                {
                    var variableDeclaration = (VariableDeclarationSyntax)variableDeclarator.Parent;

                    if (variableDeclaration.Variables.Count == 1)
                    {
                        ReportDiagnostic(context, variableDeclaration.Parent, CSharpFacts.GetTitle(variableDeclaration.Parent));
                    }
                    else
                    {
                        ReportDiagnostic(context, variableDeclarator, CSharpFacts.GetTitle(variableDeclaration.Parent));
                    }
                }
                else
                {
                    ReportDiagnostic(context, node, CSharpFacts.GetTitle(node));
                }
            }

            UnusedMemberWalkerCache.Free(walker);

            bool ShouldAnalyzeDebuggerDisplayAttribute()
            {
                foreach (AttributeListSyntax attributeList in typeDeclaration.AttributeLists)
                {
                    foreach (AttributeSyntax attribute in attributeList.Attributes)
                    {
                        if (attribute.ArgumentList?.Arguments.Count(f => f.NameEquals == null) == 1)
                            return true;
                    }
                }

                return false;
            }
        }

        private static bool IsUnityLifeCycleMethod(TypeDeclarationSyntax typeDeclaration, SemanticModel semanticModel, string methodName)
        {
            if (!_unityLifecycleMethods.Contains(methodName)) return false;

            foreach (var parentTypeSyntax in ((ClassDeclarationSyntax)typeDeclaration).BaseList.Types.Select(t => t.Type))
            {
                var symbolInfo = semanticModel.GetSymbolInfo(parentTypeSyntax);
                var typeFullName = symbolInfo.Symbol.ToString();
                if (_unityLifecycleClasses.Contains(typeFullName) && _unityMethodsByClasses[typeFullName].Contains(methodName))
                    return true;
            }

            return false;
        }

        private static bool IsMainMethod(MethodDeclarationSyntax methodDeclaration, SyntaxTokenList modifiers, string methodName)
        {
            return string.Equals(methodName, "Main", StringComparison.Ordinal)
                && modifiers.Contains(SyntaxKind.StaticKeyword)
                && methodDeclaration.TypeParameterList == null
                && methodDeclaration.ParameterList?.Parameters.Count <= 1;
        }

        private static void RemoveMethodsAndPropertiesThatAreInDebuggerDisplayAttributeValue(
            string value,
            ref Collection<NodeSymbolInfo> nodes)
        {
            int length = value.Length;

            if (length == 0)
                return;

            for (int i = 0; i < length; i++)
            {
                switch (value[i])
                {
                    case '{':
                        {
                            i++;

                            int startIndex = i;

                            while (i < length)
                            {
                                char ch = value[i];

                                if (ch == '}'
                                    || ch == ','
                                    || ch == '(')
                                {
                                    int nameLength = i - startIndex;

                                    if (nameLength > 0)
                                    {
                                        for (int j = nodes.Count - 1; j >= 0; j--)
                                        {
                                            NodeSymbolInfo nodeSymbolInfo = nodes[j];

                                            if (nodeSymbolInfo.CanBeInDebuggerDisplayAttribute
                                                && string.CompareOrdinal(nodeSymbolInfo.Name, 0, value, startIndex, nameLength) == 0)
                                            {
                                                nodes.RemoveAt(j);

                                                if (nodes.Count == 0)
                                                    return;
                                            }
                                        }
                                    }

                                    if (ch != '}')
                                    {
                                        i++;

                                        while (i < length
                                            && value[i] != '}')
                                        {
                                            i++;
                                        }
                                    }

                                    break;
                                }

                                i++;
                            }

                            break;
                        }
                    case '}':
                        {
                            return;
                        }
                    case '\\':
                        {
                            i++;
                            break;
                        }
                }
            }
        }

        private static void ReportDiagnostic(SyntaxNodeAnalysisContext context, SyntaxNode node, string declarationName)
        {
            DiagnosticHelpers.ReportDiagnostic(context, DiagnosticDescriptors.RemoveUnusedMemberDeclaration, CSharpUtility.GetIdentifier(node), declarationName);
        }
    }
}
