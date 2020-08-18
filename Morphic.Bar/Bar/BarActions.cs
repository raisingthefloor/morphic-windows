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

    /// <summary>
    /// Deserialised actions.json5.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class BarActions : IDeserializable
    {
        private static readonly BarActions Default = BarActions.FromFile(AppPaths.GetConfigFile("actions.json5", true));

        [JsonProperty("actions")]
        public Dictionary<string, BarAction> Actions { get; set; } = new Dictionary<string, BarAction>();

        public static BarActions FromFile(string file)
        {
            using StreamReader? reader = File.OpenText(file);
            return BarJson.Load<BarActions>(reader);
        }

        public static BarAction? GetAction(string identifier)
        {
            BarActions.Default.Actions.TryGetValue(identifier, out BarAction? action);
            return action;
        }

        public void Deserialized()
        {
            foreach (var (key, action) in this.Actions)
            {
                if (string.IsNullOrEmpty(action.Id))
                {
                    action.Id = key;
                }
            }
        }
    }
}
