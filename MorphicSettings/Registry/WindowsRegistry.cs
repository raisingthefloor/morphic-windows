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
using System.Linq;
using Microsoft.Win32;

namespace MorphicSettings
{
    /// <summary>
    /// Concrete <code>IRegistry</code> implementation that gets/sets values from the windows registry
    /// </summary>
    public class WindowsRegistry : IRegistry
    {

        public object? GetValue(string keyName, string valueName, object? defaultValue)
        {
            object? value = defaultValue;
            if (GetKey(keyName) is RegistryKey key)
            {
                value = key.GetValue(valueName, defaultValue, RegistryValueOptions.DoNotExpandEnvironmentNames);
            }
            return value;
        }

        public bool SetValue(string keyName, string valueName, object? value, RegistryValueKind valueKind)
        {
            if (GetKey(keyName, writable: true) is RegistryKey key)
            {
                key.SetValue(valueName, value, valueKind);
                return true;
            }
            return false;
        }

        private RegistryKey? GetKey(string keyName, bool writable = false)
        {
            var components = keyName.Split(@"\");
            var i = 0;
            var key = GetRootKey(components[i++]);
            while (key != null && i < components.Length)
            {
                key = key.OpenSubKey(components[i], writable && i == components.Length - 1);
                ++i;
            }
            return key;
        }

        private RegistryKey? GetRootKey(string name)
        {
            switch (name)
            {
                case "HKEY_CLASSES_ROOT":
                    return Registry.ClassesRoot;
                case "HKEY_CURRENT_CONFIG":
                    return Registry.CurrentConfig;
                case "HKEY_CURRENT_USER":
                    return Registry.CurrentUser;
                case "HKEY_LOCAL_MACHINE":
                    return Registry.LocalMachine;
                case "HKEY_PERFORMANCE_DATA":
                    return Registry.PerformanceData;
                case "HKEY_USERS":
                    return Registry.Users;
            }
            return null;
        }
    }
}
