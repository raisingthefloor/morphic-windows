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

using Morphic.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class IniFile
{
    // NOTE: IniFile.Properties contains top-level properties (i.e. properties which do not belong to a section)
    public List<IniProperty> Properties;

    // NOTE: each section contains zero or more properties (in addition to properties at the top level)
    public List<IniSection> Sections;

    private IniLineTerminatorOption _defaultLineTerminator = IniLineTerminatorOption.CrLf;
    public IniLineTerminatorOption DefaultLineTerminator
    {
        get 
        {
            return _defaultLineTerminator;
        }
        set
        {
            switch (value)
            {
                case IniLineTerminatorOption.Cr:
                case IniLineTerminatorOption.CrLf:
                case IniLineTerminatorOption.Lf:
                    _defaultLineTerminator = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    private struct EndOfFileContentsStruct
    {
        public List<char> Lexeme;
        public List<Lexer.IniTrivia> LeadingTrivia;

        public EndOfFileContentsStruct(List<char> lexeme, List<Lexer.IniTrivia> leadingTrivia)
        {
            this.Lexeme = lexeme;
            this.LeadingTrivia = leadingTrivia;
        }
    }
    private EndOfFileContentsStruct EndOfFileContents;

    internal IniFile(List<IniProperty> properties, List<IniSection> sections)
    {
        this.Properties = properties;
        this.Sections = sections;
    }


    #region Parser (deserializer)

    public static MorphicResult<IniFile, MorphicUnit> CreateFromString(string contents)
    {
        var properties = new List<IniProperty>();
        var sections = new List<IniSection>();
        var endOfFileContents = new EndOfFileContentsStruct(new List<char>(), new List<Lexer.IniTrivia>());

        // NOTE: since INI is a Windows file format, we use CrLf as the default line terminator option
        IniLineTerminatorOption defaultLineTerminator = IniLineTerminatorOption.CrLf;

        IniSection? currentSection = null;

        var lexer = new Lexer.IniLexer(contents);
        while (true)
        {
            Lexer.IniToken iniToken = lexer.GetNextToken();

            // if we've reached the last token, capture the end of file content and break out of this loop
            if (iniToken.Kind == Lexer.IniTokenKind.EndOfFile)
            {
                endOfFileContents.Lexeme = iniToken.Lexeme;
                endOfFileContents.LeadingTrivia = iniToken.LeadingTrivia;

                break;
            }

            // capture the last line terminator we find on any section/property line as the "default" line terminator for this IniFile instance
            switch (iniToken.Kind)
            {
                case Lexer.IniTokenKind.Section:
                case Lexer.IniTokenKind.Property:
                    switch (iniToken.LineTerminator)
                    {
                        case Lexer.IniExplicitLineTerminatorOption.Cr:
                        case Lexer.IniExplicitLineTerminatorOption.CrLf:
                        case Lexer.IniExplicitLineTerminatorOption.Lf:
                            defaultLineTerminator = IniFile.FromIniExplicitLineTerminatorOption(iniToken.LineTerminator);
                            break;
                    }
                    break;
            }

            switch (iniToken.Kind)
            {
                case Lexer.IniTokenKind.Section:
                    {
                        // new section
                        var newSection = IniSection.CreateFromLexeme(iniToken.Lexeme, IniFile.FromIniExplicitLineTerminatorOption(iniToken.LineTerminator), iniToken.LeadingTrivia, iniToken.TrailingTrivia);
                        currentSection = newSection;
                        //
                        sections.Add(newSection);
                    }
                    break;
                case Lexer.IniTokenKind.Property:
                    {
                        // new property
                        var newProperty = IniProperty.CreateFromLexeme(iniToken.Lexeme, IniFile.FromIniExplicitLineTerminatorOption(iniToken.LineTerminator), iniToken.LeadingTrivia, iniToken.TrailingTrivia);
                        //
                        if (currentSection is not null)
                        {
                            // property within a section
                            currentSection.Properties.Add(newProperty);
                        }
                        else
                        {
                            // top-level property (i.e. outside of any sections)
                            properties.Add(newProperty);
                        }
                    }
                    break;
                case Lexer.IniTokenKind.Invalid:
                    return MorphicResult.ErrorResult();
            }
        }

        var result = new IniFile(properties, sections);
        result.DefaultLineTerminator = defaultLineTerminator;
        result.EndOfFileContents = endOfFileContents;

        return MorphicResult.OkResult(result);
    }

    #endregion Parser (deserializer)


    #region Serializer

    public string PropertyAndValueAsString()
    {
        var resultBuilder = new StringBuilder();

        // top-level properties
        foreach (var property in this.Properties)
        {
            foreach (var trivia in property.LeadingTrivia)
            {
                IniFile.AppendTriviaToStringBuilder(trivia, ref resultBuilder);
            }

            resultBuilder.Append(property.PropertyAndValueAsPropertyString());
            IniFile.AppendLineTerminatorToStringBuilder(this.ToIniExplicitLineTerminatorOption(property.LineTerminator), ref resultBuilder);

            foreach (var trivia in property.TrailingTrivia)
            {
                IniFile.AppendTriviaToStringBuilder(trivia, ref resultBuilder);
            }
        }

        // sections
        foreach (var section in this.Sections)
        {
            foreach (var trivia in section.LeadingTrivia)
            {
                IniFile.AppendTriviaToStringBuilder(trivia, ref resultBuilder);
            }

            resultBuilder.Append(section.SectionNameAsSectionHeaderString());
            IniFile.AppendLineTerminatorToStringBuilder(this.ToIniExplicitLineTerminatorOption(section.LineTerminator), ref resultBuilder);

            foreach (var property in section.Properties)
            {
                foreach (var trivia in property.LeadingTrivia)
                {
                    IniFile.AppendTriviaToStringBuilder(trivia, ref resultBuilder);
                }

                resultBuilder.Append(property.PropertyAndValueAsPropertyString());
                IniFile.AppendLineTerminatorToStringBuilder(this.ToIniExplicitLineTerminatorOption(property.LineTerminator), ref resultBuilder);

                foreach (var trivia in property.TrailingTrivia)
                {
                    IniFile.AppendTriviaToStringBuilder(trivia, ref resultBuilder);
                }
            }

            foreach (var trivia in section.TrailingTrivia)
            {
                IniFile.AppendTriviaToStringBuilder(trivia, ref resultBuilder);
            }
        }

        // end of file contents
        foreach (var trivia in this.EndOfFileContents.LeadingTrivia)
        {
            IniFile.AppendTriviaToStringBuilder(trivia, ref resultBuilder);
        }
        resultBuilder.Append(this.EndOfFileContents.Lexeme.ToArray());

        return resultBuilder.ToString();
    }

    private static void AppendTriviaToStringBuilder(Lexer.IniTrivia trivia, ref StringBuilder builder)
    {
        builder.Append(trivia.Lexeme.ToArray());
        IniFile.AppendLineTerminatorToStringBuilder(trivia.LineTerminator, ref builder);
    }

    private static void AppendLineTerminatorToStringBuilder(Lexer.IniExplicitLineTerminatorOption lineTerminator, ref StringBuilder builder)
    {
        switch (lineTerminator)
        {
            case Lexer.IniExplicitLineTerminatorOption.Cr:
                builder.Append("\r");
                break;
            case Lexer.IniExplicitLineTerminatorOption.CrLf:
                builder.Append("\r\n");
                break;
            case Lexer.IniExplicitLineTerminatorOption.Lf:
                builder.Append("\n");
                break;
            case Lexer.IniExplicitLineTerminatorOption.None:
                break;
            default:
                Debug.Assert(false, "Invalid line terminator option (i.e. code bug)");
                throw new ArgumentException();
        }
    }

    #endregion Serializer


    #region Line Terminator helpers

    private static IniLineTerminatorOption FromIniExplicitLineTerminatorOption(Lexer.IniExplicitLineTerminatorOption explicitLineTerminatorOption)
    {
        switch(explicitLineTerminatorOption)
        {
            case Lexer.IniExplicitLineTerminatorOption.None:
                return IniLineTerminatorOption.None;
            case Lexer.IniExplicitLineTerminatorOption.Cr:
                return IniLineTerminatorOption.Cr;
            case Lexer.IniExplicitLineTerminatorOption.CrLf:
                return IniLineTerminatorOption.CrLf;
            case Lexer.IniExplicitLineTerminatorOption.Lf:
                return IniLineTerminatorOption.Lf;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private Lexer.IniExplicitLineTerminatorOption ToIniExplicitLineTerminatorOption(IniLineTerminatorOption lineTerminatorOption)
    {
        switch (lineTerminatorOption)
        {
            case IniLineTerminatorOption.None:
                return Lexer.IniExplicitLineTerminatorOption.None;
            case IniLineTerminatorOption.Cr:
                return Lexer.IniExplicitLineTerminatorOption.Cr;
            case IniLineTerminatorOption.CrLf:
                return Lexer.IniExplicitLineTerminatorOption.CrLf;
            case IniLineTerminatorOption.Lf:
                return Lexer.IniExplicitLineTerminatorOption.Lf;
            case IniLineTerminatorOption.UseDefault:
                return this.ToIniExplicitLineTerminatorOption(this.DefaultLineTerminator);
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    #endregion Line Terminator helpers
}
