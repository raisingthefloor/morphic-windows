using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;

namespace Morphic.Settings.Ini
{
    public class Configuration
    {

        public static async Task<Configuration?> Open(string path)
        {
            try
            {
                var iniLines = await File.ReadAllLinesAsync(path);
                if (TryParse(iniLines, out var configuration))
                {
                    return configuration;
                }
            }
            catch
            {
            }
            return null;
        }

        public async Task Write(string path)
        {
            await File.WriteAllLinesAsync(path, GetIniLines());
        }

        public string? Get(string section, string key)
        {
            if (sectionsByName.TryGetValue(section, out var configurationSection))
            {
                if (configurationSection.ValuesByKey.TryGetValue(key, out var configurationValue))
                {
                    return configurationValue.Value;
                }
            }
            return null;
        }

        public bool IsChanged { get; private set; } = false;

        public void Set(string section, string key, string? value)
        {
            IsChanged = true;
            ConfigurationSection? configurationSection;
            ConfigurationValue? configurationValue;
            if (!sectionsByName.TryGetValue(section, out configurationSection))
            {
                configurationSection = new ConfigurationSection("", section);
                configurationSection.Index = lines.Count;
                sectionsByName.Add(section, configurationSection);
                lines.Add(configurationSection);
            }
            if (configurationSection.ValuesByKey.TryGetValue(key, out configurationValue))
            {
                configurationValue.Value = value;
            }
            else
            {
                var indentation = configurationSection.Indentation;
                var keyTrailingWhitespace = "";
                var delimiterTrailingWhitespace = "";
                char delimiter = '=';
                var index = configurationSection.Index + 1;
                if (configurationSection.Values.Count > 0)
                {
                    var previousValue = configurationSection.Values[configurationSection.Values.Count - 1];
                    indentation = previousValue.Indentation;
                    keyTrailingWhitespace = previousValue.KeyTrailingWhitespace;
                    delimiter = previousValue.Delimiter;
                    delimiterTrailingWhitespace = previousValue.DelimiterTrailingWhitespace;
                    index = previousValue.Index + 1;
                }
                configurationValue = new ConfigurationValue(indentation, key, keyTrailingWhitespace, delimiter, delimiterTrailingWhitespace, value, "");
                configurationValue.Index = index;
                configurationSection.Add(configurationValue);
                lines.Insert(index, configurationValue);
            }
        }

        public IEnumerable<string> GetIniLines()
        {
            foreach (var line in lines)
            {
                yield return line.ToIniString();
            }
        }

        private Configuration(List<ConfigurationLine> lines)
        {
            this.lines = lines;
            sectionsByName = new Dictionary<string, ConfigurationSection>();
            foreach (var line in lines)
            {
                if (line is ConfigurationSection section)
                {
                    sectionsByName.Add(section.Name, section);
                }
            }
        }

        private List<ConfigurationLine> lines;

        private Dictionary<string, ConfigurationSection> sectionsByName;

        private static bool TryParse(string[] iniLines, out Configuration? configuration)
        {
            var lines = new List<ConfigurationLine>();
            ConfigurationLine? previousLine = null;
            ConfigurationSection? currentSection = null;
            foreach (var iniLine in iniLines)
            {
                if (ConfigurationLine.TryPrase(iniLine, previousLine, out var line))
                {
                    if (line != previousLine)
                    {
                        line.Index = lines.Count;
                        lines.Add(line);
                        previousLine = line;
                        if (line is ConfigurationSection section)
                        {
                            currentSection = section;
                        }
                        else if (line is ConfigurationValue value)
                        {
                            if (currentSection == null)
                            {
                                // Section expected
                                configuration = null;
                                return false;
                            }
                            currentSection.Add(value);
                        }
                    }
                }
                else
                {
                    configuration = null;
                    return false;
                }
            }
            configuration = new Configuration(lines);
            return true;
        }

    }

    internal class ConfigurationLine
    {

        public int Index = -1;
        public string Indentation;

        public ConfigurationLine(string indendation)
        {
            Indentation = indendation;
        }

