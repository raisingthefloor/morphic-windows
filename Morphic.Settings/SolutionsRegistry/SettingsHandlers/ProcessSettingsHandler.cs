namespace Morphic.Settings.SolutionsRegistry.SettingsHandlers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

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
