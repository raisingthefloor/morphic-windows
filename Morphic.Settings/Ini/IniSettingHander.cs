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
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Morphic.Settings.Ini
{
    /// <summary>
    /// A settings handler for ini files
    /// </summary>
    public class IniSettingHandler : SettingHandler
    {

        /// <summary>
        /// The setting to be handled
        /// </summary>
        public Setting Setting { get; private set; }

        /// <summary>
        /// The handler descrition indicating what file/section/key to read/write
        /// </summary>
        public IniSettingHandlerDescription Description
        {
            get
            {
                return (Setting.HandlerDescription as IniSettingHandlerDescription)!;
            }
        }

        /// <summary>
        /// Create a new ini handler from the given handler description
        /// </summary>
        /// <param name="description"></param>
        /// <param name="logger"></param>
        public IniSettingHandler(Setting setting, IIniFileFactory iniFactory, ILogger<IniSettingHandler> logger)
        {
            Setting = setting;
            this.logger = logger;
            var path = ExpandedPath(Description.Filename);
            this.iniFile = iniFactory.Open(path);
        }

        /// <summary>
        /// Expand certain whitelisted environmental variables in a path template
        /// </summary>
        /// <param name="templatePath"></param>
        /// <returns></returns>
        private static string ExpandedPath(string templatePath)
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

        /// <summary>
        /// The logger to user
        /// </summary>
        private readonly ILogger<IniSettingHandler> logger;

        /// <summary>
        /// The ini file reader/writer
        /// </summary>
        private readonly IIniFile iniFile;

        /// <summary>
        /// Write the value to the section+key
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public override Task<bool> Apply(object? value)
        {
            if (TryConvertToIni(value, out var iniValue))
            {
                try
                {
                    iniFile.SetValue(Description.Section, Description.Key, iniValue);
                    return Task.FromResult(true);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Failed to set ini value");
                }
            }
            return Task.FromResult(false);
        }

        /// <summary>
        /// Read the value from the section+key
        /// </summary>
        /// <returns></returns>
        public override Task<CaptureResult> Capture()
        {
            var result = new CaptureResult();
            try
            {
                var iniValue = iniFile.GetValue(Description.Section, Description.Key);
                result.Success = TryConvertFromIni(iniValue, Setting.Kind, out result.Value);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to capture ini value");
            }
            return Task.FromResult(result);
        }

        public bool TryConvertToIni(object? value, out string iniValue)
        {
            if (value is string stringValue)
            {
                iniValue = stringValue;
                return true;
            }
            if (value is long longValue)
            {
                iniValue = longValue.ToString();
                return true;
            }
            if (value is bool boolValue)
            {
                iniValue = boolValue ? "1" : "0";
                return true;
            }
            if (value is double doubleValue)
            {
                iniValue = doubleValue.ToString();
                return true;
            }
            iniValue = "";
            return false;
        }

        public bool TryConvertFromIni(string? iniValue, Setting.ValueKind valueKind, out object? resultValue)
        {
            if (iniValue == null)
            {
                resultValue = null;
                return false;
            }
            switch (valueKind)
            {
                case Setting.ValueKind.String:
                    resultValue = iniValue;
                    return true;
                case Setting.ValueKind.Boolean:
                    resultValue = iniValue == "1";
                    return true;
                case Setting.ValueKind.Integer:
                    if (Int64.TryParse(iniValue, out var longValue))
                    {
                        resultValue = longValue;
                        return true;
                    }
                    resultValue = null;
                    return false;
                case Setting.ValueKind.Double:
                    if (Double.TryParse(iniValue, out var doubleValue))
                    {
                        resultValue = doubleValue;
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
