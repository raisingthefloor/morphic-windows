namespace Morphic.Settings.SettingsHandlers.SystemSettings
{
    using Microsoft.Extensions.DependencyInjection;
    using Morphic.Core;
    using SolutionsRegistry;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;

    [SettingsHandlerType("systemSettings", typeof(SystemSettingsHandler))]
    public class SystemSettingGroup : SettingGroup
    {
    }

    [SrService(ServiceLifetime.Singleton)]
    public class SystemSettingsHandler : SettingsHandler
    {
		// NOTE: we return both success/failure and a list of results so that we can return partial results in case of partial failure
        public override async Task<(MorphicResult<MorphicUnit, MorphicUnit>, Values)> GetAsync(SettingGroup settingGroup, IEnumerable<Setting> settings)
        {
            var success = true;

            Values values = new Values();

            foreach (Setting setting in settings)
            {
                try
                {
                    SystemSettingItem? settingItem = this.GetSettingItem(setting.Name);
                    if(settingItem is null)
                    {
                        success = false;
                        // skip to the next setting
                        continue;
                    }

                    // NOTE: this is another area where changing the result of GetValue to a MorphicResult<,> could provide clear and granular success/error result
                    object? value = await settingItem!.GetValue();
                    values.Add(setting, value);
                }
                catch
                {
                    success = false;
                    // skip to the next setting
                    continue;
                }
            }

            return (success ? MorphicResult.OkResult() : MorphicResult.ErrorResult(), values);
        }

        public override async Task<MorphicResult<MorphicUnit, MorphicUnit>> SetAsync(SettingGroup settingGroup, Values values)
        {
            var success = true;

            foreach ((Setting setting, object? value) in values)
            {
                SystemSettingItem? settingItem = this.GetSettingItem(setting.Name);
                if (settingItem is null)
                {
                    success = false;
                    // skip to the next setting
                    continue;
                }

                try
                {
                    await settingItem!.SetValue(value);
                }
                catch
                {
                    success = false;
                }
            }

            return success ? MorphicResult.OkResult() : MorphicResult.ErrorResult();
        }

        private static Dictionary<string, SystemSettingItem> settingCache = new Dictionary<string, SystemSettingItem>();

        private SystemSettingItem? GetSettingItem(string settingName)
        {
            // Cache the instance, in case it's re-used.
            if (!settingCache.TryGetValue(settingName, out SystemSettingItem? settingItem))
            {
                try 
                {
                    settingItem = new SystemSettingItem(settingName);
                    settingCache[settingName] = settingItem;
                }
                catch (Exception ex)
                {
                    Debug.Assert(false, "Tried to get setting item; got exception: " + ex.Message);
                    return null;
                }
            }

            return settingItem;
        }
    }
}
