// Copyright 2020-2026 Raising the Floor - US, Inc.
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

using Morphic.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Morphic.WindowsNative.OsVersion;

public enum WindowsVersion
{
    Win10_v22H2,
    Win10_vFuture, // any future release of Windows 10 we're not yet aware of
    //
    Win11_v22H2,
    Win11_v23H2,
    Win11_v24H2,
    Win11_v25H2,
    Win11_v26H1,
    Win11_vFuture // any future release of Windows 11 we're not yet aware of
}

public static class OsVersion
{
    private enum WindowsPlatform
    {
        Win10,
        Win11,
    }
    //
    private sealed record KnownBuild(WindowsPlatform Platform, uint BuildNumber);
    //
    // Officially supported list of OS versions (for this version of Morphic)
    private static readonly IReadOnlyDictionary<WindowsVersion, KnownBuild> AllKnownBuilds =
        new Dictionary<WindowsVersion, KnownBuild>
        {
            { WindowsVersion.Win10_v22H2, new KnownBuild(WindowsPlatform.Win10, 19045) },
            { WindowsVersion.Win11_v22H2, new KnownBuild(WindowsPlatform.Win11, 22621) },
            { WindowsVersion.Win11_v23H2, new KnownBuild(WindowsPlatform.Win11, 22631) },
            { WindowsVersion.Win11_v24H2, new KnownBuild(WindowsPlatform.Win11, 26100) },
            { WindowsVersion.Win11_v25H2, new KnownBuild(WindowsPlatform.Win11, 26200) },
            { WindowsVersion.Win11_v26H1, new KnownBuild(WindowsPlatform.Win11, 28000) },
        };
    //
    private const uint FIRST_WIN10_BUILD_NUMBER = 10240; // Win10 v1507; not supported, but important as a start of range value
    //private static readonly KnownBuild EARLIEST_SUPPORTED_WIN10_BUILD = AllKnownBuilds.Values.Where(b => b.Platform == WindowsPlatform.Win10).MinBy(b => b.BuildNumber)!;
    private static readonly KnownBuild LATEST_KNOWN_WIN10_BUILD = AllKnownBuilds.Values.Where(b => b.Platform == WindowsPlatform.Win10).MaxBy(b => b.BuildNumber)!;
    //
    private const uint FIRST_WIN11_BUILD_NUMBER = 22000; // Win11 21H2; not supported, but important as a start range value
    //private static readonly KnownBuild EARLIEST_SUPPORTED_WIN11_BUILD = AllKnownBuilds.Values.Where(b => b.Platform == WindowsPlatform.Win11).MinBy(b => b.BuildNumber)!;
    //
    // NOTE: Windows 11 gained two parallel "cores" (branches) in early 2026: the pre-build-28000 branch and the 28000 (new 2026+ computers) branch
    private const uint FIRST_BUILD_OF_WIN11_SPLIT_IN_2026 = 28000;
    private static readonly KnownBuild LATEST_KNOWN_PRESPLIT_WIN11_BUILD = AllKnownBuilds.Values.Where(b => b.Platform == WindowsPlatform.Win11 && b.BuildNumber < FIRST_BUILD_OF_WIN11_SPLIT_IN_2026).MaxBy(b => b.BuildNumber)!;
    private static readonly KnownBuild LATEST_KNOWN_WIN11_BUILD = AllKnownBuilds.Values.Where(b => b.Platform == WindowsPlatform.Win11).MaxBy(b => b.BuildNumber)!;

