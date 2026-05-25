// Copyright 2020-2026 Raising the Floor - US, Inc.
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

using Morphic.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Morphic.WindowsNative.Display;

public class Display
{
    internal readonly Windows.Win32.Graphics.Gdi.HMONITOR MonitorHandle;
    internal readonly string DeviceName;
    internal readonly Windows.Win32.Foundation.LUID AdapterId;
    internal readonly uint SourceId;

    private Display(IntPtr monitorHandle, string deviceName, Windows.Win32.Foundation.LUID adapterId, uint sourceId)
    {
        this.MonitorHandle = (Windows.Win32.Graphics.Gdi.HMONITOR)monitorHandle;
        this.DeviceName = deviceName;
        this.AdapterId = adapterId;
        this.SourceId = sourceId;
    }

    public static MorphicResult<Display, MorphicUnit> GetDisplayByMonitorHandle(IntPtr monitorHandle)
    {
        // get the monitor's display name
        var getDisplayDeviceNameResult = Display.GetDisplayDeviceNameForMonitorHandle((Windows.Win32.Graphics.Gdi.HMONITOR)monitorHandle);
        if (getDisplayDeviceNameResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        var deviceName = getDisplayDeviceNameResult.Value!;

        // retrieve the buffer sizes needed to call QueryDisplayConfig (i.e. to get all of our displays' configs)
        uint numPathArrayElements;
        uint numModeInfoArrayElements;
        var getDisplayConfigBufferSizesResult = Windows.Win32.PInvoke.GetDisplayConfigBufferSizes(
            Windows.Win32.Devices.Display.QUERY_DISPLAY_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS | Windows.Win32.Devices.Display.QUERY_DISPLAY_CONFIG_FLAGS.QDC_VIRTUAL_MODE_AWARE, 
            out numPathArrayElements, 
            out numModeInfoArrayElements);
        switch (getDisplayConfigBufferSizesResult)
        {
            case Windows.Win32.Foundation.WIN32_ERROR.ERROR_SUCCESS:
                break;
            case Windows.Win32.Foundation.WIN32_ERROR.ERROR_INVALID_PARAMETER:
            case Windows.Win32.Foundation.WIN32_ERROR.ERROR_NOT_SUPPORTED: // no WDDM display driver available
            case Windows.Win32.Foundation.WIN32_ERROR.ERROR_ACCESS_DENIED:
            case Windows.Win32.Foundation.WIN32_ERROR.ERROR_GEN_FAILURE:
                // failure
                return MorphicResult.ErrorResult();
            default:
                // unknown error
                return MorphicResult.ErrorResult();
        }

        Span<Windows.Win32.Devices.Display.DISPLAYCONFIG_PATH_INFO> pathInfoElements = new Windows.Win32.Devices.Display.DISPLAYCONFIG_PATH_INFO[numPathArrayElements];
        Span<Windows.Win32.Devices.Display.DISPLAYCONFIG_MODE_INFO> modeInfoElements = new Windows.Win32.Devices.Display.DISPLAYCONFIG_MODE_INFO[numModeInfoArrayElements];

        var queryDisplayConfigResult = Windows.Win32.PInvoke.QueryDisplayConfig(
            Windows.Win32.Devices.Display.QUERY_DISPLAY_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS | Windows.Win32.Devices.Display.QUERY_DISPLAY_CONFIG_FLAGS.QDC_VIRTUAL_MODE_AWARE,
            ref numPathArrayElements, pathInfoElements, 
            ref numModeInfoArrayElements, modeInfoElements, 
            ref System.Runtime.CompilerServices.Unsafe.NullRef<Windows.Win32.Devices.Display.DISPLAYCONFIG_TOPOLOGY_ID>());
        switch (queryDisplayConfigResult)
        {
            case Windows.Win32.Foundation.WIN32_ERROR.ERROR_SUCCESS:
                break;
            case Windows.Win32.Foundation.WIN32_ERROR.ERROR_INVALID_PARAMETER:
            case Windows.Win32.Foundation.WIN32_ERROR.ERROR_NOT_SUPPORTED: // no WDDM display driver available
            case Windows.Win32.Foundation.WIN32_ERROR.ERROR_ACCESS_DENIED:
            case Windows.Win32.Foundation.WIN32_ERROR.ERROR_GEN_FAILURE:
            case Windows.Win32.Foundation.WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER:
                // failure
                return MorphicResult.ErrorResult();
            default:
                // unknown error
                return MorphicResult.ErrorResult();
        }
        //
        // since QueryDisplayConfig can return a smaller number of path/modeinfo elements than requested, resize the array
        pathInfoElements = pathInfoElements[..(int)numPathArrayElements];
        modeInfoElements = modeInfoElements [..(int)numModeInfoArrayElements];

        Display? result = null;

        // find the matching display (looping through all attached displays, in case there are two instances of the same display...i.e. a clone)
        var sourceDeviceName = new Windows.Win32.Devices.Display.DISPLAYCONFIG_SOURCE_DEVICE_NAME();
        foreach (var pathInfoElement in pathInfoElements)
        {
            // get the device name
            sourceDeviceName.header.adapterId = pathInfoElement.sourceInfo.adapterId;
            sourceDeviceName.header.id = pathInfoElement.sourceInfo.id;
            sourceDeviceName.header.type = Windows.Win32.Devices.Display.DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
            sourceDeviceName.header.size = (uint)Marshal.SizeOf<Windows.Win32.Devices.Display.DISPLAYCONFIG_SOURCE_DEVICE_NAME>();
            //
            var displayConfigGetDeviceInfoResult = Windows.Win32.PInvoke.DisplayConfigGetDeviceInfo(
                ref System.Runtime.CompilerServices.Unsafe.As<
                    Windows.Win32.Devices.Display.DISPLAYCONFIG_SOURCE_DEVICE_NAME,
                    Windows.Win32.Devices.Display.DISPLAYCONFIG_DEVICE_INFO_HEADER>(ref sourceDeviceName));
            switch ((Windows.Win32.Foundation.WIN32_ERROR)displayConfigGetDeviceInfoResult)
            {
                case Windows.Win32.Foundation.WIN32_ERROR.ERROR_SUCCESS:
                    break;
                case Windows.Win32.Foundation.WIN32_ERROR.ERROR_INVALID_PARAMETER:
                    System.Diagnostics.Debug.Assert(false, "Error getting device info; this is probably a programming error.");
                    return MorphicResult.ErrorResult();
                case Windows.Win32.Foundation.WIN32_ERROR.ERROR_NOT_SUPPORTED: // no WDDM display driver available
                case Windows.Win32.Foundation.WIN32_ERROR.ERROR_ACCESS_DENIED:
                case Windows.Win32.Foundation.WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER:
                case Windows.Win32.Foundation.WIN32_ERROR.ERROR_GEN_FAILURE:
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

            var viewGdiDeviceName = sourceDeviceName.viewGdiDeviceName.ToString(); // capture null-terminated string (or full buffer as string, if not null-terminated)

            if (viewGdiDeviceName == deviceName)
            {
                // in some circumstances, there could be more than one matching monitor (e.g. a clone).  We should prefer the first one, but 
                // even more than that we should prefer an internal/built-in display.  Find the best match now.

                bool isInternal;
                switch (pathInfoElement.targetInfo.outputTechnology)
                {
                    case Windows.Win32.Devices.Display.DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.DISPLAYCONFIG_OUTPUT_TECHNOLOGY_DISPLAYPORT_EMBEDDED:
                    case Windows.Win32.Devices.Display.DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.DISPLAYCONFIG_OUTPUT_TECHNOLOGY_UDI_EMBEDDED:
                    case Windows.Win32.Devices.Display.DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.DISPLAYCONFIG_OUTPUT_TECHNOLOGY_INTERNAL:
                        isInternal = true;
                        break;
                    default:
                        isInternal = false;
                        break;
                }

                // if this entry matches out monitorName and we either (a) don't have a result yet or (b) have a result but this one is _internal_ (the preference), then update our result
                if ((result is null) || (isInternal == true))
                {
                    result = new Display(monitorHandle, deviceName, sourceDeviceName.header.adapterId, sourceDeviceName.header.id);
                }
            }
        }

        // if we could not find a matching display, return an error result
        if (result is null)
        {
            return MorphicResult.ErrorResult();
        }

        return MorphicResult.OkResult(result);
    }

    //

    // NOTE: this function returns an Error if the window is not associated with any monitor
	//       which is very unlikely as it uses DEFAULTONNEAREST resolution.
    public static MorphicResult<Display, MorphicUnit> GetDisplayNearestWindowHandle(IntPtr windowHandle)
    {
        var monitorHandle = Display.GetMonitorHandleNearestWindowHandle((Windows.Win32.Foundation.HWND)windowHandle);
        if (monitorHandle.IsNull)
        {
            return MorphicResult.ErrorResult();
        }

        return Display.GetDisplayByMonitorHandle(monitorHandle);
    }

    // NOTE: this function returns a null pointer is the window handle couldn't be associated to a monitor
    private static Windows.Win32.Graphics.Gdi.HMONITOR GetMonitorHandleNearestWindowHandle(IntPtr windowHandle)
    {
        // get the handle of the monitor which is nearest to the specified window (via that window's handle); the "nearest" argument handles windows which are partially on two windows or are completely offscreen
        var monitorHandle = Windows.Win32.PInvoke.MonitorFromWindow((Windows.Win32.Foundation.HWND)windowHandle, Windows.Win32.Graphics.Gdi.MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        return monitorHandle;
    }

    //

    public static MorphicResult<Display, MorphicUnit> GetDisplayAtPoint(System.Drawing.Point point)
    {
        var monitorHandle = Display.GetMonitorHandleAtPoint(point);
        if (monitorHandle.IsNull)
        {
            return MorphicResult.ErrorResult();
        }

        return Display.GetDisplayByMonitorHandle(monitorHandle);
    }

    // NOTE: this function returns a null pointer is the point didn't map to a monitor
    private static Windows.Win32.Graphics.Gdi.HMONITOR GetMonitorHandleAtPoint(System.Drawing.Point point)
    {
        // get the handle of the monitor which contains the point; this is useful, for instance, for finding the monitor where the mouse cursor is currently positioned
        var monitorHandle = Windows.Win32.PInvoke.MonitorFromPoint(point, Windows.Win32.Graphics.Gdi.MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONULL);
        return monitorHandle;
    }

    //

    // NOTE: if the caller does not provide a windowHandle, we use the primary monitor instead
    private static MorphicResult<string, MorphicUnit> GetDisplayDeviceNameForMonitorHandle(Windows.Win32.Graphics.Gdi.HMONITOR monitorHandle)
    {
        Windows.Win32.Graphics.Gdi.MONITORINFOEXW monitorInfoEx = new();
        monitorInfoEx.monitorInfo.cbSize = (uint)Marshal.SizeOf<Windows.Win32.Graphics.Gdi.MONITORINFOEXW>();

        bool getMonitorInfoSuccess = Windows.Win32.PInvoke.GetMonitorInfo(monitorHandle, ref monitorInfoEx.monitorInfo);
        if (getMonitorInfoSuccess == false)
        {
            return MorphicResult.ErrorResult();
        }

        var deviceName = monitorInfoEx.szDevice.ToString(); // capture null-terminated string (or full buffer as string, if not null-terminated)
        return MorphicResult.OkResult(deviceName);
    }

    //

    /* get/set DPI */

    public struct GetDpiOffsetResult
    {
        public int MinimumDpiOffset;
        public int CurrentDpiOffset;
        public int MaximumDpiOffset;
    }
    //
    public MorphicResult<GetDpiOffsetResult, MorphicUnit> GetCurrentDpiOffsetAndRange()
    {
        // retrieve the DPI offset values (min, current and max) for the monitor
        var displayconfigGetDpi = new NativeHelpers.DISPLAYCONFIG_GET_DPI() { 
            header = new() { 
                type = NativeHelpers.DISPLAYCONFIG_DEVICE_INFO_GET_DPI, 
                adapterId = this.AdapterId, 
                id = this.SourceId, 
                size = (uint)Marshal.SizeOf<NativeHelpers.DISPLAYCONFIG_GET_DPI>() 
            } 
        };
        //
        var displayConfigGetDeviceInfoResult = Windows.Win32.PInvoke.DisplayConfigGetDeviceInfo(
            ref System.Runtime.CompilerServices.Unsafe.As<
                NativeHelpers.DISPLAYCONFIG_GET_DPI,
                Windows.Win32.Devices.Display.DISPLAYCONFIG_DEVICE_INFO_HEADER>(ref displayconfigGetDpi));
        switch ((Windows.Win32.Foundation.WIN32_ERROR)displayConfigGetDeviceInfoResult)
        {
            case Windows.Win32.Foundation.WIN32_ERROR.ERROR_SUCCESS:
                break;
            case Windows.Win32.Foundation.WIN32_ERROR.ERROR_INVALID_PARAMETER:
                System.Diagnostics.Debug.Assert(false, "Error getting dpi info; this is probably a programming error.");
                return MorphicResult.ErrorResult();
            default:
                // unknown error
                System.Diagnostics.Debug.Assert(false, "Error getting dpi info");
                return MorphicResult.ErrorResult();
        }

        var result = new GetDpiOffsetResult()
        {
            MinimumDpiOffset = displayconfigGetDpi.minimumDpiOffset,
            // NOTE: the current offset can be GREATER than the maximum offset (if the user has specified a custom zoom level, for instance)
            CurrentDpiOffset = displayconfigGetDpi.currentDpiOffset,
            MaximumDpiOffset = displayconfigGetDpi.maximumDpiOffset,
        };
        return MorphicResult.OkResult(result);
    }

    public async Task<MorphicResult<MorphicUnit, MorphicUnit>> SetDpiOffsetAsync(int dpiOffset)
    {
        var thisDisplay = this;

        return await Task.Run((Func<MorphicResult<MorphicUnit, MorphicUnit>>)(() =>
        {
            // set the DPI offset (current) for the monitor
            var displayconfigSetDpi = new NativeHelpers.DISPLAYCONFIG_SET_DPI()
            {
                header = new()
                {
                    type = NativeHelpers.DISPLAYCONFIG_DEVICE_INFO_SET_DPI,
                    adapterId = thisDisplay.AdapterId,
                    id = thisDisplay.SourceId,
                    size = (uint)Marshal.SizeOf<NativeHelpers.DISPLAYCONFIG_SET_DPI>()
                },
                dpiOffset = dpiOffset,
            };
            //
            var displayConfigGetDeviceInfoResult = Windows.Win32.PInvoke.DisplayConfigSetDeviceInfo(
            System.Runtime.CompilerServices.Unsafe.As<
                NativeHelpers.DISPLAYCONFIG_SET_DPI,
                Windows.Win32.Devices.Display.DISPLAYCONFIG_DEVICE_INFO_HEADER>(ref displayconfigSetDpi));
            switch ((Windows.Win32.Foundation.WIN32_ERROR)displayConfigGetDeviceInfoResult)
            {
                case Windows.Win32.Foundation.WIN32_ERROR.ERROR_SUCCESS:
                    break;
                case Windows.Win32.Foundation.WIN32_ERROR.ERROR_INVALID_PARAMETER:
                    System.Diagnostics.Debug.Assert(false, "Error setting dpi info; this is probably a programming error.");
                    return MorphicResult.ErrorResult();
                default:
                    // unknown error
                    System.Diagnostics.Debug.Assert(false, "Error setting dpi info");
                    return MorphicResult.ErrorResult();
            }

            // verify that the DPI offset was set successfully
            // NOTE: this is not technically necessary since we already have a success/failure result, but it's a good sanity check; if it's too early to check this then it's reasonable for us to skip this verification step
            var getCurrentDpiOffsetAndRangeResult = thisDisplay.GetCurrentDpiOffsetAndRange();
            if (getCurrentDpiOffsetAndRangeResult.IsError == true)
            {
                return MorphicResult.ErrorResult();
            }
            var currentDpiOffsetAndRange = getCurrentDpiOffsetAndRangeResult.Value;
            if (currentDpiOffsetAndRange.CurrentDpiOffset != dpiOffset)
            {
                System.Diagnostics.Debug.Assert(false, "Could not set DPI offset (or the system has not updated the current SPI offset value)");
                return MorphicResult.ErrorResult();
            }

            // otherwise, return success
            return MorphicResult.OkResult();
        }));
    }

    // True if the supplied dpiOffset represents a "Custom scaling" override (the Windows 8.1
    // legacy compatibility feature surfaced in Settings > System > Display > Advanced scaling
    // settings > Custom scaling). When custom scaling is in effect, Windows reports a sentinel
    // dpiOffset value (1234568 in all our testing) rather than a real offset within
    // [minimumDpiOffset, maximumDpiOffset], because the user has chosen an arbitrary percentage
    // outside the system's normal preset ladder.
    public static bool IsCustomScalingPercentage(int dpiOffset)
    {
        // 1234568 is the documented-by-observation sentinel used by Windows when custom scaling
        // is active. If a future Windows build switches sentinels, update this constant.
        return dpiOffset == 1234568;
    }

    //

    // System-wide display configuration change notification (WM_DISPLAYCHANGE broadcast).
    // Fires when Windows reconfigures any monitor: scale (DPI) change, resolution change, monitor
    // attach/detach, refresh rate change. Subscribers should re-query whatever they care about --
    // the event carries no payload because a single WM_DISPLAYCHANGE can mean any combination of
    // those reasons.
    //
    // Sits on top of HiddenMessageWindow (the process-owned top-level invisible window that
    // receives HWND_BROADCAST messages). HiddenMessageWindow.Initialize must run on a thread with
    // a Win32 message pump -- in WinAppSDK that's the UI thread; the singleton handles being
    // called repeatedly so the lazy-on-first-subscribe pattern below is safe.
    //
    // Threading: handlers fire on the UI thread (HiddenMessageWindow's WndProc thread). Each
    // handler runs on its own Task so a slow/throwing handler doesn't block the others or stall
    // the message pump.
    //
    // Lifecycle: the underlying HiddenMessageWindow.MessageReceived subscription is wired lazily
    // on the first DisplayChanged subscription and torn down when the last subscriber detaches.
    // HiddenMessageWindow itself stays alive for the process lifetime (Windows reclaims it on
    // process exit) -- that's its design.

    private static readonly object _displayChangedLock = new();
    private static EventHandler? _displayChanged;
    private static bool _hiddenMessageWindowSubscribed = false;

    public static event EventHandler DisplayChanged
    {
        add
        {
            lock (_displayChangedLock)
            {
                if (_hiddenMessageWindowSubscribed == false)
                {
                    var initializeResult = Morphic.WindowsNative.Windowing.HiddenMessageWindow.Initialize();
                    if (initializeResult.IsError)
                    {
                        Debug.Assert(false, "HiddenMessageWindow.Initialize() failed; Display.DisplayChanged will not fire");
                    }
                    else
                    {
                        Morphic.WindowsNative.Windowing.HiddenMessageWindow.MessageReceived += Display.OnHiddenMessageReceived;
                        _hiddenMessageWindowSubscribed = true;
                    }
                }
                _displayChanged += value;
            }
        }
        remove
        {
            lock (_displayChangedLock)
            {
                _displayChanged -= value;

                if (_displayChanged is null || _displayChanged!.GetInvocationList().Length == 0)
                {
                    _displayChanged = null;

                    if (_hiddenMessageWindowSubscribed == true)
                    {
                        Morphic.WindowsNative.Windowing.HiddenMessageWindow.MessageReceived -= Display.OnHiddenMessageReceived;
                        _hiddenMessageWindowSubscribed = false;
                    }
                }
            }
        }
    }

    private static void OnHiddenMessageReceived(object? sender, Morphic.WindowsNative.Windowing.WindowMessageEventArgs e)
    {
        if (e.Msg != Windows.Win32.PInvoke.WM_DISPLAYCHANGE)
        {
            return;
        }

        EventHandler? handlersToFire;
        lock (_displayChangedLock)
        {
            handlersToFire = _displayChanged;
        }
        if (handlersToFire is null)
        {
            return;
        }

        // Dispatch each handler on its own Task so a slow/throwing handler doesn't block the
        // others or stall the message pump. Sender is null -- DisplayChanged is a static event
        // with no instance.
        foreach (EventHandler handler in handlersToFire.GetInvocationList())
        {
            _ = Task.Run(() => handler.Invoke(null, EventArgs.Empty));
        }
    }

    //

    // for PerMonitorV2 DPI-aware clients, this function will return the display rectangle in PHYSICAL pixels
    // for non-DPI-aware clients, this function will return the display rectangle in VIRTUAL pixels
    public MorphicResult<System.Drawing.Rectangle, IWin32ApiError> GetDisplayRectangleInPixels()
    {
        Windows.Win32.Graphics.Gdi.MONITORINFO monitorInfo = new();
        monitorInfo.cbSize = (uint)Marshal.SizeOf<Windows.Win32.Graphics.Gdi.MONITORINFO>();
        //
        bool getMonitorInfoSuccess = Windows.Win32.PInvoke.GetMonitorInfo(this.MonitorHandle, ref monitorInfo);
        if (getMonitorInfoSuccess == false)
        {
            var win32ErrorCode = (Windows.Win32.Foundation.WIN32_ERROR)System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            // GetMonitorInfoW does not document SetLastError; if no code was set, fall back to a generic failure code.
            if (win32ErrorCode == Windows.Win32.Foundation.WIN32_ERROR.ERROR_SUCCESS)
            {
                win32ErrorCode = Windows.Win32.Foundation.WIN32_ERROR.ERROR_INVALID_DATA;
            }
            return MorphicResult.ErrorResult<IWin32ApiError>(new IWin32ApiError.Win32Error((uint)win32ErrorCode));
        }

        var displayRect = new System.Drawing.Rectangle(monitorInfo.rcMonitor.left,
            monitorInfo.rcMonitor.top,
            monitorInfo.rcMonitor.right - monitorInfo.rcMonitor.left,
            monitorInfo.rcMonitor.bottom - monitorInfo.rcMonitor.top);
        return MorphicResult.OkResult(displayRect);
    }
}
