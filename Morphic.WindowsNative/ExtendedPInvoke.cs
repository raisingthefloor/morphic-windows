// Copyright 2020-2022 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-windows/blob/master/LICENSE.txt
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

namespace Morphic.WindowsNative;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

internal struct ExtendedPInvoke
{
    #region "WinUser.h"

    // https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-createwindowexw
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr CreateWindowEx(
        WindowStylesEx dwExStyle,
        IntPtr lpClassName,
        string? lpWindowName,
        WindowStyles dwStyle,
        int X,
        int Y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    // helper declaration (for passing the class name as a UInt16)
    internal static IntPtr CreateWindowEx(ExtendedPInvoke.WindowStylesEx dwExStyle, ushort lpClassName, string? lpWindowName, ExtendedPInvoke.WindowStyles dwStyle, int X, int Y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam)
    {
        return ExtendedPInvoke.CreateWindowEx(dwExStyle, new IntPtr(lpClassName), lpWindowName, dwStyle, X, Y, nWidth, nHeight, hWndParent, hMenu, hInstance, lpParam);
    }

    // helper declaration (for passing the class name as a string)
    internal static IntPtr CreateWindowEx(ExtendedPInvoke.WindowStylesEx dwExStyle, string lpClassName, string? lpWindowName, ExtendedPInvoke.WindowStyles dwStyle, int X, int Y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam)
    {
        var pointerToClassName = Marshal.StringToHGlobalUni(lpClassName);
        try
        {
            return ExtendedPInvoke.CreateWindowEx(dwExStyle, pointerToClassName, lpWindowName, dwStyle, X, Y, nWidth, nHeight, hWndParent, hMenu, hInstance, lpParam);
        }
        finally
        {
            Marshal.FreeHGlobal(pointerToClassName);
        }
    }

    //

    // https://docs.microsoft.com/en-us/windows/win32/winmsg/window-styles
    [Flags]
    internal enum WindowStyles : uint
    {
        WS_BORDER = 0x00800000,
        WS_CAPTION = 0x00C00000,
        WS_CHILD = 0x40000000,
        WS_CHILDWINDOW = 0x40000000,
        WS_CLIPCHILDREN = 0x02000000,
        WS_CLIPSIBLINGS = 0x04000000,
        WS_DISABLED = 0x08000000,
        WS_DLGFRAME = 0x00400000,
        WS_GROUP = 0x00020000,
        WS_HSCROLL = 0x00100000,
        WS_ICONIC = 0x20000000,
        WS_MAXIMIZE = 0x01000000,
        WS_MAXIMIZEBOX = 0x00010000,
        WS_MINIMIZE = 0x20000000,
        WS_MINIMIZEBOX = 0x00020000,
        WS_OVERLAPPED = 0x00000000,
        WS_OVERLAPPEDWINDOW = WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX,
        WS_POPUP = 0x80000000,
        WS_POPUPWINDOW = WS_POPUP | WS_BORDER | WS_SYSMENU,
        WS_SIZEBOX = 0x00040000,
        WS_SYSMENU = 0x00080000,
        WS_TABSTOP = 0x00010000,
        WS_THICKFRAME = 0x00040000,
        WS_TILED = 0x00000000,
        WS_TILEDWINDOW = WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX,
        WS_VISIBLE = 0x10000000,
        WS_VSCROLL = 0x00200000
    }

    // https://docs.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles
    [Flags]
    internal enum WindowStylesEx : uint
    {
        WS_EX_ACCEPTFILES = 0x00000010,
        WS_EX_APPWINDOW = 0x00040000,
        WS_EX_CLIENTEDGE = 0x00000200,
        WS_EX_COMPOSITED = 0x02000000,
        WS_EX_CONTEXTHELP = 0x00000400,
        WS_EX_CONTROLPARENT = 0x00010000,
        WS_EX_DLGMODALFRAME = 0x00000001,
        WS_EX_LAYERED = 0x00080000,
        WS_EX_LAYOUTRTL = 0x00400000,
        WS_EX_LEFT = 0x00000000,
        WS_EX_LEFTSCROLLBAR = 0x00004000,
        WS_EX_LTRREADING = 0x00000000,
        WS_EX_MDICHILD = 0x00000040,
        WS_EX_NOACTIVATE = 0x08000000,
        WS_EX_NOINHERITLAYOUT = 0x00100000,
        WS_EX_NOPARENTNOTIFY = 0x00000004,
        WS_EX_NOREDIRECTIONBITMAP = 0x00200000,
        WS_EX_OVERLAPPEDWINDOW = WS_EX_WINDOWEDGE | WS_EX_CLIENTEDGE,
        WS_EX_PALETTEWINDOW = WS_EX_WINDOWEDGE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST,
        WS_EX_RIGHT = 0x00001000,
        WS_EX_RIGHTSCROLLBAR = 0x00000000,
        WS_EX_RTLREADING = 0x00002000,
        WS_EX_STATICEDGE = 0x00020000,
        WS_EX_TOOLWINDOW = 0x00000080,
        WS_EX_TOPMOST = 0x00000008,
        WS_EX_TRANSPARENT = 0x00000020,
        WS_EX_WINDOWEDGE = 0x00000100
    }

    //

    // https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-defwindowprocw
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    // docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowlongw
    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern Int32 GetWindowLongPtr_32bit(IntPtr hWnd, int nIndex);
    //
    // https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowlongptrw
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr_64bit(IntPtr hWnd, int nIndex);
    //
    internal static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        // NOTE: 32-bit windows does not have a DLL entrypoint labeled GetWindowLongPtr, so we alias the functions here (just like the C header files do)
        if (Marshal.SizeOf<IntPtr>() == 4) {
            return (IntPtr)ExtendedPInvoke.GetWindowLongPtr_32bit(hWnd, nIndex);
        }
        else
        {
            return ExtendedPInvoke.GetWindowLongPtr_64bit(hWnd, nIndex);
        }
    }

    // https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-loadcursorw
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);
    //
    internal enum Cursors
    {
        IDC_APPSTARTING = 32650,
        IDC_ARROW = 32512,
        IDC_CROSS = 32515,
        IDC_HAND = 32649,
        IDC_HELP = 32651,
        IDC_IBEAM = 32513,
        IDC_ICON = 32641,
        IDC_NO = 32648,
        IDC_SIZE = 32640,
        IDC_SIZEALL = 32646,
        IDC_SIZENESW = 32643,
        IDC_SIZENS = 32645,
        IDC_SIZENWSE = 32642,
        IDC_SIZEWE = 32644,
        IDC_UPARROW = 32516,
        IDC_WAIT = 32514,
    }

    // https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerclassexw
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern ushort RegisterClassEx([In] ref WNDCLASSEX lpWndClass);

    // https://docs.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-wndclassexw
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public WNDPROC lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }
    // see: https://docs.microsoft.com/en-us/previous-versions/windows/desktop/legacy/ms633573(v=vs.85)
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate IntPtr WNDPROC(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    #endregion "WinUser.h"
}
