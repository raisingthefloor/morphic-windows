// Copyright 2020-2026 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-controls-lib-cs/blob/main/LICENSE.txt
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
using System.Runtime.InteropServices;
using System.Text;

namespace Morphic.Controls;

internal class PInvokeExtensions
{
    #region commctrl

    internal const string TOOLTIPS_CLASS = "tooltips_class32";
    //
    internal const byte TTS_ALWAYSTIP = 0x01;
    //internal const byte TTS_NOPREFIX = 0x02;
    //internal const byte TTS_BALLOON = 0x40;
    internal const ushort TTF_SUBCLASS = 0x0010;
    //
    internal const ushort TTM_ADDTOOL = WM_USER + 50;
    internal const ushort TTM_DELTOOL = WM_USER + 51;

    #endregion commctrl

    #region wingdi 

    public static readonly IntPtr HGDI_ERROR = new IntPtr(-1);

    #endregion wingdi

    #region winuser

    internal const int CW_USEDEFAULT = unchecked((int)0x80000000);

    internal static readonly uint HOVER_DEFAULT = 0xFFFFFFFF;

    internal const ushort MK_LBUTTON = 0x0001;
    internal const ushort MK_RBUTTON = 0x0002;

    internal const ushort WM_USER = 0x0400;

    //

    // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook
    [Flags]
    internal enum WinEventHookFlags : uint
    {
        WINEVENT_OUTOFCONTEXT = 0x0000, // Events are ASYNC
        WINEVENT_SKIPOWNTHREAD = 0x0001, // Don't call back for events on installer's thread
        WINEVENT_SKIPOWNPROCESS = 0x0002, // Don't call back for events on installer's process
        WINEVENT_INCONTEXT = 0x0004, // Events are SYNC, this causes your dll to be injected into every process
    }

    //

    // https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-wndclassexw
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string? lpszClassName; // NOTE: this member should be initialized (i.e. non-null)
        public IntPtr hIconSm;

        public static WNDCLASSEX CreateNew()
        {
            var result = new WNDCLASSEX()
            {
                cbSize = (uint)Marshal.SizeOf(typeof(WNDCLASSEX))
            };
            return result;
        }
    }

    //

    // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nc-winuser-wndproc
    internal delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    //

    // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-createwindowexw
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr CreateWindowEx(
         PInvoke.User32.WindowStylesEx dwExStyle,
         IntPtr lpClassName,
         string? lpWindowName,
         PInvoke.User32.WindowStyles dwStyle,
         int x,
         int y,
         int nWidth,
         int nHeight,
         IntPtr hWndParent,
         IntPtr hMenu,
         IntPtr hInstance,
         IntPtr lpParam
    );

    // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowlongptrw
    internal static IntPtr GetWindowLongPtr_IntPtr(Windows.Win32.Foundation.HWND hWnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX nIndex)
    {
        if (IntPtr.Size == 4)
        {
            return (nint)Windows.Win32.PInvoke.GetWindowLong(hWnd, nIndex);
        }
        else
        {
            return PInvokeExtensions.GetWindowLongPtr(hWnd.Value, nIndex);
        }
    }
    //
    // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowlongptrw
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX nIndex);

    // see: https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-mapwindowpoints
    // NOTE: this signature is the POINT option (in which cPoints must always be set to 1).
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern int MapWindowPoints(IntPtr hWndFrom, IntPtr hWndTo, ref PInvoke.POINT lpPoints, uint cPoints);
    //
    // see: https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-mapwindowpoints
    // NOTE: this signature is the RECT option (in which cPoints must always be set to 2).
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern int MapWindowPoints(IntPtr hWndFrom, IntPtr hWndTo, ref PInvoke.RECT lpPoints, uint cPoints);

    // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-redrawwindow
    [DllImport("user32.dll")]
    internal static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, Windows.Win32.Graphics.Gdi.REDRAW_WINDOW_FLAGS flags);

    // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerclassexw
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern ushort RegisterClassEx([In] ref WNDCLASSEX lpWndClass);

    // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowlongptrw
    internal static IntPtr SetWindowLongPtr_IntPtr(Windows.Win32.Foundation.HWND hWnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX nIndex, IntPtr dwNewLong)
    {
#if PLATFORM_X86
        return (nint)Windows.Win32.PInvoke.SetWindowLong(hWnd, nIndex, (int)dwNewLong);
#else
        if (IntPtr.Size == 4)
        {
            return (nint)Windows.Win32.PInvoke.SetWindowLong(hWnd, nIndex, (int)dwNewLong);
        }
        else
        {
            return Windows.Win32.PInvoke.SetWindowLongPtr(hWnd, nIndex, dwNewLong);
        }
#endif
    }

    // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-unhookwinevent
    [DllImport("user32.dll")]
    internal static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    #endregion winuser
}
