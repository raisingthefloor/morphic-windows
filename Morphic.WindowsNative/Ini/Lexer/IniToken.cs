// Copyright 2020-2022 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-windowsnative-lib-cs/blob/main/LICENSE
//
// The R&D leading to these results received funding from the:
// * Rehabilitation Services Administration, US Dept. of Education under
//   grant H421A150006 (APCP)
// * National Institute on Disability, Independent Living, and
//   Rehabilitation Research (NIDILRR)
// * Administration for Independent Living & Dept. of Education under grants
//   H133E080022 (RERC-IT) and H133E130028/90RE5003-01-00 (UIITA-RERC)
// * European Union's Seventh Framework Programme (FP7/2007-2013) grant
//   agreement nos. 289016 (Cloud4all) and 610510 (Prosperity4All)
// * William and Flora Hewlett Foundation
// * Ontario Ministry of Research and Innovation
// * Canadian Foundation for Innovation
// * Adobe Foundation
// * Consumer Electronics Association Foundation

namespace Morphic.WindowsNative.Ini.Lexer;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

internal record IniToken
{
    public IniTokenKind Kind;
    public List<char> Lexeme;

    public IniExplicitLineTerminatorOption LineTerminator;

    public List<IniTrivia> LeadingTrivia;
    public List<IniTrivia> TrailingTrivia;

    // NOTE: for convenience, we allow callers to pass in a null list for leading/trailing trivia, but we always translate nulls to correspondingly-empty lists
    public IniToken(IniTokenKind kind, List<char> lexeme, IniExplicitLineTerminatorOption lineTerminator, List<IniTrivia>? leadingTrivia, List<IniTrivia>? trailingTrivia)
    {
        this.Kind = kind;
        this.Lexeme = lexeme;

        this.LineTerminator = lineTerminator;

        this.LeadingTrivia = leadingTrivia ?? new List<IniTrivia>();
        this.TrailingTrivia = trailingTrivia ?? new List<IniTrivia>();
    }
}
