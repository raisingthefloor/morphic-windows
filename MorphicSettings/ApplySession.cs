using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using MorphicCore;

namespace MorphicSettings
{
    /// <summary>
    /// Apply many settings at once
    /// </summary>
    class ApplySession
    {

        /// <summary>
        /// The settings manager to use for this sessions
        /// </summary>
        public Settings Settings { get; private set; }

        /// <summary>
        /// The values to set, keyed by Preference.Key
        /// </summary>
        public Dictionary<Preferences.Key, object?> ValuesByKey { get; private set; }

        /// <summary>
        /// Apply default values for any settings not specified
        /// </summary>
        public bool ApplyDefaultValues { get; set; } = true;

        /// <summary>
        /// Create a new apply session
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="valuesByKey"></param>
        public ApplySession(Settings settings, Dictionary<Preferences.Key, object?> valuesByKey)
        {
            Settings = settings;
            ValuesByKey = valuesByKey;
        }

        /// <summary>
        /// Create a new apply session using a Preferences object
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="preferences"></param>
        public ApplySession(Settings settings, Preferences preferences): this(settings, preferences.GetValuesByKey())
        {
        }

        /// <summary>
        /// Apply all the settings
        /// </summary>
        /// <returns></returns>
        public async Task Run()
        {
            var valuesByKey = ValuesByKey;
            if (ApplyDefaultValues)
            {
                valuesByKey = new Dictionary<Preferences.Key, object?>(valuesByKey);
                foreach (var solution in Solution.Registry.Values)
                {
                    foreach (var setting in solution.Settings)
                    {
                        if (setting.Default is object defaultValue)
                        {
                            var key = new Preferences.Key(solution.Id, setting.Name);
                            if (!valuesByKey.ContainsKey(key))
                            {
                                valuesByKey.Add(key, defaultValue);
                            }
                        }
                    }
                }
            }

            // TODO: figure out any order dependency among settings
            // TODO: queue things like unique service restarts
            foreach (var pair in valuesByKey)
            {
                if (Settings.Handler(pair.Key) is SettingsHandler handler)
                {
                    await handler.Apply(pair.Value);
                }
            }
        }

    }
}
