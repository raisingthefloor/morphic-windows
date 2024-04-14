// Copyright 2020-2024 Raising the Floor - US, Inc.
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
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Morphic.WindowsNative.Display;

public class Display
{
    private readonly Windows.Win32.Graphics.Gdi.HMONITOR _monitorHandle;
    public readonly string DeviceName;
    private readonly Windows.Win32.Foundation.LUID _adapterId;
    public readonly uint SourceId;

    private Display(Windows.Win32.Graphics.Gdi.HMONITOR monitorHandle, string deviceName, Windows.Win32.Foundation.LUID adapterId, uint sourceId)
    {
        _monitorHandle = monitorHandle;
        this.DeviceName = deviceName;
        _adapterId = adapterId;
        this.SourceId = sourceId;
    }

    //

    private struct GetDisplaysConfigInfoResult
    {
        public Windows.Win32.Devices.Display.DISPLAYCONFIG_MODE_INFO[] ModeInfoElements;
        public Windows.Win32.Devices.Display.DISPLAYCONFIG_PATH_INFO[] PathInfoElements;
        public Windows.Win32.Devices.Display.DISPLAYCONFIG_TOPOLOGY_ID? TopologyId;
    }
    //
    private static MorphicResult<GetDisplaysConfigInfoResult, MorphicUnit> GetConfigInfoForDisplays(Windows.Win32.Devices.Display.QUERY_DISPLAY_CONFIG_FLAGS flags)
    {
        // NOTE: per Microsoft's instructions, we call QueryDisplayConfig (at least) twice in the circumstance that the buffer size requirements changed between the GetDisplayConfigBuffer and QueryDisplayConfig function calls (resulting in QueryDisplayConfig returning ERROR_INSUFFICIENT_BUFFER)
        //       see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-querydisplayconfig#remarks
        const int QUERY_DISPLAY_CONFIG_MAX_ATTEMPTS = 3;

        bool includeTopologyIdInResult = false;
        Windows.Win32.Devices.Display.DISPLAYCONFIG_TOPOLOGY_ID topologyId = 0;
        //
        uint numPathArrayElements = 0; // number of elements in the path information table
        uint numModeInfoArrayElements = 0; // number of elements in the mode information table
        Windows.Win32.Devices.Display.DISPLAYCONFIG_MODE_INFO[] modeInfoElements = [];
        Windows.Win32.Devices.Display.DISPLAYCONFIG_PATH_INFO[] pathInfoElements = [];
        //
        for (var queryDisplayConfigAttempt = 1; queryDisplayConfigAttempt <= QUERY_DISPLAY_CONFIG_MAX_ATTEMPTS; queryDisplayConfigAttempt += 1)
        {
            // retrieve the size of buffers needed to call QueryDisplayConfig (so that we can get our displays' configurations)
            // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getdisplayconfigbuffersizes
            var getDisplayConfigBufferSizesResult = Windows.Win32.PInvoke.GetDisplayConfigBufferSizes(Windows.Win32.Devices.Display.QUERY_DISPLAY_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS, out numPathArrayElements, out numModeInfoArrayElements);
            switch (getDisplayConfigBufferSizesResult)
            {
                case Windows.Win32.Foundation.WIN32_ERROR.ERROR_SUCCESS:
                    // success
                    break;
                case Windows.Win32.Foundation.WIN32_ERROR.ERROR_INVALID_PARAMETER:
                case Windows.Win32.Foundation.WIN32_ERROR.ERROR_NOT_SUPPORTED:
                case Windows.Win32.Foundation.WIN32_ERROR.ERROR_ACCESS_DENIED:
                case Windows.Win32.Foundation.WIN32_ERROR.ERROR_GEN_FAILURE:
                    // failure
                    return MorphicResult.ErrorResult();
                default:
                    // undocumented error
                    Debug.Assert(false, "GetDisplayConfigBufferSizes returned an undocumented (error) result");
                    return MorphicResult.ErrorResult();
            }

            // NOTE: in theory, the number of elements returned by the above call could be zero, even if we got ERROR_SUCCESS as the result of the function call.  [We actually saw this behavior exhibit itself once, in a virtual machine, when calling the function via C# native P/Invoke.]
            pathInfoElements = new Windows.Win32.Devices.Display.DISPLAYCONFIG_PATH_INFO[numPathArrayElements];
            modeInfoElements = new Windows.Win32.Devices.Display.DISPLAYCONFIG_MODE_INFO[numModeInfoArrayElements];

            // WARNING: numPathArrayElements and numModeInfoArrayElements are accurate at the time that GetDisplayConfigBufferSizes was called, but technically the system could change between then and the QueryDisplayConfig function call.
            //          see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getdisplayconfigbuffersizes#remarks

            Windows.Win32.Foundation.WIN32_ERROR queryDisplayConfigResult;
            unsafe
            {
                fixed (Windows.Win32.Devices.Display.DISPLAYCONFIG_PATH_INFO* pointerToPathInfoElements = pathInfoElements)
                {
                    fixed (Windows.Win32.Devices.Display.DISPLAYCONFIG_MODE_INFO* pointerToModeInfoElements = modeInfoElements)
                    {
                        if ((flags & Windows.Win32.Devices.Display.QUERY_DISPLAY_CONFIG_FLAGS.QDC_DATABASE_CURRENT) == Windows.Win32.Devices.Display.QUERY_DISPLAY_CONFIG_FLAGS.QDC_DATABASE_CURRENT)
                        {
                            queryDisplayConfigResult = Windows.Win32.PInvoke.QueryDisplayConfig(Windows.Win32.Devices.Display.QUERY_DISPLAY_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS, ref numPathArrayElements, pointerToPathInfoElements, ref numModeInfoArrayElements, pointerToModeInfoElements, &topologyId);
                            includeTopologyIdInResult = true; // for QDC_DATABASE_CURRENT queries, return the topology id
                        }
                        else
                        {
                            queryDisplayConfigResult = Windows.Win32.PInvoke.QueryDisplayConfig(Windows.Win32.Devices.Display.QUERY_DISPLAY_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS, ref numPathArrayElements, pointerToPathInfoElements, ref numModeInfoArrayElements, pointerToModeInfoElements, null);
                            includeTopologyIdInResult = false; // for non-QDC_DATABASE_CURRENT queries, the topology id is null (not returned)
                        }
                    }
                }
            }
            bool queryDisplayConfigWasSuccess;
            switch (queryDisplayConfigResult)
            {
                case Windows.Win32.Foundation.WIN32_ERROR.ERROR_SUCCESS:
                    // success
                    queryDisplayConfigWasSuccess = true;
                    break;
                case Windows.Win32.Foundation.WIN32_ERROR.ERROR_INVALID_PARAMETER:
                case Windows.Win32.Foundation.WIN32_ERROR.ERROR_NOT_SUPPORTED:
                case Windows.Win32.Foundation.WIN32_ERROR.ERROR_ACCESS_DENIED:
                case Windows.Win32.Foundation.WIN32_ERROR.ERROR_GEN_FAILURE:
                    // failure
                    return MorphicResult.ErrorResult();
                case Windows.Win32.Foundation.WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER:
                    if (queryDisplayConfigAttempt < QUERY_DISPLAY_CONFIG_MAX_ATTEMPTS)
                    {
                        // try again (see Microsoft's remarks for GetDisplayConfigBufferSizes, linked above)
                        queryDisplayConfigWasSuccess = false;
                        break;
                    }
                    else
                    {
                        // failure
                        Debug.Assert(false, "QueryDisplayConfig returned ERROR_INSUFFICIENT_BUFFER three times; is this because GetDisplayConfigBufferSizes returned a buffer size which was incorrect, or is this something that could happen because the configuration changes between function calls?");
                        return MorphicResult.ErrorResult();
                    }
                default:
                    // undocumented error
                    Debug.Assert(false, "GetDisplayConfigBufferSizes returned an undocumented (error) result");
                    return MorphicResult.ErrorResult();
            }
            //
            // if our function call was a success, break out of this loop
            if (queryDisplayConfigWasSuccess == true)
            {
                break;
            }
        }
        //
        // NOTE: numModeInfoArrayElements and numPathArrayElements are updated by the call we just made to QueryDisplayConfig--if the actual number of elements returned was different (i.e. if a display was disconnected, etc.)
        if (modeInfoElements.Length > (int)numModeInfoArrayElements)
        {
            Array.Resize(ref modeInfoElements, (int)numModeInfoArrayElements);
        }
        if (pathInfoElements.Length > (int)numPathArrayElements)
        {
            Array.Resize(ref pathInfoElements, (int)numPathArrayElements);
        }

        var result = new GetDisplaysConfigInfoResult()
        {
            ModeInfoElements = modeInfoElements,
            PathInfoElements = pathInfoElements,
            TopologyId = includeTopologyIdInResult ? topologyId : null,
        };
        return MorphicResult.OkResult(result);
    }

