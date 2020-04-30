using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using MorphicCore;

namespace MorphicSettings
{
    /// <summary>
    /// Captures many settings at once
    /// </summary>
    class CaptureSession
    {

        /// <summary>
        /// The settings manager to use for the capture session
        /// </summary>
        public Settings Settings { get; private set; }

        /// <summary>
        /// The preferences to save to
        /// </summary>
        public Preferences Preferences { get; private set; }

        /// <summary>
        /// The keys to capture
        /// </summary>
        public List<Preferences.Key> Keys = new List<Preferences.Key>();

        /// <summary>
        /// Create a new capture session
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="preferences"></param>
        public CaptureSession(Settings settings, Preferences preferences)
        {
            Settings = settings;
            Preferences = preferences;
        }

        /// <summary>
        /// Add keys for every setting of every solution in the Solution.Registry
        /// </summary>
        public void AddAllSolutions()
        {
            foreach (var solution in Solution.Registry.Values)
            {
                foreach (var setting in solution.Settings)
                {
                    Keys.Add(new Preferences.Key(solution.Id, setting.Name));
                }
            }
        }

        /// <summary>
        /// Capture values for all the keys
        /// </summary>
        /// <returns></returns>
        public async Task Run()
        {
            foreach (var key in Keys)
            {
                if (Settings.Handler(key) is SettingsHandler handler)
                {
                    var result = await handler.Capture();
                    if (result.Success)
                    {
                        Preferences.Set(key, result.Value);
                    }
                }
            }
        }

    }
}
