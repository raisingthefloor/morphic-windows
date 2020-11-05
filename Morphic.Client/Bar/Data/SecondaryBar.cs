// SecondaryBar.cs: Configuration options for the secondary bar.
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
    using Newtonsoft.Json;

    /// <summary>
    /// Options specific to the secondary bar.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class SecondaryBar
    {
        /// <summary>
        /// Hide the secondary bar when another application gains focus.
        /// </summary>
        [JsonProperty("autohide")]
        public bool AutoHide { get; set; }

        /// <summary>
        /// Hide the pull-out button when another application gains focus.
        /// </summary>
        [JsonProperty("autohideExpander")]
        public bool AutoHideExpander { get; set; }

    }
}
