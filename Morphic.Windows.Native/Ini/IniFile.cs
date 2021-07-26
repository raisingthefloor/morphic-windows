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

        internal IniFile(List<IniProperty> properties, List<IniSection> sections)
        {
            this.Properties = properties;
            this.Sections = sections;
        }

        #region Parser 

        public static IMorphicResult<IniFile> LoadContents(string contents)
        {
            List<IniProperty> properties = new List<IniProperty>();
            List<IniSection> sections = new List<IniSection>();

            IniSection? currentSection = null;

            var lexer = new IniLexer(contents);
            while (true)
            {
                IniToken iniToken = lexer.GetNextToken();

                // if we've reached the last token, return
                if (iniToken.Kind == IniTokenKind.EndOfFile)
                {
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
            return IMorphicResult<IniFile>.SuccessResult(result);

        }

        #endregion Parser

    }
}
