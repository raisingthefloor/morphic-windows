namespace Morphic.Settings.SolutionsRegistry
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Threading.Tasks;
    using Core;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;
    using SettingsHandlers;

    [JsonObject(MemberSerialization.OptIn)]
    public class Solution
    {
        public string SolutionId { get; private set; } = null!;
        public Solutions Solutions { get; private set; } = null!;

        [JsonProperty("settings", ItemTypeNameHandling = TypeNameHandling.Objects)]
        public List<SettingGroup> SettingGroups { get; private set; } = null!;

        public Dictionary<string, Setting> AllSettings = new Dictionary<string, Setting>();

        public Setting GetSetting(string settingId)
        {
            if (this.AllSettings.TryGetValue(settingId, out Setting? setting))
            {
                return setting;
            }

            throw new KeyNotFoundException($"Setting '{settingId}' does not exist in solution '{this.SolutionId}'.");
        }

        /// <summary>Captures the preferences of this solution.</summary>
        public async Task Capture(SolutionPreferences solutionPreferences)
        {
            foreach (SettingGroup settings in this.SettingGroups)
            {
                Values values = await settings.GetAll();
                foreach ((Setting setting, object? value) in values)
                {
                    solutionPreferences.Values.Add(setting.Id, value);
                }
            }
        }

        /// <summary>Applies the preferences to this solution.</summary>
        public async Task Apply(SolutionPreferences solutionPreferences)
        {
            string[] settingIds = solutionPreferences.Values.Keys.ToArray();

            foreach (SettingGroup group in this.SettingGroups)
            {
                Values values = new Values();
                foreach ((string settingId, object? value) in solutionPreferences.Values)
                {
                    if (group.TryGetSetting(settingId, out Setting? setting))
                    {
                        values.Add(setting!, value);
                    }
                }

                await group.SettingsHandler.Set(group, values);
            }
        }

        public void Deserialized(IServiceProvider serviceProvider, Solutions solutions, string solutionId)
        {

            this.SolutionId = solutionId;
            this.Solutions = solutions;
            foreach (SettingGroup settings in this.SettingGroups)
            {
                settings.Deserialized(serviceProvider, this);
                foreach (Setting setting in settings)
                {
                    this.AllSettings.Add(setting.Id, setting);
                }
            }
        }
    }
}
