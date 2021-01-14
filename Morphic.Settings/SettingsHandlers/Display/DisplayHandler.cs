namespace Morphic.Settings.SettingsHandlers.Display
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;
    using System.Threading.Tasks;
    using Morphic.Windows.Native.Display;
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
				// option 1: get/set zoom level based on resolution
    //            Size[] all = this.GetResolutions();
    //            if (index >= 0 && index < all.Length)
    //            {
    //                this.display.SetResolution(all[index]);
    //            }

				// option 2: get/set zoom level based on scale percentage
                var all = this.display.GetDPIScales();
                if (index >= 0 && index < all.Count)
                {
                    this.display.SetDpiScale(all[index]);
                }
            }

            return Task.FromResult(true);
        }

        [Getter("zoom")]
        public async Task<object?> GetZoom(Setting settingGroup)
        {
			// option 1: get/set zoom level based on resolution
            //List<Size> all = this.GetResolutions().ToList();
            //return all.IndexOf(this.display.GetResolution());

			// option 2: get/set zoom level based on scale percentage
            var scale = Morphic.Windows.Native.Display.Display.GetMonitorScalePercentage(null);
            if (scale == null)
            {
                return null;
            }

            //List<Size> all = this.GetResolutions().ToList();
            var all = this.display.GetDPIScales();
            return all.IndexOf(scale.Value); //all.IndexOf(this.display.GetResolution());
        }

        [Getter("lastResolution")]
        public Task<object?> GetResolutionCount(Setting settingGroup)
        {
			// option 1: get/set zoom level based on resolution
            return Task.FromResult<object?>(this.GetResolutions().Length);

			// option 2: get/set zoom level based on scale percentage
            return Task.FromResult<object?>(this.display.GetDPIScales().Count);
        }
    }
}
