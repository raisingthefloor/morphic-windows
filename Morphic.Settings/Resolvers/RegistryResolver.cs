namespace Morphic.Settings.Resolvers
{
    using System.IO;
    using Microsoft.Win32;
    using SettingsHandlers.Registry;

    /// <summary>
    /// A resolver that handles registry values.
    ///
    /// Example: ${reg:HKEY_CURRENT_USER\Software\Raising the Floor\Morphic}
    /// or: ${reg:32,HKEY_CURRENT_USER\Software\Raising the Floor\Morphic}
    /// </summary>
    public class RegistryResolver : Resolver
    {
        public override string? ResolveValue(string input)
        {
            string path = input.Replace('/', '\\');
            string[] parts = path.Split("\\", 2);
            if (parts.Length != 2)
            {
                return null;
            }

            string rootName = parts[0];
            string keyPath = Path.GetDirectoryName(parts[1]) ?? string.Empty;
            string valueName = Path.GetFileName(parts[1]);

            // See if the path specifies the view (32 or 64 bit)
            RegistryView registryView;
            if (rootName.StartsWith("32,"))
            {
                registryView = RegistryView.Registry32;
                rootName = rootName.Substring(3);
            }
            else if (rootName.StartsWith("64,"))
            {
                registryView = RegistryView.Registry64;
                rootName = rootName.Substring(3);
            }
            else
            {
                registryView = RegistryView.Default;
            }

            RegistryHive? hive = RegistrySettingsHandler.GetHive(rootName);
            if (hive.HasValue)
            {
                using RegistryKey rootKey = RegistryKey.OpenBaseKey(hive.Value, registryView);
                return rootKey.OpenSubKey(keyPath)?.GetValue(valueName, null)?.ToString();
            }

            return null;
        }
    }
}
