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

namespace Morphic.WindowsNative.Ini;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class IniSection
{
    public string Name;
    public List<IniProperty> Properties;

    public IniLineTerminatorOption LineTerminator;

    internal List<Lexer.IniTrivia> LeadingTrivia = new();
    internal List<Lexer.IniTrivia> TrailingTrivia = new();

    public IniSection(string name, IniLineTerminatorOption lineTerminator)
    {
        if (name.IndexOf(']') > 0)
        {
            throw new ArgumentException("Argument 'name' may not contain the ']' character", nameof(name));
        }

        this.Name = name;
        this.Properties = new List<IniProperty>();

        this.LineTerminator = lineTerminator;
    }

    public string SectionNameAsSectionHeaderString()
    {
        var result = new StringBuilder();

        result.Append('[');
        result.Append(this.Name);
        result.Append(']');

        return result.ToString();
    }

    internal static IniSection CreateFromLexeme(List<char> lexeme, IniLineTerminatorOption lineTerminator, List<Lexer.IniTrivia>? leadingTrivia = null, List<Lexer.IniTrivia>? trailingTrivia = null)
    {
        var sectionNameAsChars = IniSection.GetSectionNameFromSectionLexeme(lexeme);
        var sectionNameAsString = new string(sectionNameAsChars.ToArray());
        //
        var result = new IniSection(sectionNameAsString, lineTerminator)
        {
            LeadingTrivia = leadingTrivia ?? new List<Lexer.IniTrivia>(),
            TrailingTrivia = trailingTrivia ?? new List<Lexer.IniTrivia>(),
        };
        return result;
    }

    private static List<char> GetSectionNameFromSectionLexeme(List<char> lexeme)
    {
        var indexOfLeftBracket = lexeme.IndexOf('[');
        if (indexOfLeftBracket < 0)
        {
            // our caller should never call this function with an invalid lexeme (missing the left bracket); the lexer should never give us one that's invalid in this repsect
            throw new InvalidOperationException();
        }

        var indexOfRightBracket = lexeme.IndexOf(']', indexOfLeftBracket + 1);
        if (indexOfRightBracket < 0)
        {
            // our caller should never call this function with an invalid lexeme (missing the right bracket); the lexer should never give us one that's invalid in this repsect
            throw new InvalidOperationException();
        }

        List<char> sectionName = lexeme.GetRange(indexOfLeftBracket + 1, indexOfRightBracket - indexOfLeftBracket - 1);
        return sectionName;
    }
}
