namespace Morphic.Settings.SettingsHandlers.Ini
{
    using Microsoft.Extensions.DependencyInjection;
    using Morphic.Core;
    using Morphic.Settings.Resolvers;
    using Newtonsoft.Json;
    using SolutionsRegistry;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Settings handler for layered stacks of INI files.
    /// </summary>
    [SrService]
    class LayeredIniSettingsHandler : SettingsHandler
    {
        private readonly IServiceProvider serviceProvider;

        public LayeredIniSettingsHandler(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        public override async Task<(IMorphicResult, Values)> GetAsync(SettingGroup settingGroup, IEnumerable<Setting> settings)
        {
            var success = true;
            var editLayer = 1;  //the deepest layer at which user settings values are found (currently hardcoding to 1 deep)

            Values values = new Values();
            try
            {
                var currLayer = 0;
                Ini ini = this.serviceProvider.GetRequiredService<Ini>();
                foreach (String path in (settingGroup as LayeredIniSettingGroup).PathLayers)
                {
                    ++currLayer;
                    await ini.ReadFile(Environment.ExpandEnvironmentVariables(path));

                    foreach (Setting setting in settings)
                    {
                        if (!values.Contains(setting))
                        {
                            string? value = ini.GetValue(setting.Name);
                            if (value != null)
                            {
                                Values.ValueType type = Values.ValueType.UserSetting;
                                if (currLayer > editLayer)
                                {
                                    type = Values.ValueType.Hardcoded;
                                }
                                values.Add(setting, value, type);
                            }
                        }
                    }
                }
                foreach (Setting setting in settings)
                {
                    if(!values.Contains(setting))
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
                await ini.ReadFile(Environment.ExpandEnvironmentVariables((settingGroup as LayeredIniSettingGroup).PathLayers[0]));

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

                await ini.WriteFile(Environment.ExpandEnvironmentVariables((settingGroup as LayeredIniSettingGroup).PathLayers[0]));

                return IMorphicResult.SuccessResult;
            }
            catch
            {
                return IMorphicResult.ErrorResult;
            }
        }
    }

    [SettingsHandlerType("LayeredIniFile", typeof(LayeredIniSettingsHandler))]
    public class LayeredIniSettingGroup : SettingGroup
    {
        [JsonProperty("pathlayers", Required = Required.Always)]
        public List<ResolvingString> PathLayers { get; set; } = null!;
    }
}