    // NOTE: this function will return null for versions of Windows which are not recognized (i.e. generally null == older beta builds or old versions which we do not support)
    public static WindowsVersion? GetWindowsVersion()
    {
        //var platform = System.Environment.OSVersion.Platform;
        var version = System.Environment.OSVersion.Version;

        if ((version.Major == 10) && (version.Minor == 0))
        {
            // if the build is a known Windows 10 build, return the corresponding WindowsVersion enum value
            foreach (var knownBuild in AllKnownBuilds)
            {
                if (knownBuild.Value.BuildNumber == version.Build)
                {
                    return knownBuild.Key;
                }
            }
            //
            // otherwise, determine if the build is a Windows 10 or Windows 11 build (or if it's so early we don't know what it is)
            // NOTE: as Microsoft is shipping both Windows 10 and Windows 11 as "10.0.###.###" releases, we may need to add some nuance to this code in the future (for 10 vs 11)
            if (version.Build > OsVersion.LATEST_KNOWN_WIN10_BUILD.BuildNumber && version.Build < OsVersion.FIRST_WIN11_BUILD_NUMBER)
            {
                return WindowsVersion.Win10_vFuture;
            }
            else if ((version.Build > OsVersion.LATEST_KNOWN_PRESPLIT_WIN11_BUILD.BuildNumber && version.Build < FIRST_BUILD_OF_WIN11_SPLIT_IN_2026) || version.Build > OsVersion.LATEST_KNOWN_WIN11_BUILD.BuildNumber)
            {
                // NOTE: Windows 11 gained two parallel "cores" (branches) in early 2026: the pre-build-28000 branch and the 28000 (new 2026+ computers) branch
                return WindowsVersion.Win11_vFuture;
            }
            else // unknown version of Windows (presumably pre-Win10, but this will also catch betas of Windows "in between versions")
            {
                return null;
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
            if (version.Build >= FIRST_WIN10_BUILD_NUMBER && version.Build < FIRST_WIN11_BUILD_NUMBER)
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
            if (version.Build >= FIRST_WIN11_BUILD_NUMBER)
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
    private static uint? GetBuildVersionForOsVersion(WindowsVersion version) =>
        version switch
        {
            WindowsVersion.Win10_vFuture 
            or WindowsVersion.Win11_vFuture => null,
            //
            _ when AllKnownBuilds.TryGetValue(version, out var knownBuild) => knownBuild.BuildNumber,
            _ => throw new System.ComponentModel.InvalidEnumArgumentException(
                nameof(version), (int)version, version.GetType()),
        };

    //

    public static MorphicResult<bool, MorphicUnit> IsEqualOrNewerThanVersion(WindowsVersion version, int? revision = null)
    {
        var versionBuild = OsVersion.GetBuildVersionForOsVersion(version);
        if (versionBuild is null)
        {
            throw new ArgumentOutOfRangeException(
                nameof(version),
                version,
                $"{nameof(WindowsVersion)} '{version}' has no associated build number and cannot be compared against the current OS build number.");
        }
        var currentVersionBuild = System.Environment.OSVersion.Version.Build;

        // for both windows 10 and windows 11, we can do straightforward build number matching
        if (currentVersionBuild >= versionBuild)
        {
            if (currentVersionBuild == versionBuild && revision is not null)
            {
                var getUpdateBuildRevisionResult = OsVersion.GetUpdateBuildRevision();
                if (getUpdateBuildRevisionResult.IsError == true)
                {
                    Debug.Assert(false, "Could not retrieve current OS revision");
                    return MorphicResult.ErrorResult();
                }
                var currentVersionRevision = getUpdateBuildRevisionResult.Value!;

                return MorphicResult.OkResult(currentVersionRevision >= revision!.Value);
            }
            else
            {
                // no revision specified; current build is >= build of `version`
                return MorphicResult.OkResult(true);
            }
        }
        else /* if (currentVersionBuild <= versionBuild) */
        {
            return MorphicResult.OkResult(false);
        }
    }

    //

    public static MorphicResult<uint, MorphicUnit> GetUpdateBuildRevision()
    {
        Microsoft.Win32.RegistryKey? registryKey;
        try
        {
            registryKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", writable: false);
        }
        catch
        {
            return MorphicResult.ErrorResult();
        }
        if (registryKey is null)
        {
            return MorphicResult.ErrorResult();
        }

        var ubrAsNullableObject = registryKey.GetValue("UBR");
        uint updateBuildRevision;
        if (ubrAsNullableObject is null)
        {
            return MorphicResult.ErrorResult();
        }
        else if (ubrAsNullableObject is uint ubrAsUint)
        {
            updateBuildRevision = ubrAsUint;
        }
        else
        {
            return MorphicResult.ErrorResult();
        }

        return MorphicResult.OkResult(updateBuildRevision);
    }
}