        public static bool TryPrase(string iniLine, ConfigurationLine? previousLine, out ConfigurationLine line)
        {
            var indentation = "";
            var i = 0;
            while (i < iniLine.Length && IsWhitespace(iniLine[i]))
            {
                indentation += iniLine[i];
                ++i;
            }

            // Comment
            if (iniLine[i] == '#' || iniLine[i] == ';')
            {
                var delimiter = iniLine[i];
                ++i;
                var leadingWhitespace = "";
                while (i < iniLine.Length && IsWhitespace(iniLine[i]))
                {
                    leadingWhitespace += iniLine[i];
                    ++i;
                }
                var text = iniLine.Substring(i);
                line = new ConfigurationComment(indentation, delimiter, leadingWhitespace, text);
                return true;
            }

            // Section
            if (iniLine[i] == '[')
            {
                var name = "";
                ++i;
                while (i < iniLine.Length && iniLine[i] != ']')
                {
                    name += iniLine[i];
                    ++i;
                }
                if (iniLine[i] == ']')
                {
                    ++i;
                    while (i < iniLine.Length && IsWhitespace(iniLine[i]))
                    {
                        ++i;
                    }
                    if (i == iniLine.Length)
                    {
                        line = new ConfigurationSection(indentation, name.Trim());
                        return true;
                    }
                }
                line = null!;
                return false;
            }

            // Value
            var key = "";
            var keyTrailingWhitespace = "";
            char keyValueDelimiter = '=';
            var delimiterTrailingWhitespace = "";
            string? value = null;
            var valueTrailingWhitespace = "";
            while (i < iniLine.Length && iniLine[i] != ':' && iniLine[i] != '=')
            {
                key += iniLine[i];
                ++i;
            }
            var j = key.Length - 1;
            while (j >= 0 && IsWhitespace(key[j]))
            {
                keyTrailingWhitespace = key[j] + keyTrailingWhitespace;
                --j;
            }
            key = key.Substring(0, j + 1);

            if (i < iniLine.Length && (iniLine[i] == ':' || iniLine[i] == '='))
            {
                if (key.Length == 0)
                {
                    // Expecting key before = or :
                    line = null!;
                    return false;
                }
                keyValueDelimiter = iniLine[i];
                while (i < iniLine.Length && IsWhitespace(iniLine[i]))
                {
                    delimiterTrailingWhitespace += iniLine[i];
                    ++i;
                }
                value = iniLine.Substring(i);
                j = value.Length - 1;
                while (j >= 0 && IsWhitespace(value[j]))
                {
                    valueTrailingWhitespace = value[j] + valueTrailingWhitespace;
                    --j;
                }
                value = value.Substring(0, j + 1);
            }
            else
            {
                // Blank line
                if (key.Length == 0)
                {
                    line = new ConfigurationLine(indentation);
                    return true;
                }
            }

            // Check for wrapped lines and unwrap into the previous line's value
            if (previousLine is ConfigurationValue previousValueLine)
            {
                if (indentation.Length > previousValueLine.Indentation.Length)
                {
                    previousValueLine.Value += " " + value;
                    previousValueLine.ValueTrailingWhitespace = valueTrailingWhitespace;
                    line = previousValueLine;
                    return true;
                }
            }

            line = new ConfigurationValue(indentation, key, keyTrailingWhitespace, keyValueDelimiter, delimiterTrailingWhitespace, value, valueTrailingWhitespace);
            return true;
        }

        public static bool IsWhitespace(char c)
        {
            return char.IsWhiteSpace(c);
        }

        public virtual string ToIniString()
        {
            return Indentation;
        }
    }

    internal class ConfigurationComment : ConfigurationLine
    {

        public char Delimiter;
        public string LeadingWhitespace;
        public string Text;

        public ConfigurationComment(string indendation, char delimiter, string leadingWhitespace, string text) : base(indendation)
        {
            Delimiter = delimiter;
            LeadingWhitespace = leadingWhitespace;
            Text = text;
        }

        public override string ToIniString()
        {
            return Indentation + Delimiter + LeadingWhitespace + Text;
        }
    }

    internal class ConfigurationSection: ConfigurationLine
    {

        public string Name = "";

        public List<ConfigurationValue> Values = new List<ConfigurationValue>();

        public Dictionary<string, ConfigurationValue> ValuesByKey = new Dictionary<string, ConfigurationValue>();

        public ConfigurationSection(string indentation, string name): base(indentation)
        {
            Name = name;
        }

        public void Add(ConfigurationValue value)
        {
            Values.Add(value);
            ValuesByKey.Add(value.Key, value);
        }

        public override string ToIniString()
        {
            return String.Format("{0}[{1}]", Indentation, Name);
        }

    }

    internal class ConfigurationValue: ConfigurationLine
    {
        public string Key;
        public string KeyTrailingWhitespace;
        public char Delimiter;
        public string DelimiterTrailingWhitespace;
        public string? Value;
        public string ValueTrailingWhitespace;

        public ConfigurationValue(string indentation, string key, string keyTrailingWhitespace, char delimiter, string delimiterTrailingWhitespace, string? value, string valueTrailingWhitespace): base(indentation)
        {
            Key = key;
            KeyTrailingWhitespace = keyTrailingWhitespace;
            Delimiter = delimiter;
            DelimiterTrailingWhitespace = delimiterTrailingWhitespace;
            Value = value;
            ValueTrailingWhitespace = valueTrailingWhitespace;
        }

        public override string ToIniString()
        {
            if (Value is string value)
            {
                // FIXME: what if value contains newlines?
                return Indentation + Key + KeyTrailingWhitespace + Delimiter + DelimiterTrailingWhitespace + value + ValueTrailingWhitespace;
            }
            return Indentation + Key + KeyTrailingWhitespace;
        }
    }
}
