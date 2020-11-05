namespace Morphic.Settings.SettingsHandlers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public abstract class SettingsHandler
    {
        public static IEnumerable<SettingsHandler> All => AllSettingsHandlers.AsReadOnly();
        private static readonly List<SettingsHandler> AllSettingsHandlers = new List<SettingsHandler>();

        protected SettingsHandler()
        {
            AllSettingsHandlers.Add(this);
        }

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

        public virtual Task<Range> GetRange(Setting setting)
        {
            return Task.FromResult(Range.All);
        }

        protected readonly HashSet<Setting> listeningSettings = new HashSet<Setting>();

        public bool IsListening(Setting setting)
        {
            return this.listeningSettings.Contains(setting);
        }

        /// <summary>
        /// Listens for changes to a setting.
        /// </summary>
        /// <returns>true if sucessful, false for failure or if the setting does not support change listening.</returns>
        public bool AddSettingListener(Setting setting)
        {
            if (!this.IsListening(setting))
            {
                this.OnSettingListenerRequired(setting);
                this.listeningSettings.Add(setting);
            }
            return true;
        }

        public void RemoveSettingListener(Setting setting)
        {
            if (this.IsListening(setting))
            {
                this.OnSettingListenerNotRequired(setting);
                this.listeningSettings.Remove(setting);
            }
        }

        /// <summary>Called when listening for changes to a setting is needed.</summary>
        protected virtual bool OnSettingListenerRequired(Setting setting)
        {
            return false;
        }

        /// <summary>Called when listening for changes to a setting is no longer needed.</summary>
        protected virtual void OnSettingListenerNotRequired(Setting setting)
        {
        }

        /// <summary>
        /// Called when a system-wide setting has changed, and all listened settings should be checked for changes.
        /// </summary>
        protected virtual void OnSystemSettingChanged()
        {
            foreach (Setting setting in this.listeningSettings)
            {
                _ = setting.CheckForChange();
            }
        }

        /// <summary>
        /// Called when a system-wide setting has changed, and all listened settings should be checked for changes.
        /// This causes all settings handlers to update the monitored settings.
        /// </summary>
        public static void SystemSettingChanged()
        {
            foreach (SettingsHandler settingsHandler in SettingsHandler.All)
            {
                settingsHandler.OnSystemSettingChanged();
            }
        }
    }
}

