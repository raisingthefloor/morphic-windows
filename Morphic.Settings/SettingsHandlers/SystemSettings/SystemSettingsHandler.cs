namespace Morphic.Settings.SettingsHandlers.SystemSettings
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using SolutionsRegistry;

    [SettingsHandlerType("systemSettings", typeof(SystemSettingsHandler))]
    public class SystemSettingGroup : SettingGroup
    {
    }

    [SrService(ServiceLifetime.Singleton)]
    public class SystemSettingsHandler : SettingsHandler
    {
        public override async Task<Values> Get(SettingGroup settingGroup, IEnumerable<Setting> settings)
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

        public override async Task<bool> Set(SettingGroup settingGroup, Values values)
        {
            foreach ((Setting setting, object? value) in values)
            {
                SystemSettingItem settingItem = this.GetSettingItem(setting.Name);
                await settingItem.SetValue(value);
            }

            return true;
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
