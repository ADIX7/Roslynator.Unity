﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Pihrtsoft.CodeAnalysis.CSharp.Refactoring
{
    internal class AddIfDirectiveRefactoring : WrapSelectedLinesRefactoring
    {
        public override string GetFirstLineText()
        {
            return "#if DEBUG";
        }

        public override string GetLastLineText()
        {
            return "#endif";
        }
    }
}
