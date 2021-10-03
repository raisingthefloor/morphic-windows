namespace Morphic.Settings.SettingsHandlers.SystemSettings
{
    using Microsoft.Extensions.DependencyInjection;
    using Morphic.Core;
    using SolutionsRegistry;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    [SettingsHandlerType("systemSettings", typeof(SystemSettingsHandler))]
    public class SystemSettingGroup : SettingGroup
    {
    }

    [SrService(ServiceLifetime.Singleton)]
    public class SystemSettingsHandler : SettingsHandler
    {
		// NOTE: we return both success/failure and a list of results so that we can return partial results in case of partial failure
        public override async Task<(IMorphicResult, Values)> GetAsync(SettingGroup settingGroup, IEnumerable<Setting> settings)
        {
            var success = true;

            Values values = new Values();

            foreach (Setting setting in settings)
            {
                try
                {
                    SystemSettingItem? settingItem = this.GetSettingItem(setting.Name);
                    if(settingItem == null)
                    {
                        success = false;
                        // skip to the next setting
                        continue;
                    }

                    // NOTE: this is another area where changing the result of GetValue to an IMorphicResult could provide clear and granular success/error result
                    object? value = await settingItem!.GetValue();
                    values.Add(setting, value);
                }
                catch
                {
                    success = false;
                    // skip to the next setting
                    continue;
                }
            }

            return (success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult, values);
        }

        public override async Task<IMorphicResult> SetAsync(SettingGroup settingGroup, Values values)
        {
            var success = true;

            foreach ((Setting setting, object? value) in values)
            {
                SystemSettingItem? settingItem = this.GetSettingItem(setting.Name);
                if (settingItem == null)
                {
                    success = false;
                    // skip to the next setting
                    continue;
                }

                try
                {
                    await settingItem!.SetValue(value);
                }
                catch
                {
                    success = false;
                }
            }

            return success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult;
        }

        private static Dictionary<string, SystemSettingItem> settingCache = new Dictionary<string, SystemSettingItem>();

        private SystemSettingItem? GetSettingItem(string settingName)
        {
            // certain setting(s) are not supported for "get" operations on Windows 10 v1809 under our current reverse-engineered "get value" scheme; filter those now
            if (Morphic.Windows.Native.OsVersion.OsVersion.GetWindows10Version() == Windows.Native.OsVersion.Windows10Version.Win10_v1809)
            {
                if (IsSettingSupportedInWindows10v1809(settingName) == false)
                {
                    return null;
                }
            }

            // Cache the instance, in case it's re-used.
            if (!settingCache.TryGetValue(settingName, out SystemSettingItem? settingItem))
            {
                settingItem = new SystemSettingItem(settingName, false);
                settingCache[settingName] = settingItem;
            }

            return settingItem;
        }

        private bool IsSettingSupportedInWindows10v1809(string settingName)
        {
            switch(settingName)
            {
                case "SystemSettings_Personalize_Color_AppsUseLightTheme":
                case "SystemSettings_Personalize_Color_SystemUsesLightTheme":
                    return false;
                default:
                    return true;
            }
        }
    }
}
