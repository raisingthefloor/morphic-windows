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
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Morphic.Controls.TrayButton.Windows10;

// TODO: resize the task button container back to where it started after we hide our tray button
// TODO: sometimes, Windows resizes the taskbar under us (in which case the task bar container runs underneath our button); we need to detect this and re-reposition gracefully
// TODO: add support for high contrast icons
// TODO: in some testing, we temporarily experienced a "spinning wheel" over our icon if the mouse cursor hovers over it (right after startup)

internal class TrayButton : IDisposable
{
    private bool disposedValue;

    private System.Drawing.Icon? _icon = null;
    private string? _text = null;
    private bool _visible = false;

    private TrayButtonNativeWindow? _nativeWindow = null;

    //private bool _highContrastModeIsOn_Cached = false;

    public event System.Windows.Forms.MouseEventHandler? MouseUp;

    public System.Drawing.Rectangle? PositionAndSize
    {
        get
        {
            return _nativeWindow?.PositionAndSize;
        }
    }

    internal TrayButton()
    {
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // dispose managed state (managed objects)
                this.DestroyManagedNativeWindow();
            }

            // free unmanaged resources (unmanaged objects) and override finalizer
            // [none]

            // set large fields to null
            // [none]

            disposedValue = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~TrayButton()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>The icon for the tray button</summary>
    public System.Drawing.Icon? Icon
    {
        get
        {
            return _icon;
        }
        set
        {
            _icon = value;

            _nativeWindow?.SetIcon(_icon);
        }
    }

    /// <summary>Tooltip for the tray button.</summary>
    public string? Text
    {
        get
        {
            return _text;
        }
        set
        {
            _text = value;

            _nativeWindow?.SetText(_text);
        }
    }

    /// <summary>Show or hide the tray button.</summary>
    public bool Visible
    {
        get
        {
            return _visible;
        }
        set
        {
            _visible = value;

            if (_visible == true)
            {
                if (_nativeWindow is null)
                {
                    CreateNativeWindow();
                }
            }
            else //if (_visible == false)
            {
                if (_nativeWindow is not null)
                {
                    this.DestroyManagedNativeWindow();
                }
            }
        }
    }

    // NOTE: this throws an exception if it fails to create the native window
    private void CreateNativeWindow()
    {
        // if the tray button window already exists; it cannot be created again
        if (_nativeWindow is not null)
        {
            throw new InvalidOperationException();
        }

        // find the window handle of the Windows taskbar
        var taskbarHandle = TrayButtonNativeWindow.FindWindowsTaskbarHandle();
        if (taskbarHandle == IntPtr.Zero)
        {
            // could not find taskbar
            throw new Exception("Could not find taskbar");
        }

        /* TODO: consider cached the current DPI of the taskbar (to track, in case the taskbar DPI changes in the future); we currently calculate the icon size based on
             *       the height/width of the window, so this check may not be necessary */

        //// cache the current high contrast on/off state (to track)
        //_highContrastModeIsOn_Cached = IsHighContrastModeOn();

        // create the native window
        var nativeWindow = new TrayButtonNativeWindow(this);

        // initialize the native window; note that we have separated "initialize" into a separate function so that our constructor doesn't throw exceptions on failure
        try
        {
            nativeWindow.Initialize(taskbarHandle);
        }
        catch (System.ComponentModel.Win32Exception/* ex*/)
        {
            // TODO: consider what exceptions we could get here, how to handle them and how to bubble them up to our caller, etc.
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }

        // set the icon for the native window
        nativeWindow.SetIcon(_icon);
        // set the (tooltip) text for the native window
        nativeWindow.SetText(_text);

        // store the reference to our new native window
        _nativeWindow = nativeWindow;
    }

    private void DestroyManagedNativeWindow()
    {
        _nativeWindow?.Dispose();
        _nativeWindow = null;
    }

    //private bool IsHighContrastModeOn()
    //{
    //    var highContrastIsOn = (Spi.Instance.GetHighContrast() & Spi.HighContrastOptions.HCF_HIGHCONTRASTON) != 0;
    //    return highContrastIsOn;
    //}

    #region Tray Button (Native Window)

    private class TrayButtonNativeWindow : System.Windows.Forms.NativeWindow, IDisposable
    {
        private TrayButton _owner;

        private IntPtr _tooltipWindowHandle = IntPtr.Zero;
        private IntPtr _iconHandle = IntPtr.Zero;

        private string? _tooltipText = null;
        private bool _tooltipInfoAdded = false;

        private System.Threading.Timer? _trayButtonPositionCheckupTimer;
        private int _trayButtonPositionCheckupTimerCounter = 0;

        [Flags]
        private enum TrayButtonVisualStateFlags
        {
            None = 0,
            Hover = 1,
            LeftButtonPressed = 2,
            RightButtonPressed = 4
        }
        private TrayButtonVisualStateFlags _visualState = TrayButtonVisualStateFlags.None;

        private Morphic.Controls.TrayButton.Windows10.WindowsNative.MouseWindowMessageHook? _mouseHook = null;

        private System.Drawing.Rectangle _trayButtonPositionAndSize;
        public System.Drawing.Rectangle PositionAndSize
        {
            get
            {
                return _trayButtonPositionAndSize;
            }
        }

        internal TrayButtonNativeWindow(TrayButton owner)
        {
            _owner = owner;
        }

        public void Initialize(IntPtr taskbarHandle)
        {
            const string nativeWindowClassName = "Morphic-TrayButton";

            // register our custom native window class
            var pointerToWndProcCallback = Marshal.GetFunctionPointerForDelegate(new PInvokeExtensions.WndProc(this.WndProcCallback));
            var lpWndClass = new PInvokeExtensions.WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf(typeof(PInvokeExtensions.WNDCLASSEX)),
                lpfnWndProc = pointerToWndProcCallback,
                lpszClassName = nativeWindowClassName,
                hCursor = LegacyWindowsApi.LoadCursor(IntPtr.Zero, (int)LegacyWindowsApi.Cursors.IDC_ARROW)
            };

            var registerClassResult = PInvokeExtensions.RegisterClassEx(ref lpWndClass);
            if (registerClassResult == 0)
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }

            var windowParams = new System.Windows.Forms.CreateParams();
            windowParams.ExStyle = (int)LegacyWindowsApi.WindowStylesEx.WS_EX_TOOLWINDOW;
            /* NOTE: as we want to be able to ensure that we're referencing the exact class we just registered, we pass the RegisterClassEx results into the 
                 * CreateWindow function (and we encode that result as a ushort here in a proprietary way) */
            windowParams.ClassName = registerClassResult.ToString(); // nativeWindowClassName;
                                                                     //windowParams.Caption = nativeWindowClassName;
            windowParams.Style = (int)(LegacyWindowsApi.WindowStyles.WS_VISIBLE | LegacyWindowsApi.WindowStyles.WS_CHILD | LegacyWindowsApi.WindowStyles.WS_CLIPSIBLINGS | LegacyWindowsApi.WindowStyles.WS_TABSTOP);
            windowParams.X = 0;
            windowParams.Y = 0;
            windowParams.Width = 32;
            windowParams.Height = 40;
            windowParams.Parent = taskbarHandle;
            //
            // NOTE: CreateHandle can throw InvalidOperationException, OutOfMemoryException, or System.ComponentModel.Win32Exception
            this.CreateHandle(windowParams);

            // create the tooltip window (although we won't provide it with any actual text until/unless the text is set
            this.CreateTooltipWindow();

            // subscribe to display settings changes (so that we know when the screen resolution changes, so that we can reposition our button)
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;

            // if the user is using Windows 11, create a mouse message hook (so we can capture the mousemove and click events over our taskbar icon)
            if (Morphic.WindowsNative.OsVersion.OsVersion.IsWindows11OrLater() == true)
            {
                _mouseHook = new Morphic.Controls.TrayButton.Windows10.WindowsNative.MouseWindowMessageHook();
                _mouseHook.WndProcEvent += _mouseHook_WndProcEvent;
            }

