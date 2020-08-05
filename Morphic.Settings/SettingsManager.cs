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
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Morphic.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Morphic.Settings
{

    /// <summary>
    /// The central settings manager
    /// </summary>
    public class SettingsManager: IServiceProvider
    {

        /// <summary>
        /// Create a new settings manager
        /// </summary>
        /// <param name="provider"></param>
        /// <param name="logger"></param>
        public SettingsManager(IServiceProvider provider, ILogger<SettingsManager> logger)
        {
            this.logger = logger;
            this.provider = provider;
        }

        /// <summary>
        /// The logger to user
        /// </summary>
        public readonly ILogger<SettingsManager> logger;

        /// <summary>
        /// The service provider to use when creating settings handlers
        /// </summary>
        private readonly IServiceProvider provider;

        public object GetService(Type serviceType)
        {
            return provider.GetService(serviceType);
        }

        /// <summary>
        /// A public registry of solutions by name
        /// </summary>
        public readonly Dictionary<string, Solution> SolutionsById = new Dictionary<string, Solution>();

        /// <summary>
        /// Populate the registry of solutions with the contents of the given json file
        /// </summary>
        /// <param name="jsonPath">The path the file containing solutions in json format</param>
        public async Task Populate(string jsonPath)
        {
            using (var stream = File.OpenRead(jsonPath))
            {
                var options = new JsonSerializerOptions();
                options.Converters.Add(new JsonElementInferredTypeConverter());
                options.Converters.Add(new SettingHandlerDescription.JsonConverter());
                options.Converters.Add(new SettingFinalizerDescription.JsonConverter());
                options.Converters.Add(new JsonStringEnumConverter());
                var solutions = await JsonSerializer.DeserializeAsync<Solution[]>(stream, options);
                foreach (var solution in solutions)
                {
                    SolutionsById.Add(solution.Id, solution);
                }
            }
        }

        public void Add(Solution solution)
        {
            SolutionsById.Add(solution.Id, solution);
        }

        /// <summary>
        /// Get the setting for the given preference key
        /// </summary>
        /// <param name="key">The key of the setting to lookup</param>
        /// <returns></returns>
        public Setting? Get(Preferences.Key key)
        {
            if (SolutionsById.TryGetValue(key.Solution, out var solution))
            {
                if (solution.SettingsByName.TryGetValue(key.Preference, out var setting))
                {
                    return setting;
                }
            }
            return null;
        }

        /// <summary>
        /// Apply the given value for the given prefernce
        /// </summary>
        /// <remarks>
        /// Looks for a settings handler for the given key
        /// </remarks>
        /// <param name="key">The preference key</param>
        /// <param name="value">The value to apply</param>
        /// <returns></returns>
        public async Task<bool> Apply(Preferences.Key key, object? value)
        {
            var results = await Apply(new Dictionary<Preferences.Key, object?>
            {
                {key, value}
            });
            if (results.TryGetValue(key, out var result))
            {
                return result;
            }
            return false;
        }

        /// <summary>
        /// Apply a batch of settings
        /// </summary>
        /// <param name="valuesByKey"></param>
        /// <returns></returns>
        public async Task<Dictionary<Preferences.Key, bool>> Apply(Dictionary<Preferences.Key, object?> valuesByKey)
        {
            var session = new ApplySession(this, valuesByKey);
            session.ApplyDefaultValues = false;
            return await session.Run();
        }

        public async Task<object?> Capture(Preferences.Key key)
        {
            var prefs = new Preferences();
            var session = new CaptureSession(this, prefs);
            session.CaptureDefaultValues = true;
            session.Keys.Add(key);
            await session.Run();
            return prefs.Get(key);
        }

        public async Task<Dictionary<Preferences.Key, object?>> Capture(IEnumerable<Preferences.Key> keys)
        {
            var prefs = new Preferences();
            var session = new CaptureSession(this, prefs);
            session.CaptureDefaultValues = true;

            foreach (Preferences.Key key in keys)
            {
                session.Keys.Add(key);
            }
            
            await session.Run();

            return prefs.GetValuesByKey();
        }
        
        public async Task<bool?> CaptureBool(Preferences.Key key)
        {
            return await Capture(key) as bool?;
        }

        /// <summary>
        /// Known SettingsManager.Keys
        /// </summary>
        public static class Keys
        {
            /// <summary>
            /// Display Zoom level on Microsoft Windows (essentially display resolution)
            /// </summary>
            public static Preferences.Key WindowsDisplayZoom = new Preferences.Key("com.microsoft.windows.display", "zoom");

            public static Preferences.Key WindowsDisplayContrastEnabled = new Preferences.Key("com.microsoft.windows.display", "contrast.enabled");
            public static Preferences.Key WindowsDisplayNightModeEnabled = new Preferences.Key("com.microsoft.windows.display", "nightmode.enabled");

            public static Preferences.Key WindowsMagnifierEnabled = new Preferences.Key("com.microsoft.windows.magnifier", "enabled");
            public static Preferences.Key WindowsMagnifierMode = new Preferences.Key("com.microsoft.windows.magnifier", "mode");
            public static Preferences.Key WindowsMagnifierMagnification = new Preferences.Key("com.microsoft.windows.magnifier", "magnification");

            public static Preferences.Key WindowsNarratorEnabled = new Preferences.Key("com.microsoft.windows.narrator", "enabled");

            public static Preferences.Key WindowsCursorArrow = new Preferences.Key("com.microsoft.windows.cursor", "arrow");
            public static Preferences.Key WindowsCursorWait = new Preferences.Key("com.microsoft.windows.cursor", "wait");
        }
    }

    /// <summary>
    /// Extension to IServiceCollection for adding settings handlers
    /// </summary>
    public static class SettingsServiceProvider
    {

        /// <summary>
        /// Add client settings handlers and give the caller an opporunity to add their own with a builder
        /// </summary>
        /// <param name="services"></param>
        /// <param name="callback"></param>
        public static void AddMorphicSettingsHandlers(this IServiceCollection services, Action<SettingsHandlerBuilder> callback)
        {
            var builder = new SettingsHandlerBuilder(services);
            builder.AddClientHandler(typeof(DisplayZoomHandler), SettingsManager.Keys.WindowsDisplayZoom);
            callback(builder);
        }
    }
}
