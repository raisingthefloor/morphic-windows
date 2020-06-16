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
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using Morphic.Core;
using Morphic.Settings.Ini;

namespace Morphic.Settings
{
    /// <summary>
    /// Apply many settings at once
    /// </summary>
    public class ApplySession
    {

        /// <summary>
        /// The settings manager to use for this sessions
        /// </summary>
        public SettingsManager SettingsManager { get; private set; }

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
        /// <param name="settingsMananger"></param>
        /// <param name="valuesByKey"></param>
        public ApplySession(SettingsManager settingsMananger, Dictionary<Preferences.Key, object?> valuesByKey)
        {
            SettingsManager = settingsMananger;
            ValuesByKey = valuesByKey;
        }

        /// <summary>
        /// Create a new apply session using a Preferences object
        /// </summary>
        /// <param name="settingsManager"></param>
        /// <param name="preferences"></param>
        public ApplySession(SettingsManager settingsManager, Preferences preferences): this(settingsManager, preferences.GetValuesByKey())
        {
        }

        /// <summary>
        /// Apply all the settings
        /// </summary>
        /// <returns></returns>
        public async Task<Dictionary<Preferences.Key, bool>> Run()
        {
            var valuesByKey = ValuesByKey;
            if (ApplyDefaultValues)
            {
                // Loop through all settings and add the default value for any setting
                // that isn't already in valuesByKey
                valuesByKey = new Dictionary<Preferences.Key, object?>(valuesByKey);
                foreach (var solution in SettingsManager.SolutionsById.Values)
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
                        else
                        {
                            SettingsManager.logger.LogWarning("null default value, skipping");
                        }
                    }
                }
            }

            var resultsByKey = new Dictionary<Preferences.Key, bool>();
            var uniqueFinalizerDescriptions = new HashSet<SettingFinalizerDescription>();
            var serviceProvider = SettingsManager;

            var logger = serviceProvider.GetService<ILogger<ApplySession>>();

            var iniFactory = serviceProvider.GetRequiredService<IIniFileFactory>();
            await iniFactory.Begin();

            // Keep track of any unique finalizers that need to run
            // TODO: enforce any order dependency among settings (currently no known dependencies, but
            // some are expected)
            foreach (var pair in valuesByKey)
            {
                if (SettingsManager.Get(pair.Key) is Setting setting)
                {
                    if (setting.CreateHandler(serviceProvider) is SettingHandler handler)
                    {
                        logger.LogInformation("Applying {0}.{1}", pair.Key.Solution, pair.Key.Preference);
                        var success = await handler.Apply(pair.Value);
                        if (success)
                        {
                            if (setting.FinalizerDescription is SettingFinalizerDescription finalizerDescription)
                            {
                                uniqueFinalizerDescriptions.Add(finalizerDescription);
                            }
                        }
                        else
                        {
                            logger.LogError("Failed to set {0}.{1}", pair.Key.Solution, pair.Key.Preference);
                        }
                        resultsByKey.Add(pair.Key, success);
                    }
                    else
                    {
                        logger.LogError("No handler for {0}.{1}", pair.Key.Solution, pair.Key.Preference);
                        resultsByKey.Add(pair.Key, false);
                    }
                }
                else
                {
                    logger.LogError("No definition found for {0}.{1}", pair.Key.Solution, pair.Key.Preference);
                    resultsByKey.Add(pair.Key, false);
                }
            }

            await iniFactory.Commit();

            if (uniqueFinalizerDescriptions.Count > 0)
            {
                logger.LogInformation("Running finalizers");
                // Run the unique finalizers
                foreach (var finalizerDescription in uniqueFinalizerDescriptions)
                {
                    if (finalizerDescription.CreateFinalizer(serviceProvider) is SettingFinalizer finalizer)
                    {
                        var success = await finalizer.Run();
                        if (!success)
                        {
                            logger.LogError("Finalizer failed");
                        }
                    }
                }
                logger.LogInformation("Finalizers done");
            }

            return resultsByKey;
        }

    }
}
