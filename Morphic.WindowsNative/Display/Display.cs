// Copyright 2020-2022 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-windowsnative-lib-cs/blob/main/LICENSE
//
// The R&D leading to these results received funding from the:
// * Rehabilitation Services Administration, US Dept. of Education under
//   grant H421A150006 (APCP)
// * National Institute on Disability, Independent Living, and
//   Rehabilitation Research (NIDILRR)
// * Administration for Independent Living & Dept. of Education under grants
//   H133E080022 (RERC-IT) and H133E130028/90RE5003-01-00 (UIITA-RERC)
// * European Union's Seventh Framework Programme (FP7/2007-2013) grant
//   agreement nos. 289016 (Cloud4all) and 610510 (Prosperity4All)
// * William and Flora Hewlett Foundation
// * Ontario Ministry of Research and Innovation
// * Canadian Foundation for Innovation
// * Adobe Foundation
// * Consumer Electronics Association Foundation

namespace Morphic.WindowsNative.Display;

using Morphic.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

public struct Display
{
    private readonly IntPtr MonitorHandle;
    public readonly string DisplayName;
    public readonly PInvoke.User32.LUID AdapterId;
    public readonly uint SourceId;

    private Display(IntPtr monitorHandle, string displayName, PInvoke.User32.LUID adapterId, uint sourceId)
    {
        this.MonitorHandle = monitorHandle;
        this.DisplayName = displayName;
        this.AdapterId = adapterId;
        this.SourceId = sourceId;
    }

    //

