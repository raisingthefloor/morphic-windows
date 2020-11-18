namespace Morphic.Settings.SettingsHandlers.Ini
{
    using System.Collections.Generic;
    using System.Linq;
    using SolutionsRegistry;

    /// <summary>
    /// Reads values from an INI file.
    /// </summary>
    [SrService]
    public class IniFileReader : IniFile
    {
        /// <summary>Parses the ini file</summary>
        /// <returns>All data found in the file.</returns>
        public Dictionary<string, string> ReadData()
        {
            this.Parse();
            return this.Data.ToDictionary(
                kv => kv.Key.Replace(".", "\\.").Replace("\n", "."),
                kv => kv.Value);
        }

        protected override bool Writer => false;

        protected override string? OnSectionBegin()
        {
            return null;
        }

        protected override string? OnValue(string key, string value, bool quoted, out bool updated)
        {
            updated = false;
            bool set = !this.Options.FirstDuplicate || !this.ValueExists(null, key);
            if (!this.Options.Strings && !quoted)
            {
                // Support non-string values?
            }

            if (set)
            {
                this.AddValue(key, value);
            }

            return null;
        }

        protected override string? OnSectionEnd(bool eof)
        {
            return null;
        }
    }
}
