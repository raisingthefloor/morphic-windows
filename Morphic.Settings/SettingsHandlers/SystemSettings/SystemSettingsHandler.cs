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
        public override async Task<Values> GetAsync(SettingGroup settingGroup, IEnumerable<Setting> settings)
        {
            Values values = new Values();

            foreach (Setting setting in settings)
            {
                SystemSettingItem settingItem = this.GetSettingItem(setting.Name);
                object? value = await settingItem.GetValue();
                values.Add(setting, value);
            }

            return values;
        }

        public override async Task<IMorphicResult> SetAsync(SettingGroup settingGroup, Values values)
        {
            foreach ((Setting setting, object? value) in values)
            {
                SystemSettingItem settingItem = this.GetSettingItem(setting.Name);
                await settingItem.SetValue(value);
            }

            return IMorphicResult.SuccessResult;
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
