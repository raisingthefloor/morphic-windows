// Copyright 2020-2022 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-windowsnative-lib-cs/blob/main/LICENSE
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

namespace Morphic.WindowsNative.OsVersion;

using Morphic.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public enum WindowsVersion
{
    Win10_v1809,
    Win10_v1903,
    Win10_v1909,
    Win10_v2004,
    Win10_v20H2,
    Win10_v21H1,
    Win10_v21H2,
    Win10_v22H2,
    Win10_vFuture, // any future release of Windows 10 we're not yet aware of
                   //
    Win11_v21H2,
    Win11_v22H2,
    Win11_vFuture // any future release of Windows 11 we're not yet aware of
}

public struct OsVersion
{
    private const int WIN10_1809_BUILD = 17763;
    private const int WIN10_1903_BUILD = 18362;
    private const int WIN10_1909_BUILD = 18363;
    private const int WIN10_2004_BUILD = 19041;
    private const int WIN10_20H2_BUILD = 19042;
    private const int WIN10_21H1_BUILD = 19043;
    private const int WIN10_21H2_BUILD = 19044;
    private const int WIN10_22H2_BUILD = 19045;
	private const int EARLIEST_KNOWN_WIN10_BUILD = 10240 /* WIN10_1507_BUILD */;
    private const int EARLIEST_SUPPORTED_WIN10_BUILD = WIN10_1809_BUILD;
    private const int LATEST_KNOWN_WIN10_BUILD = WIN10_22H2_BUILD;
    //
    private const int WIN11_21H2_BUILD = 22000;
    private const int WIN11_22H2_BUILD = 22621;
    private const int EARLIEST_KNOWN_WIN11_BUILD = 22000 /* WIN11_21H2_BUILD */;
    private const int EARLIEST_SUPPORTED_WIN11_BUILD = WIN11_21H2_BUILD;
    private const int LATEST_KNOWN_WIN11_BUILD = WIN11_22H2_BUILD;

    // NOTE: this function will return null for versions of Windows which are not recognized (generally either old beta builds or versions which are old and which we do not support)
    public static WindowsVersion? GetWindowsVersion()
    {
        //var platform = System.Environment.OSVersion.Platform;
        var version = System.Environment.OSVersion.Version;

        if ((version.Major == 10) && (version.Minor == 0))
        {
            switch (version.Build)
            {
                case WIN10_1809_BUILD:
                    return WindowsVersion.Win10_v1809;
                case WIN10_1903_BUILD:
                    return WindowsVersion.Win10_v1903;
                case WIN10_1909_BUILD:
                    return WindowsVersion.Win10_v1909;
                case WIN10_2004_BUILD:
                    return WindowsVersion.Win10_v2004;
                case WIN10_20H2_BUILD:
                    return WindowsVersion.Win10_v20H2;
                case WIN10_21H1_BUILD:
                    return WindowsVersion.Win10_v21H1;
                case WIN10_21H2_BUILD:
                    return WindowsVersion.Win10_v21H2;
                case WIN10_22H2_BUILD:
                    return WindowsVersion.Win10_v22H2;
                case WIN11_21H2_BUILD:
                    return WindowsVersion.Win11_v21H2;
                case WIN11_22H2_BUILD:
                    return WindowsVersion.Win11_v22H2;
                default:
                    // NOTE: as Microsoft is shipping both Windows 10 and Windows 11 as "10.0.###.###" releases, we may need to add some nuance to this code in the future (for 10 vs 11)
                    if (version.Build > LATEST_KNOWN_WIN10_BUILD && version.Build < EARLIEST_KNOWN_WIN11_BUILD)
                    {
                        return WindowsVersion.Win10_vFuture;
                    }
                    else if (version.Build > LATEST_KNOWN_WIN11_BUILD)
                    {
                        return WindowsVersion.Win11_vFuture;
                    }
                    else
                    {
                        return null;
                    }
            }
        }
        else if ((version.Major == 10) && (version.Minor > 0))
        {
            return WindowsVersion.Win11_vFuture;
        }
        else if (version.Major > 10)
        {
            return WindowsVersion.Win11_vFuture;
        }
        else /* if (version.Major < 10) */
        {
            return null;
        }
    }

