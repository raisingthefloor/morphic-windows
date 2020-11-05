namespace Morphic.Settings.SettingsHandlers.Ini
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using SolutionsRegistry;

    /// <summary>
    /// Writes a dictionary to an existing ini file.
    /// </summary>
    [SrService]
    public class IniFileWriter : IniFile
    {
        private string[] currentPath = { };
        private readonly HashSet<string> valuesWritten = new HashSet<string>();
        private readonly HashSet<string> sectionsWritten = new HashSet<string>();

        private readonly Dictionary<string, Dictionary<string, string>> sections =
            new Dictionary<string, Dictionary<string, string>>();

        protected override bool Writer => true;

        private readonly Regex swapDots = new Regex(@"(?<!\\)\.", RegexOptions.Compiled);

        /// <summary>
        /// Write the given data to the file.
        /// </summary>
        /// <param name="data">Dictionary of values to write.</param>
        /// <returns>The new content.</returns>
        public IniFileWriter Write(Dictionary<string, string?> data)
        {
            // Flat dictionary of all values.
            this.Data = new Dictionary<string, string>(data.Select(kv =>
                new KeyValuePair<string, string>(this.swapDots.Replace(kv.Key, PathSep).Replace("\\.", "."),
                    kv.Value ?? this.RemoveValue)));

            // Get the sections
            foreach ((string keyPath, string value) in this.Data)
            {
                string sectionPath = GetSection(keyPath);
                if (!this.sections.TryGetValue(sectionPath, out Dictionary<string, string>? sectionValues))
                {
                    sectionValues = new Dictionary<string, string>();
                    this.sections.Add(sectionPath, sectionValues);
                }

                sectionValues[keyPath] = value;
            }

            this.Parse();
            return this;
        }

        public virtual Task Save()
        {
            return File.WriteAllTextAsync(this.FilePath, this.content);
        }

        /// <summary>Gets the section from a key path.</summary>
        private static string GetSection(string keyPath)
        {
            int pos = keyPath.LastIndexOf(PathSepChar);
            return pos < 0
                ? string.Empty
                : keyPath.Substring(0, pos);
        }
        /// <summary>Gets the key name from a key path.</summary>
        private static string GetKeyName(string keyPath)
        {
            int pos = keyPath.LastIndexOf(PathSepChar);
            return pos < 0
                ? keyPath
                : keyPath.Substring(pos + 1);
        }

        private IEnumerable<KeyValuePair<string, string>> GetSectionValues(string[]? path = null)
        {
            if (this.sections.TryGetValue(this.GetKeyPath(path).TrimEnd(PathSepChar), out Dictionary<string, string>? sectionValues))
            {
                return sectionValues;
            }
            else
            {
                return ImmutableDictionary<string, string>.Empty;
            }
        }

        private IEnumerable<string> GetSectionSections(string[]? path = null)
        {
            return this.GetSectionSections(this.GetKeyPath(path));
        }
        private IEnumerable<string> GetSectionSections(string keyPath)
        {
            if (keyPath.Length > 0)
            {
                keyPath += PathSep;
                return this.sections.Keys.Where(k => k.StartsWith(keyPath));
            }
            else
            {
                return this.sections.Keys.Where(k => k.Length > 0 && !k.Contains(PathSep));
            }

        }

        private IEnumerable<KeyValuePair<string, string>> GetSectionValues(bool includeSubsections,
            string sectionPath)
        {
            IEnumerable<KeyValuePair<string, string>> items = this.Data.Where(kv => kv.Key.StartsWith(sectionPath));
            if (!includeSubsections)
            {
                items = items.Where(kv =>
                {
                    string keyName = kv.Key.Substring(sectionPath.Length);
                    return !keyName.Contains(PathSepChar);
                });
            }
            else
            {
                items = items.Where(kv =>
                {
                    string keyName = kv.Key.Substring(sectionPath.Length);
                    return !keyName.Contains(PathSepChar) || (this.IsSection(keyName) && kv.Value == sectionPath);
                });
            }

            return items.OrderBy(kv => kv.Key);
        }

        private void SetWritten(string[]? path, string key)
        {
            string pathString = this.GetKeyPath(path, key);
            this.valuesWritten.Add(pathString);
        }
        protected override string? OnValue(string key, string value, bool quoted, out bool updated)
        {
            string? result = null;
            updated = false;

            if (this.ValueExists(null, key))
            {
                string? newValue = this.GetValue(null, key);
                if ((newValue == null) || (newValue == this.RemoveValue))
                {
                    updated = true;
                    result = this.Options.KeepUnsetValues ? string.Empty : null;
                }
                else if (value != newValue)
                {
                    updated = true;
                    result = this.Stringify(newValue);
                }

                this.SetWritten(null, key);
            }

            return result;
        }

        private string[] lastSectionPath = new string[0];

        protected override string? OnSectionBegin()
        {
            List<string> output = new List<string>();
            if (this.SectionPath.Count > 0)
            {
                // Entering a sub-section of this one.
                // Add the values of the current section that haven't already been added (these are new values).
                output.AddRange(this.AddUnwrittenItems(this.currentPath));
            }

            this.currentPath = this.SectionPath.ToArray();
            this.valuesWritten.Add(this.GetKeyPath());

            return output.Count == 0
                ? string.Empty
                : string.Join(this.Options.Eol, output) + this.Options.Eol;
        }

        private IEnumerable<string> AddUnwrittenItems(string[] path)
        {
            foreach ((string? keyPath, string? value) in this.GetSectionValues(path)
                .Where(kv => !this.valuesWritten.Contains(kv.Key)))
            {
                string? newValue = this.WriteValue(keyPath, value);
                if (newValue != null)
                {
                    yield return newValue;
                }
            }
        }


        protected override string? OnSectionEnd(bool eof)
        {
            List<string> output = new List<string>();

            if (eof || this.SectionPath.Count == 0)
            {
                // end of a file where there are no sections (so begin section would not have been called)
                string? content = this.OnSectionBegin();
                if (!string.IsNullOrEmpty(content))
                {
                    output.Add(content.Trim());
                }
            }

            // Add the sub-sections which haven't already been added.
            foreach (string sectionPath in this.GetSectionSections())
            {
                output.AddRange(this.WriteSection(sectionPath).Where(l => l != null)!);
            }

            this.sectionsWritten.Add(this.GetKeyPath().TrimEnd(PathSepChar));

            return output.Count == 0
                ? string.Empty
                : string.Join(this.Options.Eol, output) + this.Options.Eol;
        }

        private string Stringify(string value)
        {
            return value;
        }

        /// <summary>Writes a new value.</summary>
        private string? WriteValue(string keyPath, string value)
        {
            if (this.IsSection(keyPath))
            {
                return string.Join(this.Options.Eol, this.WriteSection(keyPath));
            }

            this.valuesWritten.Add(keyPath);

            if (value == this.RemoveValue)
            {
                return null;
            }

            string newValue = this.Stringify(value);
            if (newValue.Contains("\n"))
            {
                newValue = this.Options.MultilineStyle switch
                {
                    "indent" => newValue.Replace("\n", "\n    ") + "\n",
                    "escape" => newValue.Replace("\n", "\\n").Replace("\r", "\\r"),
                    _ => this.Options.MultilineStyle + newValue + this.Options.MultilineStyle
                };
            }
            else
            {
                bool quote = this.Options.Quote switch
                {
                    IniOptions.QuoteOptions.Always => true,
                    IniOptions.QuoteOptions.Auto when
                        newValue.Length > 0 && (char.IsWhiteSpace(newValue[0]) || char.IsWhiteSpace(newValue.Last())) => true,
                    _ => false
                };

                if (quote)
                {
                    newValue = this.Options.QuoteChar + newValue + this.Options.QuoteChar;
                }
            }

            return GetKeyName(keyPath) + this.Options.KeyValueDelimiter + newValue;
        }

        /// <summary>Writes a new section, and its sub-sections</summary>
        private IEnumerable<string?> WriteSection(string keyPath)
        {
            if (this.sectionsWritten.Add(keyPath))
            {
                int depth = keyPath.Count(c => c == '\n') + 1;
                string sectionName = keyPath.Substring(keyPath.LastIndexOf('\n') + 1);
                yield return "";
                yield return new string('[', depth) + sectionName + new string(']', depth);

                foreach ((string? key, string? value) in this.GetSectionValues(true, keyPath + "\n"))
                {
                    yield return this.WriteValue(key, value);
                }

                foreach (string sectionSection in this.GetSectionSections(keyPath))
                {
                    foreach (string? s in this.WriteSection(sectionSection))
                    {
                        yield return s;
                    }
                }
            }
        }
    }
}

