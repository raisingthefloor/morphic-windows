namespace Morphic.Settings.SettingsHandlers.Ini
{
    using System.Text.RegularExpressions;

    public partial class IniFile
    {
        private static readonly Regex UnIndent = new Regex(@"\n[^\S\n]+", RegexOptions.Compiled);
        private static readonly Regex ParseIniFile = new Regex(@"
# The ini file regular expression.
# Groups:
#  - section: The section name
#  - sectionCount: The [ characters.
#  or:
#  - key: The name of the value
#  - value, value_ml_indent, value_ml_quote, or value_quote: The value.
#  - prefix: Everything up to the value
#  - indent: The indentation
#  - qqq: The quotes used around value_lines
#  - q: The quote used around value_quote
#  - suffix: Everything after the value + quotes (normally just \n)

# Skip leading space and comments
^
(?!\s*[;#]) # Comments
(
    # Section name, including surrounding brackets
    \s*((?<sectionCount>\[+)(?<section>[^\]]+)\]+)
|
    # key=value
    (
        # everything before the value
        (?<prefix>
            (?<indent>[^\S\n]*)
            # The key name
            (?<key>[^\n=]*?)
            ((?!$)\s)*[=:]((?!$)\s)*
        )
        # values
        (
            # multi-line, wrapped with 3 double or single quotes
            ((?<qqq>""{3}|'{3})(?<value_ml_quote>((?!\k<qqq>).|[\n])*)\k<qqq>)
            # quoted
            |((?<q>[""'])(?<value_quote>[^\n]*)\k<q>)
            # unquoted multi-line
            |(?<value_ml_indent>
                [^\n]* # first line
                (\n\k<indent>[^\S\n]+(?![#;\s])[^\n]*(?=\n))+ # next indented lines
            )
            # plain
            |(?<value>[^\r\n]*$)
        )
        # whitespace after the value
        (?<suffix>
            [^\S\n]*(\r?\n)?
        )
    )
)
        ", RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace | RegexOptions.Multiline | RegexOptions.ExplicitCapture);

    }

    public class IniFileMatch
    {
        private Match match;
        private GroupCollection groups;

        /// <summary>Entire match</summary>
        public string All => this.match.Value;

        /// <summary>The ['s in the section block</summary>
        public int SectionCount => this.GroupLength("sectionCount");

        /// <summary>Section name</summary>
        public string? Section => this.GroupValue("section");

        /// <summary>Everything up to the value.</summary>
        public string? Prefix => this.GroupValue("prefix");

        /// <summary>The indentation</summary>
        public string? Indent => this.GroupValue("indent");

        /// <summary>Name of the value</summary>
        public string Key => this.GroupValue("key") ?? string.Empty;

        /// <summary>The quotes around multi-line value (""")</summary>
        public string? Qqq => this.GroupValue("qqq");

        /// <summary>Multi-line value</summary>
        public string? Value_ml_quote => this.GroupValue("value_ml_quote");

        /// <summary>The quote around a single-line value (")</summary>
        public string? Q => this.GroupValue("q");

        /// <summary>Quoted value</summary>
        public string? Value_quote => this.GroupValue("value_quote");

        /// <summary>Indented multi-line value</summary>
        public string? Value_ml_indent => this.GroupValue("value_ml_indent");

        /// <summary>Multi-line value indentation</summary>
        public string? Value_indent => this.GroupValue("value_indent");

        /// <summary>A value</summary>
        public string? Value => this.GroupValue("value");

        /// <summary>Text after the value or final quote</summary>
        public string? Suffix => this.GroupValue("suffix");

        private int GroupLength(string groupName)
        {
            Group group = this.match.Groups[groupName];
            return group.Success ? group.Length : 0;
        }
        private string? GroupValue(string groupName)
        {
            Group group = this.match.Groups[groupName];
            return group.Success ? group.Value : null;
        }


        public IniFileMatch(Match match)
        {
            this.match = match;
            this.groups = this.match.Groups;
        }
    }
}
