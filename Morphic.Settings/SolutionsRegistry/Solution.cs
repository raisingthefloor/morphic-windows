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
        public async Task<MorphicResult<MorphicUnit, MorphicUnit>> CaptureAsync(SolutionPreferences solutionPreferences)
        {
            var success = true;

            foreach (SettingGroup settings in this.SettingGroups)
            {
                var (getResult, values) = await settings.GetAllAsync();
                if (getResult.IsError == true)
                {
                    success = false;
                    // NOTE: we will continue capturing the values we _could_ get, even if some failed
                }
                foreach ((Setting setting, object? value) in values)
                {
                    // NOTE: in our testing, sometimes the setting was already present...so we can't be adding it twice.
                    solutionPreferences.Values[setting.Id] = value;
                }
            }

            return success ? MorphicResult.OkResult() : MorphicResult.ErrorResult();
        }

        /// <summary>Applies the preferences to this solution.</summary>
        public async Task<MorphicResult<MorphicUnit, MorphicUnit>> ApplyAsync(SolutionPreferences solutionPreferences)
        {
            var success = true;

            bool captureCurrent = solutionPreferences.Previous is not null;
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

                if (settings is not null)
                {
                    // OBSERVATION: unsure why we are not capturing the values here; does this "Get" function have a side-effect we're trying to take advantage of (perhaps caching)?
                    var (settingHandlerGetResult, _) = await group.SettingsHandler.GetAsync(group, settings);
                    if (settingHandlerGetResult.IsError == true) 
                    {
                        success = false;
                    }
                    // NOTE: if this failed: unsure if we should "continue;" here and skip the setting...or proceed to setting it
                }

                var setResult = await group.SettingsHandler.SetAsync(group, values);
                if (setResult.IsError == true)
                { 
                    success = false;
                }
            }

            return success ? MorphicResult.OkResult() : MorphicResult.ErrorResult();
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
