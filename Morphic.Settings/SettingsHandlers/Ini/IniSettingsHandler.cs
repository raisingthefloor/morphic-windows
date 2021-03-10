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
        public override async Task<(IMorphicResult, Values)> GetAsync(SettingGroup settingGroup, IEnumerable<Setting> settings)
        {
            var success = true;

            Values values = new Values();
            try
            {
                IniFileReader reader = this.serviceProvider.GetRequiredService<IniFileReader>();
                reader.SetFile(settingGroup.Path);

                Dictionary<string, string> data = reader.ReadData();

                foreach (Setting setting in settings)
                {
                    if (data.TryGetValue(setting.Name, out string? value))
                    {
                        values.Add(setting, value);
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
                IniFileWriter writer = this.serviceProvider.GetService<IniFileWriter>();

                writer.SetFile(settingGroup.Path);
                Dictionary<string, string?> iniData = new Dictionary<string, string?>();
                foreach ((Setting setting, object? value) in values)
                {
                    iniData[setting.Name] = value?.ToString();
                }

                await writer.Write(iniData).Save();

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
