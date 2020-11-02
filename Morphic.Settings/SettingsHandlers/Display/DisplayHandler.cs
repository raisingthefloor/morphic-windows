namespace Morphic.Settings.SettingsHandlers.Display
{
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

        [Setter("zoom")]
        public Task<bool> SetZoom(Setting setting, object? newValue)
        {
            if (newValue is int index)
            {
                Size[] all = this.display.GetResolutions().Reverse().ToArray();
                if (index >= 0 && index < all.Length)
                {
                    this.display.SetResolution(all[index]);
                }
            }

            return Task.FromResult(true);
        }

        [Getter("zoom")]
        public async Task<object?> GetZoom(Setting settingGroup)
        {
            List<Size> all = this.display.GetResolutions().Reverse().ToList();
            return all.IndexOf(this.display.GetResolution());
        }

        [Getter("resolutionCount")]
        public Task<object?> GetResolutionCount(Setting settingGroup)
        {
            return Task.FromResult<object?>(this.display.GetResolutions().Count());
        }
    }
}
