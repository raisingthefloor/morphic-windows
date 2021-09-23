
namespace Morphic.Settings.SettingsHandlers.SPI
{
    using Morphic.Core;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    class SPISettingsHandler: SettingsHandler
    {
        private readonly IServiceProvider serviceProvider;

        public SPISettingsHandler(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        public override Task<(IMorphicResult, Values)> GetAsync(SettingGroup settingGroup, IEnumerable<Setting> settings)
        {
            throw new NotImplementedException();
        }

        public override Task<IMorphicResult> SetAsync(SettingGroup settingGroup, Values values)
        {
            throw new NotImplementedException();
        }
    }
}
