namespace Morphic.Settings.SettingsHandlers.Registry
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading.Tasks;
    using Core;
    using DotNetWindowsRegistry;
    using Microsoft.Win32;
    using SolutionsRegistry;
    using IRegistry = DotNetWindowsRegistry.IRegistry;

    [SrService]
    public class RegistrySettingsHandler : SettingsHandler
    {
        private readonly IRegistry registry;
        private readonly IServiceProvider serviceProvider;

        public RegistrySettingsHandler(IServiceProvider serviceProvider, IRegistry registry)
        {
            this.serviceProvider = serviceProvider;
            this.registry = registry;
        }

        public override Task<Values> Get(SettingGroup settingGroup, IEnumerable<Setting>? settings)
            => this.Get((RegistrySettingGroup)settingGroup, settings ?? settingGroup);

        public override Task<bool> Set(SettingGroup settingGroup, Values values)
            => this.Set((RegistrySettingGroup)settingGroup, values);

        public Task<Values> Get(RegistrySettingGroup group, IEnumerable<Setting> settings)
        {
            Values values = new Values();
            using IRegistryKey? key = this.OpenKey(group.RootKeyName, group.KeyPath);
            if (key != null)
            {
                foreach (Setting setting in settings)
                {
                    object? value = key.GetValue(setting.Name);
                    values.Add(setting, value);
                }
            }

            return Task.FromResult(values);
        }

        public Task<bool> Set(RegistrySettingGroup settingGroup, Values values)
        {
            using IRegistryKey? key = this.OpenKey(settingGroup.RootKeyName, settingGroup.KeyPath, true);

            if (key == null)
            {
                return Task.FromResult(key != null);
            }

            foreach ((Setting setting, object? value) in values)
            {
                if (key != null)
                {
                    RegistryValueKind kind = GetValueKind(setting.GetProperty("valueKind"));
                    key.SetValue(setting.Name, value, kind);
                }
            }

            return Task.FromResult(key != null);
        }

        /// <summary>
        /// Gets a root registry key, based on its name (eg, HKEY_CURRENT_USER or HKCU).
        /// </summary>
        /// <param name="rootKeyName">The root key name.</param>
        /// <param name="registry"></param>
        public static IRegistryKey GetRootKey(string rootKeyName, IRegistry registry)
        {
            RegistryHive registryHive;
            switch (rootKeyName.ToUpperInvariant())
            {
                case "HKEY_CLASSES_ROOT":
                case "HKCR":
                    registryHive = RegistryHive.ClassesRoot;
                    break;
                case "HKEY_CURRENT_CONFIG":
                case "HKCC":
                    registryHive = RegistryHive.CurrentConfig;
                    break;
                case "HKEY_CURRENT_USER":
                case "HKCU":
                    registryHive = RegistryHive.CurrentUser;
                    break;
                case "HKEY_LOCAL_MACHINE":
                case "HKLM":
                    registryHive = RegistryHive.LocalMachine;
                    break;
                case "HKEY_PERFORMANCE_DATA":
                    registryHive = RegistryHive.PerformanceData;
                    break;
                case "HKEY_USERS":
                case "HKU":
                    registryHive = RegistryHive.Users;
                    break;
                default:
                    throw new InvalidOperationException($"Unrecognised hKey value {rootKeyName}");
            }

            return registry.OpenBaseKey(registryHive, RegistryView.Default);
        }

        public static RegistryValueKind GetValueKind(string? kindString)
        {
            switch (kindString)
            {
                case "REG_EXPAND_SZ": return RegistryValueKind.ExpandString;
                case "REG_BINARY": return RegistryValueKind.Binary;
                case "REG_DWORD": return RegistryValueKind.DWord;
                case "REG_MULTI_SZ": return RegistryValueKind.MultiString;
                case "REG_QWORD": return RegistryValueKind.QWord;
                case "REG_SZ": return RegistryValueKind.String;
                default: return RegistryValueKind.None;
            }
        }

        private IRegistryKey? OpenKey(string rootName, string keyPath, bool writing = false)
        {
            IRegistryKey root = GetRootKey(rootName, this.registry);

            return writing
                ? root.CreateSubKey(keyPath)
                : root.OpenSubKey(keyPath);
        }

        private static RegistryValueKind GetValueKind(IRegistryKey registryKey, string name)
        {
            if (registryKey is WindowsRegistryKey winReg)
            {
                RegistryKey key = GetRealRegistryKey(winReg);
                return key.GetValueKind(name);
            }
            else
            {
                return RegistryValueKind.String;
            }
        }

        private static RegistryKey GetRealRegistryKey(WindowsRegistryKey registryKey)
        {
            return (RegistryKey)registryKey.GetType()
                .GetField("_registryKey", BindingFlags.Instance | BindingFlags.NonPublic)?
                .GetValue(registryKey)!;
        }
    }
}
