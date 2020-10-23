namespace Morphic.Settings.SolutionsRegistry.SettingsHandlers.SystemSettings
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;


    [SettingsHandlerType("systemSettings", typeof(SystemSettingGroup))]
    public class SystemSettingGroup : SettingGroup
    {
    }

    public class SystemSettingsHandler : SettingsHandler
    {
        public override async Task<Values> Get(SettingGroup settingGroup, IEnumerable<Setting> settings)
        {
            Values values = new Values();

            foreach (Setting setting in settings)
            {
                SettingItem settingItem = this.GetSettingItem(setting.Name);
                object? value = await settingItem.GetValue();
                values.Add(setting, value);
            }

            return values;
        }

        public override async Task<bool> Set(SettingGroup settingGroup, Values values)
        {
            foreach ((Setting setting, object? value) in values)
            {
                SettingItem settingItem = this.GetSettingItem(setting.Name);
                await settingItem.SetValue(value);
            }

            return true;
        }

        private static Dictionary<string, SettingItem> settingCache = new Dictionary<string, SettingItem>();

        private SettingItem GetSettingItem(string settingId)
        {
            // Cache the instance, in case it's re-used.
            if (!settingCache.TryGetValue(settingId, out SettingItem? settingItem))
            {
                settingItem = new SettingItem(settingId, false);
                settingCache[settingId] = settingItem;
            }

            return settingItem;
        }
    }
}
