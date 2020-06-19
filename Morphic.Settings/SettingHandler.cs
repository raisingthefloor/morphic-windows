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
using Morphic.Core;
using Morphic.Settings.Ini;
using Morphic.Settings.Registry;
using Morphic.Settings.SystemSettings;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;
using Morphic.Settings.Files;
using Morphic.Settings.Process;

namespace Morphic.Settings
{
    /// <summary>
    /// The abstract base class and factory source for settings handlers
    /// </summary>
    public abstract class SettingHandler
    {

        /// <summary>
        /// Apply the value for the setting controled by this handler
        /// </summary>
        /// <remarks>
        /// Each setting handler is created with knowledge of the setting it needs to update
        /// </remarks>
        /// <param name="value">The value to apply</param>
        /// <returns></returns>
        public abstract Task<bool> Apply(object? value);

        /// <summary>
        /// Capture the current value for the setting controlled by this handler
        /// </summary>
        /// <param name="value">The captured value</param>
        /// <returns>Whether or not the capture was successful</returns>
        public abstract Task<CaptureResult> Capture();

        public struct CaptureResult
        {
            public bool Success;
            public object? Value;
        }

        /// <summary>
        /// A lookup table of client handlers
        /// </summary>
        internal static readonly Dictionary<Preferences.Key, Type> clientHandlerTypesByKey = new Dictionary<Preferences.Key, Type>();

        /// <summary>
        /// Register a new client handler for the given preference key
        /// </summary>
        /// <param name="type"></param>
        /// <param name="key"></param>
        public static void RegisterClientHandler(Type type, Preferences.Key key)
        {
            clientHandlerTypesByKey[key] = type;
        }

        /// <summary>
        /// Expand certain whitelisted environmental variables in a path template
        /// </summary>
        /// <param name="templatePath"></param>
        /// <returns></returns>
        protected static string ExpandedPath(string templatePath)
        {
            var allowedVariables = new string[]
            {
                "APPDATA"
            };
            var path = templatePath;
            foreach (var varname in allowedVariables)
            {
                path = path.Replace($"$({varname})", Environment.GetEnvironmentVariable(varname), StringComparison.OrdinalIgnoreCase);
            }
            return path;
        }

    }

    public static class HandlerDescriptionExtensions
    {

        public static SettingHandler? CreateHandler(this Setting setting, IServiceProvider serviceProvider)
        {
            if (setting.HandlerDescription is ClientSettingHandlerDescription clientDescription)
            {
                if (SettingHandler.clientHandlerTypesByKey.TryGetValue(clientDescription.Key, out var type))
                {
                    var instance = serviceProvider.GetService(type);
                    if (instance is SettingHandler handler)
                    {
                        return handler;
                    }
                }
            }
            else if (setting.HandlerDescription is SystemSettingHandlerDescription)
            {
                var logger = serviceProvider.GetRequiredService<ILogger<SystemSettingHandler>>();
                var settingFactory = serviceProvider.GetRequiredService<ISystemSettingFactory>();
                return new SystemSettingHandler(setting, settingFactory, serviceProvider, logger);
            }
            else if (setting.HandlerDescription is RegistrySettingHandlerDescription)
            {
                var logger = serviceProvider.GetRequiredService<ILogger<RegistrySettingHandler>>();
                var registry = serviceProvider.GetRequiredService<IRegistry>();
                return new RegistrySettingHandler(setting, registry, logger);
            }
            else if (setting.HandlerDescription is IniSettingHandlerDescription)
            {
                var logger = serviceProvider.GetRequiredService<ILogger<IniSettingHandler>>();
                var iniFactory = serviceProvider.GetRequiredService<IIniFileFactory>();
                return new IniSettingHandler(setting, iniFactory, logger);
            }
            else if (setting.HandlerDescription is FilesSettingHandlerDescription)
            {
                var logger = serviceProvider.GetRequiredService<ILogger<FilesSettingHandler>>();
                var fileManager = serviceProvider.GetRequiredService<IFileManager>();
                return new FilesSettingHandler(setting, fileManager, logger);
            }
            else if (setting.HandlerDescription is ProcessSettingHandlerDescription)
            {
                var logger = serviceProvider.GetRequiredService<ILogger<ProcessSettingHandler>>();
                var processManager = serviceProvider.GetRequiredService<IProcessManager>();
                return new ProcessSettingHandler(setting, processManager, logger);
            }
            return null;
        }
    }

    /// <summary>
    /// A builder used to register client handlers with SettingsHandler and with an IServiceCollection
    /// </summary>
    public class SettingsHandlerBuilder
    {

        /// <summary>
        /// Create a new builder that uses the given service collection
        /// </summary>
        /// <param name="services"></param>
        public SettingsHandlerBuilder(IServiceCollection services)
        {
            this.services = services;
        }

        /// <summary>
        /// The service collection on which transient types will be registered
        /// </summary>
        readonly IServiceCollection services;

        /// <summary>
        /// Add a client setting handler for the given key
        /// </summary>
        /// <param name="type"></param>
        /// <param name="key"></param>
        public void AddClientHandler(Type type, Preferences.Key key)
        {
            services.AddTransient(type);
            SettingHandler.RegisterClientHandler(type, key);
        }
    }
}
