namespace Morphic.Settings.SettingsHandlers.Display
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

        public Size[] GetResolutions()
        {
            Size current = this.display.GetResolution();
            double currentRatio = current.Width / (double)current.Height;
            var result = this.display.GetResolutions().Append(current)
                // Get resolutions with a similar ratio
                .Where(r => Math.Abs(currentRatio - (r.Width / (double)r.Height)) < 0.1);

            // Remove the very similar resolutions (favouring the one closest to the current one)
            result = result.GroupBy(r => r.Width)
                .Select(g => g.OrderBy(r => Math.Abs(currentRatio - (r.Width / (double)r.Height))).First());

            // Sort them by height
            return result.OrderByDescending(r => r.Height).ToArray();
        }

        [Setter("zoom")]
        public Task<bool> SetZoom(Setting setting, object? newValue)
        {
            if (newValue is int index)
            {
                Size[] all = this.GetResolutions();
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
            List<Size> all = this.GetResolutions().ToList();
            return all.IndexOf(this.display.GetResolution());
        }

        [Getter("resolutionCount")]
        public Task<object?> GetResolutionCount(Setting settingGroup)
        {
            return Task.FromResult<object?>(this.GetResolutions().Length);
        }
    }
}
