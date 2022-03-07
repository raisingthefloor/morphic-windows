using Morphic.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace Morphic.WindowsNative.OsVersion
{
    public enum WindowsVersion
    {
        Win10_v1809,
        Win10_v1903,
        Win10_v1909,
        Win10_v2004,
        Win10_v20H2,
        Win10_v21H1,
        Win10_v21H2,
        Win10_vFuture, // any future release of Windows 10 we're not yet aware of
        //
        Win11_v21H2,
        Win11_vFuture // any future release of Windows 11 we're not yet aware of
    }

    public struct OsVersion
    {
        public static MorphicResult<uint, MorphicUnit> GetUpdateBuildRevision()
        {
            var openRegistryKeyResult = Morphic.WindowsNative.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (openRegistryKeyResult.IsError == true)
            {
                return MorphicResult.ErrorResult();
            }
            var registryKey = openRegistryKeyResult.Value!;

            var getValueResult = registryKey.GetValue<uint>("UBR");
            if (getValueResult.IsError == true)
            {
                return MorphicResult.ErrorResult();
            }
            var updateBuildRevision = getValueResult.Value!;

            return MorphicResult.OkResult(updateBuildRevision);
        }

        public static WindowsVersion? GetWindowsVersion()
        {
            //var platform = System.Environment.OSVersion.Platform;
            var version = System.Environment.OSVersion.Version;
            
            if ((version.Major == 10) && (version.Minor == 0))
            {
                switch (version.Build)
                {
                    case 17763:
                        return WindowsVersion.Win10_v1809;
                    case 18362:
                        return WindowsVersion.Win10_v1903;
                    case 18363:
                        return WindowsVersion.Win10_v1909;
                    case 19041:
                        return WindowsVersion.Win10_v2004;
                    case 19042:
                        return WindowsVersion.Win10_v20H2;
                    case 19043:
                        return WindowsVersion.Win10_v21H1;
                    case 19044:
                        return WindowsVersion.Win10_v21H2;
                    case 22000:
                        return WindowsVersion.Win11_v21H2;
                    default:
                        // NOTE: as Microsoft is shipping both Windows 10 and Windows 11 as "10.0.###.###" releases, we may need to add some nuance to this code in the future (for 10 vs 11)
                        if (version.Build > 19044 && version.Build < 22000)
                        {
                            return WindowsVersion.Win10_vFuture;
                        }
                        else if (version.Build > 22000)
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
                if (version.Build >= 17763 && version.Build < 22000)
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
                if (version.Build >= 22000)
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
    }
}
