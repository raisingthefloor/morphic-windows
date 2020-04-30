// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MorphicCore;
using Microsoft.Win32;

namespace MorphicSettings
{
    /// <summary>
    /// A solution description specifying the possible details settings for a given solution identifier
    /// </summary>
    public class Solution
    {

        /// <summary>
        /// The solution identifier, typically in reverse-domain style
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        /// <summary>
        /// The list of specific settings that make up this solution
        /// </summary>
        [JsonPropertyName("settings")]
        public Setting[] Settings { get; set; } = new Setting[0];

        /// <summary>
        /// A lookup table of settings by name
        /// </summary>
        private Dictionary<string, Setting>? settingsByName;

        /// <summary>
        /// A lazily-created lookup table of settings by name
        /// </summary>
        public Dictionary<string, Setting> SettingsByName
        {
            get
            {
                if (settingsByName == null)
                {
                    settingsByName = new Dictionary<string, Setting>(Settings.Length);
                    foreach (var setting in Settings)
                    {
                        settingsByName.Add(setting.Name, setting);
                    }
                }
                return settingsByName;
            }
        }

        /// <summary>
        /// A public registry of solutions by name
        /// </summary>
        public static readonly Dictionary<string, Solution> Registry = new Dictionary<string, Solution>();

        /// <summary>
        /// Populate the registry of solutions with the contents of the given json file
        /// </summary>
        /// <param name="jsonPath">The path the file containing solutions in json format</param>
        public static async Task PopulateRegistry(string jsonPath)
        {
            using (var stream = File.OpenRead(jsonPath))
            {
                var options = new JsonSerializerOptions();
                options.Converters.Add(new Setting.HandlerDescription.JsonConverter());
                options.Converters.Add(new JsonStringEnumConverter());
                var solutions = await JsonSerializer.DeserializeAsync<Solution[]>(stream, options);
                foreach (var solution in solutions)
                {
                    Registry.Add(solution.Id, solution);
                }
            }
        }

        /// <summary>
        /// Get the setting for the given preference key
        /// </summary>
        /// <param name="key">The key of the setting to lookup</param>
        /// <returns></returns>
        public static Setting? GetSetting(Preferences.Key key)
        {
            if (Registry.TryGetValue(key.Solution, out var solution))
            {
                if (solution.SettingsByName.TryGetValue(key.Preference, out var setting))
                {
                    return setting;
                }
            }
            return null;
        }

        public override string ToString()
        {
            return String.Format("Solution: {0}", Id);
        }

        /// <summary>
        /// A single setting within a solution
        /// </summary>
        public class Setting
        {

            /// <summary>
            /// The possible value types for a solution
            /// </summary>
            public enum ValueKind
            {
                String,
                Boolean,
                Integer,
                Double
            }

            /// <summary>
            /// The name of the setting
            /// </summary>
            /// <remarks>
            /// Together with the owning solution id, this name can be used to
            /// create a <code>Preferences.Key</code>
            /// </remarks>
            [JsonPropertyName("name")]
            public string Name { get; set; } = "";

            /// <summary>
            /// The kind of value this setting takes
            /// </summary>
            [JsonPropertyName("type")]
            public ValueKind Kind { get; set; }

            /// <summary>
            /// The description of the handler for this setting
            /// </summary>
            [JsonPropertyName("handler")]
            public HandlerDescription? Handler { get; set; }

            /// <summary>
            /// Base class for describing the handler for a setting
            /// </summary>
            public class HandlerDescription
            {

                /// <summary>
                /// The possible kinds of handlers
                /// </summary>
                public enum HandlerKind
                {
                    /// <summary>
                    /// A client handler is custom code provided by the client
                    /// </summary>
                    Client,

                    /// <summary>
                    /// A registry handler sets a specific value in the windows registry
                    /// </summary>
                    Registry,

                    /// <summary>
                    /// A registry handler sets a specific SystemSetting
                    /// </summary>
                    System,

                    /// <summary>
                    /// An ini handler sets a specific value in an ini file
                    /// </summary>
                    Ini,

                    /// <summary>
                    /// An unknown handler is used for any unrecogized or invalid handler JSON
                    /// </summary>
                    Unknown
                }

                /// <summary>
                /// The kind of this handler
                /// </summary>
                public HandlerKind Kind { get; set; }

