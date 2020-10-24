namespace Morphic.Settings.SettingsHandlers
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public abstract class SettingsHandler
    {
        /// <summary>Gets the value of the specified settings of a group.</summary>
        public abstract Task<Values> Get(SettingGroup settingGroup, IEnumerable<Setting> settings);

        /// <summary>Sets the given values to setting in a group.</summary>
        public abstract Task<bool> Set(SettingGroup settingGroup, Values values);

        /// <summary>Gets the value of all settings in a group.</summary>
        public virtual Task<Values> Get(SettingGroup settingGroup)
        {
            return this.Get(settingGroup, settingGroup);
        }

        /// <summary>Gets the value of a single setting.</summary>
        public virtual async Task<object?> Get(Setting setting)
        {
            return (await this.Get(setting.SettingGroup, new[] { setting })).FirstOrDefault().Value;
        }

        /// <summary>Set the value of a single setting.</summary>
        public virtual Task<bool> Set(Setting setting, object? newValue)
        {
            return this.Set(setting.SettingGroup, new Values(setting, newValue));
        }
    }
}