    //public static MorphicResult<Display, MorphicUnit> GetDisplayByMonitorHandle(IntPtr monitorHandle)
    //{
    //    var handleAsHMonitor = new Windows.Win32.Graphics.Gdi.HMONITOR(monitorHandle);
    //    return Display.GetDisplayByMonitorHandle(handleAsHMonitor);
    //}
    //
    private static MorphicResult<Display, MorphicUnit> GetDisplayByMonitorHandle(Windows.Win32.Graphics.Gdi.HMONITOR monitorHandle)
    {
        // get the mode and path info for all attached displays
        //
        // NOTE: we are getting configuration for QDC_ONLY_ACTIVE_PATHS instead of QDC_DATABASE_CURRENT.  There may be a nuanced difference between them.  QDC_ONLY_ACTIVE_PATHS sounds like the right option, but if this code gives us troubles (especially in VMs) then look at using QDC_DATABASE_CURRENT instead.
        //       see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-querydisplayconfig
        // NOTE: we are also passing along the QDC_VIRTUAL_MODE_AWARE flag, as used in Microsoft's examples.  There is not much documentation on this flag, but the assumption here is that it will help ensure that we work with a wider variety of displays which make up the "continuous desktop"
        //       see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-querydisplayconfig#examples
        //
        var getDisplaysConfigInfoResult = Display.GetConfigInfoForDisplays(Windows.Win32.Devices.Display.QUERY_DISPLAY_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS | Windows.Win32.Devices.Display.QUERY_DISPLAY_CONFIG_FLAGS.QDC_VIRTUAL_MODE_AWARE /* Windows 10+ */);
        if (getDisplaysConfigInfoResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        //var modeInfoElements = getDisplaysConfigInfoResult.Value!.ModeInfoElements; // unused
        var pathInfoElements = getDisplaysConfigInfoResult.Value!.PathInfoElements;
        //var topologyId = getDisplaysConfigInfoResult.Value!.TopologyId; // unused and null (although if we switch to QDC_DATABASE_CURRENT, this would be a non-null part of the above function call's result)

        // get the specified monitor's display name
        var getDisplayDeviceNameResult = Display.GetDisplayDeviceNameForMonitorHandle(monitorHandle);
        if (getDisplayDeviceNameResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        var displayDeviceName = getDisplayDeviceNameResult.Value!;

        Display? result = null;

        // find the matching display
        var requestPacket = new Windows.Win32.Devices.Display.DISPLAYCONFIG_SOURCE_DEVICE_NAME()
        {
            header = new Windows.Win32.Devices.Display.DISPLAYCONFIG_DEVICE_INFO_HEADER()
            {
                size = (uint)Marshal.SizeOf<Windows.Win32.Devices.Display.DISPLAYCONFIG_SOURCE_DEVICE_NAME>()
            }
        };
        foreach (var pathInfoElement in pathInfoElements)
        {
            // get the device name
            requestPacket.header.adapterId = pathInfoElement.sourceInfo.adapterId;
            requestPacket.header.id = pathInfoElement.sourceInfo.id;
            requestPacket.header.type = Windows.Win32.Devices.Display.DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
            //
            // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-displayconfiggetdeviceinfo
            int displayConfigGetDeviceInfoResult;
            unsafe
            {
                displayConfigGetDeviceInfoResult = Windows.Win32.PInvoke.DisplayConfigGetDeviceInfo((Windows.Win32.Devices.Display.DISPLAYCONFIG_DEVICE_INFO_HEADER*)(void*)&requestPacket);
            }
            switch ((Windows.Win32.Foundation.WIN32_ERROR)displayConfigGetDeviceInfoResult)
            {
                case Windows.Win32.Foundation.WIN32_ERROR.ERROR_SUCCESS:
                    // success
                    break;
                case Windows.Win32.Foundation.WIN32_ERROR.ERROR_INVALID_PARAMETER:
                    System.Diagnostics.Debug.Assert(false, "Error getting device info; this is probably a programming error.");
                    return MorphicResult.ErrorResult();
                case Windows.Win32.Foundation.WIN32_ERROR.ERROR_NOT_SUPPORTED:
                case Windows.Win32.Foundation.WIN32_ERROR.ERROR_ACCESS_DENIED:
                case Windows.Win32.Foundation.WIN32_ERROR.ERROR_GEN_FAILURE:
                    // failure; out of an abundance of caution, try to read the next display (so that we don't fail due to a single "bad" display entry)
                    Debug.Assert(false, "Error getting device info; this may not be an error.");
                    continue;
                    //return IMorphicResult<DisplayAdapterIdAndSourceId>.ErrorResult();
                case Windows.Win32.Foundation.WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER:
                    Debug.Assert(false, "Error getting device info due to insufficient buffer size; this probably indicates a failure.");
                    return MorphicResult.ErrorResult();
                default:
                    // unknown error
                    Debug.Assert(false, "Undocumented/unknown error getting device info; this probably indicates a programming error.");
                    return MorphicResult.ErrorResult();
            }

            var viewGdiDeviceNameAsCharSpan = requestPacket.viewGdiDeviceName.AsReadOnlySpan();
            var lengthOfViewGdiDeviceName = viewGdiDeviceNameAsCharSpan.IndexOf('\0');
            if (lengthOfViewGdiDeviceName < 0)
            {
                lengthOfViewGdiDeviceName = viewGdiDeviceNameAsCharSpan.Length;
            }
            var viewGdiDeviceName = new string(viewGdiDeviceNameAsCharSpan.ToArray(), 0, lengthOfViewGdiDeviceName);

            if (viewGdiDeviceName == displayDeviceName)
            {
                // in some circumstances, there could be more than one matching monitor (e.g. a clone).  We should prefer the first one, but 
                // even moreso we should prefer an internal/built-in display.  Find the best match now.

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

                // if this entry matches out monitorName and we either (a) don't have a result yet or (b) have a result but this one is _internal_, then update our result
                if ((result is null) || (isInternal == true))
                {
                    result = new Display(monitorHandle, displayDeviceName, requestPacket.header.adapterId, requestPacket.header.id);
                }
            }
        }

        // if we could not find a matching display, return an error result
        if (result is null)
        {
            Debug.Assert(false, "Could not find a matching display.");
            return MorphicResult.ErrorResult();
        }

        return MorphicResult.OkResult(result!);
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

    public static MorphicResult<Display, MorphicUnit> GetDisplayForPoint(System.Drawing.Point point)
    {
        var monitorHandle = Display.GetMonitorHandleForPoint(point);

        return Display.GetDisplayByMonitorHandle(monitorHandle);
    }

    public static MorphicResult<Display, MorphicUnit> GetDisplayForWindow(IntPtr windowHandle)
    {
        var handleAsWindowHandle = new Windows.Win32.Foundation.HWND(windowHandle);
        return Display.GetDisplayForWindow(handleAsWindowHandle);
    }
    //
    private static MorphicResult<Display, MorphicUnit> GetDisplayForWindow(Windows.Win32.Foundation.HWND windowHandle)
    {
        var monitorHandle = Display.GetMonitorHandleForWindow(windowHandle);

        return Display.GetDisplayByMonitorHandle(monitorHandle);
    }

    public static MorphicResult<Display, MorphicUnit> GetPrimaryDisplay()
    {
        var monitorHandle = Display.GetMonitorHandleForPrimaryMonitor();

        return Display.GetDisplayByMonitorHandle(monitorHandle);
    }

    //

    private static MorphicResult<List<Windows.Win32.Graphics.Gdi.HMONITOR>, MorphicUnit> GetAllMonitorHandles()
    {
        var monitorHandles = new List<Windows.Win32.Graphics.Gdi.HMONITOR>();

        Windows.Win32.Foundation.BOOL enumDisplayMonitorsResult;
        unsafe
        {
            enumDisplayMonitorsResult = Windows.Win32.PInvoke.EnumDisplayMonitors(
                (Windows.Win32.Graphics.Gdi.HDC)0,
                (Windows.Win32.Foundation.RECT?)null,
                (Windows.Win32.Graphics.Gdi.HMONITOR hMonitor, Windows.Win32.Graphics.Gdi.HDC hdcMonitor, Windows.Win32.Foundation.RECT* lpRect, Windows.Win32.Foundation.LPARAM lParam) =>
                {
                    monitorHandles.Add(hMonitor);

                    // return true to continue the enumeration
                    return true;
                },
                IntPtr.Zero
            );
        }
        if (enumDisplayMonitorsResult == 0)
        {
            return MorphicResult.ErrorResult();
        }

        return MorphicResult.OkResult(monitorHandles);
    }

    private static Windows.Win32.Graphics.Gdi.HMONITOR GetMonitorHandleForPoint(System.Drawing.Point point)
    {
        // get the handle of the monitor which contains the point; this is useful, for instance, for finding the monitor which currently contains the mouse pointer

        // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-monitorfrompoint
        var monitorHandle = Windows.Win32.PInvoke.MonitorFromPoint(point, Windows.Win32.Graphics.Gdi.MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);

        return monitorHandle;
    }

    private static Windows.Win32.Graphics.Gdi.HMONITOR GetMonitorHandleForPrimaryMonitor()
    {
        // implementation option 1 (preferred)
        var point = new System.Drawing.Point(0, 0); // NOTE: this should always be the top-left corner of primary display
        // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-monitorfrompoint
        var monitorHandle = Windows.Win32.PInvoke.MonitorFromPoint(point, Windows.Win32.Graphics.Gdi.MONITOR_FROM_FLAGS.MONITOR_DEFAULTTOPRIMARY);

        // implementation option 2
        // NOTE: legacy Morphic used this method, but it is unclear whether GetDesktopWindow only provides the primary display's coordinates or if it effectively covers the entire desktop's virtual canvas
        //
        // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getdesktopwindow
        //var desktopWindowHandle = Windows.Win32.PInvoke.GetDesktopWindow();
        // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-monitorfromwindow
        //var monitorHandle = Windows.Win32.PInvoke.MonitorFromWindow(desktopWindowHandle, Windows.Win32.Graphics.Gdi.MONITOR_FROM_FLAGS.MONITOR_DEFAULTTOPRIMARY);

        // implementation option 3 (NOTE: no implementation written, but notes included herein)
        // NOTE: calling EnumDisplayDevices and checking for the DISPLAY_DEVICE_PRIMARY_DEVICE flag should also give us the primary display device.
        //       see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-changedisplaysettingsexa#remarks

        return monitorHandle;
    }

    private static Windows.Win32.Graphics.Gdi.HMONITOR GetMonitorHandleForWindow(Windows.Win32.Foundation.HWND windowHandle)
    {
        // get the handle of the monitor which contains the majority of the specified windowHandle's window
        var monitorHandle = Windows.Win32.PInvoke.MonitorFromWindow(windowHandle, Windows.Win32.Graphics.Gdi.MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        return monitorHandle;
    }

    //

    // NOTE: if the caller provides a null (zero) monitorHandle, we use the primary monitor instead
    private static MorphicResult<string, MorphicUnit> GetDisplayDeviceNameForMonitorHandle(Windows.Win32.Graphics.Gdi.HMONITOR monitorHandle)
    {
        // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-monitorinfoexw
        var monitorInfo = new PInvokeExtensions.MONITORINFOEX()
        {
            szDevice = new char[(int)Windows.Win32.PInvoke.CCHDEVICENAME],
            //
            cbSize = (uint)Marshal.SizeOf<PInvokeExtensions.MONITORINFOEX>(),
        };
        //
        // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getmonitorinfow
        Windows.Win32.Foundation.BOOL getMonitorInfoSuccess;
        //
        // NOTE: Windows.Win32.PInvoke.GetMonitorInfo does not have an overload to pass in a MONITORINFOEX struct as the second parameter, so the following attempts with CsWin32 did not work
        // API call implementation 1 (using CsWin32, preferred)
        //getMonitorInfoSuccess = Windows.Win32.PInvoke.GetMonitorInfo(monitorHandle, ref monitorInfo);
        //unsafe
        //{
        //    // attempt 2 (NOTE: did not work; otherwise untested, only included here for reference)
        //    getMonitorInfoSuccess = Windows.Win32.PInvoke.GetMonitorInfo(monitorHandle, (Windows.Win32.Graphics.Gdi.MONITORINFO*)(void*)&monitorInfo);
        //}
        //
        // API call implementation 2 (using C# PInvoke, fallback position)
        getMonitorInfoSuccess = PInvokeExtensions.GetMonitorInfo(monitorHandle, ref monitorInfo);
        if (getMonitorInfoSuccess == 0)
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
    //
    public MorphicResult<GetDpiOffsetResult, MorphicUnit> GetCurrentDpiOffsetAndRange()
    {
        // retrieve the DPI values (min, current and max) for the monitor
        // NOTE: this structure is undocumented and was reverse engineered as part of the Morphic Classic project
        var requestPacket = new PInvokeExtensions.DISPLAYCONFIG_GET_DPI()
        {
            header = new Windows.Win32.Devices.Display.DISPLAYCONFIG_DEVICE_INFO_HEADER()
            {
                size = (uint)Marshal.SizeOf<PInvokeExtensions.DISPLAYCONFIG_GET_DPI>()
            }
        };
        requestPacket.header.adapterId = _adapterId;
        requestPacket.header.id = this.SourceId;
        requestPacket.header.type = PInvokeExtensions.DISPLAYCONFIG_DEVICE_INFO_GET_DPI;
        //
        // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-displayconfiggetdeviceinfo
        int displayConfigGetDeviceInfoResult;
        unsafe
        {
            displayConfigGetDeviceInfoResult = Windows.Win32.PInvoke.DisplayConfigGetDeviceInfo((Windows.Win32.Devices.Display.DISPLAYCONFIG_DEVICE_INFO_HEADER*)(void*)&requestPacket);
        }
        switch ((Windows.Win32.Foundation.WIN32_ERROR)displayConfigGetDeviceInfoResult)
        {
            case Windows.Win32.Foundation.WIN32_ERROR.ERROR_SUCCESS:
                // success
                break;
            case Windows.Win32.Foundation.WIN32_ERROR.ERROR_INVALID_PARAMETER:
                System.Diagnostics.Debug.Assert(false, "Error getting 'DPI' info; this is probably a programming error.");
                return MorphicResult.ErrorResult();
            case Windows.Win32.Foundation.WIN32_ERROR.ERROR_NOT_SUPPORTED:
            case Windows.Win32.Foundation.WIN32_ERROR.ERROR_ACCESS_DENIED:
            case Windows.Win32.Foundation.WIN32_ERROR.ERROR_GEN_FAILURE:
                Debug.Assert(false, "Error getting 'DPI' info; this may not be an error.");
                return MorphicResult.ErrorResult();
            case Windows.Win32.Foundation.WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER:
                Debug.Assert(false, "Error getting 'DPI' info due to insufficient buffer size; this probably indicates a programming error.");
                return MorphicResult.ErrorResult();
            default:
                // unknown error
                Debug.Assert(false, "Undocumented/unknown error getting 'DPI' info; this probably indicates a programming error.");
                return MorphicResult.ErrorResult();
        }

        var result = new GetDpiOffsetResult();
        result.MinimumDpiOffset = requestPacket.minimumDpiOffset;
        result.MaximumDpiOffset = requestPacket.maximumDpiOffset;
        // NOTE: the current offset can be GREATER than the maximum offset (if the user has specified a custom zoom level, for instance)
        result.CurrentDpiOffset = requestPacket.currentDpiOffset;

        return MorphicResult.OkResult(result);
    }

    //

    // NOTE: this function will always return the resolution in physical pixels, regardless of whether the app is DPI aware or not
    public MorphicResult<System.Drawing.Size, MorphicUnit> GetDisplayResolutionInPhysicalPixels()
    {
        // see: https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-devmodew
        const int privateDriverDataLength = 0;
        Windows.Win32.Graphics.Gdi.DEVMODEW deviceMode = new()
        {
            dmSize = (ushort)Marshal.SizeOf(typeof(Windows.Win32.Graphics.Gdi.DEVMODEW)),
            //
            // privateDriverData = new byte[privateDriverDataLength],
            dmDriverExtra = privateDriverDataLength,
        };
        // get the display's resolution
        // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumdisplaysettingsw
        var enumDisplaySettingsResult = Windows.Win32.PInvoke.EnumDisplaySettings(this.DeviceName, Windows.Win32.Graphics.Gdi.ENUM_DISPLAY_SETTINGS_MODE.ENUM_CURRENT_SETTINGS, ref deviceMode);
        if (enumDisplaySettingsResult == 0)
        {
            return MorphicResult.ErrorResult();
        }
        //
        var screenWidth = deviceMode.dmPelsWidth;
        var screenHeight = deviceMode.dmPelsHeight;

        return MorphicResult.OkResult(new System.Drawing.Size((int)screenWidth, (int)screenHeight));
    }

    public MorphicResult<System.Drawing.Size, MorphicUnit> GetDisplayResolutionInVirtualPixels()
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

        var result = new System.Drawing.Size((int)(displayResolutionInPhysicalPixels.Width / scalePercentage), (int)(displayResolutionInPhysicalPixels.Height / scalePercentage));
        return MorphicResult.OkResult(result);
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

        var scalePercentageResult = Display.TranslateDpiOffsetToScalePercentage(currentDpiOffsetAndRange.CurrentDpiOffset, currentDpiOffsetAndRange.MinimumDpiOffset/*, currentDpiOffsetAndRange.MaximumDpiOffset*/);
        if (scalePercentageResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        var scalePercentage = scalePercentageResult.Value!;

        return MorphicResult.OkResult(scalePercentage);
    }

    //

    // NOTE: this function is based on analysis and reverse engineering; we did not find any documentation regarding display scale % on Windows
    // NOTE: if dpiOffset is out of the min<->max range or the range is too broad, this function will return null
    public static MorphicResult<double, MorphicUnit> TranslateDpiOffsetToScalePercentage(int dpiOffset, int minimumDpiOffset/*, int maximumDpiOffset*/)
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

    // NOTE: Windows users can set a custom DPI percentage (presumably a backwards-compatibility feature from Windows 8.1)
    public static MorphicResult<double, MorphicUnit> GetCustomDpiOffsetAsPercentage()
    {
        // method 1: read the custom DPI level out of the registry (NOTE: this is machine-wide, not per-monitor)
        var openSubKeyResult = Morphic.WindowsNative.Registry.CurrentUser.OpenSubKey(@"HKEY_CURRENT_USER\Control Panel\Desktop", false);
        if (openSubKeyResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        var controlPanelDesktopRegistryKey = openSubKeyResult.Value!;
        var getValueDataResult = controlPanelDesktopRegistryKey.GetValueData<int>("LogPixels");
        if (getValueDataResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        int logPixelsValueData = getValueDataResult.Value!;
        var logPixelsAsPercentage = (double)logPixelsValueData / 96;
        return MorphicResult.OkResult(logPixelsAsPercentage);

        // method 2: read the custom DPI from .NET
        //using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromHwnd(IntPtr.Zero))
        //{
        //    // NOTE: technically DpiX and DpiY are two separate scales, but in our testing and based on Windows usage models, they should always be the same
        //    var logPixelsAsPercentage = (double)graphics.DpiX / 96;
        //    return MorphicResult.OkResult(logPixelsAsPercentage);
        //}
    }


}