    public static MorphicResult<Display, MorphicUnit> GetDisplayByMonitorHandle(IntPtr monitorHandle)
    {
        // get the monitor's display name
        var getDisplayNameResult = Display.GetDisplayNameForMonitorHandle(monitorHandle);
        if (getDisplayNameResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        var displayName = getDisplayNameResult.Value!;

        // retrieve the buffer sizes needed to call QueryDisplayConfig (i.e. to get our displays' configs)
        uint numPathArrayElements;
        uint numModeInfoArrayElements;
        var getDisplayConfigBufferSizesSuccess = ExtendedPInvoke.GetDisplayConfigBufferSizes(ExtendedPInvoke.QueryDisplayConfigFlags.QDC_ONLY_ACTIVE_PATHS, out numPathArrayElements, out numModeInfoArrayElements);
        switch (getDisplayConfigBufferSizesSuccess)
        {
            case PInvoke.Win32ErrorCode.ERROR_SUCCESS:
                break;
            case PInvoke.Win32ErrorCode.ERROR_INVALID_PARAMETER:
            case PInvoke.Win32ErrorCode.ERROR_NOT_SUPPORTED:
            case PInvoke.Win32ErrorCode.ERROR_ACCESS_DENIED:
            case PInvoke.Win32ErrorCode.ERROR_GEN_FAILURE:
                // failure
                return MorphicResult.ErrorResult();
            default:
                // unknown error
                return MorphicResult.ErrorResult();
        }

        var pathInfoElements = new ExtendedPInvoke.DISPLAYCONFIG_PATH_INFO[numPathArrayElements];
        var modeInfoElements = new ExtendedPInvoke.DISPLAYCONFIG_MODE_INFO[numModeInfoArrayElements];

        var queryDisplayConfigSuccess = ExtendedPInvoke.QueryDisplayConfig(ExtendedPInvoke.QueryDisplayConfigFlags.QDC_ONLY_ACTIVE_PATHS, ref numPathArrayElements, pathInfoElements, ref numModeInfoArrayElements, modeInfoElements, IntPtr.Zero);
        switch ((PInvoke.Win32ErrorCode)queryDisplayConfigSuccess)
        {
            case PInvoke.Win32ErrorCode.ERROR_SUCCESS:
                break;
            case PInvoke.Win32ErrorCode.ERROR_INVALID_PARAMETER:
            case PInvoke.Win32ErrorCode.ERROR_NOT_SUPPORTED:
            case PInvoke.Win32ErrorCode.ERROR_ACCESS_DENIED:
            case PInvoke.Win32ErrorCode.ERROR_GEN_FAILURE:
            case PInvoke.Win32ErrorCode.ERROR_INSUFFICIENT_BUFFER:
                // failure
                return MorphicResult.ErrorResult();
            default:
                // unknown error
                return MorphicResult.ErrorResult();
        }

        Display? result = null;

        // find the matching display
        var sourceName = ExtendedPInvoke.DISPLAYCONFIG_SOURCE_DEVICE_NAME.InitializeNew();
        foreach (var pathInfoElement in pathInfoElements)
        {
            // get the device name
            sourceName.header.adapterId = pathInfoElement.sourceInfo.adapterId;
            sourceName.header.id = pathInfoElement.sourceInfo.id;
            sourceName.header.type = ExtendedPInvoke.DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
            //
            var displayConfigGetDeviceInfoResult = ExtendedPInvoke.DisplayConfigGetDeviceInfo(ref sourceName);
            switch (displayConfigGetDeviceInfoResult)
            {
                case PInvoke.Win32ErrorCode.ERROR_SUCCESS:
                    break;
                case PInvoke.Win32ErrorCode.ERROR_INVALID_PARAMETER:
                    System.Diagnostics.Debug.Assert(false, "Error getting device info; this is probably a programming error.");
                    return MorphicResult.ErrorResult();
                case PInvoke.Win32ErrorCode.ERROR_NOT_SUPPORTED:
                case PInvoke.Win32ErrorCode.ERROR_ACCESS_DENIED:
                case PInvoke.Win32ErrorCode.ERROR_INSUFFICIENT_BUFFER:
                case PInvoke.Win32ErrorCode.ERROR_GEN_FAILURE:
                    // failure; out of an abundance of caution, try to read the next display (so that we don't fail due to a single "bad" display entry)
                    System.Diagnostics.Debug.Assert(false, "Error getting device info; this may not be an error.");
                    continue;
                //return IMorphicResult<DisplayAdapterIdAndSourceId>.ErrorResult();
                default:
                    // unknown error
                    // failure; out of an abundance of caution, try to read the next display
                    System.Diagnostics.Debug.Assert(false, "Error getting device info; this may not be an error.");
                    continue;
                    //return IMorphicResult<DisplayAdapterIdAndSourceId>.ErrorResult();
            }

            var lengthOfViewGdiDeviceName = Array.IndexOf<char>(sourceName.viewGdiDeviceName, '\0');
            if (lengthOfViewGdiDeviceName < 0)
            {
                lengthOfViewGdiDeviceName = sourceName.viewGdiDeviceName.Length;
            }
            var viewGdiDeviceName = new string(sourceName.viewGdiDeviceName, 0, lengthOfViewGdiDeviceName);

            if (viewGdiDeviceName == displayName)
            {
                // in some circumstances, there could be more than one matching monitor (e.g. a clone).  We should prefer the first one, but 
                // even more than that we should prefer an internal/built-in display.  Find the best match now.

                bool isInternal;
                switch (pathInfoElement.targetInfo.outputTechnology)
                {
                    case PInvoke.User32.DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.DISPLAYCONFIG_OUTPUT_TECHNOLOGY_DISPLAYPORT_EMBEDDED:
                    case PInvoke.User32.DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.DISPLAYCONFIG_OUTPUT_TECHNOLOGY_UDI_EMBEDDED:
                    case PInvoke.User32.DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.DISPLAYCONFIG_OUTPUT_TECHNOLOGY_INTERNAL:
                        isInternal = true;
                        break;
                    default:
                        isInternal = false;
                        break;
                }

                // if this entry matches out monitorName and we either (a) don't have a result yet or (b) have a result but this one is _internal_, then update our result
                if ((result is null) || (isInternal == true))
                {
                    result = new Display(monitorHandle, displayName, sourceName.header.adapterId, sourceName.header.id);
                }
            }
        }

        // if we could not find a matching display, return an error result
        if (result is null)
        {
            return MorphicResult.ErrorResult();
        }

        return MorphicResult.OkResult(result!.Value);
    }

    public static MorphicResult<Display, MorphicUnit> GetDisplayForWindow(IntPtr windowHandle)
    {
        var monitorHandle = Display.GetMonitorHandleForWindow(windowHandle);

        return Display.GetDisplayByMonitorHandle(monitorHandle);
    }

    public static MorphicResult<Display, MorphicUnit> GetDisplayForPoint(Point point)
    {
        var monitorHandle = Display.GetMonitorHandleForPoint(point);

        return Display.GetDisplayByMonitorHandle(monitorHandle);
    }

    public static MorphicResult<Display, MorphicUnit> GetPrimaryDisplay()
    {
        var monitorHandle = Display.GetMonitorHandleForPrimaryMonitor();

        return Display.GetDisplayByMonitorHandle(monitorHandle);
    }

    public static MorphicResult<List<Display>, MorphicUnit> GetAllDisplays()
    {
        var result = new List<Display>();

        var getAllMonitorHandlesResult = Display.GetAllMonitorHandles();
        if (getAllMonitorHandlesResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        var allMonitorHandles = getAllMonitorHandlesResult.Value!;

        foreach (var monitorHandle in allMonitorHandles)
        {
            var getDisplayByMonitorHandleResult = Display.GetDisplayByMonitorHandle(monitorHandle);
            if (getDisplayByMonitorHandleResult.IsError == true)
            {
                return MorphicResult.ErrorResult();
            }
            var display = getDisplayByMonitorHandleResult.Value!;
            result.Add(display);
        }

        return MorphicResult.OkResult(result);
    }

    //

    private static IntPtr GetMonitorHandleForWindow(IntPtr windowHandle)
    {
        // get the handle of the monitor which contains the majority of the specified windowHandle's window
        var monitorHandle = PInvoke.User32.MonitorFromWindow(windowHandle, PInvoke.User32.MonitorOptions.MONITOR_DEFAULTTONEAREST);
        return monitorHandle;
    }

    private static IntPtr GetMonitorHandleForPoint(Point point)
    {
        // get the handle of the monitor which contains the point; this is useful, for instance, for finding the monitor where the mouse cursor currently rests
        var nativePoint = new PInvoke.POINT() { x = point.X, y = point.Y };
        var monitorHandle = PInvoke.User32.MonitorFromPoint(nativePoint, PInvoke.User32.MonitorOptions.MONITOR_DEFAULTTONEAREST);

        return monitorHandle;
    }

    private static IntPtr GetMonitorHandleForPrimaryMonitor()
    {
        // get the handle of the primary monitor
        IntPtr desktopHWnd = PInvoke.User32.GetDesktopWindow();
        var monitorHandle = PInvoke.User32.MonitorFromWindow(desktopHWnd, PInvoke.User32.MonitorOptions.MONITOR_DEFAULTTOPRIMARY);

        return monitorHandle;
    }

    private static MorphicResult<List<IntPtr>, MorphicUnit> GetAllMonitorHandles()
    {
        var monitorHandles = new List<IntPtr>();

        // NOTE: we have chosen not to use the PInvoke.User32 package's EnumDisplayMonitors function because it requires unsafe code
        //unsafe
        //{
        //    var enumDisplayMonitorsResult = PInvoke.User32.EnumDisplayMonitors(
        //        IntPtr.Zero,
        //        IntPtr.Zero,
        //        (IntPtr hMonitor, IntPtr hdcMonitor, PInvoke.RECT* lpRect, void* lParam) =>
        //        {
        //            monitorHandles.Add(hMonitor);
        //
        //            // return true to continue the enumeration
        //            return true;
        //        },
        //        IntPtr.Zero);
        //}

        var enumDisplayMonitorsResult = ExtendedPInvoke.EnumDisplayMonitors(
            IntPtr.Zero,
            IntPtr.Zero,
            (IntPtr hMonitor, IntPtr hdcMonitor, ref PInvoke.RECT lpRect, IntPtr lParam) =>
            {
                monitorHandles.Add(hMonitor);

                // return true to continue the enumeration
                return true;
            },
            IntPtr.Zero);
        if (enumDisplayMonitorsResult == false)
        {
            return MorphicResult.ErrorResult();
        }

        return MorphicResult.OkResult(monitorHandles);
    }

    // NOTE: if the caller does not provide a windowHandle, we use the primary monitor instead
    private static MorphicResult<string, MorphicUnit> GetDisplayNameForMonitorHandle(IntPtr monitorHandle)
    {
        var monitorInfo = ExtendedPInvoke.MONITORINFOEXW.InitializeNew();
        bool getMonitorInfoSuccess = ExtendedPInvoke.GetMonitorInfo(monitorHandle, ref monitorInfo);
        if (getMonitorInfoSuccess == false)
        {
            return MorphicResult.ErrorResult();
        }

        int lengthOfDeviceName = Array.IndexOf(monitorInfo.szDevice, '\0');
        if (lengthOfDeviceName < 0)
        {
            // if the string is not null-terminated, select the whole string; because P/Invoke knows the maximum length from the struct's marshalling definition, this is a safe operation
            lengthOfDeviceName = monitorInfo.szDevice.Length;
        }
        var deviceName = new string(monitorInfo.szDevice, 0, lengthOfDeviceName);

        return MorphicResult.OkResult(deviceName);
    }

    //

    public struct GetDpiOffsetResult
    {
        public int MinimumDpiOffset;
        public int CurrentDpiOffset;
        public int MaximumDpiOffset;
    }

    public MorphicResult<GetDpiOffsetResult, MorphicUnit> GetCurrentDpiOffsetAndRange()
    {
        // retrieve the DPI values (min, current and max) for the monitor
        var getDpiInfo = ExtendedPInvoke.DISPLAYCONFIG_GET_DPI.InitializeNew();
        getDpiInfo.header.type = ExtendedPInvoke.DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_DPI;
        getDpiInfo.header.adapterId = this.AdapterId;
        getDpiInfo.header.id = this.SourceId;
        //
        var displayConfigGetDeviceInfoSuccess = ExtendedPInvoke.DisplayConfigGetDeviceInfo(ref getDpiInfo);
        switch (displayConfigGetDeviceInfoSuccess)
        {
            case PInvoke.Win32ErrorCode.ERROR_SUCCESS:
                break;
            case PInvoke.Win32ErrorCode.ERROR_INVALID_PARAMETER:
                System.Diagnostics.Debug.Assert(false, "Error getting dpi info; this is probably a programming error.");
                return MorphicResult.ErrorResult();
            default:
                // unknown error
                System.Diagnostics.Debug.Assert(false, "Error getting dpi info");
                return MorphicResult.ErrorResult();
        }

        var result = new GetDpiOffsetResult();
        result.MinimumDpiOffset = getDpiInfo.minimumDpiOffset;
        result.MaximumDpiOffset = getDpiInfo.maximumDpiOffset;
        // NOTE: the current offset can be GREATER than the maximum offset (if the user has specified a custom zoom level, for instance)
        result.CurrentDpiOffset = getDpiInfo.currentDpiOffset;

        return MorphicResult.OkResult(result);
    }

    public async Task<MorphicResult<MorphicUnit, MorphicUnit>> SetDpiOffsetAsync(int dpiOffset)
    {
        var thisDisplay = this;
        var adapterId = this.AdapterId;
        var sourceId = this.SourceId;

        return await Task.Run((Func<MorphicResult<MorphicUnit, MorphicUnit>>)(() =>
        {
            // retrieve the DPI values (min, current and max) for the monitor
            var setDpiInfo = ExtendedPInvoke.DISPLAYCONFIG_SET_DPI.InitializeNew();
            setDpiInfo.header.type = ExtendedPInvoke.DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_SET_DPI;
            setDpiInfo.header.adapterId = adapterId;
            setDpiInfo.header.id = sourceId;
            setDpiInfo.dpiOffset = dpiOffset;
            //
            var displayConfigGetDeviceInfoSuccess = ExtendedPInvoke.DisplayConfigSetDeviceInfo(ref setDpiInfo);
            switch (displayConfigGetDeviceInfoSuccess)
            {
                case PInvoke.Win32ErrorCode.ERROR_SUCCESS:
                    break;
                case PInvoke.Win32ErrorCode.ERROR_INVALID_PARAMETER:
                    System.Diagnostics.Debug.Assert(false, "Error setting dpi info; this is probably a programming error.");
                    return MorphicResult.ErrorResult();
                default:
                    // unknown error
                    System.Diagnostics.Debug.Assert(false, "Error setting dpi info");
                    return MorphicResult.ErrorResult();
            }

            // verify that the DPI was set successfully
            // NOTE: this is not technically necessary since we already have a success/failure result, but it's a good sanity check; if it's too early to check this then it's reasonable for us to skip this verification step
            var getCurrentDpiOffsetAndRangeResult = thisDisplay.GetCurrentDpiOffsetAndRange();
            if (getCurrentDpiOffsetAndRangeResult.IsError == true)
            {
                return MorphicResult.ErrorResult();
            }
            var currentDpiOffsetAndRange = getCurrentDpiOffsetAndRangeResult.Value!;
            if (currentDpiOffsetAndRange.CurrentDpiOffset != dpiOffset)
            {
                System.Diagnostics.Debug.Assert(false, "Could not set DPI offset (or the system has not updated the current SPI offset value)");
                return MorphicResult.ErrorResult();
            }

            // otherwise, we succeeded
            return MorphicResult.OkResult();
        }));
    }

    //

    public MorphicResult<double, MorphicUnit> GetScalePercentage()
    {
        var currentDpiOffsetAndRangeResult = this.GetCurrentDpiOffsetAndRange();
        if (currentDpiOffsetAndRangeResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        var currentDpiOffsetAndRange = currentDpiOffsetAndRangeResult.Value!;

        var scalePercentageResult = Display.TranslateDpiOffsetToScalePercentage(currentDpiOffsetAndRange.CurrentDpiOffset, currentDpiOffsetAndRange.MinimumDpiOffset, currentDpiOffsetAndRange.MaximumDpiOffset);
        if (scalePercentageResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        var scalePercentage = scalePercentageResult.Value!;

        return MorphicResult.OkResult(scalePercentage);
    }

    /// <summary>
    /// Sets the DPI scaling
    /// </summary>
    /// <param name="scalePercentage">Scaling percentage to set</param>
    public async Task<MorphicResult<MorphicUnit, MorphicUnit>> SetScalePercentageAsync(double scalePercentage)
    {
        var currentDpiOffsetAndRange = this.GetCurrentDpiOffsetAndRange();
        if (currentDpiOffsetAndRange.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }

        var newDpiOffsetResult = Display.TranslateScalePercentageToDpiOffset(scalePercentage, currentDpiOffsetAndRange.Value.MinimumDpiOffset, currentDpiOffsetAndRange.Value.MaximumDpiOffset);
        if (newDpiOffsetResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        var newDpiOffset = newDpiOffsetResult.Value!;

        // set the DPI offset (using the calculated offset)
        return await this.SetDpiOffsetAsync(newDpiOffset);
    }

    //

    // returns: an array of available scale percentages
    public MorphicResult<List<double>, MorphicUnit> GetAvailableScalePercentages()
    {
        var currentDpiOffsetAndRange = this.GetCurrentDpiOffsetAndRange();
        if (currentDpiOffsetAndRange.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }

        // special-case: if the DPI mode is using a "custom DPI" (which may be a backwards-compatible Windows 8.1 behavior), return _only_ that custom percentage
        if (Display.IsCustomDpiOffset(currentDpiOffsetAndRange.Value.CurrentDpiOffset) == true)
        {
            var customFixedDpiScalePercentageResult = Display.GetCustomDpiOffsetAsPercentage();
            if (customFixedDpiScalePercentageResult.IsError == true)
            {
                return MorphicResult.ErrorResult();
            }
            var customFixedDpiScalePercentage = customFixedDpiScalePercentageResult.Value!;
            //
            var singleScaleResult = new List<double>();
            singleScaleResult.Add(customFixedDpiScalePercentage);
            return MorphicResult.OkResult(singleScaleResult);
        }

        var minimumScaleResult = Display.TranslateDpiOffsetToScalePercentage(currentDpiOffsetAndRange.Value.MinimumDpiOffset, currentDpiOffsetAndRange.Value.MinimumDpiOffset, currentDpiOffsetAndRange.Value.MaximumDpiOffset);
        if (minimumScaleResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        var maximumScaleResult = Display.TranslateDpiOffsetToScalePercentage(currentDpiOffsetAndRange.Value.MaximumDpiOffset, currentDpiOffsetAndRange.Value.MinimumDpiOffset, currentDpiOffsetAndRange.Value.MaximumDpiOffset);
        if (maximumScaleResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }

        var fixedScalePercentagesInRange = this.GetFixedScalePercentagesInRange(minimumScaleResult.Value!, maximumScaleResult.Value!);
        return MorphicResult.OkResult(fixedScalePercentagesInRange);
    }

    public List<double> GetFixedScalePercentagesInRange(double minimum, double maximum)
    {
        var scalePercentages = new List<double>();

        var incrementAmount = 0.25;
        for (var i = minimum; i <= maximum; i += incrementAmount)
        {
            if (i >= 2.50)
                incrementAmount = 0.50;

            scalePercentages.Add(i);
        }

        return scalePercentages;
    }

    //

    // NOTE: if dpiOffset is out of the min<->max range or the range is too broad, this function will return an ErrorResult
    public static MorphicResult<double, MorphicUnit> TranslateDpiOffsetToScalePercentage(int dpiOffset, int minimumDpiOffset, int maximumDpiOffset)
    {
        // special-case: if the DPI mode is using a "custom DPI" (which may be a backwards-compatible Windows 8.1 behavior), capture that value now
        if (Display.IsCustomDpiOffset(dpiOffset) == true)
        {
            var getCustomDpiAsPercentageResult = Display.GetCustomDpiOffsetAsPercentage();
            if (getCustomDpiAsPercentageResult.IsError == true)
            {
                return MorphicResult.ErrorResult();
            }
            var customDpiOffset = getCustomDpiAsPercentageResult.Value!;
            return MorphicResult.OkResult(customDpiOffset);
        }

        /* 
            * minDpiOffset represents 100%, and offsets above that follow this scale (according to Morphic Classic research):
            * 100% - minDpiOffset
            * 125%
            * 150%
            * 175%
            * 200%
            * 225%
            * 250%
            * 300%
            * 350%
            * 400%
            * 450%
            * 500%
            */

        var percentageIndex = dpiOffset - minimumDpiOffset;

        // workaround: on Windows, behavior which set the dpiOffset to 1 less than the minDpiOffset was observed where the scale was still 100%
        // NOTE: ideally we can revisit this and find another method to help sanity-check this result or a way to retrieve this data more accurately
        if (percentageIndex == -1)
        {
            percentageIndex = 0;
        }

        if (percentageIndex < 0)
        {
            return MorphicResult.ErrorResult();
        }

        double scalePercentage;
        switch (percentageIndex)
        {
            case 0:
                scalePercentage = 1.00;
                break;
            case 1:
                scalePercentage = 1.25;
                break;
            case 2:
                scalePercentage = 1.50;
                break;
            case 3:
                scalePercentage = 1.75;
                break;
            case 4:
                scalePercentage = 2.00;
                break;
            case 5:
                scalePercentage = 2.25;
                break;
            case 6:
                scalePercentage = 2.50;
                break;
            case 7:
                scalePercentage = 3.00;
                break;
            case 8:
                scalePercentage = 3.50;
                break;
            case 9:
                scalePercentage = 4.00;
                break;
            case 10:
                scalePercentage = 4.50;
                break;
            case 11:
                scalePercentage = 5.00;
                break;
            default:
                // custom or otherwise unknown percentage
                return MorphicResult.ErrorResult();
        }

        return MorphicResult.OkResult(scalePercentage);
    }

    public static MorphicResult<int, MorphicUnit> TranslateScalePercentageToDpiOffset(double percentage, int minimumDpiOffset, int maximumDpiOffset)
    {
        /* 
            * minDpiOffset represents 100%, and offsets above that follow this scale (according to Morphic Classic research):
            * 100% - minDpiOffset
            * 125%
            * 150%
            * 175%
            * 200%
            * 225%
            * 250%
            * 300%
            * 350%
            * 400%
            * 450%
            * 500%
            */

        int dpiOffsetAboveMinimum;

        switch (percentage)
        {
            case 1.00:
                dpiOffsetAboveMinimum = 0;
                break;
            case 1.25:
                dpiOffsetAboveMinimum = 1;
                break;
            case 1.50:
                dpiOffsetAboveMinimum = 2;
                break;
            case 1.75:
                dpiOffsetAboveMinimum = 3;
                break;
            case 2.00:
                dpiOffsetAboveMinimum = 4;
                break;
            case 2.25:
                dpiOffsetAboveMinimum = 5;
                break;
            case 2.50:
                dpiOffsetAboveMinimum = 6;
                break;
            case 3.00:
                dpiOffsetAboveMinimum = 7;
                break;
            case 3.50:
                dpiOffsetAboveMinimum = 8;
                break;
            case 4.00:
                dpiOffsetAboveMinimum = 9;
                break;
            case 4.50:
                dpiOffsetAboveMinimum = 10;
                break;
            case 5.00:
                dpiOffsetAboveMinimum = 11;
                break;
            default:
                // custom or otherwise unknown percentage
                return MorphicResult.ErrorResult();
        }

        if (minimumDpiOffset + dpiOffsetAboveMinimum > maximumDpiOffset)
        {
            // if the percentage is out of range, return null
            // NOTE: we may want to consider an option which lets us "max out" the dpi offset in this scenario
            System.Diagnostics.Debug.Assert(false, "Display scale percentage is out of range for the current DPI offset range");
            return MorphicResult.ErrorResult();
        }

        // return the dpiOffset which maps to this percentage
        var dpiOffset = minimumDpiOffset + dpiOffsetAboveMinimum;
        return MorphicResult.OkResult(dpiOffset);
    }

    //

    public static bool IsCustomDpiOffset(int dpiOffset)
    {
        // special-case: if the DPI mode is using a "custom DPI" (which may be a backwards-compatible Windows 8.1 behavior), its DPI offset will be represented by a large value (always 1234568 in our testing)
        if (dpiOffset == 1234568)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    // NOTE: Windows users can set a custom DPI percentage (perhaps a backwards-compatibility feature from Windows 8.1)
    public static MorphicResult<double, MorphicUnit> GetCustomDpiOffsetAsPercentage()
    {
        // method 1: read the custom DPI level out of the registry (NOTE: this is machine-wide, not per-monitor)
#pragma warning disable CA1416 // Validate platform compatibility
        object? logPixelsAsObject = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "LogPixels", null);
#pragma warning restore CA1416 // Validate platform compatibility
        if (logPixelsAsObject is null)
        {
            return MorphicResult.ErrorResult();
        }
        if (logPixelsAsObject is int)
        {
            var logPixels = (int)logPixelsAsObject;
            var logPixelsAsPercentage = (double)logPixels / 96;
            return MorphicResult.OkResult(logPixelsAsPercentage);
        }
        else
        {
            return MorphicResult.ErrorResult();
        }

        // method 2: read the custom DPI from .NET
        //using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromHwnd(IntPtr.Zero))
        //{
        //    // NOTE: technically DpiX and DpiY are two separate scales, but in our testing and based on Windows usage models, they should always be the same
        //    var logPixelsAsPercentage = (double)graphics.DpiX / 96;
        //    return MorphicResult.OkResult(logPixelsAsPercentage);
        //}
    }

    //

    // for PerMonitorV2 DPI-aware clients, this function will return the display rectangle in PHYSICAL pixels
    // for non-DPI-aware clients, this function will return the display rectangle in VIRTUAL pixels
    public MorphicResult<Rectangle, Win32ApiError> GetDisplayRectangleInPixels()
    {
        var monitorInfo = ExtendedPInvoke.MONITORINFOEXW.InitializeNew();
        bool getMonitorInfoSuccess = ExtendedPInvoke.GetMonitorInfo(this.MonitorHandle, ref monitorInfo);
        if (getMonitorInfoSuccess == false)
        {
            var win32ErrorCode = PInvoke.Kernel32.GetLastError();
            if (win32ErrorCode != PInvoke.Win32ErrorCode.ERROR_SUCCESS)
            {
                return MorphicResult.ErrorResult(Win32ApiError.Win32Error((uint)win32ErrorCode));
            }
        }

        var displayRect = new Rectangle(monitorInfo.rcMonitor.left, monitorInfo.rcMonitor.top, monitorInfo.rcMonitor.right - monitorInfo.rcMonitor.left, monitorInfo.rcMonitor.bottom - monitorInfo.rcMonitor.top);
        return MorphicResult.OkResult(displayRect);
    }

    // for PerMonitorV2 DPI-aware clients, this function will return the display rectangle in PHYSICAL pixels
    // for non-DPI-aware clients, this function will return the display rectangle in VIRTUAL pixels
    public MorphicResult<Rectangle, MorphicUnit> GetWorkAreaRectangleInPixels()
    {
        var monitorInfo = ExtendedPInvoke.MONITORINFOEXW.InitializeNew();
        bool getMonitorInfoSuccess = ExtendedPInvoke.GetMonitorInfo(this.MonitorHandle, ref monitorInfo);
        if (getMonitorInfoSuccess == false)
        {
            return MorphicResult.ErrorResult();
        }

        var workAreaRect = new Rectangle(monitorInfo.rcWork.left, monitorInfo.rcWork.top, monitorInfo.rcWork.right - monitorInfo.rcWork.left, monitorInfo.rcWork.bottom - monitorInfo.rcWork.top);
        return MorphicResult.OkResult(workAreaRect);
    }

    // NOTE: this function will always return the resolution in physical pixels, regardless of whether the app is DPI aware or not
    public MorphicResult<Size, MorphicUnit> GetDisplayResolutionInPhysicalPixels()
    {
        // get the always-correct display resolution from EnumDisplaySettingsEx...and scale the WorkArea to match
        // NOTE: if the WorkArea specified in SystemParameters.WorkArea has the wrong proportions, we can try using
        //       monitorInfo.rcWork instead

        ExtendedPInvoke.DEVMODEW deviceMode = ExtendedPInvoke.DEVMODEW.InitializeNew();
        // get the display's resolution
        var enumDisplaySettingsResult = ExtendedPInvoke.EnumDisplaySettingsEx(this.DisplayName, ExtendedPInvoke.ENUM_CURRENT_SETTINGS, ref deviceMode, 0);
        if (enumDisplaySettingsResult == false)
        {
            return MorphicResult.ErrorResult();
        }
        //
        var screenWidth = deviceMode.dmPelsWidth;
        var screenHeight = deviceMode.dmPelsHeight;

        return MorphicResult.OkResult(new Size((int)screenWidth, (int)screenHeight));
    }

    public MorphicResult<Size, MorphicUnit> GetDisplayResolutionInVirtualPixels()
    {
        var getDisplayResolutionInPhysicalPixelsResult = this.GetDisplayResolutionInPhysicalPixels();
        if (getDisplayResolutionInPhysicalPixelsResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        var displayResolutionInPhysicalPixels = getDisplayResolutionInPhysicalPixelsResult.Value!;

        var getScalePercentageResult = this.GetScalePercentage();
        if (getScalePercentageResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        var scalePercentage = getScalePercentageResult.Value!;

        var result = new Size((int)(displayResolutionInPhysicalPixels.Width / scalePercentage), (int)(displayResolutionInPhysicalPixels.Height / scalePercentage));
        return MorphicResult.OkResult(result);
    }

    //// TODO: the following code is neither complete nor tested; additionally, it may actually set the display resolution in virtual pixels instead of physical pixels (in which case it must be renamed); it is included here only for completeness in migrating Morphic 1.x code to 2.x
    //public MorphicResult<MorphicUnit, MorphicUnit> SetDisplayResolutionInPhysicalPixels(Size newSize)
    //{
    //    ExtendedPInvoke.DEVMODEW deviceMode = ExtendedPInvoke.DEVMODEW.InitializeNew();
    //    // capture the display's current (mode) settings
    //    var enumDisplaySettingsResult = ExtendedPInvoke.EnumDisplaySettingsEx(this.DisplayName, ExtendedPInvoke.ENUM_CURRENT_SETTINGS, ref deviceMode, 0);
    //    if (enumDisplaySettingsResult == false)
    //    {
    //        return MorphicResult.ErrorResult();
    //    }

    //    // update the mode's width/height
    //    // TODO: is this sufficient?  Do we need to scale or set other properties as well?
    //    deviceMode.dmPelsWidth = (uint)newSize.Width;
    //    deviceMode.dmPelsHeight = (uint)newSize.Height;

    //    // see: https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-changedisplaysettingsexw
    //    var changeDisplaySettingsResult = ExtendedPInvoke.ChangeDisplaySettingsEx(this.DisplayName, ref deviceMode, IntPtr.Zero, ExtendedPInvoke.CDS_UPDATEREGISTRY, IntPtr.Zero);
    //    if (changeDisplaySettingsResult != ExtendedPInvoke.DISP_CHANGE_SUCCESSFUL)
    //    {
    //        // TODO: in the future, we may want to return the actual result; here are the expected error codes...
    //        //       DISP_CHANGE_BADDUALVIEW; DISP_CHANGE_BADFLAGS; DISP_CHANGE_BADMODE; DISP_CHANGE_BADPARAM; DISP_CHANGE_FAILED; DISP_CHANGE_NOTUPDATED; DISP_CHANGE_RESTART
    //        return MorphicResult.ErrorResult();
    //    }

    //    return MorphicResult.OkResult();
    //}
}
