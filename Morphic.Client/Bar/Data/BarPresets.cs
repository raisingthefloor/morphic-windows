// BarActions.cs: Deserialised presets.json5.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

namespace Morphic.Client.Bar.Data
{
    using System.Collections.Generic;
    using System.IO;
    using Config;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Deserialised presets.json5.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class BarPresets : IDeserializable
    {
        public static readonly BarPresets Default = BarPresets.FromFile(AppPaths.GetConfigFile("presets.json5", true));

        [JsonProperty("actions")]
        public JsonDict Actions { get; set; } = new JsonDict();

        [JsonProperty("defaults")]
        public JsonDict Defaults { get; set; } = new JsonDict();

        public static BarPresets FromFile(string file)
        {
            using StreamReader? reader = File.OpenText(file);
            return BarJson.Load<BarPresets>(reader);
        }

        /// <summary>
        /// Gets the parsed json object for the given identifier.
        /// </summary>
        /// <param name="identifier"></param>
        /// <returns></returns>
        public static JObject? GetActionObject(string identifier)
        {
            BarPresets.Default.Actions.TryGetValue(identifier, out JObject? jo);
            return (JObject?)jo?.DeepClone();
        }

        public void Deserialized()
        {
        }

        /// <summary>
        /// Merges a preset into a given JSON object.
        ///
        /// For bar items that are of kinds "action" or "application", respectively the "identifier" or "default" fields
        /// of their configuration block are used as a lookup in the appropriate dictionary in this class.
        ///
        /// The object found in the lookup is then merged over the original.
        ///
        /// This is performed during deserialisation, just before the class instantiation so they are unaware of
        /// such hackery.
        /// </summary>
        /// <param name="jo">The BarItem JSON object.</param>
        /// <returns></returns>
        public JObject MergePreset(JObject jo)
        {
            string? kind = jo.SelectToken("kind")?.ToString();
            bool isAction = kind == "action";
            bool isApplication = kind == "application";

            string? key = null;
            if (isAction || isApplication)
            {
                string? keyField = isAction ? "configuration.identifier" : "configuration.default";
                key = jo.SelectToken(keyField)?.ToString();
            }

            if (!string.IsNullOrEmpty(key))
            {
                JsonDict dict = isAction ? this.Actions : this.Defaults;

                dict.TryGetValue(key, out JObject? preset);

                if (preset != null)
                {
                    jo.Merge(preset.DeepClone());
                }
            }

            return jo;
        }
    }

    /// <summary>
    /// A dictionary of JSON objects.
    /// </summary>
    public class JsonDict : Dictionary<string, JObject>
    {
    }
}
