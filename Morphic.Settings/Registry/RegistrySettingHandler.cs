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
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Morphic.Settings.Registry
{

    /// <summary>
    /// A settings handler for registry settings
    /// </summary>
    public class RegistrySettingHandler: SettingHandler
    {

        /// <summary>
        /// The setting to handle
        /// </summary>
        public Setting Setting { get; private set; }

        /// <summary>
        /// The handler description for the setting to read/write
        /// </summary>
        public RegistrySettingHandlerDescription Description
        {
            get
            {
                return (Setting.HandlerDescription as RegistrySettingHandlerDescription)!;
            }
        }

        /// <summary>
        /// Create a new registry settings handler based on a handler descritpion
        /// </summary>
        /// <param name="description"></param>
        /// <param name="registry"></param>
        /// <param name="logger"></param>
        public RegistrySettingHandler(Setting setting, IRegistry registry, ILogger<RegistrySettingHandler> logger)
        {
            Setting = setting;
            this.logger = logger;
            this.registry = registry;
        }

        /// <summary>
        /// The logger to use
        /// </summary>
        private readonly ILogger<RegistrySettingHandler> logger;

        private readonly IRegistry registry;

        /// <summary>
        /// Write the given value to the appropriate registry key
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public override Task<bool> Apply(object? value)
        {
            if (TryConvertToRegistry(value, Description.ValueKind, out var registryValue))
            {
                try
                {
                    registry.SetValue(Description.KeyName, Description.ValueName, registryValue, Description.ValueKind);
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
                var registryValue = registry.GetValue(Description.KeyName, Description.ValueName, null);
                result.Success = TryConvertFromRegistry(registryValue, Setting.Kind, out result.Value);
            }catch (Exception e)
            {
                logger.LogError(e, "Failed to get registry value {0}.{1}", Description.KeyName, Description.ValueName);
            }
            return Task.FromResult(result);
        }

        public static bool TryConvertToRegistry(object? value, RegistryValueKind registryValueKind, out object? registryValue)
        {
            if (value == null)
            {
                registryValue = null;
                return false;
            }
            if (value is string stringValue)
            {
                switch (registryValueKind)
                {
                    case RegistryValueKind.String:
                    case RegistryValueKind.ExpandString:
                        registryValue = stringValue;
                        return true;
                }
                registryValue = null;
                return false;
            }
            if (value is int intValue)
            {
                value = (long)intValue;
            }
            if (value is long longValue)
            {
                switch (registryValueKind)
                {
                    case RegistryValueKind.DWord:
                        registryValue = (Int32)longValue;
                        return true;
                    case RegistryValueKind.QWord:
                        registryValue = (Int64)longValue;
                        return true;
                }
                registryValue = null;
                return false;
            }
            if (value is bool boolValue)
            {
                switch (registryValueKind)
                {
                    case RegistryValueKind.DWord:
                        registryValue = boolValue ? 1 : 0;
                        return true;
                }
                registryValue = null;
                return false;
            }
            registryValue = null;
            return false;
        }

        public static bool TryConvertFromRegistry(object? registryValue, Setting.ValueKind resultValueKind, out object? resultValue)
        {
            if (registryValue == null)
            {
                resultValue = null;
                return false;
            }
            if (registryValue is Int32 intValue)
            {
                switch (resultValueKind)
                {
                    case Setting.ValueKind.Boolean:
                        resultValue = intValue != 0;
                        return true;
                    case Setting.ValueKind.Integer:
                        resultValue = (long)intValue;
                        return true;
                }
                resultValue = null;
                return false;
            }
            if (registryValue is Int64 longValue)
            {
                switch (resultValueKind)
                {
                    case Setting.ValueKind.Boolean:
                        resultValue = longValue != 0;
                        return true;
                    case Setting.ValueKind.Integer:
                        resultValue = (long)longValue;
                        return true;
                }
                resultValue = null;
                return false;
            }
            if (registryValue is string stringValue)
            {
                switch (resultValueKind)
                {
                    case Setting.ValueKind.String:
                        resultValue = stringValue;
                        return true;
                }
                resultValue = null;
                return false;
            }
            resultValue = null;
            return false;
        }
    }
}
