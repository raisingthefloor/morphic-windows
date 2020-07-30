// BarData.cs: Information about a bar.
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
        [JsonProperty("itemTheme", ObjectCreationHandling = ObjectCreationHandling.Reuse)]
        public BarItemTheme DefaultTheme { get; set; } = new BarItemTheme();
        
        /// <summary>
        /// Theme for the bar.
        /// </summary>
        [JsonProperty("barTheme", ObjectCreationHandling = ObjectCreationHandling.Reuse)]
        public Theme BarTheme { get; set; } = new Theme();
        
        /// <summary>
        /// Gets all items.
        /// </summary>
        [JsonProperty("items")]
        public List<BarItem> AllItems { get; set; } = new List<BarItem>();
        
        /// <summary>
        /// Gets the items for the main bar.
        /// </summary>
        public IEnumerable<BarItem> PrimaryItems => this.AllItems.Where(item => !item.Hidden && item.IsPrimary).OrderByDescending(item => item.Priority);
        
        /// <summary>
        /// Gets the items for the additional buttons.
        /// </summary>
        public IEnumerable<BarItem> SecondaryItems => this.AllItems.Where(item => !item.Hidden && !item.IsPrimary).OrderByDescending(item => item.Priority);

        /// <summary>
        /// Generates the bar from a json string.
        /// </summary>
        /// <param name="json"></param>
        /// <param name="includeDefault">true to also include the default bar data.</param>
        /// <returns></returns>
        public static BarData? FromJson(string json, bool includeDefault = true)
        {
            string defaultFile = App.GetFile("default-bar.json5");
            
            BarData? defaultBar = includeDefault && File.Exists(defaultFile)
                ? BarData.FromFile(defaultFile, false)
                : null;
            return BarJson.FromJson(json, defaultBar);
        }

        public static BarData? FromFile(string jsonFile, bool includeDefault = true)
        {
            return BarData.FromJson(File.ReadAllText(jsonFile), includeDefault);
        }
    }
}