            // position the tray button in its initial position
            // NOTE: the button has no icon at this point; if we want to move this logic to the Icon set routine, 
            //       that's reasonable, but we'd need to think through any side-effects (and we'd need to do this here anyway
            //       if an icon had already been set prior to .Initialize being called)
            //if (_iconHandle != IntPtr.Zero)
            //{
            this.PositionTrayButton();
            //}
        }

        // NOTE: this function is somewhat redundant and is provided to support Windows 11; we should refactor all of this code to handle window messages centrally
        private void _mouseHook_WndProcEvent(object? sender, Morphic.Controls.TrayButton.Windows10.WindowsNative.MouseWindowMessageHook.WndProcEventArgs e)
        {
            // TODO: we should ensure that calls are queued and then called from a sequential thread (ideally a UI dispatch thread)
            switch ((LegacyWindowsApi.WindowMessage)e.Message)
            {
                case LegacyWindowsApi.WindowMessage.WM_LBUTTONDOWN:
                    _visualState |= TrayButtonVisualStateFlags.LeftButtonPressed;
                    this.RequestRedraw();
                    break;
                case LegacyWindowsApi.WindowMessage.WM_LBUTTONUP:
                    _visualState &= ~TrayButtonVisualStateFlags.LeftButtonPressed;
                    this.RequestRedraw();
                    {
                        var mouseArgs = new System.Windows.Forms.MouseEventArgs(System.Windows.Forms.MouseButtons.Left, 1, e.X, e.Y, 0);
                        _owner.MouseUp?.Invoke(_owner, mouseArgs);
                    }
                    break;
                case LegacyWindowsApi.WindowMessage.WM_MOUSELEAVE:
                    // the cursor has left our tray button's window area; remove the hover state from our visual state
                    _visualState &= ~TrayButtonVisualStateFlags.Hover;
                    // NOTE: as we aren't able to track mouseup when the cursor is outside of the button, we also remove the left/right button pressed states here
                    //       (and then we check them again when the mouse moves back over the button)
                    _visualState &= ~TrayButtonVisualStateFlags.LeftButtonPressed;
                    _visualState &= ~TrayButtonVisualStateFlags.RightButtonPressed;
                    this.RequestRedraw();
                    break;
                case LegacyWindowsApi.WindowMessage.WM_MOUSEMOVE:
                    // NOTE: this message is raised while we are tracking (whereas the SETCURSOR WM_MOUSEMOVE is captured when the mouse cursor first enters the window)
                    //
                    // NOTE: if the cursor moves off of the tray button while the button is pressed, we remove the "pressed" focus as well as the "hover" focus because
                    //       we aren't able to track mouseup when the cursor is outside of the button; consequently we also need to check the mouse pressed state during
                    //       mousemove so that we can re-enable the pressed state if/where appropriate.
                    if (((_visualState & TrayButtonVisualStateFlags.LeftButtonPressed) == 0))
                    {
                        _visualState |= TrayButtonVisualStateFlags.LeftButtonPressed;
                        this.RequestRedraw();
                    }
                    if (((_visualState & TrayButtonVisualStateFlags.RightButtonPressed) == 0))
                    {
                        _visualState |= TrayButtonVisualStateFlags.RightButtonPressed;
                        this.RequestRedraw();
                    }
                    //
                    break;
                case LegacyWindowsApi.WindowMessage.WM_RBUTTONDOWN:
                    _visualState |= TrayButtonVisualStateFlags.RightButtonPressed;
                    this.RequestRedraw();
                    break;
                case LegacyWindowsApi.WindowMessage.WM_RBUTTONUP:
                    _visualState &= ~TrayButtonVisualStateFlags.RightButtonPressed;
                    this.RequestRedraw();
                    {
                        var mouseArgs = new System.Windows.Forms.MouseEventArgs(System.Windows.Forms.MouseButtons.Right, 1, e.X, e.Y, 0);
                        _owner.MouseUp?.Invoke(_owner, mouseArgs);
                    }
                    break;
            }
        }

        internal void SetText(string? text)
        {
            _tooltipText = text;
            this.UpdateTooltipTextAndTracking();
        }

        private void CreateTooltipWindow()
        {
            if (_tooltipWindowHandle != IntPtr.Zero)
            {
                // tooltip window already exists
                return;
            }

            _tooltipWindowHandle = LegacyWindowsApi.CreateWindowEx(
                 0 /* no styles */,
                 PInvokeExtensions.TOOLTIPS_CLASS,
                 null,
                 LegacyWindowsApi.WindowStyles.WS_POPUP | (LegacyWindowsApi.WindowStyles)PInvokeExtensions.TTS_ALWAYSTIP,
                 PInvokeExtensions.CW_USEDEFAULT,
                 PInvokeExtensions.CW_USEDEFAULT,
                 PInvokeExtensions.CW_USEDEFAULT,
                 PInvokeExtensions.CW_USEDEFAULT,
                 this.Handle,
                 IntPtr.Zero,
                 IntPtr.Zero,
                 IntPtr.Zero);

            if (_tooltipWindowHandle == IntPtr.Zero)
            {
                Debug.Assert(false, "Could not create tooltip window");
            }

            this.UpdateTooltipTextAndTracking();
        }

        private void DestroyTooltipWindow()
        {
            // set the tooltip text to empty (so that UpdateTooltipText will clear out the tooltip), then update the tooltip text.
            _tooltipText = null;
            this.UpdateTooltipTextAndTracking();

            LegacyWindowsApi.DestroyWindow(_tooltipWindowHandle);
            _tooltipWindowHandle = IntPtr.Zero;
        }

        private void UpdateTooltipTextAndTracking()
        {
            if (_tooltipWindowHandle == IntPtr.Zero)
            {
                // tooltip window does not exist; failed; abort
                Debug.Assert(false, "Tooptip window does not exist; if this is an expected failure, remove this assert.");
                return;
            }

            PInvoke.RECT trayButtonClientRect;
            var getClientRectSuccess = PInvoke.User32.GetClientRect(this.Handle, out trayButtonClientRect);
            if (getClientRectSuccess == false)
            {
                // failed; abort
                Debug.Assert(false, "Could not get client rect for tray button; could not set up tooltip");
                return;
            }

            var toolinfo = new PInvokeExtensions.TOOLINFO();
            toolinfo.cbSize = (uint)Marshal.SizeOf(toolinfo);
            toolinfo.hwnd = this.Handle;
            toolinfo.uFlags = PInvokeExtensions.TTF_SUBCLASS;
            toolinfo.lpszText = _tooltipText;
            toolinfo.uId = unchecked((nuint)(nint)this.Handle); // unique identifier (for adding/deleting the tooltip)
            toolinfo.rect = trayButtonClientRect;
            //
            var pointerToToolinfo = Marshal.AllocHGlobal(Marshal.SizeOf(toolinfo));
            try
            {
                Marshal.StructureToPtr(toolinfo, pointerToToolinfo, false);
                if (toolinfo.lpszText is not null)
                {
                    if (_tooltipInfoAdded == false)
                    {
                        _ = LegacyWindowsApi.SendMessage(_tooltipWindowHandle, (int)PInvokeExtensions.TTM_ADDTOOL, 0, pointerToToolinfo);
                        _tooltipInfoAdded = true;
                    }
                    else
                    {
                        // delete and re-add the tooltipinfo; this will update all the info (including the text and tracking rect)
                        _ = LegacyWindowsApi.SendMessage(_tooltipWindowHandle, (int)PInvokeExtensions.TTM_DELTOOL, 0, pointerToToolinfo);
                        _ = LegacyWindowsApi.SendMessage(_tooltipWindowHandle, (int)PInvokeExtensions.TTM_ADDTOOL, 0, pointerToToolinfo);
                    }
                }
                else
                {
                    // NOTE: we might technically call "deltool" even when a tooltipinfo was already removed
                    _ = LegacyWindowsApi.SendMessage(_tooltipWindowHandle, (int)PInvokeExtensions.TTM_DELTOOL, 0, pointerToToolinfo);
                    _tooltipInfoAdded = false;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pointerToToolinfo);
            }
        }

        // NOTE: intial creation events are captured by this callback, but afterwards window messages are captured by WndProc instead
        private IntPtr WndProcCallback(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            switch ((LegacyWindowsApi.WindowMessage)msg)
            {
                case LegacyWindowsApi.WindowMessage.WM_CREATE:
                    if (LegacyWindowsApi.BufferedPaintInit() != LegacyWindowsApi.S_OK)
                    {
                        // failed; abort
                        Debug.Assert(false, "Could not initialize buffered paint");
                        return new IntPtr(-1); // abort window creation process
                    }
                    break;
                default:
                    break;
            }

            // pass all non-handled messages through to DefWindowProc
            return LegacyWindowsApi.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        // NOTE: the built-in CreateHandle function couldn't handle our custom class, so we have overridden CreateHandle and are calling CreateWindowEx ourselves
        public override void CreateHandle(System.Windows.Forms.CreateParams cp)
        {
            // NOTE: if cp.ClassName is a string parseable as a (UInt16) number, convert that value to an IntPtr; otherwise capture a pointer to the string
            IntPtr classNameAsIntPtr;
            bool mustReleaseClassNameAsIntPtr = false;
            //
            ushort classNameAsUInt16 = 0;
            if (ushort.TryParse(cp.ClassName, out classNameAsUInt16) == true)
            {
                classNameAsIntPtr = (IntPtr)classNameAsUInt16;
                mustReleaseClassNameAsIntPtr = false;
            }
            else
            {
                classNameAsIntPtr = Marshal.StringToHGlobalUni(cp.ClassName);
                mustReleaseClassNameAsIntPtr = true;
            }

            // TODO: in some circumstances, it is possible that we are unable to create our window; consider creating a retry mechanism (dealing with async) or notify our caller
            try
            {
                var handle = PInvokeExtensions.CreateWindowEx(
                     (PInvoke.User32.WindowStylesEx)cp.ExStyle,
                     classNameAsIntPtr,
                     cp.Caption,
                     (PInvoke.User32.WindowStyles)cp.Style,
                     cp.X,
                     cp.Y,
                     cp.Width,
                     cp.Height,
                     cp.Parent,
                     IntPtr.Zero,
                     IntPtr.Zero,
                     IntPtr.Zero
                     );

                // NOTE: in our testing, handle was sometimes IntPtr.Zero here (in which case the tray icon button's window will not exist)
                if (handle == IntPtr.Zero)
                {
                    Debug.Assert(false, "Could not create tray button window handle");
                }

                this.AssignHandle(handle);
            }
            finally
            {
                if (mustReleaseClassNameAsIntPtr == true)
                {
                    Marshal.FreeHGlobal(classNameAsIntPtr);
                }
            }
        }

        public void Dispose()
        {
            // TODO: if we are the topmost/leftmost next-to-tray-icon button, we should expand the task button container so it takes up our now-unoccupied space

            if (_mouseHook is not null)
            {
                _mouseHook.Dispose();
            }

            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;

            this.DestroyTooltipWindow();
            this.DestroyHandle();
        }

        protected override void WndProc(ref System.Windows.Forms.Message m)
        {
            var uMsg = (uint)m.Msg;

            IntPtr? result = null;

            switch ((LegacyWindowsApi.WindowMessage)uMsg)
            {
                case LegacyWindowsApi.WindowMessage.WM_DESTROY:
                    /* TODO: trace to see if WM_DESTROY is actually called here; if not, then we should place the uninit in dispose instead; we might also consider
                         *       not using BufferedPaintInit/UnInit at all (although that _might_ slow down our buffered painting execution a tiny bit) */
                    LegacyWindowsApi.BufferedPaintUnInit();
                    break;
                case LegacyWindowsApi.WindowMessage.WM_DISPLAYCHANGE:
                    // screen resolution has changed: reposition the tray button
                    // NOTE: m.wParam contains bit depth
                    // NOTE: m.lParam contains the resolutions of the screen (horizontal resolution in low-order word; vertical resolution in high-order word)
                    this.PositionTrayButton();
                    break;
                case LegacyWindowsApi.WindowMessage.WM_ERASEBKGND:
                    // we will handle erasing the background, so return a non-zero value here
                    result = new IntPtr(1);
                    break;
                case LegacyWindowsApi.WindowMessage.WM_LBUTTONUP:
                    _visualState &= ~TrayButtonVisualStateFlags.LeftButtonPressed;
                    this.RequestRedraw();
                    {
                        var hitPoint = this.ConvertMouseMessageLParamToScreenPoint(m.LParam);
                        if (hitPoint is null)
                        {
                            // failed; abort
                            Debug.Assert(false, "Could not map tray button hit point to screen coordinates");
                            break;
                        }
                        var mouseArgs = new System.Windows.Forms.MouseEventArgs(System.Windows.Forms.MouseButtons.Left, 1, hitPoint.Value.X, hitPoint.Value.Y, 0);
                        _owner.MouseUp?.Invoke(_owner, mouseArgs);
                    }
                    result = new IntPtr(0);
                    break;
                case LegacyWindowsApi.WindowMessage.WM_MOUSEACTIVATE:
                    // do not activate our window (and discard this message)
                    result = new IntPtr(LegacyWindowsApi.MA_NOACTIVATEANDEAT);
                    break;
                case LegacyWindowsApi.WindowMessage.WM_MOUSELEAVE:
                    // the cursor has left our tray button's window area; remove the hover state from our visual state
                    _visualState &= ~TrayButtonVisualStateFlags.Hover;
                    // NOTE: as we aren't able to track mouseup when the cursor is outside of the button, we also remove the left/right button pressed states here
                    //       (and then we check them again when the mouse moves back over the button)
                    _visualState &= ~TrayButtonVisualStateFlags.LeftButtonPressed;
                    _visualState &= ~TrayButtonVisualStateFlags.RightButtonPressed;
                    this.RequestRedraw();
                    result = new IntPtr(0);
                    break;
                case LegacyWindowsApi.WindowMessage.WM_MOUSEMOVE:
                    // NOTE: this message is raised while we are tracking (whereas the SETCURSOR WM_MOUSEMOVE is captured when the mouse cursor first enters the window)
                    //
                    // NOTE: if the cursor moves off of the tray button while the button is pressed, we remove the "pressed" focus as well as the "hover" focus because
                    //       we aren't able to track mouseup when the cursor is outside of the button; consequently we also need to check the mouse pressed state during
                    //       mousemove so that we can re-enable the pressed state if/where appropriate.
                    if (((_visualState & TrayButtonVisualStateFlags.LeftButtonPressed) == 0) && ((m.WParam.ToInt64() & LegacyWindowsApi.MK_LBUTTON) != 0))
                    {
                        _visualState |= TrayButtonVisualStateFlags.LeftButtonPressed;
                        this.RequestRedraw();
                    }
                    if (((_visualState & TrayButtonVisualStateFlags.RightButtonPressed) == 0) && ((m.WParam.ToInt64() & LegacyWindowsApi.MK_RBUTTON) != 0))
                    {
                        _visualState |= TrayButtonVisualStateFlags.RightButtonPressed;
                        this.RequestRedraw();
                    }
                    //
                    result = new IntPtr(0);
                    break;
                case LegacyWindowsApi.WindowMessage.WM_NCHITTEST:
                    var hitTestX = (short)((m.LParam.ToInt64() >> 0) & 0xFFFF);
                    var hitTestY = (short)((m.LParam.ToInt64() >> 16) & 0xFFFF);
                    //
                    LegacyWindowsApi.RECT trayButtonRectInScreenCoordinates;
                    if (LegacyWindowsApi.GetWindowRect(this.Handle, out trayButtonRectInScreenCoordinates) == false)
                    {
                        // fail; abort
                        Debug.Assert(false, "Could not get rect of tray button in screen coordinates");
                        return;
                    }
                    //
                    if ((hitTestX >= trayButtonRectInScreenCoordinates.Left) && (hitTestX < trayButtonRectInScreenCoordinates.Right) &&
                              (hitTestY >= trayButtonRectInScreenCoordinates.Top) && (hitTestY < trayButtonRectInScreenCoordinates.Bottom))
                    {
                        // inside client area
                        result = new IntPtr(1); // HTCLIENT
                    }
                    else
                    {
                        // nowhere
                        // TODO: determine if there is another response we should be returning instead; the documentation is not clear in this regard
                        result = new IntPtr(0); // HTNOWHERE
                    }
                    break;
                case LegacyWindowsApi.WindowMessage.WM_NCPAINT:
                    // no non-client (frame) area to paint
                    result = new IntPtr(0);
                    break;
                case LegacyWindowsApi.WindowMessage.WM_PAINT:
                    this.Paint(m.HWnd);
                    result = new IntPtr(0);
                    break;
                case LegacyWindowsApi.WindowMessage.WM_RBUTTONUP:
                    _visualState &= ~TrayButtonVisualStateFlags.RightButtonPressed;
                    this.RequestRedraw();
                    {
                        var hitPoint = this.ConvertMouseMessageLParamToScreenPoint(m.LParam);
                        if (hitPoint is null)
                        {
                            // failed; abort
                            Debug.Assert(false, "Could not map tray button hit point to screen coordinates");
                            break;
                        }
                        var mouseArgs = new System.Windows.Forms.MouseEventArgs(System.Windows.Forms.MouseButtons.Right, 1, hitPoint.Value.X, hitPoint.Value.Y, 0);
                        _owner.MouseUp?.Invoke(_owner, mouseArgs);
                    }
                    result = new IntPtr(0);
                    break;
                case LegacyWindowsApi.WindowMessage.WM_SETCURSOR:
                    // wParam: window handle
                    // lParam: low-order word is the high-test result for the cursor position; high-order word specifies the mouse message that triggered this event
                    var hitTestResult = (uint)((m.LParam.ToInt64() >> 0) & 0xFFFF);
                    var mouseMsg = (uint)((m.LParam.ToInt64() >> 16) & 0xFFFF);
                    switch ((LegacyWindowsApi.WindowMessage)mouseMsg)
                    {
                        case LegacyWindowsApi.WindowMessage.WM_LBUTTONDOWN:
                            _visualState |= TrayButtonVisualStateFlags.LeftButtonPressed;
                            this.RequestRedraw();
                            result = new IntPtr(1);
                            break;
                        case LegacyWindowsApi.WindowMessage.WM_LBUTTONUP:
                            result = new IntPtr(1);
                            break;
                        case LegacyWindowsApi.WindowMessage.WM_MOUSEMOVE:
                            // if we are not yet tracking the mouse position (i.e. this is effectively "mouse enter") then do so now
                            if ((_visualState & TrayButtonVisualStateFlags.Hover) == 0)
                            {
                                // track mousehover (for tooltips) and mouseleave (to remove hover effect)
                                var eventTrack = new LegacyWindowsApi.TRACKMOUSEEVENT(LegacyWindowsApi.TMEFlags.TME_LEAVE, this.Handle, LegacyWindowsApi.HOVER_DEFAULT);
                                var trackMouseEventSuccess = LegacyWindowsApi.TrackMouseEvent(ref eventTrack);
                                if (trackMouseEventSuccess == false)
                                {
                                    // failed
                                    Debug.Assert(false, "Could not set up tracking of tray button window area");
                                    return;
                                }

                                _visualState |= TrayButtonVisualStateFlags.Hover;

                                this.RequestRedraw();
                            }
                            result = new IntPtr(1);
                            break;
                        case LegacyWindowsApi.WindowMessage.WM_RBUTTONDOWN:
                            _visualState |= TrayButtonVisualStateFlags.RightButtonPressed;
                            this.RequestRedraw();
                            result = new IntPtr(1);
                            break;
                        case LegacyWindowsApi.WindowMessage.WM_RBUTTONUP:
                            result = new IntPtr(1);
                            break;
                        default:
                            //Debug.WriteLine("UNHANDLED SETCURSOR Mouse Message: " + mouseMsg.ToString());
                            break;
                    }
                    break;
                case LegacyWindowsApi.WindowMessage.WM_SIZE:
                    result = new IntPtr(0);
                    break;
                case LegacyWindowsApi.WindowMessage.WM_WINDOWPOSCHANGED:
                    result = new IntPtr(0);
                    break;
                case LegacyWindowsApi.WindowMessage.WM_WINDOWPOSCHANGING:
                    // in this implementation, we don't do anything with this message; nothing to do here
                    result = new IntPtr(0);
                    break;
                default:
                    // unhandled message; this will be passed onto DefWindowProc instead
                    break;
            }

            if (result.HasValue == true)
            {
                m.Result = result.Value;
            }
            else
            {
                m.Result = LegacyWindowsApi.DefWindowProc(m.HWnd, (uint)m.Msg, m.WParam, m.LParam);
            }
        }

        private void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e)
        {
            // start a timer which will verify that the button is positioned properly (and will give up after a certain number of attempts)
            var checkupInterval = new TimeSpan(0, 0, 0, 0, 250);
            _trayButtonPositionCheckupTimerCounter = 40; // count down for 10 seconds (0.250 x 40)
            _trayButtonPositionCheckupTimer = new System.Threading.Timer(TrayButtonPositionCheckup, null, checkupInterval, checkupInterval);
        }
        private void TrayButtonPositionCheckup(object? state)
        {
            if (_trayButtonPositionCheckupTimerCounter <= 0)
            {
                _trayButtonPositionCheckupTimer?.Dispose();
                _trayButtonPositionCheckupTimer = null;
                return;
            }
            //
            _trayButtonPositionCheckupTimerCounter = Math.Max(_trayButtonPositionCheckupTimerCounter - 1, 0);

            // check the current and desired positions of the notify tray icon
            var calculateResult = this.CalculateCurrentAndTargetRectOfTrayButton();
            if (calculateResult is not null)
            {
                if (calculateResult.Value.changeToRect is not null)
                {
                    this.PositionTrayButton();
                }
            }
        }

        private LegacyWindowsApi.POINT? ConvertMouseMessageLParamToScreenPoint(IntPtr lParam)
        {
            var x = (ushort)((lParam.ToInt64() >> 0) & 0xFFFF);
            var y = (ushort)((lParam.ToInt64() >> 16) & 0xFFFF);
            // convert x and y to screen coordinates
            var hitPoint = new PInvoke.POINT { x = x, y = y };

            // NOTE: the instructions for MapWindowPoints instruct us to call SetLastError before calling MapWindowPoints to ensure that we can distinguish a result of 0 from an error if the last win32 error wasn't set (because it wasn't an error)
            Marshal.SetLastPInvokeError(0);
            //
            // NOTE: the PInvoke implementation of MapWindowPoints did not support passing in a POINT struct, so we manually declared the function
            var mapWindowPointsResult = PInvokeExtensions.MapWindowPoints(this.Handle, IntPtr.Zero, ref hitPoint, 1);
            if (mapWindowPointsResult == 0 && Marshal.GetLastWin32Error() != 0)
            {
                // failed; abort
                Debug.Assert(false, "Could not map tray button hit point to screen coordinates");
                return null;
            }

            var result = new LegacyWindowsApi.POINT(hitPoint.x, hitPoint.y);
            return result;
        }
        private void Paint(IntPtr hWnd)
        {
            LegacyWindowsApi.PAINTSTRUCT ps = new LegacyWindowsApi.PAINTSTRUCT();
            IntPtr paintDc = LegacyWindowsApi.BeginPaint(hWnd, out ps);
            try
            {
                IntPtr bufferedPaintDc;
                // NOTE: ps.rcPaint was an empty rect in our intiail tests, so we are using a manually-created clientRect (from GetClientRect) here instead
                var paintBufferHandle = LegacyWindowsApi.BeginBufferedPaint(ps.hdc, ref ps.rcPaint, LegacyWindowsApi.BP_BUFFERFORMAT.BPBF_TOPDOWNDIB, IntPtr.Zero, out bufferedPaintDc);
                try
                {
                    if (ps.rcPaint == LegacyWindowsApi.RECT.Empty)
                    {
                        // no rectangle; nothing to do
                        return;
                    }

                    // clear our buffer background (to ARGB(0,0,0,0))
                    var bufferedPaintClearSuccess = LegacyWindowsApi.BufferedPaintClear(paintBufferHandle, ref ps.rcPaint);
                    if (bufferedPaintClearSuccess != LegacyWindowsApi.S_OK)
                    {
                        // failed; abort
                        Debug.Assert(false, "Could not clear tray button's background");
                        return;
                    }

                    // if the user has pressed (mousedown) on our tray button or is hovering over it, highlight the tray button now
                    Double highlightOpacity = 0.0;
                    if (((_visualState & TrayButtonVisualStateFlags.LeftButtonPressed) != 0) ||
                              ((_visualState & TrayButtonVisualStateFlags.RightButtonPressed) != 0))
                    {
                        highlightOpacity = 0.25;
                    }
                    else if ((_visualState & TrayButtonVisualStateFlags.Hover) != 0)
                    {
                        highlightOpacity = 0.1;
                    }
                    //
                    if (highlightOpacity > 0.0)
                    {
                        this.DrawHighlightBackground(bufferedPaintDc, ps.rcPaint, System.Drawing.Color.White, highlightOpacity);
                    }

                    // calculate the size and position of our icon
                    int iconWidthAndHeight = this.CalculateWidthAndHeightForIcon(ps.rcPaint);
                    //
                    var xLeft = ((ps.rcPaint.Right - ps.rcPaint.Left) - iconWidthAndHeight) / 2;
                    var yTop = ((ps.rcPaint.Bottom - ps.rcPaint.Top) - iconWidthAndHeight) / 2;

                    if (_iconHandle != IntPtr.Zero && iconWidthAndHeight > 0)
                    {
                        var drawIconSuccess = LegacyWindowsApi.DrawIconEx(bufferedPaintDc, xLeft, yTop, _iconHandle, iconWidthAndHeight, iconWidthAndHeight, 0 /* not animated */, IntPtr.Zero /* no triple-buffering */, LegacyWindowsApi.DrawIconFlags.DI_NORMAL | LegacyWindowsApi.DrawIconFlags.DI_NOMIRROR);
                        if (drawIconSuccess == false)
                        {
                            // failed; abort
                            Debug.Assert(false, "Could not draw tray button's icon");
                            return;
                        }
                    }
                }
                finally
                {
                    LegacyWindowsApi.EndBufferedPaint(paintBufferHandle, true);
                }
            }
            finally
            {
                LegacyWindowsApi.EndPaint(hWnd, ref ps);
            }
        }

        private int CalculateWidthAndHeightForIcon(LegacyWindowsApi.RECT rect)
        {
            int result;
            // NOTE: we currently measure the size of our icon by measuring the size of the rectangle
            // NOTE: we use the larger of the two dimensions (height vs width) to determine our icon size; we may reconsider this in the future if we support non-square icons
            int largerDimensionLenth;
            if (rect.Bottom - rect.Top > rect.Right - rect.Left)
            {
                largerDimensionLenth = rect.Bottom - rect.Top;
            }
            else
            {
                largerDimensionLenth = rect.Right - rect.Left;
            }
            //
            if (largerDimensionLenth >= 48)
            {
                result = 32;
            }
            else if (largerDimensionLenth >= 36)
            {
                result = 24;
            }
            else if (largerDimensionLenth >= 30)
            {
                result = 20;
            }
            else if (largerDimensionLenth >= 24)
            {
                result = 16;
            }
            else
            {
                result = 0;
            }

            return result;
        }

        private void DrawHighlightBackground(IntPtr hdc, LegacyWindowsApi.RECT rect, System.Drawing.Color color, Double opacity)
        {
            // GDI doesn't have a concept of semi-transparent pixels - the only function that honours them is AlphaBlend.
            // Create a bitmap containing a single pixel - and then use AlphaBlend to stretch it to the size of the rect.

            // set up the 1x1 pixel bitmap's configuration
            var pixelBitmapInfo = new LegacyWindowsApi.BITMAPINFO();
            pixelBitmapInfo.bmiHeader = new LegacyWindowsApi.BITMAPINFOHEADER()
            {
                biWidth = 1,
                biHeight = 1,
                biPlanes = 1, // must be 1
                biBitCount = 32, // maximum of 2^32 colors
                biCompression = LegacyWindowsApi.BitmapCompressionType.BI_RGB,
                biSizeImage = 0,
                biClrUsed = 0,
                biClrImportant = 0
            };
            pixelBitmapInfo.bmiHeader.biSize = (uint)Marshal.SizeOf(pixelBitmapInfo.bmiHeader);
            pixelBitmapInfo.bmiColors = new LegacyWindowsApi.RGBQUAD[1];

            // calculate the pixel color as a uint32 (in AARRGGBB order)
            uint pixelColor = (
                 (((uint)color.A) << 24) | // NOTE: we ignore the alpha value in our call to AlphaBlend
                 (((uint)color.R) << 16) |
                 (((uint)color.G) << 8) |
                 (((uint)color.B) << 0));

            // create the memory device context for the pixel
            var pixelDc = LegacyWindowsApi.CreateCompatibleDC(hdc);
            if (pixelDc == IntPtr.Zero)
            {
                // failed; abort
                Debug.Assert(false, "Could not create device context for highlight pixel.");
                return;
            }
            try
            {
                IntPtr pixelDibBitValues;
                var pixelDibHandle = LegacyWindowsApi.CreateDIBSection(pixelDc, ref pixelBitmapInfo, LegacyWindowsApi.DIB_RGB_COLORS, out pixelDibBitValues, IntPtr.Zero, 0);
                if (pixelDibHandle == IntPtr.Zero)
                {
                    // failed; abort
                    Debug.Assert(false, "Could not create DIB for highlight pixel.");
                    return;
                }
                //
                try
                {
                    var selectedBitmapHandle = LegacyWindowsApi.SelectObject(pixelDc, pixelDibHandle);
                    if (selectedBitmapHandle == IntPtr.Zero)
                    {
                        // failed; abort
                        Debug.Assert(false, "Could not select object into the pixel device context.");
                        return;
                    }
                    try
                    {
                        // write over the single pixel's value (with the passed-in pixel)
                        Marshal.WriteIntPtr(pixelDibBitValues, new IntPtr(pixelColor));

                        // draw the highlight (stretching the pixel to the full rectangle size)
                        LegacyWindowsApi.BLENDFUNCTION blendFunction = new LegacyWindowsApi.BLENDFUNCTION()
                        {
                            BlendOp = (byte)LegacyWindowsApi.AC_SRC_OVER,
                            BlendFlags = 0, // must be zero
                            SourceConstantAlpha = (byte)(opacity * 255), // the requested opacity level
                            AlphaFormat = 0
                        };
                        var RESULT_TO_USE = LegacyWindowsApi.AlphaBlend(hdc, rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top, pixelDc, 0, 0, 1, 1, blendFunction);
                    }
                    finally
                    {
                        _ = LegacyWindowsApi.SelectObject(pixelDc, selectedBitmapHandle);
                    }
                }
                finally
                {
                    _ = LegacyWindowsApi.DeleteObject(pixelDibHandle);
                }
            }
            finally
            {
                _ = LegacyWindowsApi.DeleteDC(pixelDc);
            }
        }

        public void SetIcon(System.Drawing.Icon? icon)
        {
            if (icon is not null)
            {
                _iconHandle = icon.Handle;
            }
            else
            {
                _iconHandle = IntPtr.Zero;
            }

            // TODO: if we support non-square icons, then reposition the tray button based on the new dimensions of the icon (in case it's wider/narrower)
            //this.PositionTrayButton();

            // trigger a redraw
            this.RequestRedraw();
        }

        private void PositionTrayButton()
        {
            var trayButtonRects = CalculateCurrentAndTargetRectOfTrayButton();
            if (trayButtonRects is null)
            {
                // fail; abort
                Debug.Assert(false, "Could not calculate current and/or new rects for tray button");
                return;
            }
            //
            var currentRect = trayButtonRects.Value.currentRect;
            var changeToRect = trayButtonRects.Value.changeToRect;
            var taskbarOrientation = trayButtonRects.Value.orientation;

            if (_mouseHook is not null)
            {
                // update our tracking region to track the new position (unless we haven't moved, in which case continue to track our current position)
                if (changeToRect is not null)
                {
                    _mouseHook.UpdateTrackingRegion(changeToRect.Value.ToPInvokeRect());
                }
                else if (currentRect is not null)
                {
                    _mouseHook.UpdateTrackingRegion(currentRect.Value.ToPInvokeRect());
                }
                else
                {
                    Debug.Assert(false, "Could not determine current RECT of tray button");
                }
            }

            // if changeToRect is more leftmost/topmost than the task button container's right side, then shrink the task button container appropriately
            LegacyWindowsApi.RECT? newTaskButtonContainerRect = null;
            if (changeToRect is not null)
            {
                var taskbarTripletHandles = this.GetTaskbarTripletHandles();
                var taskbarTripletRects = this.GetTaskbarTripletRects(taskbarTripletHandles.TaskbarHandle, taskbarTripletHandles.TaskButtonContainerHandle, taskbarTripletHandles.NotifyTrayHandle);
                if (taskbarTripletRects is null)
                {
                    // failed; abort
                    Debug.Assert(false, "could not get rects of taskbar or its important children");
                    return;
                }
                var taskButtonContainerRect = taskbarTripletRects.Value.TaskButtonContainerRect;

                if ((taskbarOrientation == System.Windows.Forms.Orientation.Horizontal) && (taskButtonContainerRect.Right > changeToRect.Value.Left))
                {
                    newTaskButtonContainerRect = new LegacyWindowsApi.RECT(new System.Windows.Rect(
                         taskButtonContainerRect.Left,
                         taskButtonContainerRect.Top,
                         Math.Max(taskButtonContainerRect.Right - taskButtonContainerRect.Left - (taskButtonContainerRect.Right - changeToRect.Value.Left), 0),
                         taskButtonContainerRect.Bottom - taskButtonContainerRect.Top
                         ));
                }
                else if ((taskbarOrientation == System.Windows.Forms.Orientation.Vertical) && taskButtonContainerRect.Bottom > changeToRect.Value.Top)
                {
                    newTaskButtonContainerRect = new LegacyWindowsApi.RECT(new System.Windows.Rect(
                         taskButtonContainerRect.Left,
                         taskButtonContainerRect.Top,
                         taskButtonContainerRect.Right - taskButtonContainerRect.Left,
                         taskButtonContainerRect.Bottom - taskButtonContainerRect.Top - Math.Max(taskButtonContainerRect.Bottom - changeToRect.Value.Top, 0)
                         ));
                }
            }
            //
            if (newTaskButtonContainerRect is not null)
            {
                var taskButtonContainerHandle = TrayButtonNativeWindow.FindWindowsTaskbarTaskButtonContainerHandle();

                // shrink the task button container
                // NOTE: this is a blocking call, waiting until the task button container is resized; we do this intentionally so that we see its updated size synchronously
                var repositionTaskButtonContainerSuccess = LegacyWindowsApi.SetWindowPos(
                     taskButtonContainerHandle,
                     IntPtr.Zero,
                     newTaskButtonContainerRect.Value.Left,
                     newTaskButtonContainerRect.Value.Top,
                     newTaskButtonContainerRect.Value.Right - newTaskButtonContainerRect.Value.Left,
                     newTaskButtonContainerRect.Value.Bottom - newTaskButtonContainerRect.Value.Top,
                          LegacyWindowsApi.SetWindowPosFlags.SWP_NOACTIVATE /* do not activate the window */ |
                          LegacyWindowsApi.SetWindowPosFlags.SWP_NOMOVE /* retain the current x and y position, out of an abundance of caution */ |
                          LegacyWindowsApi.SetWindowPosFlags.SWP_NOZORDER /* retain the current Z order (ignoring the hWndInsertAfter parameter) */
                     );

                if (repositionTaskButtonContainerSuccess == false)
                {
                    // failed; abort
                    Debug.Assert(false, "Could not resize taskbar's task button container");
                    return;
                }

                // capture our control's native window's new position and size
                // NOTE: since we suppressed repositioning of the taskbar container above (i.e. just resizing it), we are only capturing the updated size here (out of an abundance of caution)
                _trayButtonPositionAndSize.Width = newTaskButtonContainerRect!.Value.Right - newTaskButtonContainerRect!.Value.Left;
                _trayButtonPositionAndSize.Height = newTaskButtonContainerRect!.Value.Bottom - newTaskButtonContainerRect!.Value.Top;
            }

            // if our button needs to move (either because we don't know the old RECT or because the new RECT is different), do so now
            if (changeToRect is not null)
            {
                if (currentRect.HasValue == false || (currentRect.Value != changeToRect.Value))
                {
                    var taskbarHandle = TrayButtonNativeWindow.FindWindowsTaskbarHandle();

                    // convert our tray button's position from desktop coordinates to "child" coordinates within the taskbar
                    PInvoke.RECT childRect = new() { left = changeToRect.Value.Left, top = changeToRect.Value.Top, right = changeToRect.Value.Right, bottom = changeToRect.Value.Bottom };
                    var mapWindowPointsResult = PInvokeExtensions.MapWindowPoints(IntPtr.Zero /* use screen coordinates */, taskbarHandle, ref childRect, 2 /* 2 indicates that lpPoints is a RECT */);
                    if (mapWindowPointsResult == 0 && Marshal.GetLastWin32Error() != LegacyWindowsApi.ERROR_SUCCESS)
                    {
                        // failed; abort
                        Debug.Assert(false, "Could not map tray button RECT points to taskbar window handle");
                        return;
                    }

                    var repositionTrayButtonSuccess = LegacyWindowsApi.SetWindowPos(
                         this.Handle,
                         LegacyWindowsApi.HWND_TOP,
                         childRect.left,
                         childRect.top,
                         childRect.right - childRect.left,
                         childRect.bottom - childRect.top,
                         LegacyWindowsApi.SetWindowPosFlags.SWP_NOACTIVATE /* do not activate the window */ |
                         LegacyWindowsApi.SetWindowPosFlags.SWP_SHOWWINDOW /* display the tray button */
                         );

                    if (repositionTrayButtonSuccess == false)
                    {
                        // failed; abort
                        Debug.Assert(false, "Could not reposition and/or resize tray button");
                        return;
                    }

                    // capture our control's native window's new position and size
                    _trayButtonPositionAndSize = new(childRect.left, childRect.top, childRect.right - childRect.left, childRect.bottom - childRect.top);
                }

                // as we have moved/resized, request a repaint
                this.RequestRedraw();

                // if we have tooltip text, update its tracking rectangle
                if (_tooltipText is not null)
                {
                    UpdateTooltipTextAndTracking();
                }
            }
        }

        private (IntPtr TaskbarHandle, IntPtr TaskButtonContainerHandle, IntPtr NotifyTrayHandle) GetTaskbarTripletHandles()
        {
            var taskbarHandle = TrayButtonNativeWindow.FindWindowsTaskbarHandle();
            var taskButtonContainerHandle = TrayButtonNativeWindow.FindWindowsTaskbarTaskButtonContainerHandle();
            var notifyTrayHandle = TrayButtonNativeWindow.FindWindowsTaskbarNotificationTrayHandle();

            return (taskbarHandle, taskButtonContainerHandle, notifyTrayHandle);
        }

        private (LegacyWindowsApi.RECT TaskbarRect, LegacyWindowsApi.RECT TaskButtonContainerRect, LegacyWindowsApi.RECT NotifyTrayRect)? GetTaskbarTripletRects(IntPtr taskbarHandle, IntPtr taskButtonContainerHandle, IntPtr notifyTrayHandle)
        {
            // find the taskbar and its rect
            LegacyWindowsApi.RECT taskbarRect = new LegacyWindowsApi.RECT();
            if (LegacyWindowsApi.GetWindowRect(taskbarHandle, out taskbarRect) == false)
            {
                // failed; abort
                Debug.Assert(false, "Could not obtain window handle to taskbar.");
                return null;
            }

            // find the window handles and rects of the task button container and the notify tray (which are children inside of the taskbar)
            //
            LegacyWindowsApi.RECT taskButtonContainerRect = new LegacyWindowsApi.RECT();
            if (LegacyWindowsApi.GetWindowRect(taskButtonContainerHandle, out taskButtonContainerRect) == false)
            {
                // failed; abort
                Debug.Assert(false, "Could not obtain window handle to taskbar's task button list container.");
                return null;
            }
            //
            LegacyWindowsApi.RECT notifyTrayRect = new LegacyWindowsApi.RECT();
            if (LegacyWindowsApi.GetWindowRect(notifyTrayHandle, out notifyTrayRect) == false)
            {
                // failed; abort
                Debug.Assert(false, "Could not obtain window handle to taskbar's notify tray.");
                return null;
            }

            return (taskbarRect, taskButtonContainerRect, notifyTrayRect);
        }

        private (LegacyWindowsApi.RECT availableAreaRect, List<LegacyWindowsApi.RECT> childRects) CalculateEmptyRectsBetweenTaskButtonContainerAndNotifyTray(IntPtr taskbarHandle, System.Windows.Forms.Orientation taskbarOrientation, bool isRightToLeft, LegacyWindowsApi.RECT taskbarRect, LegacyWindowsApi.RECT taskButtonContainerRect, LegacyWindowsApi.RECT notifyTrayRect)
        {
            // calculate the total "free area" rectangle (the area between the task button container and the notify tray where we want to place our tray button)
            LegacyWindowsApi.RECT freeAreaAvailableRect;
            if (taskbarOrientation == System.Windows.Forms.Orientation.Horizontal)
            {
                if (isRightToLeft == false)
                {
                    freeAreaAvailableRect = new LegacyWindowsApi.RECT(new System.Windows.Rect(taskButtonContainerRect.Right, taskbarRect.Top, Math.Max(notifyTrayRect.Left - taskButtonContainerRect.Right, 0), Math.Max(taskbarRect.Bottom - taskbarRect.Top, 0)));
                }
                else
                {
                    freeAreaAvailableRect = new LegacyWindowsApi.RECT(new System.Windows.Rect(notifyTrayRect.Right, taskbarRect.Top, Math.Max(taskButtonContainerRect.Left - notifyTrayRect.Right, 0), Math.Max(taskbarRect.Bottom - taskbarRect.Top, 0)));
                }
            }
            else
            {
                freeAreaAvailableRect = new LegacyWindowsApi.RECT(new System.Windows.Rect(taskbarRect.Left, taskButtonContainerRect.Bottom, Math.Max(taskbarRect.Right - taskbarRect.Left, 0), Math.Max(notifyTrayRect.Top - taskButtonContainerRect.Bottom, 0)));
            }

            // capture a list of all child windows within the taskbar; we'll use this list to enumerate the rects of all the taskbar's children
            var taskbarChildHandles = TrayButtonNativeWindow.EnumerateChildWindows(taskbarHandle);
            //
            // find the rects of all windows within the taskbar; we need this information so that we do not overlap any other accessory windows which are trying to sit in the same area as us
            var taskbarChildHandlesWithRects = new Dictionary<IntPtr, LegacyWindowsApi.RECT>();
            foreach (var taskbarChildHandle in taskbarChildHandles)
            {
                LegacyWindowsApi.RECT taskbarChildRect = new LegacyWindowsApi.RECT();
                if (LegacyWindowsApi.GetWindowRect(taskbarChildHandle, out taskbarChildRect) == true)
                {
                    taskbarChildHandlesWithRects.Add(taskbarChildHandle, taskbarChildRect);
                }
                else
                {
                    Debug.Assert(false, "Could not capture RECTs of all taskbar child windows");
                }
            }

            // remove any child rects which are contained inside the task button container (so that we eliminate any subchildren from our calculations)
            foreach (var taskbarChildHandle in taskbarChildHandles)
            {
                if (taskbarChildHandlesWithRects.ContainsKey(taskbarChildHandle) == true)
                {
                    var taskbarChildRect = taskbarChildHandlesWithRects[taskbarChildHandle];
                    if (taskbarChildRect.IsInside(taskButtonContainerRect))
                    {
                        taskbarChildHandlesWithRects.Remove(taskbarChildHandle);
                    }
                }
            }

            // remove our own (tray button) window handle from the list (so that we don't see our current screen rect as "taken" in the list of occupied RECTs)
            taskbarChildHandlesWithRects.Remove(this.Handle);

            // create a list of children which are located between the task button container and the notify tray (i.e. windows which are occupying the same region we want to
            // occupy...so we can try to avoid overlapping)
            List<LegacyWindowsApi.RECT> freeAreaChildRects = new List<LegacyWindowsApi.RECT>();
            foreach (var taskbarChildHandle in taskbarChildHandles)
            {
                if (taskbarChildHandlesWithRects.ContainsKey(taskbarChildHandle) == true)
                {
                    var taskbarChildRect = taskbarChildHandlesWithRects[taskbarChildHandle];
                    if ((taskbarChildRect.IsInside(freeAreaAvailableRect) == true) &&
                    (taskbarChildRect.HasNonZeroWidthOrHeight() == false))
                    {
                        freeAreaChildRects.Add(taskbarChildRect);
                    }
                }
            }

            return (freeAreaAvailableRect, freeAreaChildRects);
        }

        // NOTE: this function returns a newPosition IF the tray button should be moved
        private (LegacyWindowsApi.RECT? currentRect, LegacyWindowsApi.RECT? changeToRect, System.Windows.Forms.Orientation orientation)? CalculateCurrentAndTargetRectOfTrayButton()
        {
            // NOTE: there are scenarios we must deal with where there may be multiple potential "taskbar button" icons to the left of the notification tray; in those scenarios, we must:
            // 1. Position ourself to the left of the other icon-button(s) (or in an empty space in between them)
            // 2. Reposition our icon when the other icon-button(s) are removed from the taskbar (e.g. when their host applications close them)
            // 3. If we detect that we and another application are writing on top of each other (or repositioning the taskbar button container on top of our icon), then we must fail
            //    gracefully and let our host application know so it can warn the user, place the icon in the notification tray instead, etc.

            // To position the tray button, we need to find three windows:
            // 1. the taskbar itself
            // 2. the section of the taskbar which holds the taskbar buttons (i.e. to the right of the start button and find/cortana/taskview buttons, but to the left of the notification tray) */
            // 3. the notification tray
            //
            // We will then resize the section of the taskbar that holds the taskbar buttons so that we can place our tray button to its right (i.e. to the left of the notification tray).

            var taskbarTripletHandles = this.GetTaskbarTripletHandles();
            var taskbarHandle = taskbarTripletHandles.TaskbarHandle;

            var taskbarRects = this.GetTaskbarTripletRects(taskbarTripletHandles.TaskbarHandle, taskbarTripletHandles.TaskButtonContainerHandle, taskbarTripletHandles.NotifyTrayHandle);
            if (taskbarRects is null)
            {
                return null;
            }
            var taskbarRect = taskbarRects.Value.TaskbarRect;
            var taskButtonContainerRect = taskbarRects.Value.TaskButtonContainerRect;
            var notifyTrayRect = taskbarRects.Value.NotifyTrayRect;

            // determine the taskbar's orientation
            System.Windows.Forms.Orientation taskbarOrientation;
            if ((taskbarRect.Right - taskbarRect.Left) > (taskbarRect.Bottom - taskbarRect.Top))
            {
                taskbarOrientation = System.Windows.Forms.Orientation.Horizontal;
            }
            else
            {
                taskbarOrientation = System.Windows.Forms.Orientation.Vertical;
            }

            // if the taskbar is horizontal, determine if it's LeftToRight (standard) or RightToLeft (for Arabic, Hebrew, etc.)
            bool isRightToLeft = false;
            if (taskbarOrientation == System.Windows.Forms.Orientation.Horizontal)
            {
                var centerXOfTaskbar = taskbarRect.Left + ((taskbarRect.Right - taskbarRect.Left) / 2);
                if (notifyTrayRect.Right < centerXOfTaskbar)
                {
                    isRightToLeft = true;
                }
            }

            // calculate all of the free rects between the task button container and notify tray
            var calculateEmptyRectsResult = this.CalculateEmptyRectsBetweenTaskButtonContainerAndNotifyTray(taskbarHandle, taskbarOrientation, isRightToLeft, taskbarRect, taskButtonContainerRect, notifyTrayRect);
            var freeAreaChildRects = calculateEmptyRectsResult.childRects;
            var freeAreaAvailableRect = calculateEmptyRectsResult.availableAreaRect;

            /* determine the rect for our tray button; based on our current positioning strategy, this will either be its existing position or the leftmost/topmost "next to tray" position.  
                 * If we are determining the leftmost/topmost "next to tray" position, we will find the available space between the task button container and the notification tray (or any 
                 * already-present controls that are already left/top of the notification tray); if there is not enough free space available in that area then we will shrink the task button
                 * container to make room. */
            //
            /* NOTE: there are some deficiencies to our current positioning strategy.  Of note...
                 * 1. In some circumstances, it might be possible that we are leaving "holes" of available space between the task button container and the notification tray; but if that
                 *    happens, it might be something beyond our control (as other apps may have created that space).  One concern is if we shrink our icon (in which case we should in theory
                 *    shrink the space to our top/left)
                 * 2. If other apps draw their next-to-tray buttons after us and are not watching for conflicts then they could draw over us; a mitigation measure in that instance might be to
                 *    use a timer to check that our tray button is not obscured and then remedy the situation; if we got into a "fight" over real estate that appeared to never terminate then
                 *    we could destroy our icon and raise an event letting the application know it should choose an alternate strategy (such as a notification tray icon) instead.
                 * 3. If a more-rightmost/bottommost icon's application is closed while we are running, the taskbar could be resized to obscure us; we might need a timer (or we might need to
                 *    capture the appropriate window message) to discover this scenario.
                 * In summary there is no standardized system (other than perhaps the "(dock) toolbar in taskbar" mechanism); if we find that we encounter problems in the field with our current
                 * strategy, we may want to consider rebuilding this functionality via the "toolbar in taskbar" mechanism.  See HP Support Assistant for an example of another application
                 * which is doing what we are trying to do with the next-to-tray button strategy */

            // establish the appropriate size for our tray button (i.e. same height/width as taskbar, and with an aspect ratio of 8:10)
            int trayButtonHeight;
            int trayButtonWidth;
            if (taskbarOrientation == System.Windows.Forms.Orientation.Horizontal)
            {
                trayButtonHeight = taskbarRect.Bottom - taskbarRect.Top;
                trayButtonWidth = (int)((Double)trayButtonHeight * 0.8);
            }
            else
            {
                trayButtonWidth = taskbarRect.Right - taskbarRect.Left;
                trayButtonHeight = (int)((Double)trayButtonWidth * 0.8);
            }

            // get our current rect (in case we can just reuse the current position...and also to make sure it doesn't need to be resized)
            LegacyWindowsApi.RECT currentRectAsNonNullable;
            LegacyWindowsApi.RECT? currentRect = null;
            LegacyWindowsApi.RECT? currentRectForResult = null;
            if (LegacyWindowsApi.GetWindowRect(this.Handle, out currentRectAsNonNullable) == true)
            {
                currentRect = currentRectAsNonNullable;
                currentRectForResult = currentRectAsNonNullable;
            }

            // if the current position of our window isn't the right size for our icon, then set it to NULL so we don't try to reuse it.
            if ((currentRect is not null) &&
                 ((currentRect.Value.Right - currentRect.Value.Left != trayButtonWidth) || (currentRect.Value.Bottom - currentRect.Value.Top != trayButtonHeight)))
            {
                currentRect = null;
            }

            // calculate the new rect for our tray button's window
            LegacyWindowsApi.RECT? newRect = null;

            // if the space occupied by our already-existing rect is not overlapped by anyone else and is in the free area, keep using the same space
            if ((currentRect is not null) && (currentRect.Value.Intersects(freeAreaAvailableRect) == true))
            {
                // by default, assume that our currentRect is still available (i.e. not overlapped)
                bool currentRectIsNotOverlapped = true;

                // make sure we do not overlap another control in the free area
                foreach (var freeAreaChildRect in freeAreaChildRects)
                {
                    if (currentRect.Value.Intersects(freeAreaChildRect) == true)
                    {
                        // overlap conflict
                        currentRectIsNotOverlapped = false;
                        break;
                    }
                }

                if (currentRectIsNotOverlapped == true)
                {
                    // set "newRect" (the variable for where we will now place our tray button) to the same position we were already at
                    newRect = currentRect;
                }
            }

            // if our current (already-used-by-us) rect was not available, choose the leftmost/topmost space available; note that "rightmost" is actually leftmost when the system is using an RTL orientation (e.g. Arabic, Hebrew)
            if (newRect is null)
            {
                if (taskbarOrientation == System.Windows.Forms.Orientation.Horizontal)
                {
                    // horizontal taskbar: find the leftmost rect in the available space (which we'll then carve the "rightmost" section out of)
                    // OBSERVATION: leftmost is actually rightmost in RTL layouts (e.g. Arabic, Hebrew)
                    LegacyWindowsApi.RECT leftmostRect = freeAreaAvailableRect;

                    foreach (var freeAreaChildRect in freeAreaChildRects)
                    {
                        if (isRightToLeft == false)
                        {
                            if (freeAreaChildRect.Left < leftmostRect.Right)
                            {
                                leftmostRect.Right = freeAreaChildRect.Left;
                            }
                        }
                        else
                        {
                            if (freeAreaChildRect.Right > leftmostRect.Left)
                            {
                                leftmostRect.Left = freeAreaChildRect.Right;
                            }
                        }
                    }

                    // choose the rightmost space in the leftmostRect area (or leftmost for RTL layouts); expand our tray button towards the left (right for RTL) if/as necessary
                    if (isRightToLeft == false)
                    {
                        newRect = new LegacyWindowsApi.RECT(new System.Windows.Rect(leftmostRect.Right - trayButtonWidth, leftmostRect.Bottom - trayButtonHeight, trayButtonWidth, trayButtonHeight));
                    }
                    else
                    {
                        newRect = new LegacyWindowsApi.RECT(new System.Windows.Rect(leftmostRect.Left, leftmostRect.Bottom - trayButtonHeight, trayButtonWidth, trayButtonHeight));
                    }
                }
                else
                {
                    // vertical taskbar: find the topmost rect in the available space (which we'll then carve the "bottommost" section out of)
                    LegacyWindowsApi.RECT topmostRect = freeAreaAvailableRect;

                    foreach (var freeAreaChildRect in freeAreaChildRects)
                    {
                        if (freeAreaChildRect.Top < topmostRect.Bottom)
                        {
                            topmostRect.Bottom = freeAreaChildRect.Top;
                        }
                    }

                    // choose the bottommost space in the topmostRect area; expand our tray button towards the top if/as necessary
                    newRect = new LegacyWindowsApi.RECT(new System.Windows.Rect(topmostRect.Right - trayButtonWidth, topmostRect.Bottom - trayButtonHeight, trayButtonWidth, trayButtonHeight));
                }
            }

            LegacyWindowsApi.RECT? changeToRect = null;
            if (newRect != currentRectForResult)
            {
                changeToRect = newRect;
            }

            return (currentRectForResult, changeToRect, taskbarOrientation);
        }

        private bool RequestRedraw()
        {
            return LegacyWindowsApi.RedrawWindow(
                 this.Handle,
                 IntPtr.Zero,
                 IntPtr.Zero,
                 LegacyWindowsApi.RedrawWindowFlags.RDW_ERASE | LegacyWindowsApi.RedrawWindowFlags.RDW_INVALIDATE | LegacyWindowsApi.RedrawWindowFlags.RDW_ALLCHILDREN
                 );
        }

        internal static List<IntPtr> EnumerateChildWindows(IntPtr parentHwnd)
        {
            var result = new List<IntPtr>();

            // create an unmanaged pointer to our list (using a GC-managed handle)
            GCHandle resultGCHandle = GCHandle.Alloc(result, GCHandleType.Normal);
            // convert our GCHandle into an IntPtr (which we will unconvert back to a GCHandler in the EnumChildWindows callback) 
            IntPtr resultGCHandleAsIntPtr = GCHandle.ToIntPtr(resultGCHandle);

            try
            {
                var enumFunction = new LegacyWindowsApi.EnumWindowsProc(TrayButtonNativeWindow.EnumerateChildWindowsCallback);
                LegacyWindowsApi.EnumChildWindows(parentHwnd, enumFunction, resultGCHandleAsIntPtr);

            }
            finally
            {
                if (resultGCHandle.IsAllocated)
                {
                    resultGCHandle.Free();
                }
            }

            return result;
        }
        internal static bool EnumerateChildWindowsCallback(IntPtr hwnd, IntPtr lParam)
        {
            // convert lParam back into the result list object
            var resultGCHandle = GCHandle.FromIntPtr(lParam);
            List<IntPtr>? result = resultGCHandle.Target as List<IntPtr>;

            if (result is not null)
            {
                result.Add(hwnd);
            }
            else
            {
                Debug.Assert(false, "Could not enumerate child windows");
            }

            return true;
        }

        internal static IntPtr FindWindowsTaskbarHandle()
        {
            return LegacyWindowsApi.FindWindow("Shell_TrayWnd", null);
        }

        private static IntPtr FindWindowsTaskbarTaskButtonContainerHandle()
        {
            var taskbarHandle = TrayButtonNativeWindow.FindWindowsTaskbarHandle();
            if (taskbarHandle == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }
            return LegacyWindowsApi.FindWindowEx(taskbarHandle, IntPtr.Zero, "ReBarWindow32", null);
        }

        private static IntPtr FindWindowsTaskbarNotificationTrayHandle()
        {
            var taskbarHandle = TrayButtonNativeWindow.FindWindowsTaskbarHandle();
            if (taskbarHandle == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }
            return LegacyWindowsApi.FindWindowEx(taskbarHandle, IntPtr.Zero, "TrayNotifyWnd", null);
        }

    }
    #endregion
}
