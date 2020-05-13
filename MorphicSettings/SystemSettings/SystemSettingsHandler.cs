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
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.Win32;
using System.Threading;

namespace MorphicSettings
{
    /// <summary>
    /// A setting handler for Windows System Settings
    /// </summary>
    /// <remarks>
    /// Information about System Settings can be found in the Windows Registry under
    /// 
    /// HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\SystemSettings\SettingId\SomeSettingId
    /// 
    /// Each subkey has an value for DllPath, which contains a string of the absolute path to a DLL
    /// that in turn contains a GetSetting() function.
    /// 
    /// The result of calling GetSetting("SomeSettingId") is an object that has GetValue() and SetValue() methods,
    /// which read and write the setting, respectively.
    /// </remarks>
    class SystemSettingsHandler: SettingHandler
    {

        /// <summary>
        /// The handler description from the solution registry
        /// </summary>
        public SystemSettingHandlerDescription Description;

        /// <summary>
        /// The system setting instance that does most of the work
        /// </summary>
        private readonly ISystemSetting systemSetting;

        /// <summary>
        /// Create a new system settings handler with the given description and logger
        /// </summary>
        /// <param name="description"></param>
        /// <param name="logger"></param>
        public SystemSettingsHandler(SystemSettingHandlerDescription description, ISystemSettingFactory systemSettingFactory, IServiceProvider serviceProvider, ILogger<SystemSettingsHandler> logger)
        {
            Description = description;
            systemSetting = systemSettingFactory.Create(Description.SettingId, serviceProvider);
            this.logger = logger;
        }

        /// <summary>
        /// The logger to use
        /// </summary>
        private readonly ILogger<SystemSettingsHandler> logger;

        /// <summary>
        /// Apply the value to the setting
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public override async Task<bool> Apply(object? value)
        {
            if (value != null)
            {
                try
                {
                    await systemSetting.SetValue(value);
                    return true;
                }catch (Exception e)
                {
                    logger.LogError(e, "Failed to set system setting {0}", Description.SettingId);
                    return false;
                }
            }
            else
            {
                logger.LogError("null value");
                return false;
            }
        }

        public override async Task<CaptureResult> Capture()
        {
            var result = new CaptureResult();
            try
            {
                result.Value = await systemSetting.GetValue();
                result.Success = true;
            }
            catch(Exception e)
            {
                logger.LogError(e, "Failed to GetValue() for {0}", Description.SettingId);
            }
            return result;

        }

    }
}
