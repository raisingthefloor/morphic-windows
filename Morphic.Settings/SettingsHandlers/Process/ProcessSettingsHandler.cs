namespace Morphic.Settings.SettingsHandlers.Process
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using SolutionsRegistry;

    [SrService]
    public class ProcessSettingsHandler : SettingsHandler
    {
        public override Task<Values> Get(SettingGroup settingGroup, IEnumerable<Setting> settings)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> Set(SettingGroup settingGroup, Values values)
        {
            throw new NotImplementedException();
        }
    }
}
