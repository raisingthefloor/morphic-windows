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

using Morphic.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Morphic.Controls.TrayButton.Windows11;

internal class TrayButtonNativeWindow : IDisposable
{
    private bool disposedValue;

    // NOTE: s_morphicTrayButtonClassInfoExAtom and s_wndProcDelegate are initialized together
    private static ushort? s_morphicTrayButtonClassInfoExAtom = null;
    // create a static wndproc delegate (which will work as a trampoline to a window's wndproc function, using the hwnd-specific userdata which stores a reference to each instance)
    // [this is done to prevent the delegate from being GC'd while the window class is registered]
    private static Windows.Win32.UI.WindowsAndMessaging.WNDPROC? s_wndProcDelegate;

    private Windows.Win32.Foundation.HWND _hwnd = Windows.Win32.Foundation.HWND.Null;
    // NOTE: a GC handle to the class instance is stored as userdata for each native window's hwnd (so that we can trampoline from the static wndproc to the instance-specific WndProc callback)
    private GCHandle _gcHandle;

    private bool _visible;
    private bool _taskbarIsTopmost;

    private System.Threading.Timer? _resurfaceTaskbarButtonTimer;
    private static readonly TimeSpan RESURFACE_TASKBAR_BUTTON_INTERVAL_TIMESPAN = new TimeSpan(0, 0, 30);

    private ArgbImageNativeWindow? _argbImageNativeWindow = null;

    private Windows.Win32.Foundation.HWND _tooltipWindowHandle;
    private bool _tooltipInfoAdded = false;
    private string? _tooltipText;

    private Windows.Win32.Foundation.RECT _trayButtonPositionAndSize;
    public System.Drawing.Rectangle PositionAndSize
    {
        get
        {
            return new(_trayButtonPositionAndSize.X, _trayButtonPositionAndSize.Y, _trayButtonPositionAndSize.Width, _trayButtonPositionAndSize.Height);
        }
    }

    private Windows.Win32.UI.Accessibility.HWINEVENTHOOK _locationChangeWindowEventHook = Windows.Win32.UI.Accessibility.HWINEVENTHOOK.Null;
    private Windows.Win32.UI.Accessibility.WINEVENTPROC? _locationChangeWindowEventProc = null;

    private Windows.Win32.UI.Accessibility.HWINEVENTHOOK _objectReorderWindowEventHook = Windows.Win32.UI.Accessibility.HWINEVENTHOOK.Null;
    private Windows.Win32.UI.Accessibility.WINEVENTPROC? _objectReorderWindowEventProc = null;

    private Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue;

    // state variables to ensure that we don't call ObjectReorderWindowEventProc more than once every 20ms
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _objectReorderThrottleTimer;
    private bool _objectReorderPending = false; // a reorder proc call is in process
    private LinkedList<Windows.Win32.Foundation.HWND> _objectReorderedQueue = new(); // a trailing reorder proc call has been requested (with these paramaters)
    private object _objectReorderLockObject = new(); // for synchronization

    [Flags]
    private enum TrayButtonVisualStateFlags
    {
        None = 0, // normal visual state
        Hover = 1,
        LeftButtonPressed = 2,
        RightButtonPressed = 4
    }
    private TrayButtonVisualStateFlags _visualState = TrayButtonVisualStateFlags.None;

    private const byte ALPHA_VALUE_FOR_TRANSPARENT_BUT_HIT_TESTABLE = 1;

    public event EventHandler<Morphic.Controls.MouseEventArgs>? MouseUp;

    private TrayButtonNativeWindow()
    {
    }

    //

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // dispose managed state (managed objects)
                if (_objectReorderWindowEventHook != IntPtr.Zero)
                {
                    Windows.Win32.PInvoke.UnhookWinEvent(_objectReorderWindowEventHook);
                }
                if (_locationChangeWindowEventHook != IntPtr.Zero)
                {
                    Windows.Win32.PInvoke.UnhookWinEvent(_locationChangeWindowEventHook);
                }

                _argbImageNativeWindow?.Dispose();

