﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Roslynator.Tests;
using Xunit;

namespace Roslynator.CSharp.Refactorings.Tests
{
    public class RR0212AddParameterToInterfaceMemberTests : AbstractCSharpRefactoringVerifier
    {
        private readonly CodeVerificationOptions _options;

        public RR0212AddParameterToInterfaceMemberTests()
        {
            _options = base.Options.AddAllowedCompilerDiagnosticIds(new string[] { "CS0535", "CS0539" });
        }

        public override CodeVerificationOptions Options => _options;

        public override string RefactoringId { get; } = RefactoringIdentifiers.AddParameterToInterfaceMember;

        [Fact, Trait(Traits.Refactoring, RefactoringIdentifiers.AddParameterToInterfaceMember)]
        public async Task Test_Method()
        {
            await VerifyRefactoringAsync(@"
interface IFoo
{
    void M(object p);
}

class C : IFoo
{
    public void [||]M(object p, object p2)
    {
    }
}
", @"
interface IFoo
{
    void M(object p, object p2);
}

class C : IFoo
{
    public void M(object p, object p2)
    {
    }
}
", equivalenceKey: EquivalenceKey.Join(RefactoringId, "IFoo.M(object)"));
        }

        [Fact, Trait(Traits.Refactoring, RefactoringIdentifiers.AddParameterToInterfaceMember)]
        public async Task Test_ExplicitlyImplementedMethod()
        {
            await VerifyRefactoringAsync(@"
interface IFoo
{
    void M(object p);
}

class C : IFoo
{
    void IFoo.[||]M(object p, object p2)
    {
    }
}
", @"
interface IFoo
{
    void M(object p, object p2);
}

class C : IFoo
{
    void IFoo.M(object p, object p2)
    {
    }
}
", equivalenceKey: EquivalenceKey.Join(RefactoringId, "IFoo.M(object)"));
        }

        [Fact, Trait(Traits.Refactoring, RefactoringIdentifiers.AddParameterToInterfaceMember)]
        public async Task Test_Indexer()
        {
            await VerifyRefactoringAsync(@"
interface IFoo
{
    object this[object p] { get; }
}

class C : IFoo
{
    public object [||]this[object p, object p2] => null;
}
", @"
interface IFoo
{
    object this[object p, object p2] { get; }
}

class C : IFoo
{
    public object this[object p, object p2] => null;
}
", equivalenceKey: EquivalenceKey.Join(RefactoringId, "IFoo.this[object]"));
        }

        [Fact, Trait(Traits.Refactoring, RefactoringIdentifiers.AddParameterToInterfaceMember)]
        public async Task Test_ExplicitlyImplementedIndexer()
        {
            await VerifyRefactoringAsync(@"
interface IFoo
{
    object this[object p] { get; }
}

class C : IFoo
{
    object IFoo.[||]this[object p, object p2] => null;
}
", @"
interface IFoo
{
    object this[object p, object p2] { get; }
}

class C : IFoo
{
    object IFoo.this[object p, object p2] => null;
}
", equivalenceKey: EquivalenceKey.Join(RefactoringId, "IFoo.this[object]"));
        }
    }
}