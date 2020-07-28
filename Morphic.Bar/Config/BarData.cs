// BarData.cs: Information about a bar.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

namespace Morphic.Bar.Config
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Newtonsoft.Json;

    /// <summary>
    /// Describes a bar.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class BarData
    {
        /// <summary>
        /// Bar identifier (currently unused by the client)
        /// </summary>
        [JsonProperty("id")]
        public string? Id { get; set; }

        /// <summary>
        /// Name of the bar (currently unused by the client)
        /// </summary>
        [JsonProperty("name")]
        public string? Name { get; set; }

        /// <summary>Initial bar position.</summary>
        [JsonProperty("position", ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public BarPosition Position { get; set; } = new BarPosition();

        /// <summary>
        /// Base theme for bar items - items will take values from this if they haven't got their own.
        /// </summary>
        [JsonProperty("itemTheme", ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public BarItemTheme DefaultTheme { get; set; } = new BarItemTheme(Theme.Undefined());
        
        /// <summary>
        /// Theme for the bar.
        /// </summary>
        [JsonProperty("barTheme", ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public Theme BarTheme { get; set; } = Theme.Undefined();
        
        /// <summary>
        /// Gets all items.
        /// </summary>
        [JsonProperty("items")]
        public List<BarItem> AllItems { get; set; } = new List<BarItem>();
        
        /// <summary>
        /// Gets the items for the main bar.
        /// </summary>
        public IEnumerable<BarItem> BarItems => this.AllItems.Where(item => !item.Hidden && !item.IsExtra);
        
        /// <summary>
        /// Gets the items for the additional buttons.
        /// </summary>
        public IEnumerable<BarItem> ExtraItems => this.AllItems.Where(item => !item.Hidden && item.IsExtra);

        /// <summary>
        /// Generates the bar from a json string.
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        public static BarData FromJson(string json)
        {
            return BarJson.FromJson(json);
        }

        public static BarData FromFile(string jsonFile)
        {
            return BarData.FromJson(File.ReadAllText(jsonFile));
        }
    }
}