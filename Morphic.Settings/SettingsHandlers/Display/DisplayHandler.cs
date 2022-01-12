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

        //public Size[] GetResolutions()
        //{
        //    Size current = Display.GetResolution();
        //    double currentRatio = current.Width / (double)current.Height;
        //    var result = Display.GetResolutions().Append(current)
        //        // Get resolutions with a similar ratio
        //        .Where(r => Math.Abs(currentRatio - (r.Width / (double)r.Height)) < 0.1);

        //    // Remove the very similar resolutions (favouring the one closest to the current one)
        //    result = result.GroupBy(r => r.Width)
        //        .Select(g => g.OrderBy(r => Math.Abs(currentRatio - (r.Width / (double)r.Height))).First());

        //    // Sort them by height
        //    return result.OrderByDescending(r => r.Height).ToArray();
        //}

        [Setter("zoom")]
        public async Task<bool> SetZoom(Setting setting, object? newValue)
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
                var all = Display.GetDPIScales();
                double? newDpiScale = null;
                if (all is not null && index >= 0 && index < all.Count)
                {
                    newDpiScale = all[index];
                }
                //
                // NOTE: due to current architectural limitations, Morphic v1.x uses the mouse cursor to determine which display to zoom (instead of finding the display the MorphicBar is on)
                var getCurrentPositionResult = Morphic.Windows.Native.Mouse.Mouse.GetCurrentPosition();
                if (getCurrentPositionResult.IsError == true)
                {
                    return false;
                }
                var currentMousePosition = getCurrentPositionResult.Value!;
                //
                var getDisplayForPointResult = Morphic.Windows.Native.Display.Display.GetDisplayForPoint(currentMousePosition);
                if (getDisplayForPointResult.IsError == true)
                {
                    return false;
                }
                var targetDisplay = getDisplayForPointResult.Value!;
                //
                if (newDpiScale is not null)
                {
                    // capture the current scale
                    var oldDpiScale = targetDisplay.GetMonitorScalePercentage();
                    //
                    // capture the recommended scale
                    Display.GetDpiOffsetResult? currentDpiOffsetAndRange;
                    double? recommendedDpiScale;
                    var getCurrentDpiOffsetAndRangeResult = targetDisplay.GetCurrentDpiOffsetAndRange();
                    if (getCurrentDpiOffsetAndRangeResult.IsSuccess == true)
                    {
                        currentDpiOffsetAndRange = getCurrentDpiOffsetAndRangeResult.Value!;
                        var translateDpiOffsetToScalePercentageResult = Display.TranslateDpiOffsetToScalePercentage(0, currentDpiOffsetAndRange.Value.MinimumDpiOffset, currentDpiOffsetAndRange.Value.MaximumDpiOffset);
                        if (translateDpiOffsetToScalePercentageResult.IsError == true)
                        {
                            recommendedDpiScale = null;
                        }
                        else
                        {
                            recommendedDpiScale = translateDpiOffsetToScalePercentageResult.Value!;
                        }
                    }
                    else
                    {
                        currentDpiOffsetAndRange = null;
                        recommendedDpiScale = null;
                    }
                    //
                    // set the new percentage
                    _ = await targetDisplay.SetDpiScaleAsync(newDpiScale.Value);
                    // report the display scale (percentage) change
                    if (oldDpiScale is not null)
                    {
                        var segmentation = new Segmentation();
                        if (recommendedDpiScale is not null)
                        {
                            var relativePercent = newDpiScale / recommendedDpiScale;
                            segmentation.Add("scalePercent", ((int)(relativePercent * 100)).ToString());

                            var recommendedDpiIndex = -currentDpiOffsetAndRange!.Value.MinimumDpiOffset;
                            var relativeDotOffset = index - recommendedDpiIndex;
                            segmentation.Add("dotOffset", relativeDotOffset.ToString());
                        }
                        //
                        if (newDpiScale > oldDpiScale) {
                            // NOTE: we can't call our main Countly logic from here (which skips Countly event recording if it's not enabled), so we just swallow any "not init'd" errors here
                            try
                            {
                                await Countly.RecordEvent("textSizeIncrease", 1, segmentation);
                            }
                            catch (InvalidOperationException)
                            {
                            }
                        }
                        else
                        {
                            // NOTE: we can't call our main Countly logic from here (which skips Countly event recording if it's not enabled), so we just swallow any "not init'd" errors here
                            try
                            {
                                await Countly.RecordEvent("textSizeDecrease", 1, segmentation);
                            }
                            catch (InvalidOperationException)
                            {
                            }
                        }
                    }
                }
            }

            return true;
        }

        [Getter("zoom")]
        public Task<object?> GetZoom(Setting settingGroup)
        {
            // method 1: get/set zoom level based on resolution
            //List<Size> all = this.GetResolutions().ToList();
            //return all.IndexOf(this.display.GetResolution());

            // method 2: get/set zoom level based on scale percentage
            //
            // NOTE: due to current architectural limitations, Morphic v1.x uses the mouse cursor to determine which display to zoom (instead of finding the display the MorphicBar is on)
            var getCurrentPositionResult = Morphic.Windows.Native.Mouse.Mouse.GetCurrentPosition();
            if (getCurrentPositionResult.IsError == true)
            {
                return Task.FromResult<object?>(null);
            }
            var currentMousePosition = getCurrentPositionResult.Value!;
            //
            var getDisplayForPointResult = Morphic.Windows.Native.Display.Display.GetDisplayForPoint(currentMousePosition);
            if (getDisplayForPointResult.IsError == true)
            {
                return Task.FromResult<object?>(null);
            }
            var targetDisplay = getDisplayForPointResult.Value!;
            //
            var scale = targetDisplay.GetMonitorScalePercentage();
            if (scale is null)
            {
                return Task.FromResult<object?>(null);
            }

            //List<Size> all = this.GetResolutions().ToList();
            var all = Display.GetDPIScales();
            return Task.FromResult<object?>(all.IndexOf(scale.Value)); //all.IndexOf(this.display.GetResolution());
        }

        [Getter("zoomLevelCount")]
        public Task<object?> GetZoomLevelCount(Setting settingGroup)
        {
			// method 1: get/set zoom level based on resolution
            //return Task.FromResult<object?>(this.GetResolutions().Length);

			// method 2: get/set zoom level based on scale percentage
            return Task.FromResult<object?>(Display.GetDPIScales().Count);
        }
    }
}
