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
using System.Security;
using System.Collections.Generic;
using System.Text;
using Microsoft.Win32;
using Microsoft.Extensions.Logging;

namespace MorphicSettings
{

    /// <summary>
    /// A settings handler for registry settings
    /// </summary>
    class RegistrySettingsHandler: SettingsHandler
    {

        /// <summary>
        /// The handler description for the setting to read/write
        /// </summary>
        public Solution.Setting.RegistryHandlerDescription Description { get; private set; }

        /// <summary>
        /// Create a new registry settings handler based on a handler descritpion
        /// </summary>
        /// <param name="description"></param>
        /// <param name="logger"></param>
        public RegistrySettingsHandler(Solution.Setting.RegistryHandlerDescription description, ILogger<RegistrySettingsHandler> logger)
        {
            Description = description;
            this.logger = logger;
        }

        /// <summary>
        /// The logger to use
        /// </summary>
        private readonly ILogger<RegistrySettingsHandler> logger;

        /// <summary>
        /// Write the given value to the appropriate registry key
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public override Task<bool> Apply(object? value)
        {
            if (value is object nonnullValue)
            {
                try
                {
                    Registry.SetValue(Description.KeyName, Description.ValueName, nonnullValue, Description.ValueKind);
                    return Task.FromResult(true);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Failed to set registry value {0}.{1}", Description.KeyName, Description.ValueName);
                }
            }
            return Task.FromResult(false);
        }

        /// <summary>
        /// Red the value from the appropriate registry key
        /// </summary>
        /// <returns></returns>
        public override Task<CaptureResult> Capture()
        {
            var result = new CaptureResult();
            try
            {
                result.Value = Registry.GetValue(Description.KeyName, Description.ValueName, null);
                result.Success = true;
            }catch (Exception e)
            {
                logger.LogError(e, "Failed to get registry value {0}.{1}", Description.KeyName, Description.ValueName);
            }
            return Task.FromResult(result);
        }

    }
}
