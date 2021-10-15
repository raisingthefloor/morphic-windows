namespace Morphic.Settings.SettingsHandlers.Ini
{
    using Microsoft.Extensions.DependencyInjection;
    using Morphic.Core;
    using SolutionsRegistry;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Settings handler for INI files.
    /// </summary>
    [SrService]
    public class IniSettingsHandler : SettingsHandler
    {
        private readonly IServiceProvider serviceProvider;

        public IniSettingsHandler(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

		// NOTE: we return both success/failure and a set of values so that we can return a partially list in case of partial failure
        public override async Task<(MorphicResult<MorphicUnit, MorphicUnit>, Values)> GetAsync(SettingGroup settingGroup, IEnumerable<Setting> settings)
        {
            var success = true;

            Values values = new Values();
            try
            {
                Ini ini = this.serviceProvider.GetRequiredService<Ini>();
                await ini.ReadFile(settingGroup.Path);

                foreach (Setting setting in settings)
                {
                    string? value = ini.GetValue(setting.Name);
                    if (value is not null)
                    {
                        values.Add(setting, value);
                    }
                }
            }
            catch
            {
                success = false;
            }

            return (success ? MorphicResult.OkResult() : MorphicResult.ErrorResult(), values);
        }

        public override async Task<MorphicResult<MorphicUnit, MorphicUnit>> SetAsync(SettingGroup settingGroup, Values values)
        {
            try
            {
                Ini ini = this.serviceProvider.GetRequiredService<Ini>();
                await ini.ReadFile(settingGroup.Path);

                foreach ((Setting setting, object? value) in values)
                {
                    ini.SetValue(setting.Name, value?.ToString());
                }

                await ini.WriteFile(settingGroup.Path);

                return MorphicResult.OkResult();
            }
            catch
            {
                return MorphicResult.ErrorResult();
            }
        }
    }

    [SettingsHandlerType("iniFile", typeof(IniSettingsHandler))]
    public class IniSettingGroup : SettingGroup
    {

    }
}
