namespace Morphic.Settings.SolutionsRegistry
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Core;
    using Newtonsoft.Json;
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
            bool captureCurrent = solutionPreferences.Previous != null;
            foreach (SettingGroup group in this.SettingGroups)
            {
                Values values = new Values();
                List<Setting>? settings = captureCurrent ? null : new List<Setting>();

                foreach ((string settingId, object? value) in solutionPreferences.Values)
                {
                    if (group.TryGetSetting(settingId, out Setting? setting))
                    {
                        settings?.Add(setting);
                        values.Add(setting!, value);
                    }
                }

                if (settings != null)
                {
                    await group.SettingsHandler.Get(group, settings);
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

        /// <summary>
        /// Resolves a setting from a settingId, which can be either a full path to the setting or just the ID of a
        /// setting in this solution.
        /// </summary>
        /// <param name="settingPath">Path to the setting (solutionId/settingId), or just the settingId.</param>
        /// <returns></returns>
        public Setting? ResolveSettingId(string? settingPath)
        {
            try
            {
                if (string.IsNullOrEmpty(settingPath))
                {
                    return null;
                }
                else if (settingPath.Contains('/'))
                {
                    return this.Solutions.GetSetting(settingPath);
                }
                else
                {
                    return this.GetSetting(settingPath);
                }
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }
    }
}
