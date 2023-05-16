// WinApi.cs: Utilities for using the Windows API.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

namespace Morphic.Client
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.InteropServices;
    using System.Windows;

    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Names come from the Windows API")]
    [SuppressMessage("ReSharper", "IdentifierTypo", Justification = "Names come from the Windows API")]
    internal static class WinApi
    {
        #region Window Positioning

        public static RECT ToRECT(this Rect rc)
        {
            return new RECT(rc);
        }

        public static POINT ToPOINT(this Point pt)
        {
            return new POINT(pt);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            /// <summary>
            /// Creates the struct using the data from the pointer.
            /// </summary>
            /// <param name="pointer">The pointer to the unmanaged memory.</param>
            /// <returns>A RECT structure, from the data pointed to by the pointer.</returns>
            public static RECT FromPointer(IntPtr pointer)
            {
                return Marshal.PtrToStructure<WinApi.RECT>(pointer)!;
            }

            /// <summary>
            /// Copies the values from this struct to the pointer.
            /// </summary>
            /// <param name="pointer">The pointer to the unmanaged memory.</param>
            public void CopyToPointer(IntPtr pointer)
            {
                Marshal.StructureToPtr(this, pointer, false);
            }

            /// <summary>
            /// Creates a .NET Rect from this win32 RECT.
            /// </summary>
            /// <returns></returns>
            public Rect ToRect()
            {
                return new Rect(this.Left, this.Top, this.Right - this.Left, this.Bottom - this.Top);
            }


            /// <summary>
            /// Creates a win32 RECT from a .NET Rect.
            /// </summary>
            /// <param name="rect">The rectangle.</param>
            public RECT(Rect rect)
            {
                this.Left = (int)rect.Left;
                this.Top = (int)rect.Top;
                this.Right = (int)rect.Right;
                this.Bottom = (int)rect.Bottom;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPOS
        {
            public IntPtr hwndInsertAfter;
            public IntPtr hwnd;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public int flags;

            /// <summary>
            /// Creates the struct using the data from the pointer.
            /// </summary>
            /// <param name="pointer">The pointer to the unmanaged memory.</param>
            /// <returns>A RECT structure, from the data pointed to by the pointer.</returns>
            public static WINDOWPOS FromPointer(IntPtr pointer)
            {
                return Marshal.PtrToStructure<WinApi.WINDOWPOS>(pointer)!;
            }

            /// <summary>
            /// Copies the values from this struct to the pointer.
            /// </summary>
            /// <param name="pointer">The pointer to the unmanaged memory.</param>
            public void CopyToPointer(IntPtr pointer)
            {
                Marshal.StructureToPtr(this, pointer, false);
            }
        }

        public const int SWP_NOSIZE = 0x1;
        public const int SWP_NOMOVE = 0x2;

        public const int WM_WINDOWPOSCHANGING = 0x0046;
        public const int WM_WINDOWPOSCHANGED = 0x0047;
        public const int WM_SIZING = 0x0214;
        public const int WM_MOVING = 0x0216;
        public const int WM_ENTERSIZEMOVE = 0x0231;
        public const int WM_EXITSIZEMOVE = 0x0232;
        public const int WM_SYSCOMMAND = 0x0112;

        public const int SC_DRAGMOVE = 0xf012;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(ref POINT pt);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;

            public POINT(int x, int y)
            {
                this.X = x;
                this.Y = y;
            }

            public POINT(Point pt) : this((int)pt.X, (int)pt.Y)
            {
            }

            public Point ToPoint()
            {
                return new Point(this.X, this.Y);
            }
        }

        public static Point GetCursorPos()
        {
            POINT pt = new POINT();
            WinApi.GetCursorPos(ref pt);
            return pt.ToPoint();
        }

        public static IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            return IntPtr.Size == 8
                ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
                : SetWindowLong32(hWnd, nIndex, dwNewLong);
        }

        public static IntPtr GetWindowLong(IntPtr hWnd, int nIndex)
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(hWnd, nIndex)
                : GetWindowLong32(hWnd, nIndex);
        }

        /// <summary>
        /// Better than Window.DragMove - this doesn't fire the click event of the control on which the mouse
        /// is down, and doesn't require a mouse button to be down.
        /// </summary>
        /// <param name="hWnd"></param>
        public static void DragMove(IntPtr hWnd)
        {
            SendMessage(hWnd, WinApi.WM_SYSCOMMAND, WinApi.SC_DRAGMOVE, IntPtr.Zero);
        }

        public const int GWL_STYLE = -16;
        public const int GWL_EXSTYLE = -20;
        public const int WS_SIZEBOX = 0x00040000;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int SPI_GETWORKAREA = 0x0030;

        internal const uint WM_WININICHANGE = 0x001A;


        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        // observation: wParam should be a UIntPtr (or an IntPtr) instead of an int
        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, IntPtr lParam);

        // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendnotifymessagew
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern bool SendNotifyMessage(IntPtr hWnd, uint Msg, UIntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
        internal static extern bool SystemParametersInfoRect(int uiAction, int uiParam, ref RECT pvParam, int fWinIni);

        [DllImport("user32.dll")]
        internal static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        #endregion

        #region Monitor info

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;

            public MONITORINFO(IntPtr hMonitor) : this()
            {
                this.cbSize = Marshal.SizeOf(this);
                // NOTE: we are not yet checking for a failure result from this API
                GetMonitorInfo(hMonitor, ref this);
            }
        }

        internal enum MonitorDefault
        {
            MONITOR_DEFAULTTONULL,
            MONITOR_DEFAULTTOPRIMARY,
            MONITOR_DEFAULTTONEAREST
        }

        public static MONITORINFO GetMonitorInfo() => GetMonitorInfo(IntPtr.Zero);

        public static MONITORINFO GetMonitorInfo(IntPtr hwnd)
        {
            IntPtr monitor = MonitorFromWindow(hwnd, MonitorDefault.MONITOR_DEFAULTTOPRIMARY);
            return new MONITORINFO(monitor);
        }

        public static MONITORINFO GetMonitorInfo(Point pt)
        {
            IntPtr monitor = MonitorFromPoint(pt.ToPOINT(), MonitorDefault.MONITOR_DEFAULTTONEAREST);
            return new MONITORINFO(monitor);
        }

        public static IEnumerable<MONITORINFO> GetMonitorInfoAll()
        {
            List<MONITORINFO> monitors = new List<MONITORINFO>();
            
            RECT screen = GetVirtualScreen().ToRECT();

            // NOTE: ideally our caller should simply use the display objects returned by...
            // var allMonitorHandles = Morphic.WindowsNative.Display.Display.GetAllDisplays();

            EnumDisplayMonitors(IntPtr.Zero, ref screen,
                (IntPtr monitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr data) =>
                {
                    monitors.Add(GetMonitorInfo(monitor));
                    return true;
                }, IntPtr.Zero);

            return monitors.ToArray();
        }

        public static Rect GetVirtualScreen()
        {
            return new Rect(
                GetSystemMetrics(SM_XVIRTUALSCREEN),
                GetSystemMetrics(SM_YVIRTUALSCREEN),
                GetSystemMetrics(SM_CXVIRTUALSCREEN),
                GetSystemMetrics(SM_CYVIRTUALSCREEN));
        }

        public const int SM_XVIRTUALSCREEN = 76;
        public const int SM_YVIRTUALSCREEN = 77;
        public const int SM_CXVIRTUALSCREEN = 78;
        public const int SM_CYVIRTUALSCREEN = 79;
        public const int SM_CMONITORS = 80;

        [DllImport("User32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, MonitorDefault dwFlags);

        [DllImport("User32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hWnd, MonitorDefault dwFlags);

        [DllImport("User32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")] 
        internal static extern int GetSystemMetrics(int smIndex);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, ref RECT lprcClip, EnumMonitorsDelegate lpfnEnum, IntPtr dwData);

        private delegate bool EnumMonitorsDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        #endregion

        #region App Bar API

        [StructLayout(LayoutKind.Sequential)]
        internal struct APPBARDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public IntPtr lParam;
        }

        [Flags]
        internal enum DWMWINDOWATTRIBUTE
        {
            DWMA_NCRENDERING_ENABLED = 1,
            DWMA_NCRENDERING_POLICY,
            DWMA_TRANSITIONS_FORCEDISABLED,
            DWMA_ALLOW_NCPAINT,
            DWMA_CPATION_BUTTON_BOUNDS,
            DWMA_NONCLIENT_RTL_LAYOUT,
            DWMA_FORCE_ICONIC_REPRESENTATION,
            DWMA_FLIP3D_POLICY,
            DWMA_EXTENDED_FRAME_BOUNDS,
            DWMA_HAS_ICONIC_BITMAP,
            DWMA_DISALLOW_PEEK,
            DWMA_EXCLUDED_FROM_PEEK,
            DWMA_LAST
        }

        [Flags]
        internal enum DWMNCRenderingPolicy
        {
            UseWindowStyle,
            Disabled,
            Enabled,
            Last
        }

        internal enum ABMessage
        {
            ABM_NEW = 0,
            ABM_REMOVE,
            ABM_QUERYPOS,
            ABM_SETPOS,
            ABM_GETSTATE,
            ABM_GETTASKBARPOS,
            ABM_ACTIVATE,
            ABM_GETAUTOHIDEBAR,
            ABM_SETAUTOHIDEBAR,
            ABM_WINDOWPOSCHANGED,
            ABM_SETSTATE
        }

        internal enum ABNotify
        {
            ABN_STATECHANGE = 0,
            ABN_POSCHANGED,
            ABN_FULLSCREENAPP,
            ABN_WINDOWARRANGE
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("SHELL32", CallingConvention = CallingConvention.StdCall)]
        internal static extern uint SHAppBarMessage(int dwMessage, ref APPBARDATA pData);

        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        internal static extern uint RegisterWindowMessage(string lpString);

        [DllImport("dwmapi.dll")]
        internal static extern int DwmSetWindowAttribute(IntPtr hWnd, int attr, ref int attrValue, int attrSize);

        internal static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);
        internal static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

        #endregion

        #region Window Creation and Management

        public static bool ActivateWindow(IntPtr hwnd)
        {
            if (IsIconic(hwnd))
            {
                ShowWindow(hwnd, 9);
            }

            return SetForegroundWindow(hwnd);
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr CreateWindowEx(
            WindowStylesEx dwExStyle,
            IntPtr lpClassName,
            string? lpWindowName,
            WindowStyles dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

          //[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
          //internal static extern IntPtr CreateWindowEx(
          //    WindowStylesEx dwExStyle,
          //    string lpClassName,
          //    string? lpWindowName,
          //    WindowStyles dwStyle,
          //    int x,
          //    int y,
          //    int nWidth,
          //    int nHeight,
          //    IntPtr hWndParent,
          //    IntPtr hMenu,
          //    IntPtr hInstance,
          //    IntPtr lpParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll")]
        internal static extern UInt32 GetDpiForWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

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

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern ushort RegisterClassEx([In] ref WNDCLASSEX lpWndClass);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

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

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        internal struct WNDCLASSEX
        {
            [MarshalAs(UnmanagedType.U4)]
            public uint cbSize;
            [MarshalAs(UnmanagedType.U4)]
            //public ClassStyles style;
            public uint style;
            public IntPtr lpfnWndProc;
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
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        #endregion
    }
}