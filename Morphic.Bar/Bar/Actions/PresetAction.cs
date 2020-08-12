// PresetAction.cs: A bar action which points to a pre-set action.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

namespace Morphic.Bar.Bar.Actions
{
    using System.Threading.Tasks;
    using System.Windows;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    /// <summary>
    /// Action that points to an action in actions.json5. This class instance will be replaced after deserialisation
    /// with the appropriate one from the loaded config file.
    /// </summary>
    [JsonTypeName("action")]
    public class PresetAction : BarAction
    {
        [JsonProperty("identifier", Required = Required.Always)]
        public string Identifier { get; set; } = null!;

        public override Task<bool> Invoke()
        {
            // This instance should have been resolved to the action pointed to by `Identifier`.
            App.Current.Logger.LogWarning("Invoked unresolved pre-set action '{id}'.", this.Identifier);
            MessageBox.Show($"There was a problem invoking '{this.Identifier}'.");
            return Task.FromResult(false);
        }

        /// <summary>
        /// Gets the action specified in the `actions.json5` file, which this one points to.
        /// </summary>
        /// <returns></returns>
        public BarAction? GetRealAction()
        {
            const int maxDepth = 10;
            int depth = 0;
            BarAction? action;
            PresetAction? presetAction = this;

            do
            {
                action = BarActions.GetAction(presetAction.Identifier);
                presetAction = action as PresetAction;
            } while (presetAction != null && depth++ <= maxDepth);

            if (depth > maxDepth)
            {
                App.Current.Logger.LogWarning("Recursion of resolving bar action '{id}' went too deep.",
                    this.Identifier);
                action = this;
            }

            return action;
        }
    }
}
