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

        public override async Task<(MorphicResult<MorphicUnit, MorphicUnit>, Values)> GetAsync(SettingGroup settingGroup, IEnumerable<Setting>? settings)
            => await this.GetAsync((RegistrySettingGroup)settingGroup, settings ?? settingGroup);

        public override async Task<MorphicResult<MorphicUnit, MorphicUnit>> SetAsync(SettingGroup settingGroup, Values values)
            => await this.SetAsync((RegistrySettingGroup)settingGroup, values);

		// NOTE: we return both success/failure and a list of results so that we can return partial results in case of partial failure
#pragma warning disable 1998
        public async Task<(MorphicResult<MorphicUnit, MorphicUnit>, Values)> GetAsync(RegistrySettingGroup group, IEnumerable<Setting> settings)
#pragma warning restore 1998
        {
            var success = true;

            Values values = new Values();
            IRegistryKey? key;
            try
            {
                key = this.OpenKey(group.RootKeyName, group.KeyPath);
            }
            catch
            {
                return (MorphicResult.ErrorResult(), values);
            }

            if (key is not null)
            {
                try
                {
                    foreach (Setting setting in settings)
                    {
                        object? value;
                        try
                        {
                            value = key.GetValue(setting.Name);
                            values.Add(setting, value);
                        }
                        catch
                        {
                            success = false;
                            continue;
                        }
                    }
                }
                finally
                {
                    key?.Dispose();
                }
            }

            return (success ? MorphicResult.OkResult() : MorphicResult.ErrorResult(), values);
        }

#pragma warning disable 1998
        public async Task<MorphicResult<MorphicUnit, MorphicUnit>> SetAsync(RegistrySettingGroup settingGroup, Values values)
#pragma warning restore 1998
        {
            using IRegistryKey? key = this.OpenKey(settingGroup.RootKeyName, settingGroup.KeyPath, true);

            if (key is null)
            {
                return key is not null ? MorphicResult.OkResult() : MorphicResult.ErrorResult();
            }

            foreach ((Setting setting, object? value) in values)
            {
                if (key is not null)
                {
                    RegistryValueKind kind = GetValueKind(setting.GetProperty("valueKind"));
                    key.SetValue(setting.Name, value, kind);
                }
            }

            return key is not null ? MorphicResult.OkResult() : MorphicResult.ErrorResult();
        }

        /// <summary>
        /// Gets a root registry key, based on its name (eg, HKEY_CURRENT_USER or HKCU).
        /// </summary>
        /// <param name="rootKeyName">The root key name.</param>
        /// <param name="registry"></param>
        public static IRegistryKey GetRootKey(string rootKeyName, IRegistry registry)
        {
            RegistryHive registryHive = GetHive(rootKeyName)
                ?? throw new ArgumentOutOfRangeException($"Unrecognised hKey value {rootKeyName}");
            return registry.OpenBaseKey(registryHive, RegistryView.Default);
        }

        public static RegistryHive? GetHive(string rootKeyName)
        {
            switch (rootKeyName.ToUpperInvariant())
            {
                case "HKEY_CLASSES_ROOT":
                case "HKCR":
                    return RegistryHive.ClassesRoot;
                case "HKEY_CURRENT_CONFIG":
                case "HKCC":
                    return RegistryHive.CurrentConfig;
                case "HKEY_CURRENT_USER":
                case "HKCU":
                    return RegistryHive.CurrentUser;
                case "HKEY_LOCAL_MACHINE":
                case "HKLM":
                    return RegistryHive.LocalMachine;
                case "HKEY_PERFORMANCE_DATA":
                    return RegistryHive.PerformanceData;
                case "HKEY_USERS":
                case "HKU":
                    return RegistryHive.Users;
            }

            return null;
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
