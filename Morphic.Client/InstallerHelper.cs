using Microsoft.Win32;

namespace Morphic.Client
{
    public static class InstallerHelper
    {
        public static bool IsInstalled(string applicationName)
        {
            var installed = CheckIfInstalled(applicationName, Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");

            if (!installed)
                installed = CheckIfInstalled(applicationName, Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall");

            if (!installed)
                installed = CheckIfInstalled(applicationName, Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");

            return installed;
        }

        private static bool CheckIfInstalled(string applicationName, RegistryKey baseKey, string registryKey)
        {
            var key = baseKey.OpenSubKey(registryKey);
            if (key != null)
            {
                if (FindInDisplayNames(applicationName, key))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool FindInDisplayNames(string applicationName, RegistryKey key)
        {
            foreach (var subKeyName in key.GetSubKeyNames())
            {
                var subkey = key.OpenSubKey(subKeyName);

                if (subkey.GetValue("DisplayName") is string displayName && displayName.Contains(applicationName))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
