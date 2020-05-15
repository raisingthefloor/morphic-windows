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
using System.Text;
using Morphic.Core;

namespace MorphicSettings
{
    /// <summary>
    /// Captures many settings at once
    /// </summary>
    public class CaptureSession
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
        /// Capture values that match the default system value
        /// </summary>
        public bool CaptureDefaultValues { get; set; } = false;

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
        /// Add keys for every setting of every solution in the Solution Registry
        /// </summary>
        public void AddAllSolutions()
        {
            foreach (var solution in Settings.SolutionsById.Values)
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
            var serviceProvider = Settings;

            foreach (var key in Keys)
            {
                if (Settings.Get(key) is Setting setting)
                {
                    if (setting.CreateHandler(serviceProvider) is SettingHandler handler)
                    {
                        var result = await handler.Capture();
                        if (result.Success)
                        {
                            if (CaptureDefaultValues || !setting.isDefault(result.Value))
                            {
                                Preferences.Set(key, result.Value);
                            }
                            else
                            {
                                Preferences.Remove(key);
                            }
                        }
                    }
                }
            }
        }

    }
}