                _resurfaceTaskbarButtonTimer?.Dispose();
            }

            // free unmanaged resources (unmanaged objects) and override finalizer
            //
            // destroy the tooltip window BEFORE the main window (since the tooltip is owned by the main window
            // and would be automatically destroyed with it, leaving us with an invalid handle)
            _ = this.DestroyTooltipWindow();
            //
            // free window handle
            if (_hwnd != IntPtr.Zero)
            {
                _ = Windows.Win32.PInvoke.DestroyWindow(_hwnd);
                _hwnd = Windows.Win32.Foundation.HWND.Null;
            }

            // clean up the _gcHandle if it's already allocated; we allocate the GC Handle to make our event handler trampoline possible
            if (_gcHandle.IsAllocated)
            {
                _gcHandle.Free();
            }

            // set large fields to null
            // [none]

            disposedValue = true;
        }
    }

    // NOTE: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    ~TrayButtonNativeWindow()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    //

    public static MorphicResult<TrayButtonNativeWindow, ICreateNewError> CreateNew()
    {
        var result = new TrayButtonNativeWindow();

        result._dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        /* register a custom native window class for our Morphic Tray Button (or refer to the already-registered class, if we captured it earlier in the application's execution) */
        const string nativeWindowClassName = "Morphic-TrayButton";
        //
        if (s_morphicTrayButtonClassInfoExAtom is null)
        {
            // register our control's custom native window class using a static wndproc (which will act as a trampoline to an instance-specific WndProc callback)
            s_wndProcDelegate = TrayButtonNativeWindow.StaticWndProc;
            //
            var hCursor = Windows.Win32.PInvoke.LoadCursor(Windows.Win32.Foundation.HINSTANCE.Null, Windows.Win32.PInvoke.IDC_ARROW);
            if (hCursor.IsNull == true)
            {
                Debug.Assert(false, "Could not load arrow cursor");
                var win32ErrorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                return MorphicResult.ErrorResult<ICreateNewError>(new ICreateNewError.Win32Error((uint)win32ErrorCode));
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

                    // NOTE: RegisterClassEx returns an ATOM (or 0 if the call failed)
                    registerClassResult = Windows.Win32.PInvoke.RegisterClassEx(lpWndClassEx);
                }
            }
			//
            if (registerClassResult == 0) // failure
            {
                var win32ErrorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                if (win32ErrorCode == (int)Windows.Win32.Foundation.WIN32_ERROR.ERROR_CLASS_ALREADY_EXISTS)
                {
                    Debug.Assert(false, "Class was already registered; we should have recorded this ATOM, and we cannot proceed");
                }
                return MorphicResult.ErrorResult<ICreateNewError>(new ICreateNewError.Win32Error((uint)win32ErrorCode));
            }
            s_morphicTrayButtonClassInfoExAtom = registerClassResult;
        }

        /* calculate the initial position of the tray button */
        var calculatePositionResult = TrayButtonNativeWindow.CalculatePositionAndSizeForTrayButton(null);
        if (calculatePositionResult.IsError)
        {
            switch (calculatePositionResult.Error!)
            {
                case ICalculatePositionAndSizeForTrayButtonError.CouldNotFindTaskbarRelatedHandle:
                    return MorphicResult.ErrorResult<ICreateNewError>(new ICreateNewError.CouldNotFindTaskbarRelatedHandle());
                case ICalculatePositionAndSizeForTrayButtonError.CannotFitOnTaskbar:
                    return MorphicResult.ErrorResult<ICreateNewError>(new ICreateNewError.CannotFitOnTaskbar());
                case ICalculatePositionAndSizeForTrayButtonError.Win32Error(var win32ErrorCode):
                    return MorphicResult.ErrorResult<ICreateNewError>(new ICreateNewError.Win32Error(win32ErrorCode));
                default:
                    throw new MorphicUnhandledErrorException();
            }
        }
        var trayButtonPositionAndSize = calculatePositionResult.Value!;
        if (trayButtonPositionAndSize.Width == 0 || trayButtonPositionAndSize.Height == 0)
        {
            Debug.Assert(false, "Tray button position calculated, but it's zero pixels in size");
            return MorphicResult.ErrorResult<ICreateNewError>(new ICreateNewError.CannotFitOnTaskbar());
        }
        //
        // capture our initial position and size
        result._trayButtonPositionAndSize = trayButtonPositionAndSize;

        /* get the handle for the taskbar; it will be the owner of our native window (so that our window sits above it in the zorder) */
        // NOTE: we will still need to push our window to the front of its owner's zorder stack in some circumstances, as certain actions (such as popping up the task list balloons above the task bar) will reorder the taskbar's zorder and push us behind the taskbar
        // NOTE: making the taskbar our owner has the side-effect of putting our window above full-screen applications (even though our window is not itself "always on top"); we will need to hide our window whenever a window goes full-screen on the same monitor (and re-show our window whenever the window exits full-screen mode)
        var getTaskbarHandleResult = TrayButtonNativeWindow.GetWindowsTaskbarHandle();
        if (getTaskbarHandleResult.IsError == true)
        {
            switch (getTaskbarHandleResult.Error!)
            {
                case Morphic.WindowsNative.IWin32ApiError.Win32Error(var win32ErrorCode):
                    return MorphicResult.ErrorResult<ICreateNewError>(new ICreateNewError.Win32Error(win32ErrorCode));
                default:
                    throw new MorphicUnhandledErrorException();
            }
        }
        var taskbarHandle = getTaskbarHandleResult.Value!;
        if (taskbarHandle == Windows.Win32.Foundation.HWND.Null)
        {
            return MorphicResult.ErrorResult<ICreateNewError>(new ICreateNewError.CouldNotFindTaskbarRelatedHandle());
        }

        // capture the current state of the taskbar; this is combined with the visibility value to determine whether or not the window is actually visible to the user
        var getTaskbarIsTopmostResult = TrayButtonNativeWindow.GetTaskbarIsTopmost();
        if (getTaskbarIsTopmostResult.IsError == true)
        {
            switch (getTaskbarIsTopmostResult.Error!)
            {
                case IGetTaskbarIsTopmostError.CouldNotFindTaskbarRelatedHandle:
                    return MorphicResult.ErrorResult<ICreateNewError>(new ICreateNewError.CouldNotFindTaskbarRelatedHandle());
                case IGetTaskbarIsTopmostError.Win32Error(var win32ErrorCode):
                    return MorphicResult.ErrorResult<ICreateNewError>(new ICreateNewError.Win32Error(win32ErrorCode));
                default:
                    throw new MorphicUnhandledErrorException();
            }
        }
        result._taskbarIsTopmost = getTaskbarIsTopmostResult.Value!;


        /* create an instance of our native window */

        var windowX = trayButtonPositionAndSize.left;
        var windowY = trayButtonPositionAndSize.top;
        var windowWidth = trayButtonPositionAndSize.right - trayButtonPositionAndSize.left;
        var windowHeight = trayButtonPositionAndSize.bottom - trayButtonPositionAndSize.top;

        Windows.Win32.Foundation.HWND handle;
        unsafe
        {
            var atomAsString = new Windows.Win32.Foundation.PCWSTR((char*)(nint)s_morphicTrayButtonClassInfoExAtom!.Value);
            fixed (char* pointerToNativeWindowClassName = nativeWindowClassName)
            {
                handle = Windows.Win32.PInvoke.CreateWindowEx(
                    dwExStyle: Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_LAYERED/* | Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE..WS_EX_TOOLWINDOW*//* | Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE..WS_EX_TOPMOST*/,
                    lpClassName: atomAsString,
                    lpWindowName: pointerToNativeWindowClassName,
                    dwStyle: /*Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_CLIPSIBLINGS | */Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_POPUP /*| Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_TABSTOP*/ | Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_VISIBLE,
                    X: windowX,
                    Y: windowY,
                    nWidth: windowWidth,
                    nHeight: windowHeight,
                    hWndParent: taskbarHandle,
                    hMenu: Windows.Win32.UI.WindowsAndMessaging.HMENU.Null,
                    hInstance: Windows.Win32.Foundation.HINSTANCE.Null,
                    lpParam: null
                );
            }
        }
        if (handle == IntPtr.Zero)
        {
            var win32ErrorCode = Marshal.GetLastWin32Error();
            return MorphicResult.ErrorResult<ICreateNewError>(new ICreateNewError.Win32Error((uint)win32ErrorCode));
        }
        result._hwnd = handle;

        // store instance reference in GWL_USERDATA for the hwnd (to enable message routing from the static wndproc to the instance-specific WndProc callback)
        result._gcHandle = GCHandle.Alloc(result);
        //
        // NOTE: SetWindowLongPtr can return 0 even if there is no error; see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowlongptrw
        System.Runtime.InteropServices.Marshal.SetLastPInvokeError(0);
        var setWindowLongPtrResult = PInvokeExtensions.SetWindowLongPtr_IntPtr(handle, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_USERDATA, (nint)(IntPtr)result._gcHandle);
        if (setWindowLongPtrResult == 0)
        {
            var win32ErrorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            if (win32ErrorCode != 0)
            {
                return MorphicResult.ErrorResult<ICreateNewError>(new ICreateNewError.Win32Error((uint)win32ErrorCode));
            }
        }

        // set the window's background transparency to 0% (in the range of a 0 to 255 alpha channel, with 255 being 100%)
        // NOTE: an alpha value of 0 (0%) makes our window complete see-through but it has the side-effect of not capturing any mouse events; to counteract this,
        //       we set our "tranparent" alpha value to 1 instead.  We will only use an alpha value of 0 when we want our window to be invisible and also not capture mouse events
        var setBackgroundAlphaResult = TrayButtonNativeWindow.SetBackgroundAlpha(result._hwnd, ALPHA_VALUE_FOR_TRANSPARENT_BUT_HIT_TESTABLE);
        if (setBackgroundAlphaResult.IsError)
        {
            switch (setBackgroundAlphaResult.Error!)
            {
                case Morphic.WindowsNative.IWin32ApiError.Win32Error(uint win32ErrorCode):
                    return MorphicResult.ErrorResult<ICreateNewError>(new ICreateNewError.Win32Error(win32ErrorCode));
                default:
                    throw new MorphicUnhandledErrorException();
            }
        }

        // since we are making the native window visible by default, set its visibility state
        // NOTE: this native window's visibility state is separate from the TrayButton's visibility state; the TrayButton's state is the desired visible state from the user's perspective (and can report when the button cannot currently be drawn), whereas
        //       this native window's visibility state indicates whether or not the native control should be visible IF the taskbar is on top (i.e. not in a full-screen video scenario, etc.)
        result._visible = true;

        // create an instance of the ArgbImageNativeWindow to hold our icon; we cannot draw the bitmap directly on this window as the bitmap would then be alpha-blended the same % as our background (instead of being independently blended over our window)
        var argbImageNativeWindowResult = ArgbImageNativeWindow.CreateNew(result._hwnd, windowX, windowY, windowWidth, windowHeight);
        if (argbImageNativeWindowResult.IsError == true)
        {
            result.Dispose();
            //
            // NOTE: ArgbImageNativeWindow returns the same ICreateNewError errors, so we can just pass them along...
            return MorphicResult.ErrorResult<ICreateNewError>(argbImageNativeWindowResult.Error!);
        }
        result._argbImageNativeWindow = argbImageNativeWindowResult.Value!;

        /* wire up windows event hook listeners, to watch for events which require adjusting the zorder of our window */

        // NOTE: we could provide the process handle and thread of processes/threads which we were interested in specifically, but for now we're interested in more than one window so we filter broadly
        var locationChangeWindowEventProc = new Windows.Win32.UI.Accessibility.WINEVENTPROC(result.LocationChangeWindowEventProc);
        var locationChangeWindowEventHook = Windows.Win32.PInvoke.SetWinEventHook(
            Windows.Win32.PInvoke.EVENT_OBJECT_LOCATIONCHANGE, // start index
            Windows.Win32.PInvoke.EVENT_OBJECT_LOCATIONCHANGE, // end index
            Windows.Win32.Foundation.HMODULE.Null,
            locationChangeWindowEventProc,
            0, // process handle (0 = all processes on current desktop)
            0, // thread (0 = all existing threads on current desktop)
            Windows.Win32.PInvoke.WINEVENT_OUTOFCONTEXT | Windows.Win32.PInvoke.WINEVENT_SKIPOWNPROCESS
        );
        Debug.Assert(locationChangeWindowEventHook != IntPtr.Zero, "Could not wire up location change window event listener for tray button");
        if (locationChangeWindowEventHook == IntPtr.Zero)
        {
            return MorphicResult.ErrorResult<ICreateNewError>(new ICreateNewError.CouldNotWireUpWatchEvents());
        }
        //
        result._locationChangeWindowEventHook = locationChangeWindowEventHook;
        // NOTE: we must capture the delegate so that it is not garbage collected; otherwise the native callbacks can crash the .NET execution engine
        result._locationChangeWindowEventProc = locationChangeWindowEventProc;
        //
        //
        //
        var objectReorderWindowEventProc = new Windows.Win32.UI.Accessibility.WINEVENTPROC(result.ObjectReorderWindowEventProc);
        var objectReorderWindowEventHook = Windows.Win32.PInvoke.SetWinEventHook(
             Windows.Win32.PInvoke.EVENT_OBJECT_REORDER, // start index
             Windows.Win32.PInvoke.EVENT_OBJECT_REORDER, // end index
             Windows.Win32.Foundation.HMODULE.Null,
             objectReorderWindowEventProc,
             0, // process handle (0 = all processes on current desktop)
             0, // thread (0 = all existing threads on current desktop)
            Windows.Win32.PInvoke.WINEVENT_OUTOFCONTEXT | Windows.Win32.PInvoke.WINEVENT_SKIPOWNPROCESS
        );
        Debug.Assert(objectReorderWindowEventHook != IntPtr.Zero, "Could not wire up object reorder window event listener for tray button");
        if (objectReorderWindowEventHook == IntPtr.Zero)
        {
            return MorphicResult.ErrorResult<ICreateNewError>(new ICreateNewError.CouldNotWireUpWatchEvents());
        }
        //
        result._objectReorderWindowEventHook = objectReorderWindowEventHook;
        // NOTE: we must capture the delegate so that it is not garbage collected; otherwise the native callbacks can crash the .NET execution engine
        result._objectReorderWindowEventProc = objectReorderWindowEventProc;

        // create the tooltip window (although we won't provide it with any actual text until/unless the text is set
        result._tooltipWindowHandle = result.CreateTooltipWindow();
        result._tooltipText = null;

        // start a timer on the new instance, to resurface the Morphic tray button icon from time to time (just in case it gets hidden under the taskbar)
        result._resurfaceTaskbarButtonTimer = new(result.ResurfaceTaskButtonTimerCallback, null, TrayButtonNativeWindow.RESURFACE_TASKBAR_BUTTON_INTERVAL_TIMESPAN, TrayButtonNativeWindow.RESURFACE_TASKBAR_BUTTON_INTERVAL_TIMESPAN);

        return MorphicResult.OkResult(result);
    }

    //

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

        // if the instance is already set up (i.e. during window creation), pass the message to its instance-specific WndProc callback
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

    // instance wndproc — handles messages after GWL_USERDATA is set up
    private Windows.Win32.Foundation.LRESULT WndProc(Windows.Win32.Foundation.HWND hWnd, uint msg, Windows.Win32.Foundation.WPARAM wParam, Windows.Win32.Foundation.LPARAM lParam)
    {
        IntPtr? result = null;

        switch (msg)
        {
            case Windows.Win32.PInvoke.WM_LBUTTONDOWN:
                {
                    _visualState |= TrayButtonVisualStateFlags.LeftButtonPressed;
                    //
                    var updateVisualStateAlphaResult = this.UpdateVisualStateAlpha();
                    Debug.Assert(updateVisualStateAlphaResult.IsSuccess, "Could not update visual state.");

                    result = IntPtr.Zero;
                }
                break;
            case Windows.Win32.PInvoke.WM_LBUTTONUP:
                {
                    _visualState &= ~TrayButtonVisualStateFlags.LeftButtonPressed;
                    //
                    var updateVisualStateAlphaResult = this.UpdateVisualStateAlpha();
                    Debug.Assert(updateVisualStateAlphaResult.IsSuccess, "Could not update visual state.");

                    var convertLParamResult = this.ConvertMouseMessageLParamToScreenPoint(lParam);
                    if (convertLParamResult.IsSuccess == true)
                    {
                        var hitPoint = convertLParamResult.Value!;

                        var mouseArgs = new Morphic.Controls.MouseEventArgs(Morphic.Controls.MouseButtons.Left, 1, hitPoint.X, hitPoint.Y);
                        Task.Run(() => this.MouseUp?.Invoke(this, mouseArgs));
                    }
                    else
                    {
                        switch (convertLParamResult.Error!)
                        {
                            case Morphic.WindowsNative.IWin32ApiError.Win32Error(var win32ErrorCode):
                                Debug.Assert(false, "Could not map tray button hit point to screen coordinates; win32 errcode: " + win32ErrorCode.ToString());
                                break;
                            default:
                                throw new MorphicUnhandledErrorException();
                        }
                    }

                    result = IntPtr.Zero;
                }
                break;
            case Windows.Win32.PInvoke.WM_MOUSELEAVE:
                {
                    // the cursor has left our tray button's window area; remove the hover state from our visual state
                    _visualState &= ~TrayButtonVisualStateFlags.Hover;

                    // NOTE: as we aren't able to track mouseup when the cursor is outside of the button, we also remove the left/right button pressed states here
                    //       (and then we check them again when the mouse moves back over the button)
                    _visualState &= ~TrayButtonVisualStateFlags.LeftButtonPressed;
                    _visualState &= ~TrayButtonVisualStateFlags.RightButtonPressed;
                    //
                    var updateVisualStateAlphaResult = this.UpdateVisualStateAlpha();
                    Debug.Assert(updateVisualStateAlphaResult.IsSuccess, "Could not update visual state.");

                    result = IntPtr.Zero;
                }
                break;
            case Windows.Win32.PInvoke.WM_MOUSEMOVE:
                {
                    // NOTE: this message is raised while we are tracking (whereas the SETCURSOR WM_MOUSEMOVE is captured when the mouse cursor first enters the window)
                    //
                    // NOTE: if the cursor moves off of the tray button while the button is pressed, we would have removed the "pressed" focus as well as the "hover" focus
                    //       because we can't track mouseup when the cursor is outside of the button; consequently we also need to check the mouse pressed state during
                    //       mousemove so that we can re-visualize (re-set flags for) the pressed state as appropriate.
                    if (((_visualState & TrayButtonVisualStateFlags.LeftButtonPressed) == 0) && ((wParam.Value.ToUInt64() & PInvokeExtensions.MK_LBUTTON) != 0))
                    {
                        _visualState |= TrayButtonVisualStateFlags.LeftButtonPressed;
                        //
                        var updateVisualStateAlphaResult = this.UpdateVisualStateAlpha();
                        Debug.Assert(updateVisualStateAlphaResult.IsSuccess, "Could not update visual state.");
                    }
                    if (((_visualState & TrayButtonVisualStateFlags.RightButtonPressed) == 0) && ((wParam.Value.ToUInt64() & PInvokeExtensions.MK_RBUTTON) != 0))
                    {
                        _visualState |= TrayButtonVisualStateFlags.RightButtonPressed;
                        //
                        var updateVisualStateAlphaResult = this.UpdateVisualStateAlpha();
                        Debug.Assert(updateVisualStateAlphaResult.IsSuccess, "Could not update visual state.");
                    }

                    result = IntPtr.Zero;
                }
                break;
            case Windows.Win32.PInvoke.WM_NCDESTROY:
                {
                    // NOTE: we are calling this in response to WM_NCDESTROY (instead of WM_DESTROY)
                    //       see: https://learn.microsoft.com/en-us/windows/win32/api/uxtheme/nf-uxtheme-bufferedpaintinit
                    var bufferedPaintUnInitResult = Windows.Win32.PInvoke.BufferedPaintUnInit();
                    if (bufferedPaintUnInitResult != 0)
                    {
                        Debug.Assert(false, "Could not uninitialize buffered painting (in response to WM_NCDESTROY)");
                    }

                    // NOTE: we pass along this message (i.e. we don't return a "handled" result)

                    // clear GWL_USERDATA so no more messages are routed to this instance
                    // NOTE: SetWindowLongPtr can return 0 even if there is no error; see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowlongptrw
                    System.Runtime.InteropServices.Marshal.SetLastPInvokeError(0);
                    var setWindowLongPtrResult = PInvokeExtensions.SetWindowLongPtr_IntPtr(hWnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_USERDATA, 0);
                    Debug.Assert(setWindowLongPtrResult != 0 || System.Runtime.InteropServices.Marshal.GetLastWin32Error() == 0);
                }
                break;
            case Windows.Win32.PInvoke.WM_NCPAINT:
                {
                    // we suppress all painting of the non-client areas (so that we can have a transparent window)
                    // return zero, indicating that we processed the message
                    result = IntPtr.Zero;
                }
                break;
            case Windows.Win32.PInvoke.WM_PAINT:
                {
                    // NOTE: we override the built-in paint functionality with our own Paint function
                    this.OnPaintWindowsMessage(hWnd);
                    //
                    // return zero, indicating that we processed the message
                    result = IntPtr.Zero;
                }
                break;
            case Windows.Win32.PInvoke.WM_RBUTTONDOWN:
                {
                    _visualState |= TrayButtonVisualStateFlags.RightButtonPressed;
                    //
                    var updateVisualStateAlphaResult = this.UpdateVisualStateAlpha();
                    Debug.Assert(updateVisualStateAlphaResult.IsSuccess, "Could not update visual state.");

                    result = IntPtr.Zero;
                }
                break;
            case Windows.Win32.PInvoke.WM_RBUTTONUP:
                {
                    _visualState &= ~TrayButtonVisualStateFlags.RightButtonPressed;
                    //
                    var updateVisualStateAlphaResult = this.UpdateVisualStateAlpha();
                    Debug.Assert(updateVisualStateAlphaResult.IsSuccess, "Could not update visual state.");

                    var convertLParamResult = this.ConvertMouseMessageLParamToScreenPoint(lParam);
                    if (convertLParamResult.IsSuccess)
                    {
                        var hitPoint = convertLParamResult.Value!;

                        var mouseArgs = new Morphic.Controls.MouseEventArgs(Morphic.Controls.MouseButtons.Right, 1, hitPoint.X, hitPoint.Y);
                        Task.Run(() => this.MouseUp?.Invoke(this, mouseArgs));
                    }
                    else
                    {
                        switch (convertLParamResult.Error!)
                        {
                            case Morphic.WindowsNative.IWin32ApiError.Win32Error(var win32ErrorCode):
                                Debug.Assert(false, "Could not map tray button hit point to screen coordinates; win32 errcode: " + win32ErrorCode.ToString());
                                break;
                            default:
                                throw new MorphicUnhandledErrorException();
                        }
                    }

                    result = IntPtr.Zero;
                }
                break;
            case Windows.Win32.PInvoke.WM_SETCURSOR:
                {
                    // wParam: window handle
                    // lParam: low-order word is the high-test result for the cursor position; high-order word specifies the mouse message that triggered this event

                    var hitTestResult = (uint)((lParam.Value.ToInt64() >> 0) & 0xFFFF);
                    var mouseMsg = (uint)((lParam.Value.ToInt64() >> 16) & 0xFFFF);

                    // NOTE: for messages which we handle, we return "TRUE" (1) to halt further message processing; this may not technically be necessary
                    //       see: https://learn.microsoft.com/en-us/windows/win32/menurc/wm-setcursor
                    switch (mouseMsg)
                    {
                        case Windows.Win32.PInvoke.WM_LBUTTONDOWN:
                            {
                                _visualState |= TrayButtonVisualStateFlags.LeftButtonPressed;
                                //
                                var updateVisualStateAlphaResult = this.UpdateVisualStateAlpha();
                                Debug.Assert(updateVisualStateAlphaResult.IsSuccess, "Could not update visual state.");

                                result = new IntPtr(1);
                            }
                            break;
                        case Windows.Win32.PInvoke.WM_LBUTTONUP:
                            {
                                _visualState &= ~TrayButtonVisualStateFlags.LeftButtonPressed;
                                //
                                var updateVisualStateAlphaResult = this.UpdateVisualStateAlpha();
                                Debug.Assert(updateVisualStateAlphaResult.IsSuccess, "Could not update visual state.");

                                result = new IntPtr(1);
                            }
                            break;
                        case Windows.Win32.PInvoke.WM_MOUSEMOVE:
                            {
                                // if we are not yet tracking the mouse position (i.e. this is effectively "mouse enter"), then start tracking it now (so that we can capture its move out of our box)
                                // NOTE: we track whether or not we are tracking the mouse by analyzing the hover state of our visual state flags
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
                                        var win32ErrorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                                        Debug.Assert(false, "Could not set up tracking of tray button window area; win32 errcode: " + win32ErrorCode.ToString());
                                    }

                                    _visualState |= TrayButtonVisualStateFlags.Hover;
                                    //
                                    var updateVisualStateAlphaResult = this.UpdateVisualStateAlpha();
                                    Debug.Assert(updateVisualStateAlphaResult.IsSuccess, "Could not update visual state.");
                                }
                                result = new IntPtr(1);
                            }
                            break;
                        case Windows.Win32.PInvoke.WM_RBUTTONDOWN:
                            {
                                _visualState |= TrayButtonVisualStateFlags.RightButtonPressed;
                                //
                                var updateVisualStateAlphaResult = this.UpdateVisualStateAlpha();
                                Debug.Assert(updateVisualStateAlphaResult.IsSuccess, "Could not update visual state.");

                                result = new IntPtr(1);
                            }
                            break;
                        case Windows.Win32.PInvoke.WM_RBUTTONUP:
                            {
                                _visualState &= ~TrayButtonVisualStateFlags.RightButtonPressed;
                                //
                                var updateVisualStateAlphaResult = this.UpdateVisualStateAlpha();
                                Debug.Assert(updateVisualStateAlphaResult.IsSuccess, "Could not update visual state.");

                                result = new IntPtr(1);
                            }
                            break;
                        default:
                            // unhandled setcurosr mouse message
                            break;
                    }
                }
                break;
            default:
                break;
        }

        // if we handled the message, return 'result'; otherwise, call through to DefWindowProc to handle the message
        if (result is not null)
        {
            return (Windows.Win32.Foundation.LRESULT)result.Value!;
        }

        return Windows.Win32.PInvoke.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    // NOTE: this function may ONLY be called when responding to a WM_PAINT message
    // NOTE: we do not return any error result from this function; instead, we log or assert errors and then just abort the paint attempt
    private void OnPaintWindowsMessage(Windows.Win32.Foundation.HWND hWnd)
    {
        // create a device context for drawing; we must destroy this automatically in a finally block.  We are effectively replicating the functionality of C++'s CPaintDC.
        Windows.Win32.Graphics.Gdi.PAINTSTRUCT paintStruct;
        // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-beginpaint
        var deviceContext = Windows.Win32.PInvoke.BeginPaint(hWnd, out paintStruct)!;
        if (deviceContext == IntPtr.Zero)
        {
            // no display context is available
            Debug.Assert(false, "Cannot paint TrayButton in response to WM_PAINT message; no display device context is available.");
            return;
        }
        try
        {
            // NOTE: to avoid flickering, we use buffered painting to erase the background, fill the background with a single (white) brush, and then apply the painted area to the window in a single paint operation
            // see: https://learn.microsoft.com/en-us/windows/win32/api/uxtheme/nf-uxtheme-beginbufferedpaint
            Windows.Win32.Graphics.Gdi.HDC bufferedPaintDc;
            var paintBufferHandle = Windows.Win32.PInvoke.BeginBufferedPaint(paintStruct.hdc, in paintStruct.rcPaint, Windows.Win32.UI.Controls.BP_BUFFERFORMAT.BPBF_TOPDOWNDIB, null, out bufferedPaintDc);
            if (paintBufferHandle == IntPtr.Zero)
            {
                var win32ErrorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                Debug.Assert(false, "Cannot begin a buffered paint operation for TrayButton (when responding to a WM_PAINT message); win32 errcode: " + win32ErrorCode.ToString());
                return;
            }
            try
            {
                // NOTE: this is the section where we call all of our actual (buffered) paint operations

                // clear our window's background (i.e. the buffer background)
                // see: https://learn.microsoft.com/en-us/windows/win32/api/uxtheme/nf-uxtheme-bufferedpaintclear
                var bufferedPaintClearHresult = Windows.Win32.PInvoke.BufferedPaintClear(paintBufferHandle, paintStruct.rcPaint);
                if (bufferedPaintClearHresult != Windows.Win32.Foundation.HRESULT.S_OK)
                {
                    Debug.Assert(false, "Could not clear background of TrayButton window--using buffered clearing (when responding to a WM_Paint message); result: " + bufferedPaintClearHresult.ToString());
                    return;
                }

                // create a solid white brush
                // see: https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-createsolidbrush
                var createSolidBrushResult = Windows.Win32.PInvoke.CreateSolidBrush((Windows.Win32.Foundation.COLORREF)0x00FFFFFF);
                if (createSolidBrushResult == IntPtr.Zero)
                {
                    Debug.Assert(false, "Could not create white brush to paint the background of the TrayButton window (when responding to a WM_Paint message).");
                    return;
                }
                var whiteBrush = createSolidBrushResult;
                //
                try
                {
                    int fillRectResult;
                    unsafe
                    {
                        // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-fillrect
                        fillRectResult = Windows.Win32.PInvoke.FillRect(bufferedPaintDc, &paintStruct.rcPaint, whiteBrush);
                    }
                    Debug.Assert(fillRectResult != 0, "Could not fill highlight background of Tray icon with white brush");
                }
                finally
                {
                    // clean up the white solid brush we created for the fill operation
                    // see: https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-deleteobject
                    var deleteObjectSuccess = Windows.Win32.PInvoke.DeleteObject(whiteBrush);
                    Debug.Assert(deleteObjectSuccess == true, "Could not delete white brush object used to highlight Tray icon");
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
            _ = Windows.Win32.PInvoke.EndPaint(hWnd, in paintStruct);
        }
    }

    public bool Visibility
    {
        get => _visible;
        set
        {
            if (_visible != value)
            {
                _visible = value;
                var updateVisibilityResult = this.UpdateVisibility();
                if (updateVisibilityResult.IsError == true)
                {
                    // NOTE: we may want to consider parsing out errors here
                    Debug.Assert(false, "Could not update .Visible");
                }
            }
        }
    }

    private MorphicResult<MorphicUnit, Morphic.WindowsNative.IWin32ApiError> UpdateVisibility()
    {
        _argbImageNativeWindow?.SetVisible(this.ShouldWindowBeVisible());
        //
        var updateVisualStateAlphaResult = this.UpdateVisualStateAlpha();
        if (updateVisualStateAlphaResult.IsError == true)
        {
            switch (updateVisualStateAlphaResult.Error!)
            {
                case Morphic.WindowsNative.IWin32ApiError.Win32Error(var win32ErrorCode):
                    return MorphicResult.ErrorResult<Morphic.WindowsNative.IWin32ApiError>(new Morphic.WindowsNative.IWin32ApiError.Win32Error(win32ErrorCode));
                default:
                    throw new MorphicUnhandledErrorException();
            }
        }

        return MorphicResult.OkResult();
    }

    private bool ShouldWindowBeVisible()
    {
        return (_visible == true) && (_taskbarIsTopmost == true);
    }

    private MorphicResult<MorphicUnit, Morphic.WindowsNative.IWin32ApiError> UpdateVisualStateAlpha()
    {
        // default to "Normal" visual state
        Double highlightOpacity = 0.0;

        if (this.ShouldWindowBeVisible())
        {
            if (((_visualState & TrayButtonVisualStateFlags.LeftButtonPressed) != 0) ||
                    ((_visualState & TrayButtonVisualStateFlags.RightButtonPressed) != 0))
            {
                highlightOpacity = 0.25;
            }
            else if ((_visualState & TrayButtonVisualStateFlags.Hover) != 0)
            {
                highlightOpacity = 0.1;
            }

            var alpha = (byte)((double)255 * highlightOpacity);
            var setBackgroundAlphaResult = TrayButtonNativeWindow.SetBackgroundAlpha(_hwnd, Math.Max(alpha, ALPHA_VALUE_FOR_TRANSPARENT_BUT_HIT_TESTABLE));
            if (setBackgroundAlphaResult.IsError == true)
            {
                switch (setBackgroundAlphaResult.Error!)
                {
                    case Morphic.WindowsNative.IWin32ApiError.Win32Error(var win32ErrorCode):
                        return MorphicResult.ErrorResult<Morphic.WindowsNative.IWin32ApiError>(new Morphic.WindowsNative.IWin32ApiError.Win32Error(win32ErrorCode));
                    default:
                        throw new MorphicUnhandledErrorException();
                }
            }
        }
        else
        {
            // collapsed or hidden controls should be invisible
            var setBackgroundAlphaResult = TrayButtonNativeWindow.SetBackgroundAlpha(_hwnd, 0);
            if (setBackgroundAlphaResult.IsError == true)
            {
                switch (setBackgroundAlphaResult.Error!)
                {
                    case Morphic.WindowsNative.IWin32ApiError.Win32Error(var win32ErrorCode):
                        return MorphicResult.ErrorResult<Morphic.WindowsNative.IWin32ApiError>(new Morphic.WindowsNative.IWin32ApiError.Win32Error(win32ErrorCode));
                    default:
                        throw new MorphicUnhandledErrorException();
                }
            }
        }

        return MorphicResult.OkResult();
    }

    private static MorphicResult<MorphicUnit, Morphic.WindowsNative.IWin32ApiError> SetBackgroundAlpha(Windows.Win32.Foundation.HWND handle, byte alpha)
    {
        // set the window's background transparency to 0% (in the range of a 0 to 255 alpha channel, with 255 being 100%)
        var setLayeredWindowAttributesSuccess = Windows.Win32.PInvoke.SetLayeredWindowAttributes(handle, (Windows.Win32.Foundation.COLORREF)0, alpha, Windows.Win32.UI.WindowsAndMessaging.LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA);
        if (setLayeredWindowAttributesSuccess == false)
        {
            var win32Error = (uint)System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            return MorphicResult.ErrorResult<Morphic.WindowsNative.IWin32ApiError>(new Morphic.WindowsNative.IWin32ApiError.Win32Error(win32Error));
        }

        return MorphicResult.OkResult();
    }

    //

    private void LocationChangeWindowEventProc(Windows.Win32.UI.Accessibility.HWINEVENTHOOK hWinEventHook, uint eventType, Windows.Win32.Foundation.HWND hwnd, int idObject, int idChild, uint idEventThread, uint dwmsEventTime)
    {
        // we cannot process a location change message if the hwnd is zero
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        // attempt to capture the class name for the window; if the window has already been destroyed, this will fail
        string? className = null;
        var getWindowClassNameResult = TrayButtonNativeWindow.GetWindowClassName(hwnd);
        if (getWindowClassNameResult.IsError == true)
        {
            // this window has no class name (or has already been destroyed); there is nothing for us to do
            return;
        }
        className = getWindowClassNameResult.Value!;

        if (className == "TaskListThumbnailWnd" || className == "TaskListOverlayWnd")
        {
            // if the window being moved was one of the task list windows (i.e. the windows that pop up above the taskbar), then our zorder has probably been pushed down.  To counteract this, we make sure our window is "TOPMOST"
            // NOTE: in initial testing, we set the window to TOPMOST in the ExStyles during handle construction.  This was not always successful in keeping the window topmost, however, possibly because the taskbar becomes "more" topmost sometimes.  So we re-set the window zorder here instead (without activating the window).
            this.BringTaskButtonTopmostWithoutActivating();
        }
        else if (className == "Shell_TrayWnd"/* || className == "ReBarWindow32"*/ || className == "TrayNotifyWnd")
        {
            // if the window being moved was the taskbar or the taskbar's notification tray, recalculate and update our position
            // NOTE: we might also consider watching for location changes of the task button container, but as we don't use it for position/size calculations at the present time we do not watch accordingly
            var repositionResult = this.RecalculatePositionAndRepositionWindow();
            // NOTE: if we want to handle error cases of RecalculatePositionAndRepositionWindow, we can do so here.
            Debug.Assert(repositionResult.IsSuccess, "Could not reposition Tray Button window");
        }
    }

    //

    // NOTE: this function is used to temporary suppress taskbar button resurface checks (which are done when the app needs to place other content above the taskbar and above our control...such as a right-click context menu)
    public void SuppressTaskbarButtonResurfaceChecks(bool suppress)
    {
        if (suppress == true)
        {
            _resurfaceTaskbarButtonTimer?.Dispose();
            _resurfaceTaskbarButtonTimer = null;
        }
        else
        {
            _resurfaceTaskbarButtonTimer = new(this.ResurfaceTaskButtonTimerCallback, null, TrayButtonNativeWindow.RESURFACE_TASKBAR_BUTTON_INTERVAL_TIMESPAN, TrayButtonNativeWindow.RESURFACE_TASKBAR_BUTTON_INTERVAL_TIMESPAN);
        }
    }

    //

    // NOTE: just in case we miss any edge cases to resurface our button, we resurface it from time to time on a timer
    private void ResurfaceTaskButtonTimerCallback(object? state)
    {
        this.BringTaskButtonTopmostWithoutActivating();
    }

    private void BringTaskButtonTopmostWithoutActivating()
    {
        var setWindowPosSuccess = Windows.Win32.PInvoke.SetWindowPos(_hwnd, Windows.Win32.Foundation.HWND.HWND_TOPMOST, 0, 0, 0, 0, Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOMOVE | Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOSIZE | Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
        if (setWindowPosSuccess == false)
        {
            var win32ErrorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            Debug.Assert(false, "Could not bring task button topmost; win32 error: " + win32ErrorCode.ToString());
        }
    }

    private void ObjectReorderWindowEventProc(Windows.Win32.UI.Accessibility.HWINEVENTHOOK hWinEventHook, uint eventType, Windows.Win32.Foundation.HWND hwnd, int idObject, int idChild, uint idEventThread, uint dwmsEventTime)
    {
        // make sure that HandleObjectReorder isn't called in an infinite RAPID loop (if two windows are fighting for "topmost")
        const int THROTTLE_WAIT_TIME_IN_MS = 20;
        lock (_objectReorderLockObject)
        {
            // if an object reorder is pending, then set the _objectReorderQueued flag so that the timer handling the pending event knows to retrigger itself
            if (_objectReorderPending == true)
            {
                _objectReorderedQueue.AddLast(hwnd);
            }
            else
            {
                // mark that we've started a reorder and also the wait timer (which will check to see if another call needs to execute)
                _objectReorderPending = true;
                //
                Debug.Assert(_objectReorderedQueue.Count == 0);

                // immediately start one call using the provided hwnd
                _ = _dispatcherQueue.TryEnqueue(() => { this.HandleObjectReorder(hwnd); });

                // create a timer to check if the reorder needs to happen again after WAIT_TIME_IN_MS
                _objectReorderThrottleTimer = _dispatcherQueue.CreateTimer();
                _objectReorderThrottleTimer!.Interval = TimeSpan.FromMilliseconds(THROTTLE_WAIT_TIME_IN_MS);
                _objectReorderThrottleTimer!.IsRepeating = false;
                _objectReorderThrottleTimer!.Tick += (s, e) =>
                {
                    _objectReorderThrottleTimer?.Stop();

                    lock (_objectReorderLockObject)
                    {
                        if (_objectReorderedQueue.Count > 0)
                        {
                            // a reorder proc call request came in while we were waiting; queue up that call now
                            var queuedHwnd = _objectReorderedQueue.First!.Value;
                            _objectReorderedQueue.RemoveFirst();
                            // now remove any other instances of this hwnd; if this is too aggressive, we could just remove the first element (in a loop, for as long as it equaled this hwnd)
                            var queueNode = _objectReorderedQueue.First;
                            while (queueNode is not null)
                            {
                                var nextQueueNode = queueNode.Next;
                                if (queueNode.Value == queuedHwnd)
                                {
                                    _objectReorderedQueue.Remove(queueNode);
                                }
                                queueNode = nextQueueNode;
                            }

                            _ = _dispatcherQueue.TryEnqueue(() => { this.HandleObjectReorder(queuedHwnd); });

                            // also restart this timer
                            _objectReorderThrottleTimer?.Start();
                        }
                        else
                        {
                            // nothing left to do
                            _objectReorderPending = false;

                            // the timer should now be discarded
                            _objectReorderThrottleTimer = null;
                        }
                    }
                };
                _objectReorderThrottleTimer.Start();
            }
        }
    }

    private void HandleObjectReorder(Windows.Win32.Foundation.HWND hwnd) { 
        // we cannot process an object reorder message if the hwnd is zero
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        // attempt to capture the class name for the window; if the window has already been destroyed, this will fail
        string? className = null;
        var getWindowClassNameResult = TrayButtonNativeWindow.GetWindowClassName(hwnd);
        if (getWindowClassNameResult.IsError == true)
        {
            Debug.WriteLine("WARNING: Could not get window class name; has the window already been destroyed?");
            return;
        }
        className = getWindowClassNameResult.Value!;

        // capture the desktop handle
        // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getdesktopwindow
        var desktopHandle = Windows.Win32.PInvoke.GetDesktopWindow();

        // if the reordered window was either the taskbar or the desktop, update the _taskbarIsTopmost state; this will generally be triggered when an app goes full-screen (or full-screen mode is exited)
        if (className == "Shell_TrayWnd" || hwnd == desktopHandle)
        {
            // whenever the window ordering changes, resurface our control
            this.BringTaskButtonTopmostWithoutActivating();

            // determine if the taskbar is topmost; the taskbar's topmost flag is removed when an app goes full-screen and should cover the taskbar (e.g. a full-screen video)
            var getTaskbarIsTopmostResult = TrayButtonNativeWindow.GetTaskbarIsTopmost(/*hwnd -- not passed in, since the handle could be the desktop */);
            if (getTaskbarIsTopmostResult.IsError == true)
            {
                switch (getTaskbarIsTopmostResult.Error!)
                {
                    case IGetTaskbarIsTopmostError.CouldNotFindTaskbarRelatedHandle:
                        Debug.Assert(false, "Could not determine if taskbar is topmost; taskbar-related handle could not be found.");
                        return;
                    case IGetTaskbarIsTopmostError.Win32Error(var win32ErrorCode):
                        Debug.Assert(false, "Could not determine if taskbar is topmost; win32 errcode: " + win32ErrorCode.ToString());
                        return;
                    default:
                        throw new MorphicUnhandledErrorException();
                }
            }
            _taskbarIsTopmost = getTaskbarIsTopmostResult.Value!;
            //
            // NOTE: UpdateVisibility takes both the .Visibility property and the topmost state of the taskbar into consideration to determine whether or not to show the control
            var updateVisibilityResult = this.UpdateVisibility();
            if (updateVisibilityResult.IsError == true)
            {
                // NOTE: we may want to consider parsing out errors here
                Debug.Assert(false, "Could not update .Visibility");
            }
        }
    }

    private interface IGetTaskbarIsTopmostError
    {
        public record CouldNotFindTaskbarRelatedHandle : IGetTaskbarIsTopmostError;
        public record Win32Error(uint Win32ErrorCode) : IGetTaskbarIsTopmostError;
    }
    //
    private static MorphicResult<bool, IGetTaskbarIsTopmostError> GetTaskbarIsTopmost(Windows.Win32.Foundation.HWND? taskbarHWnd = null)
    {
        Windows.Win32.Foundation.HWND taskbarHandle;
        if (taskbarHWnd is not null)
        {
            taskbarHandle = taskbarHWnd!.Value;
        }
        else
        {
            var getTaskbarHandleResult = TrayButtonNativeWindow.GetWindowsTaskbarHandle();
            if (getTaskbarHandleResult.IsError == true)
            {
                switch (getTaskbarHandleResult.Error!)
                {
                    case Morphic.WindowsNative.IWin32ApiError.Win32Error(var win32ErrorCode):
                        return MorphicResult.ErrorResult<IGetTaskbarIsTopmostError>(new IGetTaskbarIsTopmostError.Win32Error(win32ErrorCode));
                    default:
                        throw new MorphicUnhandledErrorException();
                }
            }
            //
            taskbarHandle = getTaskbarHandleResult.Value!;
            if (taskbarHandle == Windows.Win32.Foundation.HWND.Null)
            {
                return MorphicResult.ErrorResult<IGetTaskbarIsTopmostError>(new IGetTaskbarIsTopmostError.CouldNotFindTaskbarRelatedHandle());
            }
        }

        var taskbarWindowExStyle = PInvokeExtensions.GetWindowLongPtr_IntPtr(taskbarHandle, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        if (taskbarWindowExStyle == IntPtr.Zero)
        {
            var win32ErrorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            return MorphicResult.ErrorResult<IGetTaskbarIsTopmostError>(new IGetTaskbarIsTopmostError.Win32Error((uint)win32ErrorCode));
        }
        var taskbarIsTopmost = ((nint)taskbarWindowExStyle & (nint)Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_TOPMOST) != 0;

        return MorphicResult.OkResult(taskbarIsTopmost);
    }

    private interface IRecalculatePositionAndRepositionWindowError
    {
        public record CouldNotBringToTop(uint Win32ErrorCode) : IRecalculatePositionAndRepositionWindowError;
        public record CouldNotCalculatePositionAndSizeForTrayButton(ICalculatePositionAndSizeForTrayButtonError InnerError) : IRecalculatePositionAndRepositionWindowError;
        public record CouldNotPositionAndResizeBitmap(IPositionAndResizeBitmapError InnerError) : IRecalculatePositionAndRepositionWindowError;
        public record CouldNotSetTooltip(IUpdateTooltipTextAndTrackingError InnerError) : IRecalculatePositionAndRepositionWindowError;
        public record CouldNotSetWindowPosition(uint Win32ErrorCode) : IRecalculatePositionAndRepositionWindowError;
    }
    //
    private MorphicResult<MorphicUnit, IRecalculatePositionAndRepositionWindowError> RecalculatePositionAndRepositionWindow()
    {
        // first, reposition our control (NOTE: this will be required to subsequently determine the position of our bitmap)
        var calculatePositionResult = TrayButtonNativeWindow.CalculatePositionAndSizeForTrayButton(_hwnd);
        if (calculatePositionResult.IsError)
        {
            Debug.Assert(false, "Cannot calculate position for tray button");
            //
            var innerError = calculatePositionResult.Error!;
            return MorphicResult.ErrorResult<IRecalculatePositionAndRepositionWindowError>(new IRecalculatePositionAndRepositionWindowError.CouldNotCalculatePositionAndSizeForTrayButton(innerError));
        }
        var trayButtonPositionAndSize = calculatePositionResult.Value!;
        //
        var size = new System.Drawing.Size(trayButtonPositionAndSize.right - trayButtonPositionAndSize.left, trayButtonPositionAndSize.bottom - trayButtonPositionAndSize.top);
        //
        // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos
        var setWindowPosResult = Windows.Win32.PInvoke.SetWindowPos(_hwnd, Windows.Win32.Foundation.HWND.Null, trayButtonPositionAndSize.left, trayButtonPositionAndSize.top, size.Width, size.Height, Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOZORDER | Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
        if (setWindowPosResult == 0)
        {
            var win32ErrorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            Debug.Assert(false, "SetWindowPos failed while trying to reposition TrayButton native window; win32 errcode: " + win32ErrorCode.ToString());
            return MorphicResult.ErrorResult<IRecalculatePositionAndRepositionWindowError>(new IRecalculatePositionAndRepositionWindowError.CouldNotSetWindowPosition((uint)win32ErrorCode));
        }
        //
        // capture our updated position and size
        _trayButtonPositionAndSize = trayButtonPositionAndSize;

        // once the control is repositioned, reposition the bitmap
        var bitmapSize = _argbImageNativeWindow?.GetBitmapSize();
        if (bitmapSize is not null)
        {
            var positionAndResizeBitmapResult = this.PositionAndResizeBitmap(bitmapSize.Value);
            if (positionAndResizeBitmapResult.IsError == true)
            {
                Debug.Assert(false, "Could not position and resize bitmap.");
                var innerError = positionAndResizeBitmapResult.Error!;
                return MorphicResult.ErrorResult<IRecalculatePositionAndRepositionWindowError>(new IRecalculatePositionAndRepositionWindowError.CouldNotPositionAndResizeBitmap(innerError));
            }
        }

        // also reposition the tooltip's tracking rectangle
        if (_tooltipText is not null)
        {
            var updateTooltipTextAndTrackingResult = this.UpdateTooltipTextAndTracking();
            if (updateTooltipTextAndTrackingResult.IsError == true)
            {
                Debug.Assert(false, "Could not update tooltip text");
                var innerError = updateTooltipTextAndTrackingResult.Error!;
                return MorphicResult.ErrorResult<IRecalculatePositionAndRepositionWindowError>(new IRecalculatePositionAndRepositionWindowError.CouldNotSetTooltip(innerError));
            }
        }

        // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-bringwindowtotop
        var bringWindowToTopSuccess = Windows.Win32.PInvoke.BringWindowToTop(_hwnd);
        if (bringWindowToTopSuccess == false)
        {
            var win32ErrorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            Debug.Assert(false, "Could not bring tray button window to top; win32 errcode: " + win32ErrorCode.ToString());
            return MorphicResult.ErrorResult<IRecalculatePositionAndRepositionWindowError>(new IRecalculatePositionAndRepositionWindowError.CouldNotBringToTop((uint)win32ErrorCode));
        }

        return MorphicResult.OkResult();
    }

    private static MorphicResult<string, Morphic.WindowsNative.IWin32ApiError> GetWindowClassName(Windows.Win32.Foundation.HWND hWnd)
    {
        Span<char> classNameSpan = stackalloc char[256];
        int getClassNameResult;
        unsafe
        {
            fixed (char* classNameSpanAsChars = classNameSpan)
            {
                getClassNameResult = Windows.Win32.PInvoke.GetClassName(hWnd, classNameSpanAsChars, classNameSpan.Length);
            }
        }
        if (getClassNameResult == 0)
        {
            var win32Error = Marshal.GetLastWin32Error();
            return MorphicResult.ErrorResult<Morphic.WindowsNative.IWin32ApiError>(new Morphic.WindowsNative.IWin32ApiError.Win32Error((uint)win32Error));
        }

        var classNameAsString = classNameSpan[..getClassNameResult].ToString();
        return MorphicResult.OkResult(classNameAsString);
    }

    //

    public interface ISetBitmapError
    {
        public record CouldNotPositionAndResizeBitmap(IPositionAndResizeBitmapError InnerError) : ISetBitmapError;
        public record CouldNotSetBitmapInArgbImageNativeWindow(ArgbImageNativeWindow.ISetBitmapError InnerError) : ISetBitmapError;
    }
    /// <summary>
    /// Sets the source bitmap from a GDI HBITMAP handle. This class does NOT take ownership;
    /// the caller must keep the HBITMAP valid until SetBitmap is called again or the window is disposed.
    /// Pass IntPtr.Zero to clear.
    /// </summary>
    public MorphicResult<MorphicUnit, ISetBitmapError> SetBitmap(IntPtr hBitmap, int width, int height)
    {
        if (hBitmap != IntPtr.Zero)
        {
            var positionAndResizeBitmapResult = this.PositionAndResizeBitmap(new System.Drawing.Size(width, height));
            if (positionAndResizeBitmapResult.IsError == true)
            {
                Debug.Assert(false, "Could not position and resize bitmap.");
                var innerError = positionAndResizeBitmapResult.Error!;
                return MorphicResult.ErrorResult<ISetBitmapError>(new ISetBitmapError.CouldNotPositionAndResizeBitmap(innerError));
            }
        }

        if (_argbImageNativeWindow is not null)
        {
            var setBitmapOnArgbImageNativeWindowResult = _argbImageNativeWindow!.SetBitmap(hBitmap, width, height);
            if (setBitmapOnArgbImageNativeWindowResult.IsError == true)
            {
                Debug.Assert(false, "Could not set bitmap on ARGB image native window.");
                var innerError = setBitmapOnArgbImageNativeWindowResult.Error!;
                return MorphicResult.ErrorResult<ISetBitmapError>(new ISetBitmapError.CouldNotSetBitmapInArgbImageNativeWindow(innerError));
            }
        }

        return MorphicResult.OkResult();
    }

    internal interface IPositionAndResizeBitmapError
    {
        public record CouldNotGetCurrentPositionAndSize(uint Win32ErrorCode) : IPositionAndResizeBitmapError;
        public record CouldNotSetNewPositionAndSize(ArgbImageNativeWindow.ISetPositionAndSizeError InnerError) : IPositionAndResizeBitmapError;
    }
    //
    private MorphicResult<MorphicUnit, IPositionAndResizeBitmapError> PositionAndResizeBitmap(System.Drawing.Size bitmapSize)
    {
        // then, reposition the bitmap
        // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowrect
        Windows.Win32.Foundation.RECT positionAndSize;
        var getWindowRectResult = Windows.Win32.PInvoke.GetWindowRect(_hwnd, out positionAndSize);
        if (getWindowRectResult == 0)
        {
            var win32ErrorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            return MorphicResult.ErrorResult<IPositionAndResizeBitmapError>(new IPositionAndResizeBitmapError.CouldNotGetCurrentPositionAndSize((uint)win32ErrorCode));
        }

        var argbImageNativeWindowSize = TrayButtonNativeWindow.CalculateWidthAndHeightForBitmap(positionAndSize, bitmapSize);
        var bitmapRect = TrayButtonNativeWindow.CalculateCenterRectInsideRect(positionAndSize, argbImageNativeWindowSize);

        if (_argbImageNativeWindow is not null)
        {
            var setPositionAndSizeResult = _argbImageNativeWindow!.SetPositionAndSize(bitmapRect);
            if (setPositionAndSizeResult.IsError == true)
            {
                var innerError = setPositionAndSizeResult.Error!;
                return MorphicResult.ErrorResult<IPositionAndResizeBitmapError>(new IPositionAndResizeBitmapError.CouldNotSetNewPositionAndSize(innerError));
            }
        }

        return MorphicResult.OkResult();
    }

    public MorphicResult<MorphicUnit, IUpdateTooltipTextAndTrackingError> SetText(string? text)
    {
        _tooltipText = text;

        var updateTooltipTextAndTrackingResult = this.UpdateTooltipTextAndTracking();
        if (updateTooltipTextAndTrackingResult.IsError == true)
        {
            // NOTE: we simply pass through this error
            Debug.Assert(false, "Could not update tooltip text");
            return MorphicResult.ErrorResult(updateTooltipTextAndTrackingResult.Error!);
        }

        return MorphicResult.OkResult();
    }

    //

    private Windows.Win32.Foundation.HWND CreateTooltipWindow()
    {
        if (_tooltipWindowHandle != IntPtr.Zero)
        {
            // tooltip window already exists; gracefully degrate by returning the existing window handle
            return _tooltipWindowHandle;
        }

        Windows.Win32.Foundation.HWND tooltipWindowHandle;
        unsafe
        {
            tooltipWindowHandle = Windows.Win32.PInvoke.CreateWindowEx(
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

        // NOTE: Microsoft's documentation seems to indicate that we should set the tooltip as topmost, but in our testing this was unnecessary.  It's possible that using SendMessage to add/remove tooltip text automatically handles this when the system handles showing the tooltip
        //       see: https://learn.microsoft.com/en-us/windows/win32/controls/tooltip-controls
        //Windows.Win32.PInvoke.SetWindowPos(tooltipWindowHandle, Windows.Win32.Foundation.HWND.HWND_TOPMOST, 0, 0, 0, 0, Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOMOVE | Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOSIZE | Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);

        Debug.Assert(tooltipWindowHandle.IsNull == false, "Could not create tooltip window.");

        return tooltipWindowHandle;
    }

    private MorphicResult<MorphicUnit, MorphicUnit> DestroyTooltipWindow()
    {
        if (_tooltipWindowHandle == IntPtr.Zero)
        {
            return MorphicResult.OkResult();
        }

        // set the tooltip text to empty (so that UpdateTooltipText will clear out the tooltip), then update the tooltip text.
        _tooltipText = null;
        _ = this.UpdateTooltipTextAndTracking();

        // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-destroywindow
        var destroyWindowResult = Windows.Win32.PInvoke.DestroyWindow(_tooltipWindowHandle);
        _tooltipWindowHandle = Windows.Win32.Foundation.HWND.Null;

        if (destroyWindowResult == true)
        {
            return MorphicResult.OkResult();
        }
        else
        {
            return MorphicResult.ErrorResult();
        }
    }

    internal interface IUpdateTooltipTextAndTrackingError
    {
        public record CouldNotGetTrayButtonClientRect(uint Win32ErrorCode) : IUpdateTooltipTextAndTrackingError;
        public record CouldNotUpdateTooltipViaSendMessage : IUpdateTooltipTextAndTrackingError;
        public record TooltipWindowDoesNotExist : IUpdateTooltipTextAndTrackingError;
        public record TrayButtonWindowDoesNotExist : IUpdateTooltipTextAndTrackingError;
    }
    private MorphicResult<MorphicUnit, IUpdateTooltipTextAndTrackingError> UpdateTooltipTextAndTracking()
    {
        if (_tooltipWindowHandle == IntPtr.Zero)
        {
            // tooltip window does not exist; failed; abort
            Debug.Assert(false, "Tooptip window does not exist; if this is an expected failure, remove this assert.");
            return MorphicResult.ErrorResult<IUpdateTooltipTextAndTrackingError>(new IUpdateTooltipTextAndTrackingError.TooltipWindowDoesNotExist());
        }

        var trayButtonNativeWindowHandle = _hwnd;
        if (trayButtonNativeWindowHandle == IntPtr.Zero)
        {
            // tray button window does not exist; there is no tool window to update
            return MorphicResult.ErrorResult<IUpdateTooltipTextAndTrackingError>(new IUpdateTooltipTextAndTrackingError.TrayButtonWindowDoesNotExist());
        }

        // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getclientrect
        var getClientRectSuccess = Windows.Win32.PInvoke.GetClientRect(_hwnd, out var trayButtonClientRect);
        if (getClientRectSuccess == false)
        {
            // failed; abort
            var win32ErrorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            Debug.Assert(false, "Could not get client rect for tray button; could not set up tooltip; win32 errcode: " + win32ErrorCode.ToString());
            return MorphicResult.ErrorResult<IUpdateTooltipTextAndTrackingError>(new IUpdateTooltipTextAndTrackingError.CouldNotGetTrayButtonClientRect((uint)win32ErrorCode));
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
                toolinfo.uId = unchecked((nuint)(nint)_hwnd); // unique identifier (for adding/deleting the tooltip)
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
                    // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendmessagew
                    //
                    // see: https://learn.microsoft.com/en-us/windows/win32/controls/ttm-addtool
                    var addToolSuccess = Windows.Win32.PInvoke.SendMessage(_tooltipWindowHandle, Windows.Win32.PInvoke.TTM_ADDTOOL, (Windows.Win32.Foundation.WPARAM)0, pointerToToolinfo);
                    if (addToolSuccess == 0)
                    {
                        Debug.Assert(false, "Could not add tooltip info");
                        return MorphicResult.ErrorResult<IUpdateTooltipTextAndTrackingError>(new IUpdateTooltipTextAndTrackingError.CouldNotUpdateTooltipViaSendMessage());
                    }
                    _tooltipInfoAdded = true;
                }
                else
                {
                    // delete and re-add the tooltipinfo; this will update all the info (including the text and tracking rect)
                    //
                    // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendmessagew
                    //
                    // see: https://learn.microsoft.com/en-us/windows/win32/controls/ttm-deltool
                    // NOTE: TTM_DELTOOL does not return a result
                    _ = Windows.Win32.PInvoke.SendMessage(_tooltipWindowHandle, Windows.Win32.PInvoke.TTM_DELTOOL, (Windows.Win32.Foundation.WPARAM)0, pointerToToolinfo);
                    //
                    // see: https://learn.microsoft.com/en-us/windows/win32/controls/ttm-addtool
                    var addToolSuccess = Windows.Win32.PInvoke.SendMessage(_tooltipWindowHandle, Windows.Win32.PInvoke.TTM_ADDTOOL, (Windows.Win32.Foundation.WPARAM)0, pointerToToolinfo);
                    if (addToolSuccess == 0)
                    {
                        Debug.Assert(false, "Could not update tooltip info");
                        return MorphicResult.ErrorResult<IUpdateTooltipTextAndTrackingError>(new IUpdateTooltipTextAndTrackingError.CouldNotUpdateTooltipViaSendMessage());
                    }
                }
            }
            else /* if (_tooltipInfoAdded == true) */
            {
                // NOTE: we might technically call "deltool" even when a tooltipinfo was already removed
                //
                // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendmessagew
                //
                // see: https://learn.microsoft.com/en-us/windows/win32/controls/ttm-deltool
                _ = Windows.Win32.PInvoke.SendMessage(_tooltipWindowHandle, Windows.Win32.PInvoke.TTM_DELTOOL, (Windows.Win32.Foundation.WPARAM)0, pointerToToolinfo);
                _tooltipInfoAdded = false;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(pointerToToolinfo);
        }

        return MorphicResult.OkResult();
    }

    //

    /* helper functions */

    internal static Windows.Win32.Foundation.RECT CalculateCenterRectInsideRect(Windows.Win32.Foundation.RECT outerRect, System.Drawing.Size innerSize)
    {
        var outerWidth = outerRect.right - outerRect.left;
        var outerHeight = outerRect.bottom - outerRect.top;

        var innerWidth = Math.Min(innerSize.Width, outerWidth);
        var innerHeight = Math.Min(innerSize.Height, outerHeight);

        var left = outerRect.left + ((outerWidth - innerWidth) / 2);
        var top = outerRect.top + ((outerHeight - innerHeight) / 2);
        var right = left + innerWidth;
        var bottom = top + innerHeight;

        return new Windows.Win32.Foundation.RECT()
        {
            left = left,
            top = top,
            right = right,
            bottom = bottom,
        };
    }

    internal interface ICalculatePositionAndSizeForTrayButtonError
    {
        public record CouldNotFindTaskbarRelatedHandle : ICalculatePositionAndSizeForTrayButtonError;
        public record CannotFitOnTaskbar : ICalculatePositionAndSizeForTrayButtonError;
        public record Win32Error(uint Win32ErrorCode) : ICalculatePositionAndSizeForTrayButtonError;
    }
    internal static MorphicResult<Windows.Win32.Foundation.RECT, ICalculatePositionAndSizeForTrayButtonError> CalculatePositionAndSizeForTrayButton(IntPtr? trayButtonHandle)
    {
        // NOTE: in this implementation, we simply place the tray button over the taskbar, directly to the left of the system tray
        //       in the future, we may want to consider searching for any children which might occupy the area--and any system windows which are owned by the taskbar or any of its children--and then try to find a place to the "left" of those

        // get the handles for the taskbar, task button container, and the notify tray
        //
        var getTaskbarHandleResult = TrayButtonNativeWindow.GetWindowsTaskbarHandle();
        if (getTaskbarHandleResult.IsError == true)
        {
            switch (getTaskbarHandleResult.Error!)
            {
                case Morphic.WindowsNative.IWin32ApiError.Win32Error(var win32ErrorCode):
                    return MorphicResult.ErrorResult<ICalculatePositionAndSizeForTrayButtonError>(new ICalculatePositionAndSizeForTrayButtonError.Win32Error(win32ErrorCode));
                default:
                    throw new MorphicUnhandledErrorException();
            }
        }
        var taskbarHandle = getTaskbarHandleResult.Value!;
        if (taskbarHandle == Windows.Win32.Foundation.HWND.Null)
        {
            return MorphicResult.ErrorResult<ICalculatePositionAndSizeForTrayButtonError>(new ICalculatePositionAndSizeForTrayButtonError.CouldNotFindTaskbarRelatedHandle());
        }
        //
        var getTaskButtonContainerHandleResult = TrayButtonNativeWindow.GetWindowsTaskbarTaskButtonContainerHandle(taskbarHandle);
        if (getTaskButtonContainerHandleResult.IsError == true)
        {
            switch (getTaskButtonContainerHandleResult.Error!)
            {
                case Morphic.WindowsNative.IWin32ApiError.Win32Error(var win32ErrorCode):
                    return MorphicResult.ErrorResult<ICalculatePositionAndSizeForTrayButtonError>(new ICalculatePositionAndSizeForTrayButtonError.Win32Error(win32ErrorCode));
                default:
                    throw new MorphicUnhandledErrorException();
            }
        }
        var taskButtonContainerHandle = getTaskButtonContainerHandleResult.Value!;
        if (taskButtonContainerHandle == Windows.Win32.Foundation.HWND.Null)
        {
            return MorphicResult.ErrorResult<ICalculatePositionAndSizeForTrayButtonError>(new ICalculatePositionAndSizeForTrayButtonError.CouldNotFindTaskbarRelatedHandle());
        }
        //
        var getNotifyTrayHandle = TrayButtonNativeWindow.GetWindowsTaskbarNotificationTrayHandle(taskbarHandle);
        if (getNotifyTrayHandle.IsError == true)
        {
            switch (getNotifyTrayHandle.Error!)
            {
                case Morphic.WindowsNative.IWin32ApiError.Win32Error(var win32ErrorCode):
                    return MorphicResult.ErrorResult<ICalculatePositionAndSizeForTrayButtonError>(new ICalculatePositionAndSizeForTrayButtonError.Win32Error(win32ErrorCode));
                default:
                    throw new MorphicUnhandledErrorException();
            }
        }
        var notifyTrayHandle = getNotifyTrayHandle.Value!;
        if (notifyTrayHandle == Windows.Win32.Foundation.HWND.Null)
        {
            return MorphicResult.ErrorResult<ICalculatePositionAndSizeForTrayButtonError>(new ICalculatePositionAndSizeForTrayButtonError.CouldNotFindTaskbarRelatedHandle());
        }

        // get the RECTs for the taskbar, task button container and the notify tray
        //
        var getTaskbarRectSuccess = Windows.Win32.PInvoke.GetWindowRect(taskbarHandle, out var taskbarRect);
        if (getTaskbarRectSuccess == false)
        {
            var win32ErrorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            return MorphicResult.ErrorResult<ICalculatePositionAndSizeForTrayButtonError>(new ICalculatePositionAndSizeForTrayButtonError.Win32Error((uint)win32ErrorCode));
        }
        //
        var getTaskButtonContainerRectSuccess = Windows.Win32.PInvoke.GetWindowRect(taskButtonContainerHandle, out var taskButtonContainerRect);
        if (getTaskButtonContainerRectSuccess == false)
        {
            var win32ErrorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            return MorphicResult.ErrorResult<ICalculatePositionAndSizeForTrayButtonError>(new ICalculatePositionAndSizeForTrayButtonError.Win32Error((uint)win32ErrorCode));
        }
        //
        var getNotifyTrayRectSuccess = Windows.Win32.PInvoke.GetWindowRect(notifyTrayHandle, out var notifyTrayRect);
        if (getNotifyTrayRectSuccess == false)
        {
            var win32ErrorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            return MorphicResult.ErrorResult<ICalculatePositionAndSizeForTrayButtonError>(new ICalculatePositionAndSizeForTrayButtonError.Win32Error((uint)win32ErrorCode));
        }

        // determine the taskbar's orientation
        //
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
            var centerXOfTaskbar = taskbarRect.X + (taskbarRect.Width / 2);
            if (notifyTrayRect.right < centerXOfTaskbar)
            {
                isRightToLeft = true;
            }
        }

        // establish the appropriate size for our tray button (i.e. same height/width as taskbar, and with an aspect ratio of 8:10)
        int trayButtonHeight;
        int trayButtonWidth;
        // NOTE: on some computers, the taskbar and notify tray return an inaccurate size, but the task button container appears to always return the correct size; therefore we match our primary dimension to the taskbutton container's same dimension
        // NOTE: the inaccurate size returned by GetWindowRect may be due to our moving this class from the main application to a helper library (i.e. perhaps the pixel scaling isn't applying correctly), or it could just be a weird quirk on some computers.
        //       [The GetWindowRect issue happens with both our own homebuilt PINVOKE methods as well as with PInvoke.User32.GetWindowRect; the function is returning the correct left, bottom and right positions of the taskbar and notify tray--but is
        //       sometimes misrepresenting the top (i.e. height) value of both the taskbar and notify tray rects]
        if (taskbarOrientation == Morphic.Controls.Orientation.Horizontal)
        {
            // option 1: base our primary dimension off of the taskbutton container's same dimension
            trayButtonHeight = taskButtonContainerRect.bottom - taskButtonContainerRect.top;
            //
            // option 2: base our primary dimension off of the taskbar's same dimension
            //trayButtonHeight = taskbarRect.bottom - taskbarRect.top;
            //
            // [and then scale the secondary dimension to 80% of the size of the primary dimension]
            trayButtonWidth = (int)((Double)trayButtonHeight * 0.8);
        }
        else
        {
            // option 1: base our primary dimension off of the taskbutton container's same dimension
            trayButtonWidth = taskButtonContainerRect.right - taskButtonContainerRect.left;
            //
            // option 2: base our primary dimension off of the taskbar's same dimension
            //trayButtonWidth = taskbarRect.right - taskbarRect.left;
            //
            // [and then scale the secondary dimension to 80% of the size of the primary dimension]
            trayButtonHeight = (int)((Double)trayButtonWidth * 0.8);
        }

        // choose a space in the rightmost/bottommost position of the taskbar; note that "rightmost" is actually leftmost when the system is using an RTL orientation (e.g. Arabic, Hebrew)
        int trayButtonX;
        int trayButtonY;
        if (taskbarOrientation == Morphic.Controls.Orientation.Horizontal)
        {
            if (isRightToLeft == false)
            {
                trayButtonX = notifyTrayRect.left - trayButtonWidth;
                if (trayButtonX - trayButtonWidth < taskbarRect.left)
                {
                    return MorphicResult.ErrorResult<ICalculatePositionAndSizeForTrayButtonError>(new ICalculatePositionAndSizeForTrayButtonError.CannotFitOnTaskbar());
                }
            }
            else
            {
                trayButtonX = notifyTrayRect.right;
                if (trayButtonX + trayButtonWidth > taskbarRect.right)
                {
                    return MorphicResult.ErrorResult<ICalculatePositionAndSizeForTrayButtonError>(new ICalculatePositionAndSizeForTrayButtonError.CannotFitOnTaskbar());
                }
            }
            //
            // NOTE: if we have any issues with positioning, try to replace taskbarRect.bottom with taskButtoncontainerRect.bottom (if we chose option #1 for our size calculations above)
            trayButtonY = taskbarRect.bottom - trayButtonHeight;
        }
        else /* if (taskbarOrientation == Morphic.Controls.Orientation.Vertical) */
        {
            // NOTE: if we have any issues with positioning, try to replace taskbarRect.bottom with taskButtoncontainerRect.right (if we chose option #1 for our size calculations above)
            trayButtonX = taskbarRect.right - trayButtonWidth;
            //
            trayButtonY = notifyTrayRect.top - trayButtonHeight;
            if (trayButtonY - trayButtonHeight < taskbarRect.top)
            {
                return MorphicResult.ErrorResult<ICalculatePositionAndSizeForTrayButtonError>(new ICalculatePositionAndSizeForTrayButtonError.CannotFitOnTaskbar());
            }
        }

        var result = new Windows.Win32.Foundation.RECT() { left = trayButtonX, top = trayButtonY, right = trayButtonX + trayButtonWidth, bottom = trayButtonY + trayButtonHeight };
        return MorphicResult.OkResult(result);
    }

    //

    private MorphicResult<System.Drawing.Point, Morphic.WindowsNative.IWin32ApiError> ConvertMouseMessageLParamToScreenPoint(IntPtr lParam)
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
                Debug.Assert(false, "Could not map tray button hit point to screen coordinates; win32 errcode: " + win32ErrorCode.ToString());
                return MorphicResult.ErrorResult<Morphic.WindowsNative.IWin32ApiError>(new Morphic.WindowsNative.IWin32ApiError.Win32Error((uint)win32ErrorCode));
            }
        }

        var result = hitPoints[0];
        return MorphicResult.OkResult(result);
    }

    //

    // NOTE: this function takes the window size as input and calculates the size of the icon to display, centered, within the window.
    private static System.Drawing.Size CalculateWidthAndHeightForBitmap(Windows.Win32.Foundation.RECT availableRect, System.Drawing.Size bitmapSize)
    {
        var availableSize = new System.Drawing.Size(availableRect.right - availableRect.left, availableRect.bottom - availableRect.top);

        /* determine the larger dimension (width or height) */
        //int largerDimensionSize;
        //int smallerDimensionSize;
        System.Drawing.Size insideMarginsSize;
        if (availableSize.Height > availableSize.Width)
        {
            //largerDimensionSize = availableSize.Height;
            //smallerDimensionSize = availableSize.Width;
            //
            // strategy 1: consume up to 90% of the width of the box and up to 66% of the height
            //insideMarginsSize = new((int)((double)availableSize.Width * 0.9), (int)((double)availableSize.Height * (2.0 / 3.0)));
            //
            // strategy 2: consume up to 66% of the width of the box and up to 66% of the height
            insideMarginsSize = new((int)((double)availableSize.Width * (2.0 / 3.0)), (int)((double)availableSize.Height * (2.0 / 3.0)));
        }
        else
        {
            //largerDimensionSize = availableSize.Width;
            //smallerDimensionSize = availableSize.Height;
            //
            // strategy 1: consume up to 66% of the width of the box and up to 90% of the height
            //insideMarginsSize = new((int)((double)availableSize.Width * (2.0 / 3.0)), (int)((double)availableSize.Height * 0.9));
            //
            // strategy 2: consume up to 66% of the width of the box and up to 66% of the height
            insideMarginsSize = new((int)((double)availableSize.Width * (2.0 / 3.0)), (int)((double)availableSize.Height * (2.0 / 3.0)));
        }

        /* shrink the bitmap size down so that it fits inside the available rect */

        // by default, assume the bitmap will be the size of the source image
        int bitmapWidth = bitmapSize.Width;
        int bitmapHeight = bitmapSize.Height;
        //
        // if bitmap is wider than the available rect, shrink it equally in both directions
        if (bitmapWidth > insideMarginsSize.Width)
        {
            double scaleFactor = (double)insideMarginsSize.Width / (double)bitmapWidth;
            bitmapWidth = insideMarginsSize.Width;
            bitmapHeight = (int)((double)bitmapHeight * scaleFactor);
        }
        //
        // if bitmap is taller than the available rect, shrink it further (and equally in both directions)
        if (bitmapHeight > insideMarginsSize.Height)
        {
            double scaleFactor = (double)insideMarginsSize.Height / (double)bitmapHeight;
            bitmapWidth = (int)((double)bitmapWidth * scaleFactor);
            bitmapHeight = insideMarginsSize.Height;
        }

        // if bitmap does not touch either of the two margins (i.e. is too small), enlarge it now.
        if (bitmapWidth != insideMarginsSize.Width && bitmapHeight != insideMarginsSize.Height)
        {
            // if bitmap is not as wide as the insideMarginsWidth, enlarge it now (equally in both directions)
            if (bitmapWidth < insideMarginsSize.Width)
            {
                double scaleFactor = (double)insideMarginsSize.Width / (double)bitmapWidth;
                bitmapWidth = insideMarginsSize.Width;
                bitmapHeight = (int)((double)bitmapHeight * scaleFactor);
            }
            //
            // if bitmap is now too tall, shrink it back down (equally in both directions)
            if (bitmapHeight > insideMarginsSize.Height)
            {
                double scaleFactor = (double)insideMarginsSize.Height / (double)bitmapHeight;
                bitmapWidth = (int)((double)bitmapWidth * scaleFactor);
                bitmapHeight = insideMarginsSize.Height;
            }
        }

        return new System.Drawing.Size(bitmapWidth, bitmapHeight);
    }

    //

    private static MorphicResult<Windows.Win32.Foundation.HWND, Morphic.WindowsNative.IWin32ApiError> GetWindowsTaskbarHandle()
    {
        var result = Windows.Win32.PInvoke.FindWindow("Shell_TrayWnd", null);
        if (result == Windows.Win32.Foundation.HWND.Null)
        {
            var win32ErrorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            return MorphicResult.ErrorResult<Morphic.WindowsNative.IWin32ApiError>(new Morphic.WindowsNative.IWin32ApiError.Win32Error((uint)win32ErrorCode));
        }

        return MorphicResult.OkResult(result);
    }
    //
    private static MorphicResult<Windows.Win32.Foundation.HWND, Morphic.WindowsNative.IWin32ApiError> GetWindowsTaskbarTaskButtonContainerHandle(Windows.Win32.Foundation.HWND taskbarHandle)
    {
        if (taskbarHandle == Windows.Win32.Foundation.HWND.Null)
        {
            return MorphicResult.OkResult(Windows.Win32.Foundation.HWND.Null);
        }

        var result = Windows.Win32.PInvoke.FindWindowEx(taskbarHandle, Windows.Win32.Foundation.HWND.Null, "ReBarWindow32", null);
        if (result == Windows.Win32.Foundation.HWND.Null)
        {
            var win32ErrorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            return MorphicResult.ErrorResult<Morphic.WindowsNative.IWin32ApiError>(new Morphic.WindowsNative.IWin32ApiError.Win32Error((uint)win32ErrorCode));
        }

        return MorphicResult.OkResult(result);
    }
    //
    private static MorphicResult<Windows.Win32.Foundation.HWND, Morphic.WindowsNative.IWin32ApiError> GetWindowsTaskbarNotificationTrayHandle(Windows.Win32.Foundation.HWND taskbarHandle)
    {
        if (taskbarHandle == Windows.Win32.Foundation.HWND.Null)
        {
            return MorphicResult.OkResult(Windows.Win32.Foundation.HWND.Null);
        }

        var result = Windows.Win32.PInvoke.FindWindowEx(taskbarHandle, Windows.Win32.Foundation.HWND.Null, "TrayNotifyWnd", null);
        if (result == Windows.Win32.Foundation.HWND.Null)
        {
            var win32ErrorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            return MorphicResult.ErrorResult<Morphic.WindowsNative.IWin32ApiError>(new Morphic.WindowsNative.IWin32ApiError.Win32Error((uint)win32ErrorCode));
        }

        return MorphicResult.OkResult(result);
    }
}
