namespace Morphic.Settings.SettingsHandlers.Native
{
    using Microsoft.Extensions.DependencyInjection;
    using Morphic.Core;
    using SolutionsRegistry;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    
    [SrService]
    class NativeSettingsHandler : SettingsHandler
    {
        private readonly IServiceProvider serviceProvider;

        public NativeSettingsHandler(IServiceProvider serviceProvider)
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

    [SettingsHandlerType("native", typeof(NativeSettingsHandler))]
    public class NativeSettingGroup : SettingGroup
    {

    }
}
