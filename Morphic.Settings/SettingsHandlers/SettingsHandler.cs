namespace Morphic.Settings.SettingsHandlers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Bson;

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

        public virtual Task<Range> GetRange(Setting setting)
        {
            return Task.FromResult(Range.All);
        }

        private Dictionary<Setting, List<ISettingChangeListener>> changeListeners =
            new Dictionary<Setting, List<ISettingChangeListener>>();

        public bool IsListening(Setting setting)
        {
            return this.changeListeners.ContainsKey(setting);
        }

        /// <summary>
        /// Listens for changes to a setting.
        /// </summary>
        /// <returns>true if sucessful, false for failure or if the setting does not support change listening.</returns>
        public bool AddChangeListener(Setting setting, ISettingChangeListener listener)
        {
            if (!this.changeListeners.TryGetValue(setting, out List<ISettingChangeListener>? listeners))
            {
                if (!this.OnChangeListenerRequired(setting))
                {
                    return false;
                }

                listeners = new List<ISettingChangeListener>();
                this.changeListeners.Add(setting, listeners);
            }

            listeners.Add(listener);
            return true;
        }

        public void RemoveChangeListener(Setting setting, ISettingChangeListener listener)
        {
            if (this.changeListeners.TryGetValue(setting, out List<ISettingChangeListener>? listeners))
            {
                listeners.Remove(listener);

                if (listeners.Count == 0)
                {
                    this.OnChangeListenerNotRequired(setting);
                    this.changeListeners.Remove(setting);
                }
            }
        }

        /// <summary>Called when listening for changes to a setting is needed.</summary>
        protected virtual bool OnChangeListenerRequired(Setting setting)
        {
            return false;
        }

        /// <summary>Called when listening for changes to a setting is no longer needed.</summary>
        protected virtual void OnChangeListenerNotRequired(Setting setting)
        {
        }

    }
}

