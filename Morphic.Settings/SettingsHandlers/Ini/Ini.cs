using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Morphic.Core;
using Morphic.Settings.SolutionsRegistry;
using Morphic.Windows.Native.Ini;

namespace Morphic.Settings.SettingsHandlers.Ini
{
    /// <summary>
    /// Handles the reading and writing of INI files.
    /// </summary>
    [SrService]
    public class Ini
    {
        protected IniFile? IniFile;

        /// <summary>The loaded ini file content.</summary>
        protected string? Content { get; set; }

        /// <summary>The path to the ini file being read from.</summary>
        public string FilePath { get; protected set; } = null!;

        /// <summary>
        /// Reads the given ini file.
        /// </summary>
        /// <param name="filePath">The file to read from.</param>
        public virtual async Task ReadFile(string filePath)
        {
            this.FilePath = filePath;
            this.Content = File.Exists(this.FilePath)
                ? await File.ReadAllTextAsync(this.FilePath)
                : string.Empty;
            this.Parse();
        }

        /// <summary>
        /// Parse the ini file.
        /// </summary>
        /// <exception cref="InvalidOperationException">INI file has not been read yet.</exception>
        /// <exception cref="SolutionsRegistryException">INI file parser failure.</exception>
        public void Parse()
        {
            if (this.Content is null)
            {
                throw new InvalidOperationException("INI file has not been loaded");
            }

            MorphicResult<IniFile, MorphicUnit> result = IniFile.CreateFromString(this.Content);
            this.IniFile = result.IsSuccess
                ? result.Value
                : throw new SolutionsRegistryException($"INI file parsing failed for ${this.FilePath}");
        }

        /// <summary>
        /// Write the data to the ini file.
        /// </summary>
        /// <param name="filePath">The file to write to.</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">INI file has not been read yet.</exception>
        public virtual Task WriteFile(string filePath)
        {
            if (this.IniFile is null)
            {
                throw new InvalidOperationException("INI file has not been parsed");
            }

            return File.WriteAllTextAsync(filePath, this.IniFile.ToString());
        }

        /// <summary>
        /// Splits a string which identifies a dot (.) delimited path to a property. Dots in section names or property
        /// keys can be escaped using a backslash (\).
        /// </summary>
        /// <param name="path">The property path.</param>
        /// <returns>Array of path segments.</returns>
        private static string[] GetPathSegments(string path)
        {
            return Regex.Split(path, @"(?<!\\)\.")
                .Select(segment => segment.Replace("\\.", "."))
                .ToArray();
        }

        /// <summary>
        /// Gets a property value.
        /// </summary>
        /// <param name="path">The dot delimited path to the property.</param>
        /// <returns>The value of the property, null if not found.</returns>
        /// <exception cref="InvalidOperationException">INI file has not been read yet.</exception>
        public string? GetValue(string path)
        {
            if (this.IniFile is null)
            {
                throw new InvalidOperationException("INI file has not been parsed");
            }

            string[] parts = GetPathSegments(path);

            string propertyName = parts.Last();
            string? sectionName = parts.Length > 1 ? parts[0] : null;

            List<IniProperty>? properties = sectionName is null
                ? this.IniFile.Properties
                : this.IniFile.Sections.FirstOrDefault(s => s.Name == sectionName)?.Properties;

            IniProperty? property = properties?.FirstOrDefault(p => p.Key == propertyName);

            return property?.Value;
        }

        /// <summary>
        /// Sets a property value. If the given value is null, the property is removed.
        /// </summary>
        /// <param name="path">The dot delimited path to the property.</param>
        /// <param name="value">The new value.</param>
        /// <exception cref="InvalidOperationException">INI file has not been read yet.</exception>
        public void SetValue(string path, string? value)
        {
            if (this.IniFile is null)
            {
                throw new InvalidOperationException("INI file has not been parsed");
            }

            string[] parts = GetPathSegments(path);

            string propertyName = parts.Last();
            string? sectionName = parts.Length > 1 ? parts[0] : null;

            List<IniProperty> properties;
            if (sectionName is null)
            {
                properties = this.IniFile.Properties;
            }
            else
            {
                IniSection? section = this.IniFile.Sections.FirstOrDefault(s => s.Name == sectionName);
                if (section is null)
                {
                    section = new IniSection(sectionName);
                    this.IniFile.Sections.Add(section);
                }

                properties = section.Properties;
            }

            IniProperty? property = properties.FirstOrDefault(p => p.Key == propertyName);
            if (value is null)
            {
                if (property is not null)
                {
                    properties.Remove(property);
                }
            }
            else if (property is null)
            {
                properties.Add(new IniProperty(propertyName, value));
            }
            else
            {
                property.Value = value;
            }
        }

        /// <summary>
        /// Read the entire ini file into a flat dictionary, whose keys are dot delimited paths.
        /// </summary>
        /// <returns>Dictionary containing every property in the ini file.</returns>
        /// <exception cref="InvalidOperationException">INI file has not been read yet.</exception>
        public Dictionary<string,string> ReadData()
        {
            if (this.IniFile is null)
            {
                throw new InvalidOperationException("INI file has not been parsed");
            }

            Dictionary<string, string> data = new();

            foreach (IniProperty property in this.IniFile.Properties)
            {
                string key = property.Key.Replace(".", "\\.");
                data[key] = property.Value;
            }

            foreach (IniSection section in this.IniFile.Sections)
            {
                foreach (IniProperty property in section.Properties)
                {
                    string sectionName = section.Name.Replace(".", "\\.");
                    string key = property.Key.Replace(".", "\\.");
                    data[$"{sectionName}.{key}"] = property.Value;
                }
            }

            return data;
        }

        /// <summary>
        /// Writes the properties in a flat dictionary into the ini file.
        /// </summary>
        /// <param name="data">Dictionary of properties, whose keys are dot delimited paths.</param>
        /// <exception cref="InvalidOperationException">INI file has not been read yet.</exception>
        public void WriteData(Dictionary<string, string> data)
        {
            foreach ((string key, string value) in data)
            {
                this.SetValue(key, value);
            }
        }
    }
}