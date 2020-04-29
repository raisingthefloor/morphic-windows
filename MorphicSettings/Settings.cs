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
using MorphicCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace MorphicSettings
{

    /// <summary>
    /// The central settings manager
    /// </summary>
    public class Settings
    {

        /// <summary>
        /// Create a new settings manager
        /// </summary>
        /// <param name="provider"></param>
        /// <param name="logger"></param>
        public Settings(IServiceProvider provider, ILogger<Settings> logger)
        {
            this.logger = logger;
            this.provider = provider;
        }

        /// <summary>
        /// The logger to user
        /// </summary>
        private readonly ILogger<Settings> logger;

        /// <summary>
        /// The service provider to use when creating settings handlers
        /// </summary>
        private readonly IServiceProvider provider;

        /// <summary>
        /// Apply the given value for the given prefernce
        /// </summary>
        /// <remarks>
        /// Looks for a settings handler for the given key
        /// </remarks>
        /// <param name="key">The preference key</param>
        /// <param name="value">The value to apply</param>
        /// <returns></returns>
        public bool Apply(Preferences.Key key, object? value)
        {
            logger.LogInformation("Apply {0}", key);
            if (SettingsHandler.Handler(provider, key) is SettingsHandler handler)
            {
                try
                {
                    if (handler.Apply(value))
                    {
                        return true;
                    }
                    logger.LogError("Failed to set {0}", key);
                    return false;
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Failed to set {0}", key);
                    return false;
                }
            }
            else
            {
                logger.LogInformation("No handler for {0}", key);
                return false;
            }
        }

        /// <summary>
        /// Known settings keys
        /// </summary>
        public static class Keys
        {
            /// <summary>
            /// Display Zoom level on Microsoft Windows (essentially display resolution)
            /// </summary>
            public static Preferences.Key WindowsDisplayZoom = new Preferences.Key("com.microsoft.windows.display", "zoom");
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
            builder.AddClientHandler(typeof(DisplayZoomHandler), Settings.Keys.WindowsDisplayZoom);
            callback(builder);
        }
    }
}
