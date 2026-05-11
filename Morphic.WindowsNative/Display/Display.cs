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
using System.Runtime.InteropServices;
using System.Text;

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


    //

    // NOTE: this function returns a null pointer is the point didn't map to a monitor
    private static Windows.Win32.Graphics.Gdi.HMONITOR GetMonitorHandleForPoint(System.Drawing.Point point)
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

        var deviceName = monitorInfoEx.szDevice.ToString();
        return MorphicResult.OkResult(deviceName);
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
