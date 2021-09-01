﻿namespace Morphic.Settings.SettingsHandlers.Ini
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
        public override async Task<(IMorphicResult, Values)> GetAsync(SettingGroup settingGroup, IEnumerable<Setting> settings)
        {
            var success = true;

            Values values = new Values();
            try
            {
                Ini ini = this.serviceProvider.GetRequiredService<Ini>();
                await ini.ReadFile(Environment.ExpandEnvironmentVariables(settingGroup.Path));

                foreach (Setting setting in settings)
                {
                    string? value = ini.GetValue(setting.Name);
                    if (value != null)
                    {
                        values.Add(setting, value, Values.ValueType.UserSetting);
                    }
                    else
                    {
                        values.Add(setting, null, Values.ValueType.NotFound);
                    }
                }
            }
            catch
            {
                success = false;
            }

            return (success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult, values);
        }

        public override async Task<IMorphicResult> SetAsync(SettingGroup settingGroup, Values values)
        {
            try
            {
                Ini ini = this.serviceProvider.GetRequiredService<Ini>();
                await ini.ReadFile(Environment.ExpandEnvironmentVariables(settingGroup.Path));

                foreach ((Setting setting, object? value) in values)
                {
                    if (setting.Default == value?.ToString())   //deletes values that match the default
                    {
                        ini.SetValue(setting.Name, null);
                    }
                    else
                    {
                        ini.SetValue(setting.Name, value?.ToString());
                    }
                }

                await ini.WriteFile(Environment.ExpandEnvironmentVariables(settingGroup.Path));

                return IMorphicResult.SuccessResult;
            }
            catch
            {
                return IMorphicResult.ErrorResult;
            }
        }
    }

    [SettingsHandlerType("iniFile", typeof(IniSettingsHandler))]
    public class IniSettingGroup : SettingGroup
    {

    }
}