    public static bool IsWindows10()
    {
        //var platform = System.Environment.OSVersion.Platform;
        var version = System.Environment.OSVersion.Version;

        if ((version.Major == 10) && (version.Minor == 0))
        {
            // NOTE: as Microsoft is shipping both Windows 10 and Windows 11 as "10.0.###.###" releases, we may need to add some nuance to this code in the future (for 10 vs 11)
            if (version.Build >= EARLIEST_KNOWN_WIN10_BUILD && version.Build < EARLIEST_KNOWN_WIN11_BUILD)
            {
                return true;
            }
        }

        // otherwise, return false
        return false;
    }

    public static bool IsWindows11OrLater()
    {
        var version = System.Environment.OSVersion.Version;

        if ((version.Major == 10) && (version.Minor == 0))
        {
            // NOTE: as Microsoft is shipping both Windows 10 and Windows 11 as "10.0.###.###" releases, we may need to add some nuance to this code in the future (for 10 vs 11)
            if (version.Build >= EARLIEST_KNOWN_WIN11_BUILD)
            {
                return true;
            }
        }
        else if ((version.Major == 10) && (version.Minor > 0))
        {
            return true;
        }
        else if (version.Major > 10)
        {
            return true;
        }

        // otherwise, return false
        return false;
    }

    // NOTE: this function will return null if the build version is not known for the specified WindowsVersion
    private static uint? GetBuildVersionForOsVersion(WindowsVersion version)
    {
        switch (version)
        {
            case WindowsVersion.Win10_v1809:
                return WIN10_1809_BUILD;
            case WindowsVersion.Win10_v1903:
                return WIN10_1903_BUILD;
            case WindowsVersion.Win10_v1909:
                return WIN10_1909_BUILD;
            case WindowsVersion.Win10_v2004:
                return WIN10_2004_BUILD;
            case WindowsVersion.Win10_v20H2:
                return WIN10_20H2_BUILD;
            case WindowsVersion.Win10_v21H2:
                return WIN10_21H2_BUILD;
            case WindowsVersion.Win10_v22H2:
                return WIN10_22H2_BUILD;
            case WindowsVersion.Win10_vFuture:
                return null;
            case WindowsVersion.Win11_v21H2:
                return WIN11_21H2_BUILD;
            case WindowsVersion.Win11_v22H2:
                return WIN11_22H2_BUILD;
            case WindowsVersion.Win11_vFuture:
                return null;
            default:
                Debug.Assert(false, "Unknown Windows version; please add the corresponding case to correct this error");
                return null;
        }
    }

    public static bool IsEqualOrNewerThanVersion(WindowsVersion version)
    {
        var versionBuild = OsVersion.GetBuildVersionForOsVersion(version);
        if (versionBuild is null)
        {
            throw new ArgumentOutOfRangeException(nameof(version));
        }
        var currentVersionBuild = System.Environment.OSVersion.Version.Build;

        // for both windows 10 and windows 11, we can do straightforward build number matching
        if (currentVersionBuild >= versionBuild)
        {
            return true;
        }
        else /* if (currentVersionBuild <= versionBuild) */
        {
            return false;
        }
    }

    public static bool IsNewerThanVersion(WindowsVersion version)
    {
        var versionBuild = OsVersion.GetBuildVersionForOsVersion(version);
        if (versionBuild is null)
        {
            throw new ArgumentOutOfRangeException(nameof(version));
        }
        var currentVersionBuild = System.Environment.OSVersion.Version.Build;

        // for both windows 10 and windows 11, we can do straightforward build number matching
        if (currentVersionBuild > versionBuild)
        {
            return true;
        }
        else /* if (currentVersionBuild <= versionBuild) */
        {
            return false;
        }
    }

    //

    public static MorphicResult<uint, MorphicUnit> GetUpdateBuildRevision()
    {
        var openRegistryKeyResult = Morphic.WindowsNative.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        if (openRegistryKeyResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        var registryKey = openRegistryKeyResult.Value!;

        var getValueResult = registryKey.GetValueData<uint>("UBR");
        if (getValueResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        var updateBuildRevision = getValueResult.Value!;

        return MorphicResult.OkResult(updateBuildRevision);
    }
}