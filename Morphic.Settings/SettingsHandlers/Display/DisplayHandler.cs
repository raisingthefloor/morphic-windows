namespace Morphic.Settings.SettingsHandlers.SystemSettings
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;
    using System.Threading.Tasks;
    using Windows.Native.Display;
    using SolutionsRegistry;

    [SettingsHandlerType("displaySettings", typeof(DisplaySettingsHandler))]
    public class DisplaySettingGroup : SettingGroup
    {
    }

    [SrService]
    public class DisplaySettingsHandler : FixedSettingsHandler
    {
        private readonly Display display;

        public DisplaySettingsHandler(Display display)
        {
            this.display = display;
        }

        [SetterAttribute("zoom")]
        public Task<bool> SetZoom(Setting setting, object? newValue)
        {
            if (newValue is int index)
            {
                Size[] all = this.display.GetResolutions().ToArray();
                if (index >= 0 && index < all.Length)
                {
                    this.display.SetResolution(all[index]);
                }
            }

            return Task.FromResult(true);
        }

        [GetterAttribute("zoom")]
        public async Task<object?> GetZoom(Setting settingGroup)
        {
            List<Size> all = this.display.GetResolutions().ToList();
            return all.IndexOf(this.display.GetResolution());
        }

        [GetterAttribute("resolutionCount")]
        public Task<object?> GetResolutionCount(Setting settingGroup)
        {
            return Task.FromResult<object?>(this.display.GetResolutions().Count());
        }
    }
}
