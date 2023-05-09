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

#define OFFLINE_INSTALL

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
    // knownfolders.h
    internal struct KNOWNFOLDERID
    {
        public static Guid FOLDERID_Downloads = new Guid(0x374de290, 0x123f, 0x4565, 0x91, 0x64, 0x39, 0xc4, 0x92, 0x5e, 0x46, 0x7b); // {374DE290-123F-4565-9164-39C4925E467B}
    }

    // NOTE: when using this function, ppszPath must be freed manually using FreeCoTaskMem
    [DllImport("shell32.dll", CharSet=CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rfid,
        uint dwFlags,
        IntPtr hToken,
        out IntPtr ppszPath);

    private const int S_OK = 0;

    internal static MorphicResult<string, MorphicUnit> GetKnownFolderPath(Guid knownFolderId)
    {
        IntPtr pointerToPath;
        var getKnownFolderPathResult = AtOnDemandHelpers.SHGetKnownFolderPath(knownFolderId, 0, IntPtr.Zero, out pointerToPath);
        try
        {
            if (getKnownFolderPathResult == S_OK)
            {
                var path = Marshal.PtrToStringUni(pointerToPath);
                return MorphicResult.OkResult(path!);
            }
            else
            {
                return MorphicResult.ErrorResult();
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(pointerToPath);
        }
    }

    // TODO: make sure the user is logged in before calling this function (and make sure we get the user's login info/preferences so we can search for AT which is not installed)
    internal static List<AtSoftwareDetails> GetListOfAtSoftwareToInstall()
    {
#if OFFLINE_INSTALL
        var getKnownFolderPathResult = AtOnDemandHelpers.GetKnownFolderPath(KNOWNFOLDERID.FOLDERID_Downloads);
        string pathToDownloadsFolder;
        if (getKnownFolderPathResult.IsError == true)
        {
            Debug.Assert(false, "Could not get path to downloads folder");
            return new();
        }
        pathToDownloadsFolder = getKnownFolderPathResult.Value!;
#endif

        var listOfAllAtSoftware = new List<AtSoftwareDetails>();

        // NOTE: in the future, we should move the list of available AT to a data file (or to a trusted server)
        // TODO: check the user's preferences (and the set of available AT), to determine which AT they want to use (instead of adding the full list here)

#if OFFLINE_INSTALL
        //
        // fusion
        var fusionSoftware = new AtSoftwareDetails()
        {
            ShortName = "fusion",
            ProductName = "Fusion",
            ManufacturerName = "Freedom Scientific Inc.",
            DownloadUri = new Uri(System.IO.Path.Combine(pathToDownloadsFolder, "atod\\fusion")), // local URI
            InstallMethod = AtSoftwareInstallMethod.MultipleOfflineInstallers
        };
        listOfAllAtSoftware.Add(fusionSoftware);
        //
        // JAWS
        var jawsSoftware = new AtSoftwareDetails()
        {
            ShortName = "jaws",
            ProductName = "JAWS",
            ManufacturerName = "Freedom Scientific Inc.",
            DownloadUri = new Uri(System.IO.Path.Combine(pathToDownloadsFolder, "atod\\jaws")), // local URI
            InstallMethod = AtSoftwareInstallMethod.MultipleOfflineInstallers
        };
        listOfAllAtSoftware.Add(jawsSoftware);
#endif
        //
        // read&write
        var readAndWriteSoftware = new AtSoftwareDetails()
        {
            ShortName = "readandwrite",
            ProductName = "Read&Write",
            ManufacturerName = "Texthelp Ltd",
#if OFFLINE_INSTALL
            DownloadUri = new Uri(System.IO.Path.Combine(pathToDownloadsFolder, "atod\\readandwrite\\setup.zip")), // local URI
#else
            DownloadUri = new Uri("https://fastdownloads2.texthelp.com/readwrite12/installers/us/setup.zip"), // US download URI
            //DownloadUri = new Uri("https://fastdownloads2.texthelp.com/readwrite12/installers/uk/setup.zip"), // UK download URI
#endif
            InstallMethod = AtSoftwareInstallMethod.ZipFileWithEmbeddedMsi("setup.msi")
        };
        listOfAllAtSoftware.Add(readAndWriteSoftware);
#if OFFLINE_INSTALL
        //
        // ZoomText
        var zoomtextSoftware = new AtSoftwareDetails()
        {
            ShortName = "zoomtext",
            ProductName = "ZoomText",
            ManufacturerName = "Freedom Scientific Inc.",
            DownloadUri = new Uri(System.IO.Path.Combine(pathToDownloadsFolder, "atod\\zoomtext")), // local URI
            InstallMethod = AtSoftwareInstallMethod.MultipleOfflineInstallers
        };
        listOfAllAtSoftware.Add(zoomtextSoftware);
#endif

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
            case "fusion":
                installerGuid = new Guid(0x7FD10D99, 0x9C47, 0x448F, 0x90, 0x08, 0x9E, 0x1D, 0x2C, 0xC4, 0xE5, 0xEA); // {7FD10D99-9C47-448F-9008-9E1D2CC4E5EA}
                break;
            case "jaws":
                // NOTE: this is not listed in the Program Files when JAWS is installed with Fusion; we should double-check against visible entries when JAWS is installed by itself
                installerGuid = new Guid(0xCCEA40E1, 0x5E4E, 0x4707, 0xB2, 0xF4, 0xC1, 0x2A, 0x74, 0xA9, 0x09, 0x56); // {CCEA40E1-5E4E-4707-B2F4-C12A74A90956} // JAWS 2023 Base (from the Fusion Suite, is assumed to be JAWS itself)
                break;
            case "readandwrite":
                installerGuid = new Guid(0x355AB00F, 0x48E8, 0x474E, 0xAC, 0xC4, 0xD9, 0x17, 0xBA, 0xFA, 0x4D, 0x58); // {355AB00F-48E8-474E-ACC4-D917BAFA4D58}
                break;
            case "zoomtext":
                // NOTE: this is not listed in the Program Files when ZoomText is installed with Fusion; we should double-check against visible entries when ZoomText is installed by itself
                installerGuid = new Guid(0x06A0FE6D, 0xFB33, 0x45A0, 0xB7, 0x43, 0xFE, 0xFE, 0x21, 0xE8, 0xCD, 0x99); // {06A0FE6D-FB33-45A0-B743-FEFE21E8CD99}
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

    // NOTE: this variant of DownloadFile downloads the specified file to the current user's temporary folder; it returns a path to the filename
    // NOTE: we should look into adding "file cleanup" code to DownloadFileAsync, in case the download was aborted
    internal static async Task<MorphicResult<string, MorphicUnit>> DownloadFileAsync(Uri uri, Action<double>? progressFunction = null)
    {
        // create a unique, zero-length file to store the download
        string destinationPath;
        try
        {
            destinationPath = System.IO.Path.GetTempFileName();
        }
        catch
        {
            // NOTE: in the future, we may want to return an appropriate error to let the caller know that we couldn't create a temporary file to store the downloaded data
            return MorphicResult.ErrorResult();
        }

        var result = await AtOnDemandHelpers.DownloadFileAsync(uri, destinationPath, true, progressFunction);
        if (result.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }

        return MorphicResult.OkResult(destinationPath);
    }

    // NOTE: we should look into adding "file cleanup" code to DownloadFileAsync, in case the download was aborted
    internal static async Task<MorphicResult<MorphicUnit, MorphicUnit>> DownloadFileAsync(Uri uri, string destinationPath, bool overwriteExistingFile, Action<double>? progressFunction = null)
    {
        // NOTE: WebClient is deprecated, but we have been unable to find any other mechanism that consistently provides the total download size (i.e. content size).
        //       We tried System.Net.Http.HttpClient and it appears to provide _no_ way to get this information; trying to get the content length from the read stream results in an exception
        //       We tried Windows.Web.Http.HttpClient and this will provide the info to us in the Progress parameter of the GetAsync result -- but only if we download the entire file into memory (which doesn't work for our downloads which are often in the 100s of MBs) before saving to disk
        //       We tried using BackgroundDownloader, but that only works inside an AppContainer--and we have concerns about being able to protect the file between download and installation (i.e. know what our checksum is when downloaded and then again verify that when we open up the MSI installer)
        //       The one other potential option is BITS--which we need to look into.  It may be the best replacement (and may effectively be what the BackgroundDownloader which requires an AppContainer is using)

#pragma warning disable SYSLIB0014 // Type or member is obsolete
        var webClient = new System.Net.WebClient();
#pragma warning restore SYSLIB0014 // Type or member is obsolete

        // NOTE: for now, we only call the progressComplete callback if progress has increased at least 0.1% since the last callback
        const double MINIMUM_PERCENTAGE_INCREASE_BETWEEN_PROGRESS_CALLBACKS = 0.001;
        //
        double lastPercentageComplete = 0;
        webClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler((/*object*/ sender, /*DownloadProgressChangedEventArgs*/ e) =>
        {
            if (e.TotalBytesToReceive > 0)
            {
                // if our progress has increased by a whole-digit percent, then update our caller
                var percentageComplete = ((double)e.BytesReceived) / ((double)e.TotalBytesToReceive);
                if (percentageComplete > lastPercentageComplete + MINIMUM_PERCENTAGE_INCREASE_BETWEEN_PROGRESS_CALLBACKS)
                {
                    lastPercentageComplete = percentageComplete;

                    _ = Task.Run(() =>
                    {
                        progressFunction?.Invoke(percentageComplete);
                    });
                }
            }
        });

        if (overwriteExistingFile == false && System.IO.File.Exists(destinationPath) == true)
        {
            return MorphicResult.ErrorResult();
        }

        try
        {
            // NOTE: this will always overwrite the existing file; therefore we do a manual check (above) to protect against overwrite
            await webClient.DownloadFileTaskAsync(uri, destinationPath);

            return MorphicResult.OkResult();
        }
        catch
        {
            // NOTE: we may need to clean up our download here!
            return MorphicResult.ErrorResult();
        }
    }

    // NOTE: ideally a caller would do cleanup themselves, to distinguish between an "unzip zipFile" error and a "delete zipFile" error--since the latter is likely recoverable
    internal static async Task<MorphicResult<MorphicUnit, MorphicUnit>> UnzipThenDelete(string zipFile, string destinationDirectory)
    {
        // unzip the file
        var unzipResult = await AtOnDemandHelpers.UnzipAsync(zipFile, destinationDirectory);

        // delete the zip file
        try
        {
            System.IO.File.Delete(zipFile);
        }
        catch
        {
            return MorphicResult.ErrorResult();
        }

        if (unzipResult.IsSuccess)
        {
            return MorphicResult.OkResult();
        }
        else
        {
            return MorphicResult.ErrorResult();
        }
    }

    internal static async Task<MorphicResult<MorphicUnit, MorphicUnit>> UnzipAsync(string zipFile, string destinationDirectory)
    {
        try
        {
            await Task.Run(() =>
            {
                // NOTE: this takes a moment, so we await; it would be ideal to show an "indeterminate" state temporarily
                System.IO.Compression.ZipFile.ExtractToDirectory(zipFile, destinationDirectory);
            });
        }
        catch
        {
            return MorphicResult.ErrorResult();
        }

        return MorphicResult.OkResult();
    }
}
