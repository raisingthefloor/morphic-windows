// WindowMovement.cs: Low-level window positioning.
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
    using System.Windows;
    using System.Windows.Interop;

    /// <summary>
    /// Intercepts the low-level Window move/resize messages, so the window rect can be adjusted while it's
    /// being moved or resized.
    ///
    /// All dimensions in and out of this class are measured in device pixels.
    /// </summary>
    public class WindowMovement
    {
        private readonly Window window;
        private bool firstEvent;
        private Rect initialRect = Rect.Empty;
        private Vector mouseOffset;
        private Point mouseStart;

        public WindowMovement(Window window)
        {
            this.window = window;

            this.window.SourceInitialized += this.WindowOnSourceInitialized;
        }

        /// <summary>true if the user is currently moving the window.</summary>
        public bool IsMoving { get; private set; }

        /// <summary>Prevent the window from being moved.</summary>
        public bool NoMove { get; set; }

        /// <summary>true to keep the window in place during resizing.</summary>
        public bool AnchorSizingRect { get; set; } = true;

        public IntPtr WindowHandle { get; private set; }

        /// <summary>
        /// true to ignore NoMove property.
        /// </summary>
        private bool IgnoreLock { get; set; }

        private WinApi.MONITORINFO MonitorInfo => WinApi.GetMonitorInfo(this.WindowHandle);

        /// <summary>Raised while the window is being moved.</summary>
        public event EventHandler<MovementEventArgs>? Moving;

        public event EventHandler? EnterSizeMove;
        public event EventHandler? MoveComplete;
        public event EventHandler? Ready;

        private void WindowOnSourceInitialized(object? sender, EventArgs e)
        {
            // Add the hook for the windows messages.
            this.WindowHandle = new WindowInteropHelper(this.window).Handle;
            HwndSource.FromHwnd(this.WindowHandle)?.AddHook(this.WindowProc);

            this.OnReady();
        }

        private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            IntPtr result = IntPtr.Zero;

            switch (msg)
            {
                case WinApi.WM_WINDOWPOSCHANGED:
                case WinApi.WM_WINDOWPOSCHANGING:
                    // Prevent the window being moved.
                    if (!this.IgnoreLock && this.NoMove)
                    {
                        WinApi.WINDOWPOS windowPos = WinApi.WINDOWPOS.FromPointer(lParam);
                        if (this.NoMove)
                        {
                            windowPos.flags |= WinApi.SWP_NOMOVE;
                        }

                        windowPos.CopyToPointer(lParam);
                    }
                    break;
                
                case WinApi.WM_ENTERSIZEMOVE:
                    this.firstEvent = true;
                    this.OnEnterSizeMove();
                    break;

                case WinApi.WM_EXITSIZEMOVE:
                    if (this.IsMoving)
                    {
                        this.OnMoveComplete();
                    }

                    this.IsMoving = false;
                    break;

                case WinApi.WM_MOVING:
                    MovementEventArgs eventArgs = new MovementEventArgs();

                    eventArgs.Cursor = WinApi.GetCursorPos();
                    eventArgs.Rect = WinApi.RECT.FromPointer(lParam).ToRect();

                    eventArgs.IsFirst = this.firstEvent;
                    if (this.firstEvent)
                    {
                        this.mouseStart = eventArgs.Cursor;
                        this.initialRect = eventArgs.Rect;
                        this.mouseOffset = this.mouseStart - this.initialRect.TopLeft;
                    }

                    eventArgs.InitialRect = this.initialRect;
                    eventArgs.SupposedRect = this.initialRect;

                    if (!this.firstEvent)
                    {
                        eventArgs.SupposedRect.X = eventArgs.Cursor.X - this.mouseOffset.X;
                        eventArgs.SupposedRect.Y = eventArgs.Cursor.Y - this.mouseOffset.Y;
                    }

                    this.IsMoving = true;

                    // Call the event handler.
                    this.OnMoving(eventArgs);

                    if (!eventArgs.NewInitialRect.IsEmpty)
                    {
                        this.initialRect = eventArgs.NewInitialRect;
                        if (this.firstEvent)
                        {
                            this.mouseOffset = this.mouseStart - this.initialRect.TopLeft;
                        }
                        else
                        {
                                this.mouseOffset.X = this.initialRect.Width / 2;
                                this.mouseOffset.Y = this.initialRect.Height / 2;
                            if (Math.Abs(this.mouseOffset.X) > this.initialRect.Width)
                            {
                            }
                            if (Math.Abs(this.mouseOffset.Y) > this.initialRect.Height)
                            {
                            }
                        }
                    }

                    this.firstEvent = false;
                    if (eventArgs.Handled)
                    {
                        new WinApi.RECT(eventArgs.Rect).CopyToPointer(lParam);
                        handled = true;
                        result = new IntPtr(1);
                    }

                    break;
            }

            return result;
        }

        /// <summary>Called when the window is being moved.</summary>
        /// <param name="e"></param>
        protected virtual void OnMoving(MovementEventArgs e)
        {
            this.Moving?.Invoke(this, e);
        }

        protected virtual void OnEnterSizeMove()
        {
            this.EnterSizeMove?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnMoveComplete()
        {
            this.MoveComplete?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnReady()
        {
            this.Ready?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Like Window.DragMove, but doesn't fire the Click event.
        /// </summary>
        /// <param name="respectNoMove">true to leave `NoMove` alone.</param>
        public void DragMove(bool respectNoMove = false)
        {
            if (!this.NoMove || !respectNoMove)
            {
                this.IgnoreLock = true;
                WinApi.DragMove(this.WindowHandle);
                this.IgnoreLock = false;
            }
        }

        /// <summary>
        /// Gets the work area of the screen the window is (mostly) on.
        /// </summary>
        /// <returns></returns>
        public Rect GetWorkArea()
        {
            return this.MonitorInfo.rcWork.ToRect();
        }

        /// <summary>
        /// Gets the work area at the given point.
        /// </summary>
        /// <param name="pt"></param>
        /// <returns></returns>
        public Rect GetWorkArea(Point pt)
        {
            return WinApi.GetMonitorInfo(pt).rcWork.ToRect();
        }

        /// <summary>
        /// Get the window rectangle.
        /// </summary>
        /// <returns></returns>
        internal WinApi.RECT GetWindowRc()
        {
            WinApi.GetWindowRect(this.WindowHandle, out WinApi.RECT rc);
            return rc;
        }

        /// <summary>
        /// Get the window rectangle.
        /// </summary>
        /// <returns></returns>
        public Rect GetWindowRect()
        {
            return this.GetWindowRc().ToRect();
        }

        /// <summary>
        /// Gets the size of the screen, that the window is located on.
        /// </summary>
        /// <returns></returns>
        public Rect GetScreenSize()
        {
            return this.MonitorInfo.rcMonitor.ToRect();
        }

        internal void SetWindowRect(WinApi.RECT rc)
        {
            bool ignored = this.IgnoreLock;
            this.IgnoreLock = true;
            WinApi.MoveWindow(this.WindowHandle, rc.Left, rc.Top, rc.Right - rc.Left, rc.Bottom - rc.Top, true);
            this.IgnoreLock = ignored;
        }

        public void SetWindowRect(Rect rect)
        {
            bool ignored = this.IgnoreLock;
            this.IgnoreLock = true;
            WinApi.MoveWindow(this.WindowHandle, (int)rect.Left, (int)rect.Top, (int)rect.Width, (int)rect.Height, true);
            this.IgnoreLock = ignored;
        }

        public static Point GetCursorPos()
        {
            return WinApi.GetCursorPos();
        }

        public class MovementEventArgs : EventArgs
        {
            /// <summary>The window rect before the resize/move began.</summary>
            public Rect InitialRect = Rect.Empty;

            /// <summary>
            /// Set in the event handler to change the initial rect, which used to calculate the SupposedRect property.
            /// </summary>
            public Rect NewInitialRect = Rect.Empty;

            /// <summary>The window rect. Update in the event handler to change it.</summary>
            public Rect Rect;

            /// <summary>The window rect that the window should have, if there were no adjustments to Rect.</summary>
            public Rect SupposedRect = Rect.Empty;

            /// <summary>
            /// Set in the event handler to true if the event has been handled and the window rect has changed.
            /// </summary>
            public bool Handled { get; set; }

            /// <summary>true if this is the first event of the current resize/move loop.</summary>
            public bool IsFirst { get; set; }

            /// <summary>The mouse cursor position on the screen.</summary>
            public Point Cursor { get; set; }
        }
   }
}