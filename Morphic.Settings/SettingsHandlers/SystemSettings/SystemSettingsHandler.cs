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
                    SystemSettingItem settingItem = this.GetSettingItem(setting.Name);
                    // NOTE: this is another area where changing the result of GetValue to an IMorphicResult could provide clear and granular success/error result
                    object? value = await settingItem.GetValue();
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
                SystemSettingItem settingItem = this.GetSettingItem(setting.Name);
                try
                {
                    await settingItem.SetValue(value);
                }
                catch
                {
                    success = false;
                }
            }

            return success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult;
        }

        private static Dictionary<string, SystemSettingItem> settingCache = new Dictionary<string, SystemSettingItem>();

        private SystemSettingItem GetSettingItem(string settingName)
        {
            // Cache the instance, in case it's re-used.
            if (!settingCache.TryGetValue(settingName, out SystemSettingItem? settingItem))
            {
                settingItem = new SystemSettingItem(settingName, false);
                settingCache[settingName] = settingItem;
            }

            return settingItem;
        }
    }
}
