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

using Morphic.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Morphic.Windows.Native.Ini
{
    public class IniFile
    {
        // NOTE: properties indicates properties which do not belong to a section
        public List<IniProperty> Properties;

        // NOTE: each section contains zero or more properties (in addition to properties in the root)
        public List<IniSection> Sections;

        private struct EndOfFileContentsStruct
        {
            public List<char> Lexeme;
            public List<IniTrivia> LeadingTrivia;

            public EndOfFileContentsStruct(List<char> lexeme, List<IniTrivia> leadingTrivia)
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

        #region Parser 

        public static IMorphicResult<IniFile> CreateFromString(string contents)
        {
            var properties = new List<IniProperty>();
            var sections = new List<IniSection>();
            var endOfFileContents = new EndOfFileContentsStruct(new List<char>(), new List<IniTrivia>());

            IniSection? currentSection = null;

            var lexer = new IniLexer(contents);
            while (true)
            {
                IniToken iniToken = lexer.GetNextToken();

                // if we've reached the last token, capture the end of file content and break out of this loop
                if (iniToken.Kind == IniTokenKind.EndOfFile)
                {
                    endOfFileContents.Lexeme = iniToken.Lexeme;
                    endOfFileContents.LeadingTrivia = iniToken.LeadingTrivia;

                    break;
                }

                switch(iniToken.Kind)
                {
                    case IniTokenKind.Section:
                        {
                            // new section
                            var newSection = IniSection.CreateFromLexeme(iniToken.Lexeme, iniToken.LeadingTrivia, iniToken.TrailingTrivia);
                            currentSection = newSection;
                            //
                            sections.Add(newSection);
                        }
                        break;
                    case IniTokenKind.Property:
                        {
                            // new property
                            var newProperty = IniProperty.CreateFromLexeme(iniToken.Lexeme, iniToken.LeadingTrivia, iniToken.TrailingTrivia);
                            //
                            if (currentSection != null)
                            {
                                // property within a section
                                currentSection.Properties.Add(newProperty);
                            }
                            else
                            {
                                // root property (i.e. outside of any sections)
                                properties.Add(newProperty);
                            }
                        }
                        break;
                    case IniTokenKind.Invalid:
                        return IMorphicResult<IniFile>.ErrorResult();
                }
            }

            var result = new IniFile(properties, sections);
            result.EndOfFileContents = endOfFileContents;

            return IMorphicResult<IniFile>.SuccessResult(result);
        }

        #endregion Parser

        #region Serializer

        public override string ToString()
        {
            var result = new StringBuilder();

            // top-level properties
            foreach (var property in this.Properties)
            {
                foreach (var trivia in property.LeadingTrivia)
                {
                    IniFile.AppendTriviaToStringBuilder(trivia, ref result);
                }

                result.Append(property.ToString());
                // TODO: consider storing and reproducing the "original" line terminator instead (or a default one, if none was specified)
                IniFile.AppendLineTerminatorToStringBuilder(IniLineTerminatorOption.CrLf, ref result);

                foreach (var trivia in property.TrailingTrivia)
                {
                    IniFile.AppendTriviaToStringBuilder(trivia, ref result);
                }
            }

            // sections
            foreach (var section in this.Sections)
            {
                foreach (var trivia in section.LeadingTrivia)
                {
                    IniFile.AppendTriviaToStringBuilder(trivia, ref result);
                }

                result.Append(section.ToString());
                // TODO: consider storing and reproducing the "original" line terminator instead (or a default one, if none was specified)
                IniFile.AppendLineTerminatorToStringBuilder(IniLineTerminatorOption.CrLf, ref result);

                foreach (var property in section.Properties)
                {
                    foreach (var trivia in property.LeadingTrivia)
                    {
                        IniFile.AppendTriviaToStringBuilder(trivia, ref result);
                    }

                    result.Append(property.ToString());
                    // TODO: consider storing and reproducing the "original" line terminator instead (or a default one, if none was specified)
                    IniFile.AppendLineTerminatorToStringBuilder(IniLineTerminatorOption.CrLf, ref result);

                    foreach (var trivia in property.TrailingTrivia)
                    {
                        IniFile.AppendTriviaToStringBuilder(trivia, ref result);
                    }
                }

                foreach (var trivia in section.TrailingTrivia)
                {
                    IniFile.AppendTriviaToStringBuilder(trivia, ref result);
                }
            }

            // end of file contents
            foreach (var trivia in this.EndOfFileContents.LeadingTrivia)
            {
                IniFile.AppendTriviaToStringBuilder(trivia, ref result);
            }
            result.Append(this.EndOfFileContents.Lexeme.ToArray());

            return result.ToString();
        }

        private static void AppendTriviaToStringBuilder(IniTrivia trivia, ref StringBuilder builder)
        {
            builder.Append(trivia.Lexeme.ToArray());
            IniFile.AppendLineTerminatorToStringBuilder(trivia.LineTerminator ?? IniLineTerminatorOption.None, ref builder);
        }

        private static void AppendLineTerminatorToStringBuilder(IniLineTerminatorOption lineTerminator, ref StringBuilder builder)
        {
            switch (lineTerminator)
            {
                case IniLineTerminatorOption.Cr:
                    builder.Append("\r");
                    break;
                case IniLineTerminatorOption.CrLf:
                    builder.Append("\r\n");
                    break;
                case IniLineTerminatorOption.Lf:
                    builder.Append("\n");
                    break;
                case IniLineTerminatorOption.None:
                    break;
                default:
                    Debug.Assert(false, "Invalid line terminator option (i.e. code bug)");
                    throw new ArgumentException();
            }
        }

        #endregion Serializer

    }
}
