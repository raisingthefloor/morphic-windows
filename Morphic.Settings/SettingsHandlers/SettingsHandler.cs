namespace Morphic.Settings.SettingsHandlers
{
    using Morphic.Core;
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
		// NOTE: we return both success/failure and a list of results so that we can return partial results in case of partial failure
        public abstract Task<(MorphicResult<MorphicUnit, MorphicUnit>, Values)> GetAsync(SettingGroup settingGroup, IEnumerable<Setting> settings);

        /// <summary>Sets the given values to setting in a group.</summary>
        public abstract Task<MorphicResult<MorphicUnit, MorphicUnit>> SetAsync(SettingGroup settingGroup, Values values);

        /// <summary>Gets the value of all settings in a group.</summary>
		// NOTE: we return both success/failure and a list of results so that we can return partial results in case of partial failure
        public virtual async Task<(MorphicResult<MorphicUnit, MorphicUnit>, Values)> GetAsync(SettingGroup settingGroup)
        {
            return await this.GetAsync(settingGroup, settingGroup);
        }

        /// <summary>Gets the value of a single setting.</summary>
        public virtual async Task<MorphicResult<object?, MorphicUnit>> GetAsync(Setting setting)
        {
            // NOTE: we are returning an error if doing a GetAsync was a failure...even if we got one item back.  We need to have a more granular error reporting strategy and
            //       need to determine when it might be safe to return a value even though there was an error
            var (getResult, value) = await this.GetAsync(setting.SettingGroup, new[] { setting });
            if (getResult.IsError == true)
            {
                return MorphicResult.ErrorResult();
            }
                
            return MorphicResult.OkResult((object?)value.FirstOrDefault().Value);
        }

        /// <summary>Set the value of a single setting.</summary>
        public virtual async Task<MorphicResult<MorphicUnit, MorphicUnit>> SetAsync(Setting setting, object? newValue)
        {
            return await this.SetAsync(setting.SettingGroup, new Values(setting, newValue));
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
                // OBSERVATION: we don't check for either success/failure of checking for changes...or capture whether anything was indeed changed
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

