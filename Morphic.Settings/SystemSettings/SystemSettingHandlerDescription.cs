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
using System.IO;
using System.Threading.Tasks;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Morphic.Core;
using Microsoft.Win32;
using Morphic.Settings.SystemSettings;
using System.Linq;
using Morphic.Settings.Files;

namespace Morphic.Settings
{

    /// <summary>
    /// An ini handler description that specifies which part of the ini file should be udpated
    /// </summary>
    public class SystemSettingHandlerDescription : SettingHandlerDescription
    {

        /// <summary>
        /// The filename of the ini file, possibly including environmental variables
        /// </summary>
        public string SettingId;

        /// <summary>
        /// The kind of value expected for this setting
        /// </summary>
        public SystemValueKind ValueKind;

        /// <summary>
        /// Mapping from integer values to string values
        /// </summary>
        public string[]? IntegerMap;

        /// <summary>
        /// Mapping from string values to integer values
        /// </summary>
        public Dictionary<string, long>? ReverseIntegerMap;

        /// <summary>
        /// Create a new ini file handler
        /// </summary>
        /// <param name="settingId"></param>
        public SystemSettingHandlerDescription(string settingId, SystemValueKind valueKind) : base(HandlerKind.System)
        {
            SettingId = settingId;
            ValueKind = valueKind;
        }

        public SystemSettingHandlerDescription(JsonElement element) : base(HandlerKind.System)
        {

            SettingId = element.GetProperty("setting_id").GetString();
            var valueType = element.GetProperty("value_type").GetString();
            ValueKind = Enum.Parse<SystemValueKind>(valueType, ignoreCase: true);
            if (element.TryGetProperty("integer_map", out var integerMap))
            {
                var enumerator = integerMap.EnumerateArray();
                var list = new List<string>();
                var reverse = new Dictionary<string, long>();
                long i = 0;
                foreach (var child in enumerator)
                {
                    var str = child.GetString();
                    list.Add(str);
                    reverse.Add(str, i);
                    ++i;
                }
                IntegerMap = list.ToArray();
                ReverseIntegerMap = reverse;
            }
        }

        public override bool Equals(object? obj)
        {
            if (obj is SystemSettingHandlerDescription other)
            {
                return other.SettingId == SettingId;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return SettingId.GetHashCode();
        }
    }
}
