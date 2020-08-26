// WinApi.cs: Utilities for using the Windows API.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

namespace Morphic.Bar.UI.AppBarWindow
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
                this.Left = (int) rect.Left;
                this.Top = (int) rect.Top;
                this.Right = (int) rect.Right;
                this.Bottom = (int) rect.Bottom;
            }

            public override string ToString()
            {
                return $"{this.Left} {this.Top} {this.Right} {this.Bottom}";
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

            public POINT(Point pt) : this((int) pt.X, (int) pt.Y)
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


        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, IntPtr lParam);

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

        [DllImport("SHELL32", CallingConvention = CallingConvention.StdCall)]
        internal static extern uint SHAppBarMessage(int dwMessage, ref APPBARDATA pData);

        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        internal static extern uint RegisterWindowMessage(string msg);

        [DllImport("dwmapi.dll")]
        internal static extern int DwmSetWindowAttribute(IntPtr hWnd, int attr, ref int attrValue, int attrSize);

        #endregion
    }
}