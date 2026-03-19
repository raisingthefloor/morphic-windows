#if INCLUDE_WINDOWS_10_SUPPORT
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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

    public event EventHandler<Morphic.Controls.MouseEventArgs>? MouseUp;

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
        if (taskbarHandle == Windows.Win32.Foundation.HWND.Null)
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

    private class TrayButtonNativeWindow : IDisposable
    {
        private TrayButton _owner;

        // NOTE: s_morphicTrayButtonClassInfoExAtom and s_wndProcDelegate are initialized together
        private static ushort? s_morphicTrayButtonClassInfoExAtom = null;
        // create a static wndproc delegate (which will work as a trampoline to a window's wndproc function, using the hwnd-specific userdata which stores a reference to each instance)
        // [this is done to prevent the delegate from being GC'd while the window class is registered]
        private static Windows.Win32.UI.WindowsAndMessaging.WNDPROC? s_wndProcDelegate;

        private Windows.Win32.Foundation.HWND _hwnd = Windows.Win32.Foundation.HWND.Null;
        // NOTE: a GC handle to the class instance is stored as userdata for each native window's hwnd (so that we can trampoline from the static wndproc to the instance-specific WndProc callback)
        private GCHandle _gcHandle;

        private Windows.Win32.Foundation.HWND _tooltipWindowHandle = Windows.Win32.Foundation.HWND.Null;
        private IntPtr _iconHandle = IntPtr.Zero;

        private string? _tooltipText = null;
        private bool _tooltipInfoAdded = false;

        // NOTE: this timer is used to reposition the tray button when the screen resolution changes (and it keeps watch at an accelerated pace, to make sure the taskbar has stopped moving around)
        private System.Threading.Timer? _trayButtonPositionCheckupTimer;
        private int _trayButtonPositionCheckupTimerCounter = 0;

        // NOTE: this timer is used to reposition the tray button when adjacent taskbar widgets (e.g. Windows 10 weather) change in size
        private System.Threading.Timer? _trayButtonWidgetPositionCheckupTimer;
        //
        private TimeSpan _widgetPositionCheckupInterval = new TimeSpan(0, 0, 0, 1, 0); // NOTE: this is a failsafe mechanism; if our position changes more than this many times per minute, we will back off the widget reposition timer (to avoid a potential super-glitchy user experience, with a taskbar button that won't stop moving; this may also help avoid the unnecessary movement of adjacent widgets)
        private Queue<DateTimeOffset> _widgetPositionChangeHistory = new();

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

            /* register a custom native window class for our TrayButton (or refer to the already-registered class, if we captured it earlier in the application's execution) */
            if (s_morphicTrayButtonClassInfoExAtom is null)
            {
                // register our control's custom native window class using a static wndproc (which will act as a trampoline to an instance-specific WndProc callback)
                s_wndProcDelegate = TrayButtonNativeWindow.StaticWndProc;
                //
                var hCursor = Windows.Win32.PInvoke.LoadCursor(Windows.Win32.Foundation.HINSTANCE.Null, Windows.Win32.PInvoke.IDC_ARROW);
                if (hCursor.IsNull == true)
                {
	                Debug.Assert(false, "Could not load arrow cursor");
	                //var win32ErrorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
	                return; // MorphicResult.ErrorResult<IInitializeError>(new IInitializeError.Win32Error((uint)win32ErrorCode));
                }
                //
                Windows.Win32.UI.WindowsAndMessaging.WNDCLASSEXW lpWndClassEx;
                ushort registerClassResult;
                unsafe
                {
                    fixed (char* pointerToNativeWindowClassName = nativeWindowClassName)
                    {
                        lpWndClassEx = new Windows.Win32.UI.WindowsAndMessaging.WNDCLASSEXW()
                        {
                            cbSize = (uint)Marshal.SizeOf<Windows.Win32.UI.WindowsAndMessaging.WNDCLASSEXW>(),
                            lpfnWndProc = s_wndProcDelegate,
                            lpszClassName = pointerToNativeWindowClassName,
                            hCursor = hCursor,
                        };
                    }
                    // NOTE: RegisterClassEx returns an ATOM (or 0 if the call failed)
                    registerClassResult = Windows.Win32.PInvoke.RegisterClassEx(lpWndClassEx);
                }
                //
                if (registerClassResult == 0)
                {
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                }
                s_morphicTrayButtonClassInfoExAtom = registerClassResult;
            }

            /* create an instance of our native window */
            Windows.Win32.Foundation.HWND handle;
            unsafe
            {
                var atomAsString = new Windows.Win32.Foundation.PCWSTR((char*)(nint)s_morphicTrayButtonClassInfoExAtom!.Value);
                fixed (char* pointerToNativeWindowClassName = nativeWindowClassName)
                {
                    handle = Windows.Win32.PInvoke.CreateWindowEx(
                        dwExStyle: Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_TOOLWINDOW,
                        lpClassName: atomAsString,
                        lpWindowName: pointerToNativeWindowClassName,
                        dwStyle: Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_VISIBLE | Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_CHILD | Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_CLIPSIBLINGS | Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_TABSTOP,
                        X: 0,
                        Y: 0,
                        nWidth: 32,
                        nHeight: 40,
                        hWndParent: (Windows.Win32.Foundation.HWND)taskbarHandle,
                        hMenu: Windows.Win32.UI.WindowsAndMessaging.HMENU.Null,
                        hInstance: Windows.Win32.Foundation.HINSTANCE.Null,
                        lpParam: null
                    );
                }
            }
            if (handle.IsNull)	
            {
                Debug.Assert(false, "Could not create tray button window handle");
                return;
            }
            _hwnd = handle;

            // store instance reference in GWL_USERDATA for the hwnd (to enable message routing from the static wndproc to the instance-specific WndProc callback)
            _gcHandle = GCHandle.Alloc(this);
            //
            // NOTE: SetWindowLongPtr can return 0 even if there is no error; see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowlongptrw
            System.Runtime.InteropServices.Marshal.SetLastPInvokeError(0);
            var setWindowLongPtrResult = PInvokeExtensions.SetWindowLongPtr_IntPtr(_hwnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_USERDATA, (nint)(IntPtr)_gcHandle);
            if (setWindowLongPtrResult == 0)
            {
                var win32ErrorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                if (win32ErrorCode != 0)
                {
                    Debug.Assert(false, "Could not set GWL_USERDATA for tray button window; win32 error: " + win32ErrorCode.ToString());
                }
            }

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

            // NOTE: due to the weather, news and other taskbar widgets introduced in late versions of Windows 10, we need to re-validate and re-position the taskbar icon when adjacent widgets overlay its position
            _trayButtonWidgetPositionCheckupTimer = new System.Threading.Timer(TrayButtonWidgetPositionCheckup, null, _widgetPositionCheckupInterval, _widgetPositionCheckupInterval);
        }

        // NOTE: this function is somewhat redundant and is provided to support Windows 11; we should refactor all of this code to handle window messages centrally
        private void _mouseHook_WndProcEvent(object? sender, Morphic.Controls.TrayButton.Windows10.WindowsNative.MouseWindowMessageHook.WndProcEventArgs e)
        {
            // TODO: we should ensure that calls are queued and then called from a sequential thread (ideally a UI dispatch thread)
            switch ((uint)e.Message)
            {
                case Windows.Win32.PInvoke.WM_LBUTTONDOWN:
                    _visualState |= TrayButtonVisualStateFlags.LeftButtonPressed;
                    this.RequestRedraw();
                    break;
                case Windows.Win32.PInvoke.WM_LBUTTONUP:
                    _visualState &= ~TrayButtonVisualStateFlags.LeftButtonPressed;
                    this.RequestRedraw();
                    {
                        var mouseArgs = new Morphic.Controls.MouseEventArgs(Morphic.Controls.MouseButtons.Left, 1, e.X, e.Y);
                        _owner.MouseUp?.Invoke(_owner, mouseArgs);
                    }
                    break;
                case Windows.Win32.PInvoke.WM_MOUSELEAVE:
                    // the cursor has left our tray button's window area; remove the hover state from our visual state
                    _visualState &= ~TrayButtonVisualStateFlags.Hover;
                    // NOTE: as we aren't able to track mouseup when the cursor is outside of the button, we also remove the left/right button pressed states here
                    //       (and then we check them again when the mouse moves back over the button)
                    _visualState &= ~TrayButtonVisualStateFlags.LeftButtonPressed;
                    _visualState &= ~TrayButtonVisualStateFlags.RightButtonPressed;
                    this.RequestRedraw();
                    break;
                case Windows.Win32.PInvoke.WM_MOUSEMOVE:
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
                case Windows.Win32.PInvoke.WM_RBUTTONDOWN:
                    _visualState |= TrayButtonVisualStateFlags.RightButtonPressed;
                    this.RequestRedraw();
                    break;
                case Windows.Win32.PInvoke.WM_RBUTTONUP:
                    _visualState &= ~TrayButtonVisualStateFlags.RightButtonPressed;
                    this.RequestRedraw();
                    {
                        var mouseArgs = new Morphic.Controls.MouseEventArgs(Morphic.Controls.MouseButtons.Right, 1, e.X, e.Y);
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

            unsafe
            {
                _tooltipWindowHandle = Windows.Win32.PInvoke.CreateWindowEx(
                    0 /* no styles */,
                    Windows.Win32.PInvoke.TOOLTIPS_CLASS,
                    null,
                    Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_POPUP | (Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE)Windows.Win32.PInvoke.TTS_ALWAYSTIP,
                    Windows.Win32.PInvoke.CW_USEDEFAULT,
                    Windows.Win32.PInvoke.CW_USEDEFAULT,
                    Windows.Win32.PInvoke.CW_USEDEFAULT,
                    Windows.Win32.PInvoke.CW_USEDEFAULT,
                    _hwnd,
                    null,
                    null,
                    null);
            }

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

            _ = Windows.Win32.PInvoke.DestroyWindow(_tooltipWindowHandle);
            _tooltipWindowHandle = Windows.Win32.Foundation.HWND.Null;
        }

        private void UpdateTooltipTextAndTracking()
        {
            if (_tooltipWindowHandle == IntPtr.Zero)
            {
                // tooltip window does not exist; failed; abort
                Debug.Assert(false, "Tooptip window does not exist; if this is an expected failure, remove this assert.");
                return;
            }

            Windows.Win32.Foundation.RECT trayButtonClientRect;
            var getClientRectSuccess = Windows.Win32.PInvoke.GetClientRect(_hwnd, out trayButtonClientRect);
            if (getClientRectSuccess == false)
            {
                // failed; abort
                Debug.Assert(false, "Could not get client rect for tray button; could not set up tooltip");
                return;
            }

            IntPtr pointerToToolinfo;
            unsafe
            {
                fixed (char* pointerToTooltipText = _tooltipText)
                {
                    var toolinfo = new Windows.Win32.UI.Controls.TTTOOLINFOW();
                    toolinfo.cbSize = (uint)(Marshal.SizeOf<Windows.Win32.UI.Controls.TTTOOLINFOW>() - IntPtr.Size); // TTTOOLINFOW_V1_SIZE (required for TTM_ADDTOOL)
                    toolinfo.hwnd = _hwnd;
                    toolinfo.uFlags = Windows.Win32.UI.Controls.TOOLTIP_FLAGS.TTF_SUBCLASS;
                    toolinfo.lpszText = pointerToTooltipText;
                    toolinfo.uId = unchecked((nuint)(nint)_hwnd.Value); // unique identifier (for adding/deleting the tooltip)
                    toolinfo.rect = trayButtonClientRect;
                    //
                    pointerToToolinfo = Marshal.AllocHGlobal(Marshal.SizeOf(toolinfo));
                    Marshal.StructureToPtr(toolinfo, pointerToToolinfo, false);
                }
            }
            try
            {
                if (_tooltipText is not null)
                {
                    if (_tooltipInfoAdded == false)
                    {
                        _ = Windows.Win32.PInvoke.SendMessage(_tooltipWindowHandle, Windows.Win32.PInvoke.TTM_ADDTOOL, 0, pointerToToolinfo);
                        _tooltipInfoAdded = true;
                    }
                    else
                    {
                        // delete and re-add the tooltipinfo; this will update all the info (including the text and tracking rect)
                        _ = Windows.Win32.PInvoke.SendMessage(_tooltipWindowHandle, Windows.Win32.PInvoke.TTM_DELTOOL, 0, pointerToToolinfo);
                        _ = Windows.Win32.PInvoke.SendMessage(_tooltipWindowHandle, Windows.Win32.PInvoke.TTM_ADDTOOL, 0, pointerToToolinfo);
                    }
                }
                else
                {
                    // NOTE: we might technically call "deltool" even when a tooltipinfo was already removed
                    _ = Windows.Win32.PInvoke.SendMessage(_tooltipWindowHandle, Windows.Win32.PInvoke.TTM_DELTOOL, 0, pointerToToolinfo);
                    _tooltipInfoAdded = false;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pointerToToolinfo);
            }
        }

	    // static wndproc (registered with the window class); this static wndproc callback routes messages to instance-specific callbacks (using the instance reference stored in GWL_USERDATA); also handles creation-time (pre-window-fully-init'd) messages
        private static Windows.Win32.Foundation.LRESULT StaticWndProc(Windows.Win32.Foundation.HWND hWnd, uint msg, Windows.Win32.Foundation.WPARAM wParam, Windows.Win32.Foundation.LPARAM lParam)
        {
            // try to retrieve the instance from GWL_USERDATA
            var userData = PInvokeExtensions.GetWindowLongPtr_IntPtr(hWnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_USERDATA);
            TrayButtonNativeWindow? instance = null;
            if (userData != IntPtr.Zero)
            {
                try
                {
                    var gcHandle = GCHandle.FromIntPtr(userData);
                    instance = (TrayButtonNativeWindow?)gcHandle.Target;
                }
                catch
                {
                    // GCHandle was freed (window outlived the instance); fall through to DefWindowProc
                }
            }

            // if the instance is already set up, pass the message to its instance-specific WndProc callback
            if (instance is not null)
            {
                return instance.WndProc(hWnd, msg, wParam, lParam);
            }
            else
            {
                // if no instance is associated with the hwnd (i.e. during initial creation of the window), handle callbacks here instead
                switch (msg)
                {
                    case Windows.Win32.PInvoke.WM_CREATE:
                        // see: https://learn.microsoft.com/en-us/windows/win32/api/uxtheme/nf-uxtheme-bufferedpaintinit
                        if (Windows.Win32.PInvoke.BufferedPaintInit() != Windows.Win32.Foundation.HRESULT.S_OK)
                        {
                            Debug.Assert(false, "Could not initialize buffered paint");
                            return (Windows.Win32.Foundation.LRESULT)(-1); // abort window creation process
                        }
                        break;
                    default:
                        break;
                }
            }

            // pass all non-handled messages through to DefWindowProc
            return Windows.Win32.PInvoke.DefWindowProc(hWnd, msg, wParam, lParam);
        }


        public void Dispose()
        {
            // TODO: if we are the topmost/leftmost next-to-tray-icon button, we should expand the task button container so it takes up our now-unoccupied space

            if (_mouseHook is not null)
            {
                _mouseHook.Dispose();
            }

            _trayButtonWidgetPositionCheckupTimer?.Dispose();
            _trayButtonWidgetPositionCheckupTimer = null;

            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;

            this.DestroyTooltipWindow();

            // clear the GWL_USERDATA to prevent the static wndproc from routing to this (disposed) instance
            if (!_hwnd.IsNull)
            {
                _ = PInvokeExtensions.SetWindowLongPtr_IntPtr(_hwnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_USERDATA, 0);
                _ = Windows.Win32.PInvoke.DestroyWindow(_hwnd);
                _hwnd = Windows.Win32.Foundation.HWND.Null;
            }

            if (_gcHandle.IsAllocated)
            {
                _gcHandle.Free();
            }
        }

        // instance wndproc — handles messages after GWL_USERDATA is set up
        private Windows.Win32.Foundation.LRESULT WndProc(Windows.Win32.Foundation.HWND hWnd, uint msg, Windows.Win32.Foundation.WPARAM wParam, Windows.Win32.Foundation.LPARAM lParam)
        {
            Windows.Win32.Foundation.LRESULT? result = null;

            switch (msg)
            {
                case Windows.Win32.PInvoke.WM_DESTROY:
                    /* TODO: trace to see if WM_DESTROY is actually called here; if not, then we should place the uninit in dispose instead; we might also consider
                         *       not using BufferedPaintInit/UnInit at all (although that _might_ slow down our buffered painting execution a tiny bit) */
                    // see: https://learn.microsoft.com/en-us/windows/win32/api/uxtheme/nf-uxtheme-bufferedpaintuninit
                    _ = Windows.Win32.PInvoke.BufferedPaintUnInit();
                    break;
                case Windows.Win32.PInvoke.WM_DISPLAYCHANGE:
                    // screen resolution has changed: reposition the tray button
                    // NOTE: wParam contains bit depth
                    // NOTE: lParam contains the resolutions of the screen (horizontal resolution in low-order word; vertical resolution in high-order word)
                    this.PositionTrayButton();
                    break;
                case Windows.Win32.PInvoke.WM_ERASEBKGND:
                    // we will handle erasing the background, so return a non-zero value here
                    result = (Windows.Win32.Foundation.LRESULT)1;
                    break;
                case Windows.Win32.PInvoke.WM_LBUTTONUP:
                    _visualState &= ~TrayButtonVisualStateFlags.LeftButtonPressed;
                    this.RequestRedraw();
                    {
                        var hitPoint = this.ConvertMouseMessageLParamToScreenPoint((IntPtr)(nint)lParam);
                        if (hitPoint is null)
                        {
                            // failed; abort
                            Debug.Assert(false, "Could not map tray button hit point to screen coordinates");
                            break;
                        }
                        var mouseArgs = new Morphic.Controls.MouseEventArgs(Morphic.Controls.MouseButtons.Left, 1, hitPoint.Value.X, hitPoint.Value.Y);
                        _owner.MouseUp?.Invoke(_owner, mouseArgs);
                    }
                    result = (Windows.Win32.Foundation.LRESULT)0;
                    break;
                case Windows.Win32.PInvoke.WM_MOUSEACTIVATE:
                    // do not activate our window (and discard this message)
                    result = (Windows.Win32.Foundation.LRESULT)(int)Windows.Win32.PInvoke.MA_NOACTIVATEANDEAT;
                    break;
                case Windows.Win32.PInvoke.WM_MOUSELEAVE:
                    // the cursor has left our tray button's window area; remove the hover state from our visual state
                    _visualState &= ~TrayButtonVisualStateFlags.Hover;
                    // NOTE: as we aren't able to track mouseup when the cursor is outside of the button, we also remove the left/right button pressed states here
                    //       (and then we check them again when the mouse moves back over the button)
                    _visualState &= ~TrayButtonVisualStateFlags.LeftButtonPressed;
                    _visualState &= ~TrayButtonVisualStateFlags.RightButtonPressed;
                    this.RequestRedraw();
                    result = (Windows.Win32.Foundation.LRESULT)0;
                    break;
                case Windows.Win32.PInvoke.WM_MOUSEMOVE:
                    // NOTE: this message is raised while we are tracking (whereas the SETCURSOR WM_MOUSEMOVE is captured when the mouse cursor first enters the window)
                    //
                    // NOTE: if the cursor moves off of the tray button while the button is pressed, we remove the "pressed" focus as well as the "hover" focus because
                    //       we aren't able to track mouseup when the cursor is outside of the button; consequently we also need to check the mouse pressed state during
                    //       mousemove so that we can re-enable the pressed state if/where appropriate.
                    if (((_visualState & TrayButtonVisualStateFlags.LeftButtonPressed) == 0) && ((wParam.Value.ToUInt64() & PInvokeExtensions.MK_LBUTTON) != 0))
                    {
                        _visualState |= TrayButtonVisualStateFlags.LeftButtonPressed;
                        this.RequestRedraw();
                    }
                    if (((_visualState & TrayButtonVisualStateFlags.RightButtonPressed) == 0) && ((wParam.Value.ToUInt64() & PInvokeExtensions.MK_RBUTTON) != 0))
                    {
                        _visualState |= TrayButtonVisualStateFlags.RightButtonPressed;
                        this.RequestRedraw();
                    }
                    //
                    result = (Windows.Win32.Foundation.LRESULT)0;
                    break;
                case Windows.Win32.PInvoke.WM_NCHITTEST:
                    var hitTestX = (short)(((nint)lParam >> 0) & 0xFFFF);
                    var hitTestY = (short)(((nint)lParam >> 16) & 0xFFFF);
                    //
                    Windows.Win32.Foundation.RECT trayButtonRectInScreenCoordinates;
                    if (Windows.Win32.PInvoke.GetWindowRect(_hwnd, out trayButtonRectInScreenCoordinates) == false)
                    {
                        // fail; abort
                        Debug.Assert(false, "Could not get rect of tray button in screen coordinates");
                        return Windows.Win32.PInvoke.DefWindowProc(hWnd, msg, wParam, lParam);
                    }
                    //
                    if ((hitTestX >= trayButtonRectInScreenCoordinates.left) && (hitTestX < trayButtonRectInScreenCoordinates.right) &&
                              (hitTestY >= trayButtonRectInScreenCoordinates.top) && (hitTestY < trayButtonRectInScreenCoordinates.bottom))
                    {
                        // inside client area
                        result = (Windows.Win32.Foundation.LRESULT)1; // HTCLIENT
                    }
                    else
                    {
                        // nowhere
                        // TODO: determine if there is another response we should be returning instead; the documentation is not clear in this regard
                        result = (Windows.Win32.Foundation.LRESULT)0; // HTNOWHERE
                    }
                    break;
                case Windows.Win32.PInvoke.WM_NCPAINT:
                    // no non-client (frame) area to paint
                    result = (Windows.Win32.Foundation.LRESULT)0;
                    break;
                case Windows.Win32.PInvoke.WM_PAINT:
                    this.Paint((IntPtr)hWnd.Value);
                    result = (Windows.Win32.Foundation.LRESULT)0;
                    break;
                case Windows.Win32.PInvoke.WM_RBUTTONUP:
                    _visualState &= ~TrayButtonVisualStateFlags.RightButtonPressed;
                    this.RequestRedraw();
                    {
                        var hitPoint = this.ConvertMouseMessageLParamToScreenPoint((IntPtr)(nint)lParam);
                        if (hitPoint is null)
                        {
                            // failed; abort
                            Debug.Assert(false, "Could not map tray button hit point to screen coordinates");
                            break;
                        }
                        var mouseArgs = new Morphic.Controls.MouseEventArgs(Morphic.Controls.MouseButtons.Right, 1, hitPoint.Value.X, hitPoint.Value.Y);
                        _owner.MouseUp?.Invoke(_owner, mouseArgs);
                    }
                    result = (Windows.Win32.Foundation.LRESULT)0;
                    break;
                case Windows.Win32.PInvoke.WM_SETCURSOR:
                    // wParam: window handle
                    // lParam: low-order word is the hit-test result for the cursor position; high-order word specifies the mouse message that triggered this event
                    var hitTestResult = (uint)(((nint)lParam >> 0) & 0xFFFF);
                    var mouseMsg = (uint)(((nint)lParam >> 16) & 0xFFFF);
                    switch (mouseMsg)
                    {
                        case Windows.Win32.PInvoke.WM_LBUTTONDOWN:
                            _visualState |= TrayButtonVisualStateFlags.LeftButtonPressed;
                            this.RequestRedraw();
                            result = (Windows.Win32.Foundation.LRESULT)1;
                            break;
                        case Windows.Win32.PInvoke.WM_LBUTTONUP:
                            result = (Windows.Win32.Foundation.LRESULT)1;
                            break;
                        case Windows.Win32.PInvoke.WM_MOUSEMOVE:
                            // if we are not yet tracking the mouse position (i.e. this is effectively "mouse enter") then do so now
                            if ((_visualState & TrayButtonVisualStateFlags.Hover) == 0)
                            {
                                // track mousehover (for tooltips) and mouseleave (to remove hover effect)
                                // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-trackmouseevent
                                var eventTrack = new Windows.Win32.UI.Input.KeyboardAndMouse.TRACKMOUSEEVENT()
                                {
                                    cbSize = (uint)Marshal.SizeOf(typeof(Windows.Win32.UI.Input.KeyboardAndMouse.TRACKMOUSEEVENT)),
                                    dwFlags = Windows.Win32.UI.Input.KeyboardAndMouse.TRACKMOUSEEVENT_FLAGS.TME_LEAVE,
                                    hwndTrack = _hwnd,
                                    dwHoverTime = PInvokeExtensions.HOVER_DEFAULT,
                                };
                                var trackMouseEventSuccess = Windows.Win32.PInvoke.TrackMouseEvent(ref eventTrack);
                                if (trackMouseEventSuccess == false)
                                {
                                    // failed
                                    Debug.Assert(false, "Could not set up tracking of tray button window area");
                                    return Windows.Win32.PInvoke.DefWindowProc(hWnd, msg, wParam, lParam);
                                }

                                _visualState |= TrayButtonVisualStateFlags.Hover;

                                this.RequestRedraw();
                            }
                            result = (Windows.Win32.Foundation.LRESULT)1;
                            break;
                        case Windows.Win32.PInvoke.WM_RBUTTONDOWN:
                            _visualState |= TrayButtonVisualStateFlags.RightButtonPressed;
                            this.RequestRedraw();
                            result = (Windows.Win32.Foundation.LRESULT)1;
                            break;
                        case Windows.Win32.PInvoke.WM_RBUTTONUP:
                            result = (Windows.Win32.Foundation.LRESULT)1;
                            break;
                        default:
                            //Debug.WriteLine("UNHANDLED SETCURSOR Mouse Message: " + mouseMsg.ToString());
                            break;
                    }
                    break;
                case Windows.Win32.PInvoke.WM_SIZE:
                    result = (Windows.Win32.Foundation.LRESULT)0;
                    break;
                case Windows.Win32.PInvoke.WM_WINDOWPOSCHANGED:
                    result = (Windows.Win32.Foundation.LRESULT)0;
                    break;
                case Windows.Win32.PInvoke.WM_WINDOWPOSCHANGING:
                    // in this implementation, we don't do anything with this message; nothing to do here
                    result = (Windows.Win32.Foundation.LRESULT)0;
                    break;
                default:
                    // unhandled message; this will be passed onto DefWindowProc instead
                    break;
            }

            if (result.HasValue == true)
            {
                return result.Value;
            }
            else
            {
                return Windows.Win32.PInvoke.DefWindowProc(hWnd, msg, wParam, lParam);
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
        //
        private void TrayButtonWidgetPositionCheckup(object? state)
        {
            const int NUM_CHANGE_HISTORY_ENTRIES_TO_AVERAGE = 10;
            const int WIDGET_POSITION_INTERVAL_BACKOFF_MULTIPLIER = 2;

            // check the current and desired positions of the notify tray icon
            var calculateResult = this.CalculateCurrentAndTargetRectOfTrayButton();
            if (calculateResult is not null)
            {
                if (calculateResult.Value.changeToRect is not null)
                {
                    // record the repositioning event's timestamp
                    var currentRepositioningEventTimestamp = DateTimeOffset.UtcNow;
                    _widgetPositionChangeHistory.Enqueue(currentRepositioningEventTimestamp);
                    while (_widgetPositionChangeHistory.Count > NUM_CHANGE_HISTORY_ENTRIES_TO_AVERAGE)
                    {
                        _ = _widgetPositionChangeHistory.Dequeue();
                    }

                    // reposition the tray button
                    this.PositionTrayButton();

                    // determine if our repositioning is happening too frequently; if so, then increase its check interval (multiplied by WIDGET_POSITION_INTERVAL_BACKOFF_MULTIPLIER)
                    var oldestChange = _widgetPositionChangeHistory.Peek();
                    var numberOfHistoryEvents = _widgetPositionChangeHistory.Count;
                    var totalDuration = currentRepositioningEventTimestamp.Subtract(oldestChange);
                    if (numberOfHistoryEvents == NUM_CHANGE_HISTORY_ENTRIES_TO_AVERAGE)
                    {
                        TimeSpan averageIntervalPerChange = totalDuration / numberOfHistoryEvents;
                        if (averageIntervalPerChange < _widgetPositionCheckupInterval * WIDGET_POSITION_INTERVAL_BACKOFF_MULTIPLIER)
                        {
                            _widgetPositionCheckupInterval *= WIDGET_POSITION_INTERVAL_BACKOFF_MULTIPLIER;
                            _trayButtonWidgetPositionCheckupTimer?.Change(_widgetPositionCheckupInterval, _widgetPositionCheckupInterval);
                        }
                    }
                }
            }
        }

        private System.Drawing.Point? ConvertMouseMessageLParamToScreenPoint(IntPtr lParam)
        {
            var x = (ushort)((lParam.ToInt64() >> 0) & 0xFFFF);
            var y = (ushort)((lParam.ToInt64() >> 16) & 0xFFFF);
            // convert x and y to screen coordinates
            Span<System.Drawing.Point> hitPoints = stackalloc System.Drawing.Point[1];
            hitPoints[0] = new System.Drawing.Point(x, y);

            // NOTE: the instructions for MapWindowPoints instruct us to call SetLastError before calling MapWindowPoints to ensure that we can distinguish a result of 0 from an error if the last win32 error wasn't set (because it wasn't an error)
            // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-mapwindowpoints
            Marshal.SetLastPInvokeError(0);
            //
            // NOTE: the PInvoke implementation of MapWindowPoints did not support passing in a POINT struct, so we manually declared the function
            var mapWindowPointsResult = Windows.Win32.PInvoke.MapWindowPoints(_hwnd, Windows.Win32.Foundation.HWND.Null, hitPoints);
            if (mapWindowPointsResult == 0)
            {
                // failed (if the last error != 0)
                var win32ErrorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                if (win32ErrorCode != 0)
                {
                    // failed; abort
                    Debug.Assert(false, "Could not map tray button hit point to screen coordinates");
                    return null;
                }
            }

            var result = hitPoints[0];
            return result;
        }
        private void Paint(IntPtr hWnd)
        {
            Windows.Win32.Graphics.Gdi.PAINTSTRUCT paintStruct;
            // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-beginpaint
            var paintDc = Windows.Win32.PInvoke.BeginPaint((Windows.Win32.Foundation.HWND)hWnd, out paintStruct);
            try
            {
                Windows.Win32.Graphics.Gdi.HDC bufferedPaintDc;
                // NOTE: paintStruct.rcPaint was an empty rect in our initial tests, so we are using a manually-created clientRect (from GetClientRect) here instead
                // see: https://learn.microsoft.com/en-us/windows/win32/api/uxtheme/nf-uxtheme-beginbufferedpaint
                var paintBufferHandle = Windows.Win32.PInvoke.BeginBufferedPaint(paintStruct.hdc, in paintStruct.rcPaint, Windows.Win32.UI.Controls.BP_BUFFERFORMAT.BPBF_TOPDOWNDIB, null, out bufferedPaintDc);
                try
                {
                    if (paintStruct.rcPaint.IsEmpty)
                    {
                        // no rectangle; nothing to do
                        return;
                    }

                    // clear our buffer background (to ARGB(0,0,0,0))
                    // see: https://learn.microsoft.com/en-us/windows/win32/api/uxtheme/nf-uxtheme-bufferedpaintclear
                    var bufferedPaintClearHresult = Windows.Win32.PInvoke.BufferedPaintClear(paintBufferHandle, paintStruct.rcPaint);
                    if (bufferedPaintClearHresult != Windows.Win32.Foundation.HRESULT.S_OK)
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
                        this.DrawHighlightBackground(bufferedPaintDc, paintStruct.rcPaint, System.Drawing.Color.White, highlightOpacity);
                    }

                    // calculate the size and position of our icon
                    int iconWidthAndHeight = this.CalculateWidthAndHeightForIcon(paintStruct.rcPaint);
                    //
                    var xLeft = ((paintStruct.rcPaint.right - paintStruct.rcPaint.left) - iconWidthAndHeight) / 2;
                    var yTop = ((paintStruct.rcPaint.bottom - paintStruct.rcPaint.top) - iconWidthAndHeight) / 2;

                    if (_iconHandle != IntPtr.Zero && iconWidthAndHeight > 0)
                    {
                        var drawIconSuccess = Windows.Win32.PInvoke.DrawIconEx(bufferedPaintDc, xLeft, yTop, (Windows.Win32.UI.WindowsAndMessaging.HICON)_iconHandle, iconWidthAndHeight, iconWidthAndHeight, 0 /* not animated */, Windows.Win32.Graphics.Gdi.HBRUSH.Null /* no triple-buffering */, Windows.Win32.UI.WindowsAndMessaging.DI_FLAGS.DI_NORMAL | Windows.Win32.UI.WindowsAndMessaging.DI_FLAGS.DI_NOMIRROR);
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
                    // complete the buffered paint operation and free the buffered paint handle
                    // see: https://learn.microsoft.com/en-us/windows/win32/api/uxtheme/nf-uxtheme-endbufferedpaint
                    var endBufferedPaintHresult = Windows.Win32.PInvoke.EndBufferedPaint(paintBufferHandle, true /* copy buffer to DC, completing the paint operation */);
                    Debug.Assert(endBufferedPaintHresult == Windows.Win32.Foundation.HRESULT.S_OK, "Error while attempting to end buffered paint operation for TrayButton; hresult: " + endBufferedPaintHresult.ToString());
                }
            }
            finally
            {
                // mark the end of painting; this function must always be called when BeginPaint was called (and succeeded), and only after drawing is complete
                //
                // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-endpaint
                // NOTE: per the MSDN docs, this function never returns zero (so there is no result to check)
                _ = Windows.Win32.PInvoke.EndPaint((Windows.Win32.Foundation.HWND)hWnd, in paintStruct);
            }
        }

        private int CalculateWidthAndHeightForIcon(Windows.Win32.Foundation.RECT rect)
        {
            int result;
            // NOTE: we currently measure the size of our icon by measuring the size of the rectangle
            // NOTE: we use the larger of the two dimensions (height vs width) to determine our icon size; we may reconsider this in the future if we support non-square icons
            int largerDimensionLenth;
            if (rect.bottom - rect.top > rect.right - rect.left)
            {
                largerDimensionLenth = rect.bottom - rect.top;
            }
            else
            {
                largerDimensionLenth = rect.right - rect.left;
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

        private void DrawHighlightBackground(Windows.Win32.Graphics.Gdi.HDC hdc, Windows.Win32.Foundation.RECT rect, System.Drawing.Color color, Double opacity)
        {
            // GDI doesn't have a concept of semi-transparent pixels - the only function that honours them is AlphaBlend.
            // Create a bitmap containing a single pixel - and then use AlphaBlend to stretch it to the size of the rect.

            // set up the 1x1 pixel bitmap's configuration
            Windows.Win32.Graphics.Gdi.BITMAPINFO pixelBitmapInfo;
            unsafe
            {
                pixelBitmapInfo = new Windows.Win32.Graphics.Gdi.BITMAPINFO()
                {
                    bmiHeader = new Windows.Win32.Graphics.Gdi.BITMAPINFOHEADER()
                    {
                        biSize = (uint)sizeof(Windows.Win32.Graphics.Gdi.BITMAPINFOHEADER),
                        biWidth = 1,
                        biHeight = 1,
                        biPlanes = 1, // must be 1
                        biBitCount = 32, // maximum of 2^32 colors
                        biCompression = (uint)Windows.Win32.Graphics.Gdi.BI_COMPRESSION.BI_RGB,
                        biSizeImage = 0,
                        biClrUsed = 0,
                        biClrImportant = 0
                    }
                };
            }

            // calculate the pixel color as a uint32 (in AARRGGBB order)
            uint pixelColor = (
                 (((uint)color.A) << 24) | // NOTE: we ignore the alpha value in our call to AlphaBlend
                 (((uint)color.R) << 16) |
                 (((uint)color.G) << 8) |
                 (((uint)color.B) << 0));

            // create the memory device context for the pixel
            var pixelDc = Windows.Win32.PInvoke.CreateCompatibleDC(hdc);
            if (pixelDc == IntPtr.Zero)
            {
                // failed; abort
                Debug.Assert(false, "Could not create device context for highlight pixel.");
                return;
            }
            try
            {
                Windows.Win32.DeleteObjectSafeHandle pixelDibSafeHandle;
                unsafe
                {
                    void* pixelDibBitValues;
                    pixelDibSafeHandle = Windows.Win32.PInvoke.CreateDIBSection(pixelDc, &pixelBitmapInfo, Windows.Win32.Graphics.Gdi.DIB_USAGE.DIB_RGB_COLORS, out pixelDibBitValues, null, 0);
                    if (pixelDibSafeHandle.IsInvalid) // NOTE: CreateDIBSection will return 0 on error, but checking for both -1 and 0 is fine (and convenient)
                    {
                        // failed; abort
                        Debug.Assert(false, "Could not create DIB for highlight pixel.");
                        return;
                    }

                    // write over the single pixel's value (with the passed-in pixel)
                    Marshal.WriteInt32((IntPtr)pixelDibBitValues, (int)pixelColor);
                }
                //
                try
                {
                    var selectedBitmapHandle = Windows.Win32.PInvoke.SelectObject(pixelDc, (Windows.Win32.Graphics.Gdi.HGDIOBJ)(IntPtr)pixelDibSafeHandle.DangerousGetHandle());
                    if (selectedBitmapHandle.IsNull)
                    {
                        // failed; abort
                        Debug.Assert(false, "Could not select object into the pixel device context.");
                        return;
                    }
                    try
                    {
                        // draw the highlight (stretching the pixel to the full rectangle size)
                        var blendFunction = new Windows.Win32.Graphics.Gdi.BLENDFUNCTION()
                        {
                            BlendOp = (byte)Windows.Win32.PInvoke.AC_SRC_OVER,
                            BlendFlags = 0, // must be zero
                            SourceConstantAlpha = (byte)(opacity * 255), // the requested opacity level
                            AlphaFormat = 0
                        };
                        _ = Windows.Win32.PInvoke.AlphaBlend(hdc, rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top, pixelDc, 0, 0, 1, 1, blendFunction);
                    }
                    finally
                    {
                        _ = Windows.Win32.PInvoke.SelectObject(pixelDc, selectedBitmapHandle);
                    }
                }
                finally
                {
                    pixelDibSafeHandle.Dispose();
                }
            }
            finally
            {
                _ = Windows.Win32.PInvoke.DeleteDC(pixelDc);
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
                    _mouseHook.UpdateTrackingRegion(changeToRect.Value);
                }
                else if (currentRect is not null)   
                {
                    _mouseHook.UpdateTrackingRegion(currentRect.Value);
                }
                else
                {
                    Debug.Assert(false, "Could not determine current RECT of tray button");
                }
            }

            // if changeToRect is more leftmost/topmost than the task button container's right side, then shrink the task button container appropriately
            Windows.Win32.Foundation.RECT? newTaskButtonContainerRect = null;
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

                if ((taskbarOrientation == Morphic.Controls.Orientation.Horizontal) && (taskButtonContainerRect.right > changeToRect.Value.left))
                {
                    var width = Math.Max(taskButtonContainerRect.right - taskButtonContainerRect.left - (taskButtonContainerRect.right - changeToRect.Value.left), 0);
                    var height = taskButtonContainerRect.bottom - taskButtonContainerRect.top;
                    newTaskButtonContainerRect = new Windows.Win32.Foundation.RECT()
                    {
                        left = taskButtonContainerRect.left,
                        top = taskButtonContainerRect.top,
                        right = taskButtonContainerRect.left + width,
                        bottom = taskButtonContainerRect.top + height
                    };
                }
                else if ((taskbarOrientation == Morphic.Controls.Orientation.Vertical) && taskButtonContainerRect.bottom > changeToRect.Value.top)
                {
                    var width = taskButtonContainerRect.right - taskButtonContainerRect.left;
                    var height = taskButtonContainerRect.bottom - taskButtonContainerRect.top - Math.Max(taskButtonContainerRect.bottom - changeToRect.Value.top, 0);
                    newTaskButtonContainerRect = new Windows.Win32.Foundation.RECT()
                    {
                        left = taskButtonContainerRect.left,
                        top = taskButtonContainerRect.top,
                        right = taskButtonContainerRect.left + width,
                        bottom = taskButtonContainerRect.top + height
                    };
                }
            }
            //
            if (newTaskButtonContainerRect is not null)
            {
                var taskButtonContainerHandle = TrayButtonNativeWindow.FindWindowsTaskbarTaskButtonContainerHandle();

                // shrink the task button container
                // NOTE: this is a blocking call, waiting until the task button container is resized; we do this intentionally so that we see its updated size synchronously
                var repositionTaskButtonContainerSuccess = Windows.Win32.PInvoke.SetWindowPos(
                    taskButtonContainerHandle,
                    Windows.Win32.Foundation.HWND.Null,
                    newTaskButtonContainerRect.Value.left,
                    newTaskButtonContainerRect.Value.top,
                    newTaskButtonContainerRect.Value.right - newTaskButtonContainerRect.Value.left,
                    newTaskButtonContainerRect.Value.bottom - newTaskButtonContainerRect.Value.top,
                    Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE /* do not activate the window */ |
                    Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOMOVE /* retain the current x and y position, out of an abundance of caution */ |
                    Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOZORDER /* retain the current Z order (ignoring the hWndInsertAfter parameter) */
                );

                if (repositionTaskButtonContainerSuccess == false)
                {
                    // failed; abort
                    Debug.Assert(false, "Could not resize taskbar's task button container");
                    return;
                }

                // capture our control's native window's new position and size
                // NOTE: since we suppressed repositioning of the taskbar container above (i.e. just resizing it), we are only capturing the updated size here (out of an abundance of caution)
                _trayButtonPositionAndSize.Width = newTaskButtonContainerRect!.Value.right - newTaskButtonContainerRect!.Value.left;
                _trayButtonPositionAndSize.Height = newTaskButtonContainerRect!.Value.bottom - newTaskButtonContainerRect!.Value.top;
            }

            // if our button needs to move (either because we don't know the old RECT or because the new RECT is different), do so now
            if (changeToRect is not null)
            {
                if (currentRect.HasValue == false || (Windows.Win32.PInvoke.EqualRect(currentRect.Value, changeToRect.Value) == false))
                {
                    var taskbarHandle = TrayButtonNativeWindow.FindWindowsTaskbarHandle();

                    // convert our tray button's position from desktop coordinates to "child" coordinates within the taskbar
                    Span<System.Drawing.Point> childRectAsPoints = stackalloc System.Drawing.Point[2]; // 2 indicates that lpPoints is a RECT
                    childRectAsPoints[0] = new() { X/*left*/ = changeToRect.Value.left, Y/*top*/ = changeToRect.Value.top };
                    childRectAsPoints[1] = new() { X/*right*/ = changeToRect.Value.right, Y/*bottom*/ = changeToRect.Value.bottom };
                    //
                    // NOTE: the instructions for MapWindowPoints instruct us to call SetLastError before calling MapWindowPoints to ensure that we can distinguish a result of 0 from an error if the last win32 error wasn't set (because it wasn't an error)
                    // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-mapwindowpoints
                    Marshal.SetLastPInvokeError(0);
                    //
                    var mapWindowPointsResult = Windows.Win32.PInvoke.MapWindowPoints(Windows.Win32.Foundation.HWND.Null /* use screen coordinates */, taskbarHandle, childRectAsPoints);
                    if (mapWindowPointsResult == 0 && Marshal.GetLastWin32Error() != (int)Windows.Win32.Foundation.WIN32_ERROR.ERROR_SUCCESS)
                    {
                        // failed; abort
                        Debug.Assert(false, "Could not map tray button RECT points to taskbar window handle");
                        return;
                    }
                    // assemble the mapped points into a System.Drawing.Rectangle
                    Rectangle childRect = new System.Drawing.Rectangle() { X = childRectAsPoints[0].X, Y = childRectAsPoints[0].Y, Width = childRectAsPoints[1].X - childRectAsPoints[0].X, Height = childRectAsPoints[1].Y - childRectAsPoints[0].Y };

                    var repositionTrayButtonSuccess = Windows.Win32.PInvoke.SetWindowPos(
                         _hwnd,
                         Windows.Win32.Foundation.HWND.HWND_TOP,
                         childRect.X,
                         childRect.Y,
                         childRect.Width,
                         childRect.Height,
                         Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE /* do not activate the window */ |
                         Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW /* display the tray button */
                         );

                    if (repositionTrayButtonSuccess == false)
                    {
                        // failed; abort
                        Debug.Assert(false, "Could not reposition and/or resize tray button");
                        return;
                    }

                    // capture our control's native window's new position and size
                    _trayButtonPositionAndSize = new(childRect.X, childRect.Y, childRect.Width, childRect.Height);
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

        private (Windows.Win32.Foundation.HWND TaskbarHandle, Windows.Win32.Foundation.HWND TaskButtonContainerHandle, Windows.Win32.Foundation.HWND NotifyTrayHandle) GetTaskbarTripletHandles()
        {
            var taskbarHandle = TrayButtonNativeWindow.FindWindowsTaskbarHandle();
            var taskButtonContainerHandle = TrayButtonNativeWindow.FindWindowsTaskbarTaskButtonContainerHandle();
            var notifyTrayHandle = TrayButtonNativeWindow.FindWindowsTaskbarNotificationTrayHandle();

            return (taskbarHandle, taskButtonContainerHandle, notifyTrayHandle);
        }

        private (Windows.Win32.Foundation.RECT TaskbarRect, Windows.Win32.Foundation.RECT TaskButtonContainerRect, Windows.Win32.Foundation.RECT NotifyTrayRect)? GetTaskbarTripletRects(Windows.Win32.Foundation.HWND taskbarHandle, Windows.Win32.Foundation.HWND taskButtonContainerHandle, Windows.Win32.Foundation.HWND notifyTrayHandle)
        {
            // find the taskbar and its rect
            Windows.Win32.Foundation.RECT taskbarRect = new Windows.Win32.Foundation.RECT();
            if (Windows.Win32.PInvoke.GetWindowRect(taskbarHandle, out taskbarRect) == false)
            {
                // failed; abort
                Debug.Assert(false, "Could not obtain window handle to taskbar.");
                return null;
            }

            // find the window handles and rects of the task button container and the notify tray (which are children inside of the taskbar)
            //
            Windows.Win32.Foundation.RECT taskButtonContainerRect = new Windows.Win32.Foundation.RECT();
            if (Windows.Win32.PInvoke.GetWindowRect(taskButtonContainerHandle, out taskButtonContainerRect) == false)
            {
                // failed; abort
                Debug.Assert(false, "Could not obtain window handle to taskbar's task button list container.");
                return null;
            }
            //
            Windows.Win32.Foundation.RECT notifyTrayRect = new Windows.Win32.Foundation.RECT();
            if (Windows.Win32.PInvoke.GetWindowRect(notifyTrayHandle, out notifyTrayRect) == false)
            {
                // failed; abort
                Debug.Assert(false, "Could not obtain window handle to taskbar's notify tray.");
                return null;
            }

            return (taskbarRect, taskButtonContainerRect, notifyTrayRect);
        }

        private (Windows.Win32.Foundation.RECT availableAreaRect, List<Windows.Win32.Foundation.RECT> childRects) CalculateEmptyRectsBetweenTaskButtonContainerAndNotifyTray(IntPtr taskbarHandle, Morphic.Controls.Orientation taskbarOrientation, bool isRightToLeft, Windows.Win32.Foundation.RECT taskbarRect, Windows.Win32.Foundation.RECT taskButtonContainerRect, Windows.Win32.Foundation.RECT notifyTrayRect)
        {
            // calculate the total "free area" rectangle (the area between the task button container and the notify tray where we want to place our tray button)
            Windows.Win32.Foundation.RECT freeAreaAvailableRect;
            if (taskbarOrientation == Morphic.Controls.Orientation.Horizontal)
            {
                if (isRightToLeft == false)
                {
                    freeAreaAvailableRect = new Windows.Win32.Foundation.RECT()
                    {
                        left = taskButtonContainerRect.right,
                        top = taskbarRect.top,
                        right = taskButtonContainerRect.right + Math.Max(notifyTrayRect.left - taskButtonContainerRect.right, 0),
                        bottom = taskbarRect.top + Math.Max(taskbarRect.bottom - taskbarRect.top, 0)
                    };
                }
                else
                {
                    freeAreaAvailableRect = new Windows.Win32.Foundation.RECT()
                    {
                        left = notifyTrayRect.right,
                        top = taskbarRect.top,
                        right = notifyTrayRect.right + Math.Max(taskButtonContainerRect.left - notifyTrayRect.right, 0),
                        bottom = taskbarRect.top + Math.Max(taskbarRect.bottom - taskbarRect.top, 0)
                    };
                }
            }
            else
            {
                freeAreaAvailableRect = new Windows.Win32.Foundation.RECT()
                {
                    left = taskbarRect.left,
                    top = taskButtonContainerRect.bottom,
                    right = taskbarRect.left + Math.Max(taskbarRect.right - taskbarRect.left, 0),
                    bottom = taskButtonContainerRect.bottom + Math.Max(notifyTrayRect.top - taskButtonContainerRect.bottom, 0)
                };
            }

            // capture a list of all child windows within the taskbar; we'll use this list to enumerate the rects of all the taskbar's children
            var taskbarChildHandles = TrayButtonNativeWindow.EnumerateChildWindows(taskbarHandle);
            //
            // find the rects of all windows within the taskbar; we need this information so that we do not overlap any other accessory windows which are trying to sit in the same area as us
            var taskbarChildHandlesWithRects = new Dictionary<IntPtr, Windows.Win32.Foundation.RECT>();
            foreach (var taskbarChildHandle in taskbarChildHandles)
            {
                Windows.Win32.Foundation.RECT taskbarChildRect = new Windows.Win32.Foundation.RECT();
                if (Windows.Win32.PInvoke.GetWindowRect((Windows.Win32.Foundation.HWND)taskbarChildHandle, out taskbarChildRect) == true)
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
                    if (RectMath.RectIsInside(taskbarChildRect, taskButtonContainerRect))
                    {
                        taskbarChildHandlesWithRects.Remove(taskbarChildHandle);
                    }
                }
            }

            // remove our own (tray button) window handle from the list (so that we don't see our current screen rect as "taken" in the list of occupied RECTs)
            taskbarChildHandlesWithRects.Remove((IntPtr)_hwnd.Value);

            // create a list of children which are located between the task button container and the notify tray (i.e. windows which are occupying the same region we want to
            // occupy...so we can try to avoid overlapping)
            List<Windows.Win32.Foundation.RECT> freeAreaChildRects = new List<Windows.Win32.Foundation.RECT>();
            foreach (var taskbarChildHandle in taskbarChildHandles)
            {
                if (taskbarChildHandlesWithRects.ContainsKey(taskbarChildHandle) == true)
                {
                    var taskbarChildRect = taskbarChildHandlesWithRects[taskbarChildHandle];
                    if ((RectMath.RectIsInside(taskbarChildRect, freeAreaAvailableRect) == true) &&
                    (RectMath.RectHasNonZeroWidthOrHeight(taskbarChildRect) == false))
                    {
                        freeAreaChildRects.Add(taskbarChildRect);
                    }
                }
            }

            return (freeAreaAvailableRect, freeAreaChildRects);
        }

        // NOTE: this function returns a newPosition IF the tray button should be moved
        private (Windows.Win32.Foundation.RECT? currentRect, Windows.Win32.Foundation.RECT? changeToRect, Morphic.Controls.Orientation orientation)? CalculateCurrentAndTargetRectOfTrayButton()
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
            Morphic.Controls.Orientation taskbarOrientation;
            if ((taskbarRect.right - taskbarRect.left) > (taskbarRect.bottom - taskbarRect.top))
            {
                taskbarOrientation = Morphic.Controls.Orientation.Horizontal;
            }
            else
            {
                taskbarOrientation = Morphic.Controls.Orientation.Vertical;
            }

            // if the taskbar is horizontal, determine if it's LeftToRight (standard) or RightToLeft (for Arabic, Hebrew, etc.)
            bool isRightToLeft = false;
            if (taskbarOrientation == Morphic.Controls.Orientation.Horizontal)
            {
                var centerXOfTaskbar = taskbarRect.left + ((taskbarRect.right - taskbarRect.left) / 2);
                if (notifyTrayRect.right < centerXOfTaskbar)
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
            if (taskbarOrientation == Morphic.Controls.Orientation.Horizontal)
            {
                trayButtonHeight = taskbarRect.bottom - taskbarRect.top;
                //
                // NOTE: in high contrast mode, the taskbar has a 1px border at the top; reduce the tray button height by 1px to avoid overlapping the border
                //       (and because the new rect is bottom-aligned, reducing the height also shifts it down by 1px)
                if (System.Windows.SystemParameters.HighContrast == true)
                {
                    trayButtonHeight -= 1;
                }
                //
                trayButtonWidth = (int)((Double)trayButtonHeight * 0.8);
            }
            else
            {
                trayButtonWidth = taskbarRect.right - taskbarRect.left;
                trayButtonHeight = (int)((Double)trayButtonWidth * 0.8);
            }

            // get our current rect (in case we can just reuse the current position...and also to make sure it doesn't need to be resized)
            Windows.Win32.Foundation.RECT currentRectAsNonNullable;
            Windows.Win32.Foundation.RECT? currentRect = null;
            Windows.Win32.Foundation.RECT? currentRectForResult = null;
            if (Windows.Win32.PInvoke.GetWindowRect(_hwnd, out currentRectAsNonNullable) == true)
            {
                currentRect = currentRectAsNonNullable;
                currentRectForResult = currentRectAsNonNullable;
            }

            // if the current position of our window isn't the right size for our icon, then set it to NULL so we don't try to reuse it.
            if ((currentRect is not null) &&
                 ((currentRect.Value.right - currentRect.Value.left != trayButtonWidth) || (currentRect.Value.bottom - currentRect.Value.top != trayButtonHeight)))
            {
                currentRect = null;
            }

            // calculate the new rect for our tray button's window
            Windows.Win32.Foundation.RECT? newRect = null;

            // if the space occupied by our already-existing rect is not overlapped by anyone else and is in the free area, keep using the same space
            if ((currentRect is not null) && (RectMath.RectIntersects(currentRect.Value, freeAreaAvailableRect) == true))
            {
                // by default, assume that our currentRect is still available (i.e. not overlapped)
                bool currentRectIsNotOverlapped = true;

                // make sure we do not overlap another control in the free area
                foreach (var freeAreaChildRect in freeAreaChildRects)
                {
                    if (RectMath.RectIntersects(currentRect.Value, freeAreaChildRect) == true)
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
                if (taskbarOrientation == Morphic.Controls.Orientation.Horizontal)
                {
                    // horizontal taskbar: find the leftmost rect in the available space (which we'll then carve the "rightmost" section out of)
                    // OBSERVATION: leftmost is actually rightmost in RTL layouts (e.g. Arabic, Hebrew)
                    Windows.Win32.Foundation.RECT leftmostRect = freeAreaAvailableRect;

                    foreach (var freeAreaChildRect in freeAreaChildRects)
                    {
                        if (isRightToLeft == false)
                        {
                            if (freeAreaChildRect.left < leftmostRect.right)
                            {
                                leftmostRect.right = freeAreaChildRect.left;
                            }
                        }
                        else
                        {
                            if (freeAreaChildRect.right > leftmostRect.left)
                            {
                                leftmostRect.left = freeAreaChildRect.right;
                            }
                        }
                    }

                    // choose the rightmost space in the leftmostRect area (or leftmost for RTL layouts); expand our tray button towards the left (right for RTL) if/as necessary
                    if (isRightToLeft == false)
                    {
                        newRect = new Windows.Win32.Foundation.RECT()
                        {
                            left = leftmostRect.right - trayButtonWidth,
                            top = leftmostRect.bottom - trayButtonHeight,
                            right = leftmostRect.right,
                            bottom = leftmostRect.bottom
                        };
                    }
                    else
                    {
                        newRect = new Windows.Win32.Foundation.RECT()
                        {
                            left = leftmostRect.left,
                            top = leftmostRect.bottom - trayButtonHeight,
                            right = leftmostRect.left + trayButtonWidth,
                            bottom = leftmostRect.bottom
                        };
                    }
                }
                else
                {
                    // vertical taskbar: find the topmost rect in the available space (which we'll then carve the "bottommost" section out of)
                    Windows.Win32.Foundation.RECT topmostRect = freeAreaAvailableRect;

                    foreach (var freeAreaChildRect in freeAreaChildRects)
                    {
                        if (freeAreaChildRect.top < topmostRect.bottom)
                        {
                            topmostRect.bottom = freeAreaChildRect.top;
                        }
                    }

                    // choose the bottommost space in the topmostRect area; expand our tray button towards the top if/as necessary
                    newRect = new Windows.Win32.Foundation.RECT()
                    {
                        left = topmostRect.right - trayButtonWidth,
                        top = topmostRect.bottom - trayButtonHeight,
                        right = topmostRect.right,
                        bottom = topmostRect.bottom
                    };
                }
            }

            Windows.Win32.Foundation.RECT? changeToRect = null;
            if ((newRect.HasValue != currentRectForResult.HasValue) || (newRect.HasValue == true && Windows.Win32.PInvoke.EqualRect(newRect!.Value, currentRectForResult!.Value) == false))
            {
                changeToRect = newRect;
            }
            
            return (currentRectForResult, changeToRect, taskbarOrientation);
        }

        private bool RequestRedraw()
        {
            return Windows.Win32.PInvoke.RedrawWindow(
                 _hwnd,
                 (Windows.Win32.Foundation.RECT?)null,
                 null,
                 Windows.Win32.Graphics.Gdi.REDRAW_WINDOW_FLAGS.RDW_ERASE | Windows.Win32.Graphics.Gdi.REDRAW_WINDOW_FLAGS.RDW_INVALIDATE | Windows.Win32.Graphics.Gdi.REDRAW_WINDOW_FLAGS.RDW_ALLCHILDREN
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
                var enumFunction = new Windows.Win32.UI.WindowsAndMessaging.WNDENUMPROC(TrayButtonNativeWindow.EnumerateChildWindowsCallback);
                Windows.Win32.PInvoke.EnumChildWindows((Windows.Win32.Foundation.HWND)parentHwnd, enumFunction, (nint)resultGCHandleAsIntPtr);

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
        internal static Windows.Win32.Foundation.BOOL EnumerateChildWindowsCallback(Windows.Win32.Foundation.HWND hwnd, Windows.Win32.Foundation.LPARAM lParam)
        {
            // convert lParam back into the result list object
            var resultGCHandle = GCHandle.FromIntPtr((IntPtr)(nint)lParam);
            List<IntPtr>? result = resultGCHandle.Target as List<IntPtr>;

            if (result is not null)
            {
                result.Add((IntPtr)hwnd.Value);
            }
            else
            {
                Debug.Assert(false, "Could not enumerate child windows");
            }

            return true;
        }

        internal static Windows.Win32.Foundation.HWND FindWindowsTaskbarHandle()
        {
            return Windows.Win32.PInvoke.FindWindow("Shell_TrayWnd", null);
        }

        private static Windows.Win32.Foundation.HWND FindWindowsTaskbarTaskButtonContainerHandle()
        {
            var taskbarHandle = TrayButtonNativeWindow.FindWindowsTaskbarHandle();
            if (taskbarHandle == Windows.Win32.Foundation.HWND.Null)
            {
                return Windows.Win32.Foundation.HWND.Null;
            }
            return Windows.Win32.PInvoke.FindWindowEx(taskbarHandle, Windows.Win32.Foundation.HWND.Null, "ReBarWindow32", null);
        }

        private static Windows.Win32.Foundation.HWND FindWindowsTaskbarNotificationTrayHandle()
        {
            var taskbarHandle = TrayButtonNativeWindow.FindWindowsTaskbarHandle();
            if (taskbarHandle == Windows.Win32.Foundation.HWND.Null)
            {
                return Windows.Win32.Foundation.HWND.Null;
            }
            return Windows.Win32.PInvoke.FindWindowEx(taskbarHandle, Windows.Win32.Foundation.HWND.Null, "TrayNotifyWnd", null);
        }

    }
    #endregion
}
#endif