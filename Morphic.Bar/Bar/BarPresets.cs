// BarActions.cs: Deserialised actions.json5.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

namespace Morphic.Bar.Bar
{
    using System.Collections.Generic;
    using System.IO;
    using Actions;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Deserialised actions.json5.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class BarPresets : IDeserializable
    {
        private static readonly BarPresets Default = BarPresets.FromFile(AppPaths.GetConfigFile("actions.json5", true));

        [JsonProperty("actions")]
        public Dictionary<string, JObject> Actions { get; set; } = new Dictionary<string, JObject>();

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
        public static JObject? GetObject(string identifier)
        {
            BarPresets.Default.Actions.TryGetValue(identifier, out JObject? jo);
            return (JObject?)jo?.DeepClone();
        }

        public void Deserialized()
        {
            foreach (var (key, action) in this.Actions)
            {
                // if (string.IsNullOrEmpty(action.Id))
                // {
                //     action.Id = key;
                // }
            }
        }
    }
}
