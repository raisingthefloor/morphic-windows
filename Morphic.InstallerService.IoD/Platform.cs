using System;
using System.Runtime.InteropServices;

namespace IoDCLI
{
    public static class Platform
    {
        public static Version Version
        {
            get => Environment.OSVersion.Version; 
        }

        public static string Name
        {
            get
            {
                switch(PlatformType)
                {
                    case PlatformType.Linux:
                        return "Linux";
                    case PlatformType.Windows:
                        return WindowsName;
                    default:
                        return string.Empty;
                }
            }
        }

        public static PlatformType PlatformType
        {
            get
            {
                switch(Environment.OSVersion.Platform)
                {
                    case PlatformID.Unix:
                        return PlatformType.Linux;
                    default:
                        return PlatformType.Windows;
                }
            }
        }

        private static string WindowsName
        {
            get
            {
                var name = "Windows";
                var isServer = false;

                var osVersionInfo = new OSVERSIONINFOEX();
                osVersionInfo.dwOSVersionInfoSize = Marshal.SizeOf(typeof(OSVERSIONINFOEX));
                var success = GetVersionEx(ref osVersionInfo);
                if (success)
                    isServer = osVersionInfo.wProductType == ServerNT;

                var version = Convert.ToDouble(Version.ToString(2));
                switch(version)
                {
                    case 10.0:
                        name = isServer ? "Windows Server 2016" : "Windows 10";
                        break;
                    case 6.4:
                        name = isServer ? "Windows Server 2016" : "Windows 10";
                        break;
                    case 6.3:
                        name = isServer ? "Windows Server 2012 R2" : "Windows 8.1";
                        break;
                    case 6.2:
                        name = isServer ? "Windows Server 2012" : "Windows 8";
                        break;
                    case 6.1:
                        name = isServer ? "Windows Server 2008 R2" : "Windows 7";
                        break;
                    case 6.0:
                        name = isServer ? "Windows Server 2008" : "Windows Vista";
                        break;
                    case 5.2:
                        name = isServer ? "Windows Server 2003" : "Windows XP";
                        break;
                    case 5.1:
                        name = "Windows XP";
                        break;
                    case 5.0:
                        name = "Windows 2000";
                        break;
                }

                return name;
            }
        }

        private const int ServerNT = 3;

        [StructLayout(LayoutKind.Sequential)]
        private struct OSVERSIONINFOEX
        {
            public int dwOSVersionInfoSize;
            public int dwMajorVersion;
            public int dwMinorVersion;
            public int dwBuildNumber;
            public int dwPlatformId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szCSDVersion;
            public short wServicePackMajor;
            public short wServicePackMinor;
            public short wSuiteMask;
            public byte wProductType;
            public byte wReserved;
        }

        [DllImport("kernel32.dll")]
        private static extern bool GetVersionEx(ref OSVERSIONINFOEX osVersionInfo);

        private static Guid DownloadsFolderGuid = new Guid("374DE290-123F-4565-9164-39C4925E467B");


        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int SHGetKnownFolderPath(ref Guid id, int flags, IntPtr token, out IntPtr path);

        public static string DownloadsFolder
        {
            get
            {
                if (Environment.OSVersion.Version.Major < 6) throw new NotSupportedException();

                IntPtr pathPtr = IntPtr.Zero;

                try
                {
                    SHGetKnownFolderPath(ref DownloadsFolderGuid, 0, IntPtr.Zero, out pathPtr);
                    return Marshal.PtrToStringUni(pathPtr);
                }
                finally
                {
                    Marshal.FreeCoTaskMem(pathPtr);
                }
            }
        }
    }
}
