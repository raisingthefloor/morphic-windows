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

public class IniProperty
{
    public string Key;
    public string Value;

    public IniLineTerminatorOption LineTerminator;

    internal List<Lexer.IniTrivia> LeadingTrivia = new();
    internal List<Lexer.IniTrivia> TrailingTrivia = new();

    public IniProperty(string key, string value, IniLineTerminatorOption lineTerminator)
    {
        this.Key = key;
        this.Value = value;

        this.LineTerminator = lineTerminator;
    }

    public string PropertyAndValueAsPropertyString()
    {
        var result = new StringBuilder();

        result.Append(this.Key);
        result.Append('=');
        result.Append(this.Value);

        return result.ToString();
    }

    internal static IniProperty CreateFromLexeme(List<char> lexeme, IniLineTerminatorOption lineTerminator, List<Lexer.IniTrivia>? leadingTrivia = null, List<Lexer.IniTrivia>? trailingTrivia = null)
    {
        var propertyKeyAsChars = IniProperty.GetKeyFromPropertyLexeme(lexeme);
        var propertyKeyAsString = new string(propertyKeyAsChars.ToArray());
        //
        var propertyValueAsChars = IniProperty.GetValueFromPropertyLexeme(lexeme);
        var propertyValueAsString = new string(propertyValueAsChars.ToArray());
        //
        var result = new IniProperty(propertyKeyAsString, propertyValueAsString, lineTerminator)
        {
            LeadingTrivia = leadingTrivia ?? new List<Lexer.IniTrivia>(),
            TrailingTrivia = trailingTrivia ?? new List<Lexer.IniTrivia>(),
        };
        return result;
    }

    private static List<char> GetKeyFromPropertyLexeme(List<char> lexeme)
    {
        var indexOfEquals = lexeme.IndexOf('=');
        if (indexOfEquals < 0)
        {
            // our caller should never call this function with an invalid lexeme (missing the equals sign); the lexer should never give us one that's invalid in this repsect
            throw new InvalidOperationException();
        }

        List<char> key = lexeme.GetRange(0, indexOfEquals);
        return key;
    }

    private static List<char> GetValueFromPropertyLexeme(List<char> lexeme)
    {
        var indexOfEquals = lexeme.IndexOf('=');
        if (indexOfEquals < 0)
        {
            // our caller should never call this function with an invalid lexeme (missing the equals sign); the lexer should never give us one that's invalid in this repsect
            throw new InvalidOperationException();
        }

        List<char> value = lexeme.GetRange(indexOfEquals + 1, lexeme.Count - indexOfEquals - 1);
        return value;
    }
}
