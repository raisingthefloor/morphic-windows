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
    /// A registry handler description with registry key names
    /// </summary>
    public class RegistrySettingHandlerDescription : SettingHandlerDescription
    {

        /// <summary>
        /// The full key name in the registry
        /// </summary>
        public string KeyName;

        /// <summary>
        /// The value name within the key
        /// </summary>
        public string ValueName;

        /// <summary>
        /// The expected value kind
        /// </summary>
        public RegistryValueKind ValueKind;

        /// <summary>
        /// Create a new registry handler description
        /// </summary>
        /// <param name="keyName"></param>
        /// <param name="valueName"></param>
        /// <param name="valueKind"></param>
        public RegistrySettingHandlerDescription(string keyName, string valueName, RegistryValueKind valueKind) : base(HandlerKind.Registry)
        {
            KeyName = keyName;
            ValueName = valueName;
            ValueKind = valueKind;
        }

        public RegistrySettingHandlerDescription(JsonElement element) : base(HandlerKind.Registry)
        {
            KeyName = element.GetProperty("key_name").GetString();
            ValueName = element.GetProperty("value_name").GetString();
            var valueType = element.GetProperty("value_type").GetString();
            ValueKind = Enum.Parse<RegistryValueKind>(valueType, ignoreCase: true);
        }

        public override bool Equals(object? obj)
        {
            if (obj is RegistrySettingHandlerDescription other)
            {
                return other.KeyName == KeyName && other.ValueName == ValueName;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return KeyName.GetHashCode() ^ ValueName.GetHashCode();
        }
    }

}