                /// <summary>
                /// Create a new handler for the given kind
                /// </summary>
                /// <param name="kind"></param>
                public HandlerDescription(HandlerKind kind)
                {
                    Kind = kind;
                }

                /// <summary>
                /// A custom JSON converter that creates the correct subclass based on the type property
                /// </summary>
                public class JsonConverter : System.Text.Json.Serialization.JsonConverter<HandlerDescription>
                {
                    public override HandlerDescription Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                    {
                        try
                        {
                            var document = JsonDocument.ParseValue(ref reader);
                            var element = document.RootElement;
                            var type = element.GetProperty("type").GetString();
                            switch (type)
                            {
                                case "org.raisingthefloor.morphic.client":
                                    return new HandlerDescription(HandlerKind.Client);
                                case "com.microsoft.windows.registry":
                                    {
                                        var keyName = element.GetProperty("key_name").GetString();
                                        var valueName = element.GetProperty("value_name").GetString();
                                        var valueType = element.GetProperty("value_type").GetString();
                                        var valueKind = Enum.Parse<RegistryValueKind>(valueType, ignoreCase: true);
                                        return new RegistryHandlerDescription(keyName, valueName, valueKind);
                                    }
                                case "com.microsoft.windows.ini":
                                    {
                                        var filename = element.GetProperty("filename").GetString();
                                        var section = element.GetProperty("section").GetString();
                                        var key = element.GetProperty("key").GetString();
                                        return new IniHandlerDescription(filename, section, key);
                                    }
                                case "com.microsoft.windows.system":
                                    {
                                        var settingId = element.GetProperty("setting_id").GetString();
                                        return new SystemSettingHandlerDescription(settingId);
                                    }
                            }
                        }
                        catch
                        {
                        }
                        return new HandlerDescription(HandlerKind.Unknown);
                    }

                    public override void Write(Utf8JsonWriter writer, HandlerDescription value, JsonSerializerOptions options)
                    {
                        throw new NotImplementedException();
                    }
                }
            }

            /// <summary>
            /// A registry handler description with registry key names
            /// </summary>
            public class RegistryHandlerDescription : HandlerDescription
            {

                /// <summary>
                /// The full key name in the registry
                /// </summary>
                public string KeyName;

                /// <summary>
                /// The value name within the key
                /// </summary>
                public string ValueName;

                /// <summary>
                /// The expected value kind
                /// </summary>
                public RegistryValueKind ValueKind;

                /// <summary>
                /// Create a new registry handler description
                /// </summary>
                /// <param name="keyName"></param>
                /// <param name="valueName"></param>
                /// <param name="valueKind"></param>
                public RegistryHandlerDescription(string keyName, string valueName, RegistryValueKind valueKind): base(HandlerKind.Registry)
                {
                    KeyName = keyName;
                    ValueName = valueName;
                    ValueKind = valueKind;
                }
            }

            /// <summary>
            /// An ini handler description that specifies which part of the ini file should be udpated
            /// </summary>
            public class IniHandlerDescription: HandlerDescription
            {

                /// <summary>
                /// The filename of the ini file, possibly including environmental variables
                /// </summary>
                public string Filename;

                /// <summary>
                /// The section within the ini file
                /// </summary>
                public string Section;

                /// <summary>
                /// The key within the section
                /// </summary>
                public string Key;

                /// <summary>
                /// Create a new ini file handler
                /// </summary>
                /// <param name="filename"></param>
                /// <param name="section"></param>
                /// <param name="key"></param>
                public IniHandlerDescription(string filename, string section, string key): base(HandlerKind.Ini)
                {
                    Filename = filename;
                    Section = section;
                    Key = key;
                }
            }

            /// <summary>
            /// An ini handler description that specifies which part of the ini file should be udpated
            /// </summary>
            public class SystemSettingHandlerDescription : HandlerDescription
            {

                /// <summary>
                /// The filename of the ini file, possibly including environmental variables
                /// </summary>
                public string SettingId;

                /// <summary>
                /// Create a new ini file handler
                /// </summary>
                /// <param name="settingId"></param>
                public SystemSettingHandlerDescription(string settingId) : base(HandlerKind.System)
                {
                    SettingId = settingId;
                }
            }
        }
    }
}
