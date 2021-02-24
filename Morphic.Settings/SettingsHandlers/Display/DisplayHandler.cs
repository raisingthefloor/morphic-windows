namespace Morphic.Settings.SettingsHandlers.Display
{
    using CountlySDK;
    using Morphic.Windows.Native.Display;
    using SolutionsRegistry;
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;
    using System.Threading.Tasks;

    [SettingsHandlerType("displaySettings", typeof(DisplaySettingsHandler))]
    public class DisplaySettingGroup : SettingGroup
    {
    }

    [SrService]
    public class DisplaySettingsHandler : FixedSettingsHandler
    {
        private readonly Display display;

        // TODO: convert the Display class into a class which captures a display adapter's id/name...and then returns results for that display
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
                // method 1: get/set zoom level based on resolution
                //Size[] all = this.GetResolutions();
                //if (index >= 0 && index < all.Length)
                //{
                //    this.display.SetResolution(all[index]);
                //}

                // method 2: get/set zoom level based on scale percentage
                var all = this.display.GetDPIScales();
                double? newDpiScale = null;
                if (all != null && index >= 0 && index < all.Count)
                {
                    newDpiScale = all[index];
                }
                if (newDpiScale != null)
                {
                    // capture the current scale
                    var oldDpiScale = Display.GetMonitorScalePercentage(null);
                    // capture the recommended scale
                    var currentDpiOffsetAndRange = Display.GetCurrentDpiOffsetAndRange();
                    var recommendedDpiScale = currentDpiOffsetAndRange != null ? Display.TranslateDpiOffsetToPercentage(0, currentDpiOffsetAndRange.Value.minimumDpiOffset, currentDpiOffsetAndRange.Value.maximumDpiOffset) : null;
                    // set the new percentage
                    this.display.SetDpiScale(newDpiScale.Value);
                    // report the display scale (percentage) change
                    if (oldDpiScale != null)
                    {
                        var segmentation = new Segmentation();
                        if (recommendedDpiScale != null)
                        {
                            var relativePercent = newDpiScale / recommendedDpiScale;
                            segmentation.Add("scalePercent", ((int)(relativePercent * 100)).ToString());

                            var recommendedDpiIndex = -currentDpiOffsetAndRange!.Value.minimumDpiOffset;
                            var relativeDotOffset = index - recommendedDpiIndex;
                            segmentation.Add("dotOffset", relativeDotOffset.ToString());
                        }
                        //
                        if (newDpiScale > oldDpiScale) {
                            Countly.RecordEvent("textSizeIncrease", 1, segmentation);
                        }
                        else
                        {
                            Countly.RecordEvent("textSizeDecrease", 1, segmentation);
                        }
                    }
                }
            }

            return Task.FromResult(true);
        }

        [Getter("zoom")]
        public Task<object?> GetZoom(Setting settingGroup)
        {
			// method 1: get/set zoom level based on resolution
            //List<Size> all = this.GetResolutions().ToList();
            //return all.IndexOf(this.display.GetResolution());

			// method 2: get/set zoom level based on scale percentage
            var scale = Morphic.Windows.Native.Display.Display.GetMonitorScalePercentage(null);
            if (scale == null)
            {
                return Task.FromResult<object?>(null);
            }

            //List<Size> all = this.GetResolutions().ToList();
            var all = this.display.GetDPIScales();
            return Task.FromResult<object?>(all.IndexOf(scale.Value)); //all.IndexOf(this.display.GetResolution());
        }

        [Getter("zoomLevelCount")]
        public Task<object?> GetZoomLevelCount(Setting settingGroup)
        {
			// method 1: get/set zoom level based on resolution
            //return Task.FromResult<object?>(this.GetResolutions().Length);

			// method 2: get/set zoom level based on scale percentage
            return Task.FromResult<object?>(this.display.GetDPIScales().Count);
        }
    }
}
