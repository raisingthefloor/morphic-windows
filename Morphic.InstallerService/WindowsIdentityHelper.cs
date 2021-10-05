using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;

namespace Morphic.InstallerService
{
    class WindowsIdentityHelper
    {
        [DllImport("advapi32.dll", CharSet = CharSet.Auto)]
        public static extern int RegDisablePredefinedCacheEx();

        [DllImport("userenv.dll")]
        public static extern bool LoadUserProfile(IntPtr hToken, ref ProfileInfo lpProfileInfo);

        [StructLayout(LayoutKind.Sequential)]
        public struct ProfileInfo
        {
            public int dwSize;
            public int dwFlags;
            public string lpUserName;
            public string lpProfilePath;
            public string lpDefaultPath;
            public string lpServerName;
            public string lpPolicyPath;
            public IntPtr hProfile;
        }

        [SuppressUnmanagedCodeSecurity]
        [DllImport("advapi32", SetLastError = true)]
        static extern int OpenProcessToken(IntPtr ProcessHandle, int DesiredAccess, ref IntPtr TokenHandle);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("kernel32", SetLastError = true)]
        static extern bool CloseHandle(IntPtr handle);

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public extern static bool DuplicateToken(IntPtr ExistingTokenHandle, int SECURITY_IMPERSONATION_LEVEL, ref IntPtr DuplicateTokenHandle);

        public const int TOKEN_ASSIGN_PRIMARY = 0x0001;
        public const int TOKEN_DUPLICATE = 0x0002;
        public const int TOKEN_IMPERSONATE = 0x0004;
        public const int TOKEN_QUERY = 0x0008;
        public const int TOKEN_QUERY_SOURCE = 0x0010;
        public const int TOKEN_ADJUST_PRIVILEGES = 0x0020;
        public const int TOKEN_ADJUST_GROUPS = 0x0040;
        public const int TOKEN_ADJUST_DEFAULT = 0x0080;
        public const int TOKEN_ADJUST_SESSIONID = 0x0100;
        public const int TOKEN_READ = 0x00020000 | TOKEN_QUERY;
        public const int TOKEN_WRITE = 0x00020000 | TOKEN_ADJUST_PRIVILEGES | TOKEN_ADJUST_GROUPS | TOKEN_ADJUST_DEFAULT;
        public const int TOKEN_EXECUTE = 0x00020000;

        public static List<WindowsIdentity> GetLoggedOnUsers()
        {
            var users = new List<WindowsIdentity>();
            var errs = "";
            var hToken = IntPtr.Zero;

            foreach (var proc in Process.GetProcessesByName("Morphic.InstallerService.Client"))
            {
                try
                {
                    if (OpenProcessToken(proc.Handle, TOKEN_QUERY | TOKEN_IMPERSONATE | TOKEN_DUPLICATE, ref hToken) != 0)
                    {
                        var newId = new WindowsIdentity(hToken);
                        CloseHandle(hToken);
                        users.Add(newId);
                    }
                    else
                    {
                        errs += string.Format("OpenProcess Failed {0}, privilege not held\r\n", Marshal.GetLastWin32Error());
                    }

                }
                catch (Exception ex)
                {
                    errs += string.Format("OpenProcess Failed {0}\r\n", ex.Message);
                }
            }

            if (errs.Length > 0)
            {
                throw new Exception(errs);
            }

            return users;
        }
    }
}
