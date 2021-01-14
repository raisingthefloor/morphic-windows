// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt
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

namespace Morphic.Windows.Native.Display
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;

    // ReSharper disable InconsistentNaming - Uses naming from the Windows API
    // ReSharper disable IdentifierTypo - Uses naming from the Windows API
    public class Display
    {
        // NOTE: if the caller does not provide an hWnd, we use the primary monitor instead
        // NOTE: this function returns null if it could not obtain the monitor name
        public static String? GetMonitorName(IntPtr? hWnd)
        {
            IntPtr monitorHandle;
            if (hWnd == null)
            {
                // get the handle of the primary monitor
                var desktopHWnd = WindowsApi.GetDesktopWindow();
                monitorHandle = WindowsApi.MonitorFromWindow(desktopHWnd, WindowsApi.MONITOR_DEFAULTTOPRIMARY);
            }
            else
            {
                // get the handle of the monitor which contains the majority of the specified hwnd's window
                monitorHandle = WindowsApi.MonitorFromWindow(hWnd.Value, WindowsApi.MONITOR_DEFAULTTONEAREST);
            }

            var monitorInfo = new WindowsApi.MONITORINFOEXA();
            monitorInfo.Init();
            var getMonitorInfoSuccess = WindowsApi.GetMonitorInfo(monitorHandle, ref monitorInfo);
            if (getMonitorInfoSuccess == false)
            {
                return null;
            }

            var lengthOfDeviceName = Array.IndexOf<char>(monitorInfo.szDevice, '\0');
            if (lengthOfDeviceName < 0)
            {
                lengthOfDeviceName = monitorInfo.szDevice.Length;
            }
            var deviceName = new String(monitorInfo.szDevice, 0, lengthOfDeviceName);

            return deviceName;
        }

        // NOTE: if the caller does not provide an hWnd, we use the primary monitor instead
        // NOTE: this function returns null if it could not obtain the work area name
        public static Rectangle? GetPhysicalMonitorWorkArea(IntPtr? hWnd)
        {
            IntPtr monitorHandle;
            if (hWnd == null)
            {
                // get the handle of the primary monitor
                var desktopHWnd = WindowsApi.GetDesktopWindow();
                monitorHandle = WindowsApi.MonitorFromWindow(desktopHWnd, WindowsApi.MONITOR_DEFAULTTOPRIMARY);
            }
            else
            {
                // get the handle of the monitor which contains the majority of the specified hwnd's window
                monitorHandle = WindowsApi.MonitorFromWindow(hWnd.Value, WindowsApi.MONITOR_DEFAULTTONEAREST);
            }

            var monitorInfo = new WindowsApi.MONITORINFOEXA();
            monitorInfo.Init();
            var getMonitorInfoSuccess = WindowsApi.GetMonitorInfo(monitorHandle, ref monitorInfo);
            if (getMonitorInfoSuccess == false)
            {
                return null;
            }

            return new Rectangle(monitorInfo.rcWork.left, monitorInfo.rcWork.top, monitorInfo.rcWork.right - monitorInfo.rcWork.left, monitorInfo.rcWork.bottom - monitorInfo.rcWork.top);
        }

        // GetMonitorScalePercentage is a helper function meant to enable us to always know the actual scaling percentage (even if WPF or our process doesn't know the true value)
        // NOTE: if the caller does not provide an hWnd, we use the primary monitor instead
        // NOTE: this function returns null if it could not obtain the work area name
        public static double? GetMonitorScalePercentage(IntPtr? hWnd)
        {
            // capture the current dpi "offset"
            // capture the name of our primary monitor
            // NOTE: we could pass in the hWnd of a specific window instead
            var monitorName = Morphic.Windows.Native.Display.Display.GetMonitorName(hWnd);
            if (monitorName == null)
            {
                System.Diagnostics.Debug.Assert(false, "Could not get monitor name");
                return null;
            }

            // get the adapterId and sourceId for this monitor
            var adapterIdAndSourceId = Morphic.Windows.Native.Display.Display.GetAdapterIdAndSourceId(monitorName);
            if (adapterIdAndSourceId == null)
            {
                System.Diagnostics.Debug.Assert(false, "Could not get adapter ids");
                return null;
            }

            var dpiOffsetInfo = Morphic.Windows.Native.Display.Display.GetCurrentDpiOffsetAndRange(adapterIdAndSourceId.Value.adapterId, adapterIdAndSourceId.Value.sourceId);
            if (dpiOffsetInfo == null)
            {
                return null;
            }

            // convert the dpiOffset to a percentage
            var currentDisplayScale = Morphic.Windows.Native.Display.Display.TranslateDpiOffsetToPercentage(dpiOffsetInfo.Value.currentDpiOffset,
                dpiOffsetInfo.Value.minimumDpiOffset, dpiOffsetInfo.Value.maximumDpiOffset);
            if (currentDisplayScale == null)
            {
                System.Diagnostics.Debug.Assert(false, "Current display scale % could not be calculated.");
                return null;
            }
            else
            {
                return currentDisplayScale;
            }
        }

		// This is a helper function for use by applications that need a single array of available scale percentages
        public List<double>? GetDPIScales()
        {
            // if no display adapter id info was provided, get an id to the primary monitor
            var primaryMonitorName = Display.GetMonitorName(null);
            if (primaryMonitorName == null)
            {
                return null;
            }

            var primaryMonitorIds = Display.GetAdapterIdAndSourceId(primaryMonitorName);
            if (primaryMonitorIds == null)
            {
                return null;
            }

            var currentDpiOffsetAndRange = Display.GetCurrentDpiOffsetAndRange(primaryMonitorIds.Value.adapterId, primaryMonitorIds.Value.sourceId);
            if (currentDpiOffsetAndRange == null)
            {
                return null;
            }

            // special-case: if the DPI mode is using a "custom DPI" (which may be a backwards-compatible Windows 8.1 behavior), return _only_ that custom percentage
            if (IsCustomDpiOffset(currentDpiOffsetAndRange.Value.currentDpiOffset) == true)
            {
                var customFixedDpiScalePercentage = Display.GetCustomDpiAsPercentage();
                if (customFixedDpiScalePercentage == null)
                {
                    return null;
                }
                //
                var singleScaleResult = new List<double>();
                singleScaleResult.Add(customFixedDpiScalePercentage.Value);
                return singleScaleResult;
            }

            var minimumScale = Display.TranslateDpiOffsetToPercentage(currentDpiOffsetAndRange.Value.minimumDpiOffset, currentDpiOffsetAndRange.Value.minimumDpiOffset, currentDpiOffsetAndRange.Value.maximumDpiOffset);
            if (minimumScale == null)
            {
                return null;
            }
            var maximumScale = Display.TranslateDpiOffsetToPercentage(currentDpiOffsetAndRange.Value.maximumDpiOffset, currentDpiOffsetAndRange.Value.minimumDpiOffset, currentDpiOffsetAndRange.Value.maximumDpiOffset);
            if (maximumScale == null)
            {
                return null;
            }

            return GetDPIScales(minimumScale.Value, maximumScale.Value);
        }
		//
        public List<double> GetDPIScales(double minimum, double maximum)
        {
            var scales = new List<double>();

            var incrementAmount = 0.25;
            for (var i = minimum; i <= maximum; i += incrementAmount)
            {
                if (i >= 250)
                    incrementAmount = 0.50;

                scales.Add(i);
            }

            return scales;
        }

        public struct DisplayAdapterIdAndSourceId
        {
            public WindowsApi.LUID adapterId;
            public uint sourceId;

            public DisplayAdapterIdAndSourceId(WindowsApi.LUID adapterId, uint sourceId)
            {
                this.adapterId = adapterId;
                this.sourceId = sourceId;
            }
        }
        public static DisplayAdapterIdAndSourceId? GetAdapterIdAndSourceId(String monitorName)
        {
            uint numPathArrayElements;
            uint numModeInfoArrayElements;
            var getDisplayConfigBufferSizesSuccess = WindowsApi.GetDisplayConfigBufferSizes(WindowsApi.QueryDisplayConfigFlags.QDC_ONLY_ACTIVE_PATHS, out numPathArrayElements, out numModeInfoArrayElements);
            switch (getDisplayConfigBufferSizesSuccess)
            {
                case WindowsApi.ErrorCode.ERROR_SUCCESS:
                    break;
                case WindowsApi.ErrorCode.ERROR_INVALID_PARAMETER:
                case WindowsApi.ErrorCode.ERROR_NOT_SUPPORTED:
                case WindowsApi.ErrorCode.ERROR_ACCESS_DENIED:
                case WindowsApi.ErrorCode.ERROR_GEN_FAILURE:
                    // failure
                    return null;
                default:
                    // unknown error
                    return null;
            }

            var pathInfoElements = new WindowsApi.DISPLAYCONFIG_PATH_INFO[numPathArrayElements];
            var modeInfoElements = new WindowsApi.DISPLAYCONFIG_MODE_INFO[numModeInfoArrayElements];

            var queryDisplayConfigSuccess = WindowsApi.QueryDisplayConfig(WindowsApi.QueryDisplayConfigFlags.QDC_ONLY_ACTIVE_PATHS, ref numPathArrayElements, pathInfoElements, ref numModeInfoArrayElements, modeInfoElements, IntPtr.Zero);
            switch (queryDisplayConfigSuccess)
            {
                case WindowsApi.ErrorCode.ERROR_SUCCESS:
                    break;
                case WindowsApi.ErrorCode.ERROR_INVALID_PARAMETER:
                case WindowsApi.ErrorCode.ERROR_NOT_SUPPORTED:
                case WindowsApi.ErrorCode.ERROR_ACCESS_DENIED:
                case WindowsApi.ErrorCode.ERROR_GEN_FAILURE:
                case WindowsApi.ErrorCode.ERROR_INSUFFICIENT_BUFFER:
                    // failure
                    return null;
                default:
                    // unknown error
                    return null;
            }

            DisplayAdapterIdAndSourceId? result = null;

            // find the matching display
            var sourceName = new WindowsApi.DISPLAYCONFIG_SOURCE_DEVICE_NAME();
            sourceName.Init();
            foreach (var pathInfoElement in pathInfoElements)
            {
                // get the device name
                sourceName.header.adapterId = pathInfoElement.sourceInfo.adapterId;
                sourceName.header.id = pathInfoElement.sourceInfo.id;
                sourceName.header.type = WindowsApi.DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;

                var displayConfigGetDeviceInfoSuccess = WindowsApi.DisplayConfigGetDeviceInfo(ref sourceName);
                switch (displayConfigGetDeviceInfoSuccess)
                {
                    case WindowsApi.ErrorCode.ERROR_SUCCESS:
                        break;
                    case WindowsApi.ErrorCode.ERROR_INVALID_PARAMETER:
                        System.Diagnostics.Debug.Assert(false, "Error getting device info; this is probably a programming error.");
                        return null;
                    case WindowsApi.ErrorCode.ERROR_NOT_SUPPORTED:
                    case WindowsApi.ErrorCode.ERROR_ACCESS_DENIED:
                    case WindowsApi.ErrorCode.ERROR_INSUFFICIENT_BUFFER:
                    case WindowsApi.ErrorCode.ERROR_GEN_FAILURE:
                        // failure; out of an abundance of caution, try to read the next display
                        System.Diagnostics.Debug.Assert(false, "Error getting device info; this may not be an error.");
                        continue;
                        //return null;
                    default:
                        // unknown error
                        // failure; out of an abundance of caution, try to read the next display
                        System.Diagnostics.Debug.Assert(false, "Error getting device info; this may not be an error.");
                        continue;
                        //return null;
                }

                var lengthOfViewGdiDeviceName = Array.IndexOf<char>(sourceName.viewGdiDeviceName, '\0');
                if (lengthOfViewGdiDeviceName < 0)
                {
                    lengthOfViewGdiDeviceName = sourceName.viewGdiDeviceName.Length;
                }
                var viewGdiDeviceName = new String(sourceName.viewGdiDeviceName, 0, lengthOfViewGdiDeviceName);

                if (viewGdiDeviceName == monitorName)
                {
                    // in some circumstances, there could be more than one matching monitor (e.g. a clone).  We should prefer the first one, but 
                    // even more than that we should prefer an internal/built-in display.  Find the best match now

                    bool isInternal;
                    switch (pathInfoElement.targetInfo.outputTechnology)
                    {
                        case WindowsApi.DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.DISPLAYCONFIG_OUTPUT_TECHNOLOGY_DISPLAYPORT_EMBEDDED:
                        case WindowsApi.DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.DISPLAYCONFIG_OUTPUT_TECHNOLOGY_UDI_EMBEDDED:
                        case WindowsApi.DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.DISPLAYCONFIG_OUTPUT_TECHNOLOGY_INTERNAL:
                            isInternal = true;
                            break;
                        default:
                            isInternal = false;
                            break;
                    }

                    if ((result == null) || (isInternal == true))
                    {
                        result = new DisplayAdapterIdAndSourceId(sourceName.header.adapterId, sourceName.header.id);
                    }
                }
            }

            return result;
        }

        public struct GetDpiOffsetResult
        {
            public int minimumDpiOffset;
            public int currentDpiOffset;
            public int maximumDpiOffset;
        }
        public static GetDpiOffsetResult? GetCurrentDpiOffsetAndRange(WindowsApi.LUID adapterId, uint sourceId)
        {
            // retrieve the DPI values (min, current and max) for the monitor
            var getDpiInfo = new WindowsApi.DISPLAYCONFIG_GET_DPI();
            getDpiInfo.Init();
            getDpiInfo.header.type = WindowsApi.DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_DPI;
            getDpiInfo.header.adapterId = adapterId;
            getDpiInfo.header.id = sourceId;
            //
            var displayConfigGetDeviceInfoSuccess = WindowsApi.DisplayConfigGetDeviceInfo(ref getDpiInfo);
            switch (displayConfigGetDeviceInfoSuccess)
            {
                case WindowsApi.ErrorCode.ERROR_SUCCESS:
                    break;
                case WindowsApi.ErrorCode.ERROR_INVALID_PARAMETER:
                    System.Diagnostics.Debug.Assert(false, "Error getting dpi info; this is probably a programming error.");
                    return null;
                default:
                    // unknown error
                    // failure; out of an abundance of caution, try to read the next display
                    System.Diagnostics.Debug.Assert(false, "Error getting dpi info");
                    return null;
            }

            var result = new GetDpiOffsetResult();
            result.minimumDpiOffset = getDpiInfo.minimumDpiOffset;
            result.maximumDpiOffset = getDpiInfo.maximumDpiOffset;
            // NOTE: the current offset can be GREATER than the maximum offset (if the user has specified a custom zoom level, for instance)
            result.currentDpiOffset = getDpiInfo.currentDpiOffset;

            return result;
        }

        public static int? TranslatePercentageToDpiOffset(double percentage, int minimumDpiOffset, int maximumDpiOffset)
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
                    return null;
            }

            if (minimumDpiOffset + dpiOffsetAboveMinimum > maximumDpiOffset)
            {
                // if the percentage is out of range, return null
                // NOTE: we may want to consider an option which lets us "max out" the dpi offset in this scenario
                System.Diagnostics.Debug.Assert(false, "Display scale percentage is out of range for the current DPI offset range");
                return null;
            }

            // return the dpiOffset which maps to this percentage
            return minimumDpiOffset + dpiOffsetAboveMinimum;
        }

        private static bool IsCustomDpiOffset(int dpiOffset)
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

        // NOTE: if dpiOffset is out of the min<->max range or the range is too broad, this function will return null
        public static double? TranslateDpiOffsetToPercentage(int dpiOffset, int minimumDpiOffset, int maximumDpiOffset)
        {
            // special-case: if the DPI mode is using a "custom DPI" (which may be a backwards-compatible Windows 8.1 behavior), capture that value now
            if (IsCustomDpiOffset(dpiOffset) == true)
            {
                return Display.GetCustomDpiAsPercentage();
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
                return null;
            }
            switch (percentageIndex) {
                case 0:
                    return 1.00;
                case 1:
                    return 1.25;
                case 2:
                    return 1.50;
                case 3:
                    return 1.75;
                case 4:
                    return 2.00;
                case 5:
                    return 2.25;
                case 6:
                    return 2.50;
                case 7:
                    return 3.00;
                case 8:
                    return 3.50;
                case 9:
                    return 4.00;
                case 10:
                    return 4.50;
                case 11:
                    return 5.00;
                default:
                    // custom or otherwise unknown percentage
                    return null;
            }
        }
		
        // NOTE: Windows users can set a custom DPI percentage (perhaps a backwards-compatibility feature from Windows 8.1)
        public static double? GetCustomDpiAsPercentage()
        {
            // method 1: read the custom DPI level out of the registry (NOTE: this is machine-wide, not per-monitor)
            object logPixelsAsObject = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "LogPixels", null);
            if (logPixelsAsObject is int)
            {
                var logPixels = (int)logPixelsAsObject;
                return (double)logPixels / 96;
            } 
            else
            {
                return null;
            }

            // method 2: read the custom DPI from .NET
            //using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromHwnd(IntPtr.Zero))
            //{
            //    // NOTE: technically DpiX and DpiY are two separate scales, but in our testing and based on Windows usage models, they should always be the same
            //    return (double)graphics.DpiX / 96;
            //}
        }


        /// <summary>
        /// Sets the DPI scaling
        /// </summary>
        /// <param name="scalePercentage">Scaling percentage to set</param>
        public bool SetDpiScale(double scalePercentage, DisplayAdapterIdAndSourceId? displayAdapterIdInfo = null)
        {
            WindowsApi.LUID adapterId;
            uint sourceId;
            if (displayAdapterIdInfo == null)
            {
                // if no display adapter id info was provided, get an id to the primary monitor
                var primaryMonitorName = Display.GetMonitorName(null);
                if (primaryMonitorName == null)
                {
                    return false;
                }

                var primaryMonitorIds = Display.GetAdapterIdAndSourceId(primaryMonitorName);
                if (primaryMonitorIds == null)
                {
                    return false;
                }

                adapterId = primaryMonitorIds.Value.adapterId;
                sourceId = primaryMonitorIds.Value.sourceId;
            }
            else
            {
                adapterId = displayAdapterIdInfo.Value.adapterId;
                sourceId = displayAdapterIdInfo.Value.sourceId;
            }

            var currentDpiOffsetAndRange = Display.GetCurrentDpiOffsetAndRange(adapterId, sourceId);
            if (currentDpiOffsetAndRange == null)
            {
                return false;
            }

            var newDpiOffset = Display.TranslatePercentageToDpiOffset(scalePercentage, currentDpiOffsetAndRange.Value.minimumDpiOffset, currentDpiOffsetAndRange.Value.maximumDpiOffset);
            if (newDpiOffset == null)
            {
                return false;
            }

            // set the DPI offset (using the calculated offset)
            return Display.SetDpiOffset(newDpiOffset.Value, new DisplayAdapterIdAndSourceId(adapterId, sourceId));
        }

        // NOTE: this function returns false if it fails, true if it succeeds
        public static bool SetDpiOffset(int dpiOffset, DisplayAdapterIdAndSourceId displayAdapterIdInfo)
        {
            // retrieve the DPI values (min, current and max) for the monitor
            var setDpiInfo = new WindowsApi.DISPLAYCONFIG_SET_DPI();
            setDpiInfo.Init();
            setDpiInfo.header.type = WindowsApi.DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_SET_DPI;
            setDpiInfo.header.adapterId = displayAdapterIdInfo.adapterId;
            setDpiInfo.header.id = displayAdapterIdInfo.sourceId;
            setDpiInfo.dpiOffset = dpiOffset;
            //
            var displayConfigGetDeviceInfoSuccess = WindowsApi.DisplayConfigSetDeviceInfo(ref setDpiInfo);
            switch (displayConfigGetDeviceInfoSuccess)
            {
                case WindowsApi.ErrorCode.ERROR_SUCCESS:
                    break;
                case WindowsApi.ErrorCode.ERROR_INVALID_PARAMETER:
                    System.Diagnostics.Debug.Assert(false, "Error setting dpi info; this is probably a programming error.");
                    return false;
                default:
                    // unknown error
                    // failure; out of an abundance of caution, try to read the next display
                    System.Diagnostics.Debug.Assert(false, "Error setting dpi info");
                    return false;
            }

            // verify that the DPI was set successfully
            // NOTE: this is not technically necessary since we already have a success/failure result, but it's a good sanity check; if it's too early to check this then it's reasonable for us to skip this verification step
            var currentDpiOffsetAndRange = Display.GetCurrentDpiOffsetAndRange(displayAdapterIdInfo.adapterId, displayAdapterIdInfo.sourceId);
            if (currentDpiOffsetAndRange == null)
            {
                return false;
            }
            if (currentDpiOffsetAndRange.Value.currentDpiOffset != dpiOffset)
            {
                System.Diagnostics.Debug.Assert(false, "Could not set DPI offset (or the system has not updated the current SPI offset value)");
                return false;
            }

            // otherwise, we succeeded
            return true;
        }

        /// <summary>Gets the available resolutions for a display device.</summary>
        public IEnumerable<Size> GetResolutions(string? deviceName = null)
        {
            WindowsApi.DEVMODEW mode = new WindowsApi.DEVMODEW();
            mode.Init();

            uint modeNum = 0;

            while (WindowsApi.EnumDisplaySettingsEx(deviceName, modeNum++, ref mode, 0))
            {
                yield return new Size((int)mode.dmPelsWidth, (int)mode.dmPelsHeight);
            }
        }

        private static Size Resolution(Size? newSize, string? deviceName = null)
        {
            WindowsApi.DEVMODEW mode = new WindowsApi.DEVMODEW();
            mode.Init();
            // When setting, the current display still needs to be retrieved, to pre-fill the mode struct.
            WindowsApi.EnumDisplaySettingsEx(deviceName, WindowsApi.ENUM_CURRENT_SETTINGS, ref mode, 0);
            Size originalSize = new Size((int)mode.dmPelsWidth, (int)mode.dmPelsHeight);

            if (newSize.HasValue)
            {
                const uint CDS_UPDATEREGISTRY = 1;
                mode.dmPelsWidth = (uint)newSize.Value.Width;
                mode.dmPelsHeight = (uint)newSize.Value.Height;
                WindowsApi.ChangeDisplaySettingsEx(deviceName, ref mode, IntPtr.Zero, CDS_UPDATEREGISTRY, IntPtr.Zero);
            }

            return originalSize;
        }

        /// <summary>Gets the current resolution for a display device.</summary>
        /// <returns>The current resolution.</returns>
        public Size GetResolution(string? deviceName = null)
        {
            return Resolution(null, deviceName);
        }

        /// <summary>Gets the current resolution for a display device.</summary>
        /// <returns>The previous resolution.</returns>
        public Size SetResolution(Size resolution, string? deviceName = null)
        {
            return Resolution(resolution, deviceName);
        }
    }
}
