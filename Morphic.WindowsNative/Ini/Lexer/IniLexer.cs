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
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// NOTE: this lexer is simplified: it reads entire lines (instead of following goals and parsing out individual keys, equal signs, tokens, etc.)
//       the parser takes on the additional responsibility of interpreting the "KeyValuePair" tokens to parse out the key and value, deal with double-quotes or escaped characters, etc.
internal class IniLexer
{
    // NOTE: the C# char type is UTF-16; if we need to deal with 32-bit Unicode values then we will need to do some of our own decoding/encoding using bytes instead of strings/chars
    private List<char> _remainingContents;

    public IniLexer(String contents)
    {
        _remainingContents = contents.ToList(); //.ToCharArray().ToList();
    }

    // NOTE: we capture entire lines as "tokens"; this class's job is to classify individual line entries (and tag comment/whitespace trivia), not to parse raw tokens
    public IniToken GetNextToken()
    {
        var leadingTrivia = new List<IniTrivia>();

        // NOTE: this loop will continue until we parse a line or read eof of file
        while (true)
        {
            var nextLine = this.ReadIniTextLine();

            if (nextLine is null)
            {
                return new IniToken(IniTokenKind.EndOfFile, new List<char>() /* empty lexeme, rather than null */, IniExplicitLineTerminatorOption.None, leadingTrivia, null);
            }

            var lexeme = nextLine.Text!;
            var lineTerminator = nextLine.LineTerminator;

            // check for empty lines
            if (lexeme.Count == 0)
            {
                // empty line; treat this as trivia
                var whitespaceTrivia = new IniTrivia(IniTriviaKind.Whitespace, lexeme, lineTerminator);
                leadingTrivia.Add(whitespaceTrivia);

                // capture another line of text
                continue;
            }
            else
            {
                // check for comment lines
                if (lexeme.First() == ';')
                {
                    // comment; capture this as trivia
                    var commentTrivia = new IniTrivia(IniTriviaKind.Comment, lexeme, lineTerminator);
                    leadingTrivia.Add(commentTrivia);

                    // capture another line of text
                    continue;
                }

                // check for whitespace lines
                var lineIsWhitespace = lexeme.All(ch =>
                {
                    switch (ch)
                    {
                        case ' ':
                        case '\t':
                            return true;
                        default:
                            return false;
                    }
                });
                if (lineIsWhitespace == true)
                {
                    var whitespaceTrivia = new IniTrivia(IniTriviaKind.Whitespace, lexeme, lineTerminator);
                    leadingTrivia.Add(whitespaceTrivia);

                    // capture another line of text
                    continue;
                }

                // check for an EOF marker
                if (lexeme.Count == 1 && lexeme.First() == (char)0x1A /* EOF marker */)
                {
                    // EOF marker; this will end our text capture
                    return new IniToken(IniTokenKind.EndOfFile, lexeme, lineTerminator, leadingTrivia, null);
                }

                // now categorize our lexeme (or mark it as invalid)
                char? firstNonWhitespaceCharacter = null;
                foreach (var ch in lexeme)
                {
                    switch (ch)
                    {
                        case ' ':
                        case '\t':
                            continue;
                        default:
                            firstNonWhitespaceCharacter = ch;
                            break;
                    }

                    if (firstNonWhitespaceCharacter is not null)
                    {
                        break;
                    }
                }

                // check for sections
                if (firstNonWhitespaceCharacter == '[')
                {
                    // make sure that the line contains an ending (right) bracket
                    var indexOfRightBracket = lexeme.IndexOf(']');
                    if (indexOfRightBracket > 0)
                    {
                        // found right bracket; make sure that the line does not contain any non-whitespace characters after the bracket
                        for (var index = indexOfRightBracket + 1; index < lexeme.Count; index += 1)
                        {
                            switch (lexeme[index])
                            {
                                case ' ':
                                case '\t':
                                    // whitespace
                                    break;
                                default:
                                    // if we encounter any non-whitespace characters, this an invalid token
                                    return new IniToken(IniTokenKind.Invalid, lexeme, lineTerminator, leadingTrivia, null);
                            }
                        }

                        // this section is valid; return it now
                        return new IniToken(IniTokenKind.Section, lexeme, lineTerminator, leadingTrivia, null);
                    }
                    else
                    {
                        // this line does not have a closing bracket for the section
                        return new IniToken(IniTokenKind.Invalid, lexeme, lineTerminator, leadingTrivia, null);
                    }
                }

                // NOTE: at this point, the line should be a property (i.e. key/value pair); anything else is invalid

                // check for an equals sign; we will treat anything to the left as the key and anything to the right as the value
                var indexOfEqualsCharacter = lexeme.IndexOf('=');
                if (indexOfEqualsCharacter >= 0)
                {
                    // key/value pair
                    // NOTE: our parser is responsible for parsing out the key and value (and dealing with leading/trailing whitespace, double quotes around values, etc.)
                    return new IniToken(IniTokenKind.Property, lexeme, lineTerminator, leadingTrivia, null);
                }

                // anything else is an invalid token
                return new IniToken(IniTokenKind.Invalid, lexeme, lineTerminator, leadingTrivia, null);
            }
        }
    }

