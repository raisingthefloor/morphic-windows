namespace Morphic.Settings.SolutionsRegistry.SettingsHandlers
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Newtonsoft.Json;

    [JsonObject(MemberSerialization.OptIn)]
    public class SettingGroup : IEnumerable<Setting>
    {
        public Solution Solution { get; private set; } = null!;

        public SettingsHandler SettingsHandler { get; protected set; } = null!;

        [JsonProperty("path")]
        public string Path { get; private set; } = string.Empty;

        [JsonProperty("settings")]
        private Dictionary<string, Setting> All { get; } = new Dictionary<string, Setting>();

        public virtual void Deserialized(IServiceProvider serviceProvider, Solution solution)
        {
            this.Solution = solution;
            Type settingHandlerType = Solutions.GetSettingHandlerType(this.GetType());
            this.SettingsHandler = (SettingsHandler)serviceProvider.GetRequiredService(settingHandlerType);
            foreach ((string settingId, Setting setting) in this.All)
            {
                setting.Deserialized(this, settingId);
            }
        }

        /// <summary>Gets the current values of the settings in this group.</summary>
        public Task<Values> GetAll()
        {
            return this.SettingsHandler.Get(this);
        }

        /// <summary>Sets the values of the settings in this group.</summary>
        public Task Set(Values values)
        {
            return this.SettingsHandler.Set(this, values);
        }

        IEnumerator<Setting> IEnumerable<Setting>.GetEnumerator() => this.All.Values.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => this.All.Values.GetEnumerator();

        public bool TryGetSetting(string settingId, [MaybeNullWhen(false)] out Setting setting)
        {
            return this.All.TryGetValue(settingId, out setting);
        }
    }

    /// <summary>Used by a setting to identify the type of settings handler to use.</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class SettingsHandlerTypeAttribute : Attribute
    {
        public string Name { get; }
        public Type SettingsHandlerType { get; }

        public SettingsHandlerTypeAttribute(string name, Type settingsHandlerType)
        {
            this.Name = name;
            this.SettingsHandlerType = settingsHandlerType;
        }
    }
}
