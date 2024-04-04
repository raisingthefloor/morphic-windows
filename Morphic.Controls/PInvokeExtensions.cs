// Copyright 2020-2024 Raising the Floor - US, Inc.
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

    //

    // https://learn.microsoft.com/en-us/windows/win32/api/commctrl/ns-commctrl-tttoolinfow
#pragma warning disable CS0649 // NOTE: hinst and lParam may never be written to (and will remain as IntPtr.Zero) in this implementation
    internal struct TOOLINFO
    {
        public uint cbSize;
        public uint uFlags;
        public IntPtr hwnd;
        public UIntPtr uId;
        public PInvoke.RECT rect;
        public IntPtr hinst;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string? lpszText;
        public IntPtr lParam;
        //public IntPtr reserved; // NOTE: this exists in the official declaration as a void pointer but adding it causes SendMessage to fail; pinvoke.net leaves it out and so do we
    }
#pragma warning restore CS0649 // NOTE: hinst and lParam may never be written to (and will remain as IntPtr.Zero) in this implementation

    #endregion commctrl

    #region winuser

    internal const int CW_USEDEFAULT = unchecked((int)0x80000000);

    internal const ushort MK_LBUTTON = 0x0001;
    internal const ushort MK_RBUTTON = 0x0002;

    internal const ushort WM_USER = 0x0400;

    //

    internal static readonly uint HOVER_DEFAULT = 0xFFFFFFFF;

    //

    // https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-trackmouseevent
    [Flags]
    internal enum TRACKMOUSEEVENTFlags : uint
    {
        TME_CANCEL = 0x80000000,
        TME_HOVER = 0x00000001,
        TME_LEAVE = 0x00000002,
        TME_NONCLIENT = 0x00000010,
        TME_QUERY = 0x40000000
    }

    // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook
    [Flags]
    internal enum WinEventHookFlags : uint
    {
        WINEVENT_OUTOFCONTEXT = 0x0000, // Events are ASYNC
        WINEVENT_SKIPOWNTHREAD = 0x0001, // Don't call back for events on installer's thread
        WINEVENT_SKIPOWNPROCESS = 0x0002, // Don't call back for events on installer's process
        WINEVENT_INCONTEXT = 0x0004, // Events are SYNC, this causes your dll to be injected into every process
    }

    // https://learn.microsoft.com/en-us/windows/win32/winauto/event-constants
    public enum WinEventHookType : uint
    {
        EVENT_AIA_START = 0xA000,
        EVENT_AIA_END = 0xAFFF,
        EVENT_MIN = 0x00000001,
        EVENT_MAX = 0x7FFFFFFF,
        EVENT_OBJECT_ACCELERATORCHANGE = 0x8012,
        EVENT_OBJECT_CLOAKED = 0x8017,
        EVENT_OBJECT_CONTENTSCROLLED = 0x8015,
        EVENT_OBJECT_CREATE = 0x8000,
        EVENT_OBJECT_DEFACTIONCHANGE = 0x8011,
        EVENT_OBJECT_DESCRIPTIONCHANGE = 0x800D,
        EVENT_OBJECT_DESTROY = 0x8001,
        EVENT_OBJECT_DRAGSTART = 0x8021,
        EVENT_OBJECT_DRAGCANCEL = 0x8022,
        EVENT_OBJECT_DRAGCOMPLETE = 0x8023,
        EVENT_OBJECT_DRAGENTER = 0x8024,
        EVENT_OBJECT_DRAGLEAVE = 0x8025,
        EVENT_OBJECT_DRAGDROPPED = 0x8026,
        EVENT_OBJECT_END = 0x80FF,
        EVENT_OBJECT_FOCUS = 0x8005,
        EVENT_OBJECT_HELPCHANGE = 0x8010,
        EVENT_OBJECT_HIDE = 0x8003,
        EVENT_OBJECT_HOSTEDOBJECTSINVALIDATED = 0x8020,
        EVENT_OBJECT_IME_HIDE = 0x8028,
        EVENT_OBJECT_IME_SHOW = 0x8027,
        EVENT_OBJECT_IME_CHANGE = 0x8029,
        EVENT_OBJECT_INVOKED = 0x8013,
        EVENT_OBJECT_LIVEREGIONCHANGED = 0x8019,
        EVENT_OBJECT_LOCATIONCHANGE = 0x800B,
        EVENT_OBJECT_NAMECHANGE = 0x800C,
        EVENT_OBJECT_PARENTCHANGE = 0x800F,
        EVENT_OBJECT_REORDER = 0x8004,
        EVENT_OBJECT_SELECTION = 0x8006,
        EVENT_OBJECT_SELECTIONADD = 0x8007,
        EVENT_OBJECT_SELECTIONREMOVE = 0x8008,
        EVENT_OBJECT_SELECTIONWITHIN = 0x8009,
        EVENT_OBJECT_SHOW = 0x8002,
        EVENT_OBJECT_STATECHANGE = 0x800A,
        EVENT_OBJECT_TEXTEDIT_CONVERSIONTARGETCHANGED = 0x8030,
        EVENT_OBJECT_TEXTSELECTIONCHANGED = 0x8014,
        EVENT_OBJECT_UNCLOAKED = 0x8018,
        EVENT_OBJECT_VALUECHANGE = 0x800E,
        EVENT_OEM_DEFINED_START = 0x0101,
        EVENT_OEM_DEFINED_END = 0x01FF,
        EVENT_SYSTEM_ALERT = 0x0002,
        EVENT_SYSTEM_ARRANGMENTPREVIEW = 0x8016,
        EVENT_SYSTEM_CAPTUREEND = 0x0009,
        EVENT_SYSTEM_CAPTURESTART = 0x0008,
        EVENT_SYSTEM_CONTEXTHELPEND = 0x000D,
        EVENT_SYSTEM_CONTEXTHELPSTART = 0x000C,
        EVENT_SYSTEM_DESKTOPSWITCH = 0x0020,
        EVENT_SYSTEM_DIALOGEND = 0x0011,
        EVENT_SYSTEM_DIALOGSTART = 0x0010,
        EVENT_SYSTEM_DRAGDROPEND = 0x000F,
        EVENT_SYSTEM_DRAGDROPSTART = 0x000E,
        EVENT_SYSTEM_END = 0x00FF,
        EVENT_SYSTEM_FOREGROUND = 0x0003,
        EVENT_SYSTEM_MENUPOPUPEND = 0x0007,
        EVENT_SYSTEM_MENUPOPUPSTART = 0x0006,
        EVENT_SYSTEM_MENUEND = 0x0005,
        EVENT_SYSTEM_MENUSTART = 0x0004,
        EVENT_SYSTEM_MINIMIZEEND = 0x0017,
        EVENT_SYSTEM_MINIMIZESTART = 0x0016,
        EVENT_SYSTEM_MOVESIZEEND = 0x000B,
        EVENT_SYSTEM_MOVESIZESTART = 0x000A,
        EVENT_SYSTEM_SCROLLINGEND = 0x0013,
        EVENT_SYSTEM_SCROLLINGSTART = 0x0012,
        EVENT_SYSTEM_SOUND = 0x0001,
        EVENT_SYSTEM_SWITCHEND = 0x0015,
        EVENT_SYSTEM_SWITCHSTART = 0x0014,
        EVENT_UIA_EVENTID_START = 0x4E00,
        EVENT_UIA_EVENTID_END = 0x4EFF,
        EVENT_UIA_PROPID_START = 0x7500,
        EVENT_UIA_PROPID_END = 0x75FF
    }

    //

    // https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-trackmouseevent
    [StructLayout(LayoutKind.Sequential)]
    internal struct TRACKMOUSEEVENT
    {
        public uint cbSize;
        public TRACKMOUSEEVENTFlags dwFlags;
        public IntPtr hWnd;
        public uint dwHoverTime;

        public static TRACKMOUSEEVENT CreateNew(TRACKMOUSEEVENTFlags dwFlags, IntPtr hWnd, uint dwHoverTime)
        {
            var result = new TRACKMOUSEEVENT()
            {
                cbSize = (uint)Marshal.SizeOf(typeof(TRACKMOUSEEVENT)),
                dwFlags = dwFlags,
                hWnd = hWnd,
                dwHoverTime = dwHoverTime
            };
            return result;
        }
    }

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

    // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nc-winuser-wineventproc
    internal delegate void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint idEventThread, uint dwmsEventTime);

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

    // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getclassnamew
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

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
        if (IntPtr.Size == 4)
        {
            return (nint)Windows.Win32.PInvoke.SetWindowLong(hWnd, nIndex, (int)dwNewLong);
        }
        else
        {
            return PInvokeExtensions.SetWindowLongPtr(hWnd, nIndex, dwNewLong);
        }
    }
    //
    // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowlongptrw
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX nIndex, IntPtr dwNewLong);

    // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook
    [DllImport("user32.dll")]
    internal static extern IntPtr SetWinEventHook(WinEventHookType eventMin, WinEventHookType eventMax, IntPtr hmodWinEventProc, WinEventProc lpfnWinEventProc, uint idProcess, uint idThread, WinEventHookFlags dwFlags);

    // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-trackmouseevent
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool TrackMouseEvent(ref TRACKMOUSEEVENT lpEventTrack);

    // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-unhookwinevent
    [DllImport("user32.dll")]
    internal static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    #endregion winuser
}
