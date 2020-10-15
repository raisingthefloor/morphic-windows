// AppBarAPI.cs: Interfaces with the Windows AppBar API.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

namespace Morphic.Client.Bar.UI.AppBarWindow
{
    using System;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Interop;

    public enum Edge
    {
        None = -1,
        Left = 0,
        Top = 1,
        Right = 2,
        Bottom = 3
    }

    /// <summary>
    /// The Windows AppBar API
    /// https://docs.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shappbarmessage 
    /// </summary>
    internal class AppBarApi
    {
        private readonly uint callbackMessage;
        private readonly WindowMovement windowMovement;
        private HwndSource? hwndSource;
        private bool positioning;

        public AppBarApi(WindowMovement windowMovement)
        {
            this.windowMovement = windowMovement;
            this.callbackMessage = WinApi.RegisterWindowMessage("MorphicAppBarMessage");
            this.windowMovement.Ready += (sender, args) =>
            {
                // Remove it from alt+tab
                // int style = (int) WinApi.GetWindowLong(this.WindowHandle, WinApi.GWL_EXSTYLE);
                // style |= WinApi.WS_EX_TOOLWINDOW;
                // WinApi.SetWindowLong(this.WindowHandle, WinApi.GWL_EXSTYLE, (IntPtr) style);
            };
        }

        private IntPtr WindowHandle => this.windowMovement.WindowHandle;

        /// <summary>
        /// The edge on which the app bar is.
        /// </summary>
        public Edge Edge { get; private set; } = Edge.None;

        /// <summary>
        /// Set the window to be at the given edge, and reserves desktop space for it. The window will be
        /// moved to the edge and stretched to fit. 
        /// </summary>
        /// <param name="edge">The edge to dock to.</param>
        public void Apply(Edge edge)
        {
            Edge last = this.Edge;
            this.Edge = edge;

            if (this.Edge == Edge.None)
            {
                this.Remove();
            }
            else if (this.Edge == last)
            {
                this.Update();
            }
            else
            {
                if (last != Edge.None)
                {
                    this.Remove();
                }

                this.Add();
            }
        }

        /// <summary>
        /// Updates the reserved area to match the new size of the window.
        /// </summary>
        public void Update()
        {
            if (this.Edge != Edge.None)
            {
                this.SetPos();
            }
        }

        /// <summary>
        /// Adds an app bar to the desktop.
        /// </summary>
        private void Add()
        {
            WinApi.APPBARDATA appBarData = this.AppBarData();
            appBarData.uCallbackMessage = this.callbackMessage;
            AppBarMessage(WinApi.ABMessage.ABM_NEW, ref appBarData);
            
            if (this.hwndSource == null)
            {
                this.hwndSource = HwndSource.FromHwnd(this.WindowHandle);
                this.hwndSource?.AddHook(this.WindowProc);
            }

            this.SetPos();
        }

        /// <summary>
        /// Set the position of the app bar.
        /// </summary>
        private void SetPos()
        {
            if (this.positioning)
            {
                this.positioning = false;
                return;
            }
            this.positioning = true;
            
            WinApi.APPBARDATA appBarData = this.AppBarData();
            
            appBarData.uEdge = (uint) this.Edge;

            Rect windowRect = this.windowMovement.GetWindowRect();
            Rect screen = this.windowMovement.GetScreenSize();
            
            // Request the full length of the screen, at the relevant edge.
            Rect rect = screen;
            if (this.Edge.IsHorizontal())
            {
                rect.Height = windowRect.Height;
                if (this.Edge == Edge.Bottom)
                {
                    rect.Y = screen.Bottom - windowRect.Height;
                }
            }
            else if (this.Edge.IsVertical())
            {
                rect.Width = windowRect.Width;
                if (this.Edge == Edge.Right)
                {
                    rect.X = screen.Right - windowRect.Width;
                }
            }

            // Ask for a suggested rect - Windows will adjust it to be clear of other app bars.
            appBarData.rc = rect.ToRECT();
            AppBarMessage(WinApi.ABMessage.ABM_QUERYPOS, ref appBarData);

            // Accept the edge position, ignore the rest.
            switch (this.Edge)
            {
                case Edge.Left:
                    appBarData.rc.Right = appBarData.rc.Left + (int)rect.Width;
                    break;
                case Edge.Top:
                    appBarData.rc.Bottom = appBarData.rc.Top + (int)rect.Height;
                    break;
                case Edge.Right:
                    appBarData.rc.Left = appBarData.rc.Right - (int)rect.Width;
                    break;
                case Edge.Bottom:
                    appBarData.rc.Top = appBarData.rc.Bottom - (int)rect.Height;
                    break;
            }

            // Move the window.
            this.windowMovement.NoMove = true;
            this.windowMovement.SetWindowRect(appBarData.rc);

            // Set the app-bar position.
            AppBarMessage(WinApi.ABMessage.ABM_SETPOS, ref appBarData);

            Task.Delay(500).ContinueWith(t =>
            {
                this.windowMovement.NoMove = false;
                this.windowMovement.SetWindowRect(appBarData.rc);
            });
            
            this.positioning = false;
        }

        /// <summary>
        /// Removes the app bar.
        /// </summary>
        private void Remove()
        {
            WinApi.APPBARDATA appBarData = this.AppBarData();
            AppBarMessage(WinApi.ABMessage.ABM_REMOVE, ref appBarData);
        }

        private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == this.callbackMessage)
            {
                WinApi.ABNotify abn = (WinApi.ABNotify) wParam;
                switch (abn)
                {
                    case WinApi.ABNotify.ABN_POSCHANGED:
                        this.SetPos();
                        break;
                    case WinApi.ABNotify.ABN_STATECHANGE:
                        break;
                    case WinApi.ABNotify.ABN_FULLSCREENAPP:
                        break;
                    case WinApi.ABNotify.ABN_WINDOWARRANGE:
                        break;
                }
            }
            
            return IntPtr.Zero;
        }

        private WinApi.APPBARDATA AppBarData()
        {
            WinApi.APPBARDATA appBarData = new WinApi.APPBARDATA();
            appBarData.cbSize = Marshal.SizeOf(appBarData);
            appBarData.hWnd = this.WindowHandle;
            return appBarData;
        }

        private static void AppBarMessage(WinApi.ABMessage message, ref WinApi.APPBARDATA appBarData)
        {
            WinApi.SHAppBarMessage((int)message, ref appBarData);
        }
    }
}