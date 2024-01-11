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

namespace Morphic.Client.Dialogs.AtOnDemand;

using Morphic.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Streaming.Adaptive;
using Windows.Networking.BackgroundTransfer;
using Windows.Storage.Streams;

internal class AtOnDemandHelpers
{
    // TODO: make sure the user is logged in before calling this function (and make sure we get the user's login info/preferences so we can search for AT which is not installed)
    internal static List<AtSoftwareDetails> GetListOfAtSoftwareToInstall()
    {
        var listOfAllAtSoftware = new List<AtSoftwareDetails>();
        // NOTE: in the future, we should move the list of available AT to a data file (or to a trusted server)
        // TODO: check the user's preferences (and the set of available AT), to determine which AT they want to use (instead of adding the full list here)
        //
        // read&write
        var readAndWriteSoftware = new AtSoftwareDetails()
        {
            ShortName = "readandwrite",
            ProductName = "Read&Write",
            ManufacturerName = "Texthelp Ltd",
            DownloadUri = new Uri("https://fastdownloads2.texthelp.com/readwrite12/installers/us/setup.zip"), // US download URI
            //DownloadUri = new Uri("https://fastdownloads2.texthelp.com/readwrite12/installers/uk/setup.zip"), // UK download URI
            InstallMethod = AtSoftwareInstallMethod.ZipFileWithEmbeddedMsi("setup.msi")
        };
        listOfAllAtSoftware.Add(readAndWriteSoftware);

        var result = new System.Collections.Generic.List<AtSoftwareDetails>();

        // check to see which AT software applications are not installed
        foreach(var atSoftware in listOfAllAtSoftware)
        {
            var isInstalledResult = AtOnDemandHelpers.IsAtSoftwareInstalled(atSoftware.ShortName);
            if (isInstalledResult.IsError == true)
            {
                Debug.Assert(false, "Failure while trying to determine if AT application '" + atSoftware + "' is installed; this is likely a code bug");
            }
            else
            {
                var isInstalled = isInstalledResult.Value!;
                if (isInstalled == false)
                {
                    result.Add(atSoftware);
                }
            }
        }

        return result;
    }

    private static MorphicResult<bool, MorphicUnit> IsAtSoftwareInstalled(string shortName)
    {
        Guid installerGuid;

        switch (shortName)
        {
            case "readandwrite":
                installerGuid = new Guid(0x355AB00F, 0x48E8, 0x474E, 0xAC, 0xC4, 0xD9, 0x17, 0xBA, 0xFA, 0x4D, 0x58); // {355AB00F-48E8-474E-ACC4-D917BAFA4D58}
                break;
            default:
                return MorphicResult.ErrorResult();
        }

        var installerGuidAsString = installerGuid.ToString("B");

        // x64 bit installer, per-machine based installation
        // HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall
        var subkey1Result = Morphic.WindowsNative.Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall");
        if (subkey1Result.IsError == false)
        {
            var registryKey = subkey1Result.Value!;
            var subKeyExistsResult = registryKey.SubKeyExists(installerGuidAsString);
            if (subKeyExistsResult.IsSuccess == true && subKeyExistsResult.Value! == true)
            {
                return MorphicResult.OkResult(true);
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
                return MorphicResult.OkResult(true);
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
                return MorphicResult.OkResult(true);
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
                return MorphicResult.OkResult(true);
            }
        }

        // if we could not find the installer (represented as a registry key), return false
        return MorphicResult.OkResult(false);
    }
}
