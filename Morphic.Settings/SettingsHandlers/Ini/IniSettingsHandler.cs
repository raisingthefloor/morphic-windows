namespace Morphic.Settings.SettingsHandlers.Ini
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using SolutionsRegistry;

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

        public override Task<Values> Get(SettingGroup settingGroup, IEnumerable<Setting> settings)
        {
            IniFileReader reader = this.serviceProvider.GetRequiredService<IniFileReader>();
            reader.SetFile(settingGroup.Path);

            Values values = new Values();
            Dictionary<string, string> data = reader.ReadData();

            foreach (Setting setting in settings)
            {
                if (data.TryGetValue(setting.Name, out string? value))
                {
                    values.Add(setting, value);
                }
            }

            return Task.FromResult(values);
        }

        public override async Task<bool> Set(SettingGroup settingGroup, Values values)
        {
            IniFileWriter writer = this.serviceProvider.GetService<IniFileWriter>();

            writer.SetFile(settingGroup.Path);
            Dictionary<string, string?> iniData = new Dictionary<string, string?>();
            foreach ((Setting setting, object? value) in values)
            {
                iniData[setting.Name] = value?.ToString();
            }

            await writer.Write(iniData).Save();

            return true;
        }
    }

    [SettingsHandlerType("iniFile", typeof(IniSettingsHandler))]
    public class IniSettingGroup : SettingGroup
    {

    }
}
