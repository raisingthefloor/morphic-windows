// Copyright 2023 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-windows/blob/master/LICENSE.txt
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

namespace Morphic.Settings.SettingsHandlers.Install
{
    using Morphic.Core;
    using SolutionsRegistry;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;

    [SrService]
    public class InstalledApplicationSettingsHandler : FixedSettingsHandler
    {
        [Getter("isInstalled")]
        public Task<object?> GetIsInstalled(Setting setting)
        {
            var settingGroup = setting.SettingGroup as InstalledApplicationSettingGroup;
            var productCodeAsString = settingGroup.ProductCode;
            Guid productCodeAsGuid;
            var guidParseSuccess = Guid.TryParse(productCodeAsString, out productCodeAsGuid);
            if (guidParseSuccess == false)
            {
                Debug.Assert(false, "Could not parse application's GUID from solution registry");
                // gracefully degrade by returning false
                return Task.FromResult<object?>(false);
            }

            // reformat the GUID in the format used in the registry
            var installerGuidAsString = productCodeAsGuid.ToString("B");

            // NOTE: this code is largely duplicated in Morphic.Client; we should refactor it out into a helper library

            // x64 bit installer, per-machine based installation
            // HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall
            var subkey1Result = Morphic.WindowsNative.Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall");
            if (subkey1Result.IsError == false)
            {
                var registryKey = subkey1Result.Value!;
                var subKeyExistsResult = registryKey.SubKeyExists(installerGuidAsString);
                if (subKeyExistsResult.IsSuccess == true && subKeyExistsResult.Value! == true)
                {
                    return Task.FromResult<object?>(true);
                }
            }

            // x86 bit installer, per-machine based installation
            // HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall
            var subkey2Result = Morphic.WindowsNative.Registry.LocalMachine.OpenSubKey("SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall");
            if (subkey2Result.IsError == false)
            {
                var registryKey = subkey2Result.Value!;
                var subKeyExistsResult = registryKey.SubKeyExists(installerGuidAsString);
                if (subKeyExistsResult.IsSuccess == true && subKeyExistsResult.Value! == true)
                {
                    return Task.FromResult<object?>(true);
                }
            }

            // x64 bit installer, per-user based installation
            // HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall
            var subkey3Result = Morphic.WindowsNative.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall");
            if (subkey3Result.IsError == false)
            {
                var registryKey = subkey3Result.Value!;
                var subKeyExistsResult = registryKey.SubKeyExists(installerGuidAsString);
                if (subKeyExistsResult.IsSuccess == true && subKeyExistsResult.Value! == true)
                {
                    return Task.FromResult<object?>(true);
                }
            }

            // x86 bit installer, per-user based installation
            // HKCU\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall
            var subkey4Result = Morphic.WindowsNative.Registry.CurrentUser.OpenSubKey("SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall");
            if (subkey4Result.IsError == false)
            {
                var registryKey = subkey4Result.Value!;
                var subKeyExistsResult = registryKey.SubKeyExists(installerGuidAsString);
                if (subKeyExistsResult.IsSuccess == true && subKeyExistsResult.Value! == true)
                {
                    return Task.FromResult<object?>(true);
                }
            }

            // if we reach here, we were unable to find proof that the application is installed on the system
            return Task.FromResult<object?>(false);
        }
    }
}
