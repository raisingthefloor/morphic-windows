namespace Morphic.Settings.SettingsHandlers.Display
{
    using Morphic.WindowsNative.Display;
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
                var getCurrentPositionResult = Morphic.WindowsNative.Mouse.Mouse.GetCurrentPosition();
                if (getCurrentPositionResult.IsError == true)
                {
                    return false;
                }
                var currentMousePosition = getCurrentPositionResult.Value!;
                //
                var getDisplayForPointResult = Morphic.WindowsNative.Display.Display.GetDisplayForPoint(currentMousePosition);
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
            var getCurrentPositionResult = Morphic.WindowsNative.Mouse.Mouse.GetCurrentPosition();
            if (getCurrentPositionResult.IsError == true)
            {
                return Task.FromResult<object?>(null);
            }
            var currentMousePosition = getCurrentPositionResult.Value!;
            //
            var getDisplayForPointResult = Morphic.WindowsNative.Display.Display.GetDisplayForPoint(currentMousePosition);
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
            // TODO: in theory, the scale should match up--but it's a double (which could in theory have small rounding issues) and it could be a custom zoom level; we may want to consider returning the percentage and having OTHER code do the "dot" thing which can also highlight two dots (or the closest dot) if the numbers don't match up
            return Task.FromResult<object?>(all?.IndexOf(scale.Value)); //all.IndexOf(this.display.GetResolution());
        }

        [Getter("zoomLevelCount")]
        public Task<object?> GetZoomLevelCount(Setting settingGroup)
        {
			// method 1: get/set zoom level based on resolution
            //return Task.FromResult<object?>(this.GetResolutions().Length);

			// method 2: get/set zoom level based on scale percentage
            return Task.FromResult<object?>(Display.GetDPIScales()?.Count);
        }
    }
}
