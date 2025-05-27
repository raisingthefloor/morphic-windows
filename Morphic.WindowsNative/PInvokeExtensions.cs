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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Morphic.WindowsNative;

internal class PInvokeExtensions
{
    #region SrRestorePtApi

    internal const int MAX_DESC_W = 256;

    #endregion SrRestorePtApi

    #region wingdi

    public static readonly Windows.Win32.Devices.Display.DISPLAYCONFIG_DEVICE_INFO_TYPE DISPLAYCONFIG_DEVICE_INFO_GET_DPI = unchecked((Windows.Win32.Devices.Display.DISPLAYCONFIG_DEVICE_INFO_TYPE)(-3));

    // Reverse-engineered DPI scaling code, utilizing the CCD APIs
    // https://docs.microsoft.com/en-us/windows-hardware/drivers/display/ccd-apis
    //
    // NOTE: this structure is undocumented and was reverse engineered as part of the Morphic Classic project
    // NOTE: all offsets are indices (relative to the OS's recommended DPI scaling value; the recommended DPI scaling value is always zero)
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DISPLAYCONFIG_GET_DPI
    {
        public Windows.Win32.Devices.Display.DISPLAYCONFIG_DEVICE_INFO_HEADER header;

        public int minimumDpiOffset;
        public int currentDpiOffset;
        public int maximumDpiOffset;
    }

    #endregion wingdi

    #region winuser

    // NOTE: MONITORINFOEX is used by the GetMonitorInfo function
    // https://docs.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-monitorinfoexw
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MONITORINFOEX
    {
        public uint cbSize;
        public Windows.Win32.Foundation.RECT rcMonitor;
        public Windows.Win32.Foundation.RECT rcWork;
        public uint dwFlags;
        // NOTE: szDevice must be marshalled as a ByValArray instead of a ByValTString so that Marshal.SizeOf can calculate a value
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)Windows.Win32.PInvoke.CCHDEVICENAME)]
        public char[] szDevice;
    }

    // https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getmonitorinfow
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern Windows.Win32.Foundation.BOOL GetMonitorInfo(Windows.Win32.Graphics.Gdi.HMONITOR hMonitor, ref MONITORINFOEX lpmi);

    // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowlongptrw
    internal static IntPtr GetWindowLongPtr_IntPtr(Windows.Win32.Foundation.HWND hWnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX nIndex)
    {
#if PLATFORM_X86
        return (nint)Windows.Win32.PInvoke.GetWindowLong(hWnd, nIndex);
#else
        if (IntPtr.Size == 4)
        {
            return (nint)Windows.Win32.PInvoke.GetWindowLong(hWnd, nIndex);
        }
        else
        {
            return Windows.Win32.PInvoke.GetWindowLongPtr(hWnd, nIndex);
        }
#endif
    }

    #endregion winuser
}