    #region Helper functions

    private record IniTextLine
    {
        public List<char> Text;
        public IniExplicitLineTerminatorOption LineTerminator;

        public IniTextLine(List<char> text, IniExplicitLineTerminatorOption lineTerminator)
        {
            this.Text = text;
            this.LineTerminator = lineTerminator;
        }
    }
    //
    private IniTextLine? ReadIniTextLine()
    {
        if (_remainingContents.Count == 0)
        {
            return null;
        }

        var text = new List<char>();

        // capture all contents up to end of line or end of file
        while (_remainingContents.Count > 0)
        {
            var ch = _remainingContents.First();
            switch (ch)
            {
                case '\r':
                case '\n':
                    // new line

                    var lineTerminator = this.GetLineTerminator();
                    //
                    return new IniTextLine(text, lineTerminator);
                case (char)0x1A:
                    // end of file marker

                    // capture the end of file marker
                    text.Add(ch);
                    _remainingContents.RemoveAt(0);

                    // empty any remaining contents (i.e. stop at a hard EOF marker)
                    _remainingContents.Clear();

                    return new IniTextLine(text, IniExplicitLineTerminatorOption.None);
                default:
                    // any other character

                    // capture the character
                    text.Add(ch);
                    _remainingContents.RemoveAt(0);

                    break;
            }
        }

        // if we reach here, we have captured a line without an end of line marker; return the contents
        return new IniTextLine(text, IniExplicitLineTerminatorOption.None);
    }

    private IniExplicitLineTerminatorOption GetLineTerminator()
    {
        if (_remainingContents.Count == 0)
        {
            return IniExplicitLineTerminatorOption.None;
        }

        var ch0 = _remainingContents[0];

        switch (ch0)
        {
            case '\r':
                // \r or \r\n
                {
                    _remainingContents.RemoveAt(0);
                    //
                    // if there are characters remaining, see if we can find an \n after this \r 
                    if (_remainingContents.Count > 0)
                    {
                        var ch1 = _remainingContents[0];
                        if (ch1 == '\n')
                        {
                            // \r\n
                            _remainingContents.RemoveAt(0);
                            return IniExplicitLineTerminatorOption.CrLf;
                        }
                    }

                    // if we did not find an \n after the \r, return just \r

                    // \r
                    return IniExplicitLineTerminatorOption.Cr;
                }
            case '\n':
                // \n
                {
                    _remainingContents.RemoveAt(0);

                    return IniExplicitLineTerminatorOption.Lf;
                }
            default:
                Debug.Assert(false, "Seeking line terminator characters but found another character instead.");
                return IniExplicitLineTerminatorOption.None;
        }
    }

    #endregion
}
