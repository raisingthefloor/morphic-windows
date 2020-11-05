namespace Morphic.Settings.SettingsHandlers.Ini
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// INI File parser.
    /// </summary>
    public abstract partial class IniFile
    {
        public class IniOptions
        {
            public string Eol { get; set; } = string.Empty;
            public bool FirstDuplicate { get; set; }
            public bool Strings { get; set; }

            /// <summary>
            /// How multi-line values are written:
            /// - ''' or """: Surround the value with 3x single or double quotes (default).
            /// - indent: Indent the additional lines.
            /// - escape: Use an escaped n (\n).
            /// - anything else: Wrap the value with the given value.
            /// </summary>
            public string MultilineStyle { get; set; } = "\"\"\"";

            public QuoteOptions Quote { get; set; } = QuoteOptions.Auto;
            public string QuoteChar { get; set; } = "\"";
            public string KeyValueDelimiter { get; set; } = "=";
            public bool KeepUnsetValues { get; set; }

            public enum QuoteOptions
            {
                /// <summary>Only quotes values that begin or end with a space.</summary>
                Auto = 0,
                Never = 1,
                Always = 2
            }
        }

        protected readonly string RemoveValue = "\f\a\r\t:morphic";
        protected static readonly string PathSep = "\n";
        protected static readonly char PathSepChar = '\n';

        public IniOptions Options { get; set; } = new IniOptions();

        protected string? content;
        public string? FilePath { get; protected set; }

        /// <summary>Sets the file, and reads the content.</summary>
        public virtual void SetFile(string path)
        {
            this.FilePath = path;
            this.content = File.ReadAllText(this.FilePath);
        }

        /// <summary>Read the file, invoking OnSectionBegin, OnSectionEnd, and OnValue as required.</summary>
        protected void Parse()
        {
            if (this.content == null)
            {
                throw new InvalidOperationException("Content has not been set for the INI file.");
            }

            if (string.IsNullOrEmpty(this.Options.Eol))
            {
                if (!this.content.Contains("\n"))
                {
                    this.Options.Eol = Environment.NewLine;
                }
                else if (this.content.Contains("\r\n"))
                {
                    this.Options.Eol = "\r\n";
                }
                else
                {
                    this.Options.Eol = "\n";
                }
            }

            string output = string.Empty;
            string lfContent = ReplaceEol(this.content, "\n");
            if (this.Writer)
            {
                output = ParseIniFile.Replace(lfContent, match => this.ProcessMatch(new IniFileMatch(match)));
            }
            else
            {
                foreach (IniFileMatch match in ParseIniFile.Matches(lfContent).Select(m => new IniFileMatch(m)))
                {
                    _ = this.ProcessMatch(match);
                }
            }

            string? append = this.EndSection(0, true);
            if (this.Writer && append != null)
            {
                bool hasEol = output.EndsWith("\n");
                if (!hasEol)
                {
                    output += "\n";
                    if (append.EndsWith("\n"))
                    {
                        append = append.Substring(0, append.Length - 1);
                    }
                }

                output += append;
            }

            this.Result = ReplaceEol(output, this.Options.Eol);
        }

        protected static string ReplaceEol(string input, string eol)
        {
            string normalised = input.Replace("\r\n", "\n").Replace("\r", "\n");
            return eol != "\n" ? normalised.Replace("\n", eol) : normalised;
        }


        public string? Result { get; set; }

        protected readonly Stack<string> SectionPath = new Stack<string>();
        protected string CurrentSection => this.SectionPath.Peek();

        protected Dictionary<string, string> Data = new Dictionary<string, string>();

        /// <summary>
        /// Called when a value has been parsed.
        /// </summary>
        /// <param name="key">Value name.</param>
        /// <param name="value">Value value.</param>
        /// <param name="quoted">true if the value was quoted.</param>
        /// <param name="updated">set to true if the value should be updated in the file.</param>
        /// <returns>If update is true, this is the new value to write back to the file. null to remove the entry.</returns>
        protected abstract string? OnValue(string key, string value, bool quoted, out bool updated);

        /// <summary>Called at the beginning of a section</summary>
        protected abstract string? OnSectionBegin();

        /// <summary>Called at the end of a section</summary>
        protected abstract string? OnSectionEnd(bool eof);

        /// <summary>Set to true if writing.</summary>
        protected virtual bool Writer => false;

        private static readonly Regex MatchEol = new Regex("(\r?\n)", RegexOptions.Compiled);

        /// <summary>Handles a match from the regular expression.</summary>
        private string ProcessMatch(IniFileMatch match)
        {
            string result = this.Writer ? match.All : string.Empty;

            if (match.Section != null)
            {
                // Parsed a section header.
                int depth = match.SectionCount;
                if (depth > this.SectionPath.Count + 1)
                {
                    Console.WriteLine("INI file: Current section is not a direct descendant of the previous section");
                }

                string? endContent = this.EndSection(depth, false);
                if (this.Writer && endContent != null)
                {
                    result = endContent + result;
                }

                this.SectionPath.Push(match.Section);

                string? prepend = this.OnSectionBegin();
                if (this.Writer && prepend != null)
                {
                    result = prepend + result;
                }
            }
            else
            {
                // Parsed an item.

                // Get the value.
                string value = match.Value ?? match.Value_ml_indent ?? match.Value_ml_quote ?? match.Value_quote
                    ?? throw new InvalidOperationException("No value");

                if (match.Value_quote != null)
                {
                    value = value.Replace("\\\"", "\"");
                }
                else if (match.Value_ml_indent != null || match.Value_ml_quote != null)
                {
                    value = UnIndent.Replace(value, this.Options.Eol);
                }

                if (value.Contains("\n"))
                {
                    value = ReplaceEol(value, this.Options.Eol);
                }

                bool quoted = match.Value_ml_quote != null || match.Value_quote != null;

                // Call the handler
                string? newValue = this.OnValue(match.Key, value, quoted, out bool updated);

                // Update the file, if required.
                if (this.Writer && updated)
                {
                    if (newValue == null)
                    {
                        result = string.Empty;
                    }
                    else
                    {
                        string quote = match.Qqq ?? match.Q ?? string.Empty;

                        if (match.Value_ml_indent != null)
                        {
                            newValue = MatchEol.Replace(newValue, $"{this.Options.Eol}{match.Indent}    ");
                        }

                        result = match.Prefix + quote + newValue + quote + match.Suffix;
                    }
                }
            }

            return result;
        }


        /// <summary>
        /// Called during parsing when a [section] has ended, either due to the start of another section, or at the end
        /// of file.
        /// Adjust the current path to match the new depth, calling the sectionEnd callback for each level.
        /// </summary>
        /// <param name="depth"></param>
        /// <param name="eof"></param>
        /// <returns></returns>
        private string? EndSection(int depth, bool eof)
        {
            string? result = string.Empty;
            while (depth <= this.SectionPath.Count)
            {
                string? endContent = this.OnSectionEnd(eof);
                if (this.Writer && endContent != null)
                {
                    result += endContent;
                }

                if (this.SectionPath.Count == 0)
                {
                    break;
                }

                this.SectionPath.Pop();
            }

            return result;
        }

        protected string GetKeyPath(string[]? path = null, string? key = null)
        {
            path ??= this.SectionPath.Reverse().ToArray();
            string pathString = path.Length == 0
                ? string.Empty
                : string.Join(PathSepChar, path) + PathSep;
            return key == null
                ? pathString
                : $"{pathString}{key}";
        }

        protected bool IsSection(string keyPath)
        {
            return keyPath.EndsWith(PathSepChar);
        }

        protected void AddValue(string key, string value)
        {
            string keyPath = this.GetKeyPath(null, key);
            this.Data[keyPath] = value;
        }

        protected bool ValueExists(string[]? path, string key)
        {
            string keyPath = this.GetKeyPath(path, key);
            return this.Data.ContainsKey(keyPath);
        }

        protected string? GetValue(string[]? path, string key)
        {
            string keyPath = this.GetKeyPath(path, key);
            return this.Data.TryGetValue(keyPath, out string? value)
                ? value
                : null;
        }
    }
}
