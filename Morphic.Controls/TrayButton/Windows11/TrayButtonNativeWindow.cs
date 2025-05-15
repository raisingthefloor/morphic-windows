// Copyright 2020-2025 Raising the Floor - US, Inc.
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
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Morphic.Controls.TrayButton.Windows11;

internal class TrayButtonNativeWindow : System.Windows.Forms.NativeWindow, IDisposable
{
    private bool disposedValue;

    private static ushort? s_morphicTrayButtonClassInfoExAtom = null;

    private System.Windows.Visibility _visibility;
    private bool _taskbarIsTopmost;

    private System.Threading.Timer? _resurfaceTaskbarButtonTimer;
    private static readonly TimeSpan RESURFACE_TASKBAR_BUTTON_INTERVAL_TIMESPAN = new TimeSpan(0, 0, 30);

    private ArgbImageNativeWindow? _argbImageNativeWindow = null;

    private IntPtr _tooltipWindowHandle;
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

    private IntPtr _locationChangeWindowEventHook = IntPtr.Zero;
    private PInvokeExtensions.WinEventProc? _locationChangeWindowEventProc = null;

    private IntPtr _objectReorderWindowEventHook = IntPtr.Zero;
    private PInvokeExtensions.WinEventProc? _objectReorderWindowEventProc = null;

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

    public event System.Windows.Forms.MouseEventHandler? MouseUp;

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
                    PInvokeExtensions.UnhookWinEvent(_objectReorderWindowEventHook);
                }
                if (_locationChangeWindowEventHook != IntPtr.Zero)
                {
                    PInvokeExtensions.UnhookWinEvent(_locationChangeWindowEventHook);
                }

                _argbImageNativeWindow?.Dispose();
            }

            // free unmanaged resources (unmanaged objects) and override finalizer
            this.DestroyHandle();
            _ = this.DestroyTooltipWindow();

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

        /* register a custom native window class for our Morphic Tray Button (or refer to the already-registered class, if we captured it earlier in the application's execution) */
        const string nativeWindowClassName = "Morphic-TrayButton";
        //
        if (s_morphicTrayButtonClassInfoExAtom is null)
        {
            // register our control's custom native window class
            var pointerToWndProcCallback = Marshal.GetFunctionPointerForDelegate(new PInvokeExtensions.WndProc(result.WndProcCallback));
            var lpWndClassEx = new PInvokeExtensions.WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf(typeof(PInvokeExtensions.WNDCLASSEX)),
                lpfnWndProc = pointerToWndProcCallback,
                lpszClassName = nativeWindowClassName,
                hCursor = PInvoke.User32.LoadCursor(IntPtr.Zero, (IntPtr)PInvoke.User32.Cursors.IDC_ARROW).DangerousGetHandle()
            };

            // NOTE: RegisterClassEx returns an ATOM (or 0 if the call failed)
            var registerClassResult = PInvokeExtensions.RegisterClassEx(ref lpWndClassEx);
            if (registerClassResult == 0) // failure
            {
                var win32Exception = new PInvoke.Win32Exception(Marshal.GetLastWin32Error());
                if (win32Exception.NativeErrorCode == PInvoke.Win32ErrorCode.ERROR_CLASS_ALREADY_EXISTS)
                {
                    Debug.Assert(false, "Class was already registered; we should have recorded this ATOM, and we cannot proceed");
                }
                return MorphicResult.ErrorResult<ICreateNewError>(new ICreateNewError.Win32Error((uint)win32Exception.ErrorCode));
            }
            s_morphicTrayButtonClassInfoExAtom = registerClassResult;
        }

        /* calculate the initial position of the tray button */
        var calculatePositionResult = TrayButtonNativeWindow.CalculatePositionAndSizeForTrayButton(null);
        if (calculatePositionResult.IsError)
        {
            Debug.Assert(false, "Cannot calculate position for tray button");
            return MorphicResult.ErrorResult<ICreateNewError>(new ICreateNewError.CouldNotCalculateWindowPosition());
        }
        var trayButtonPositionAndSize = calculatePositionResult.Value!;
        //
        // capture our initial position and size
        result._trayButtonPositionAndSize = trayButtonPositionAndSize;

        /* get the handle for the taskbar; it will be the owner of our native window (so that our window sits above it in the zorder) */
        // NOTE: we will still need to push our window to the front of its owner's zorder stack in some circumstances, as certain actions (such as popping up the task list balloons above the task bar) will reorder the taskbar's zorder and push us behind the taskbar
        // NOTE: making the taskbar our owner has the side-effect of putting our window above full-screen applications (even though our window is not itself "always on top"); we will need to hide our window whenever a window goes full-screen on the same monitor (and re-show our window whenever the window exits full-screen mode)
        var taskbarHandle = TrayButtonNativeWindow.GetWindowsTaskbarHandle();

        // capture the current state of the taskbar; this is combined with the visibility value to determine whether or not the window is actually visible to the user
        result._taskbarIsTopmost = TrayButtonNativeWindow.IsTaskbarTopmost();


        /* create an instance of our native window */

        var windowParams = new System.Windows.Forms.CreateParams()
        {
            ClassName = s_morphicTrayButtonClassInfoExAtom.ToString(), // for simplicity, we pass the value of the custom class as its integer self but in string form; our CreateWindow function will parse this and convert it to an int
            Caption = nativeWindowClassName,
            Style = unchecked((int)(/*PInvoke.User32.WindowStyles.WS_CLIPSIBLINGS | */PInvoke.User32.WindowStyles.WS_POPUP /*| PInvoke.User32.WindowStyles.WS_TABSTOP*/ | PInvoke.User32.WindowStyles.WS_VISIBLE)),
            ExStyle = (int)(PInvoke.User32.WindowStylesEx.WS_EX_LAYERED/* | PInvoke.User32.WindowStylesEx.WS_EX_TOOLWINDOW*//* | PInvoke.User32.WindowStylesEx.WS_EX_TOPMOST*/),
            //ClassStyle = ?,
            X = trayButtonPositionAndSize.left,
            Y = trayButtonPositionAndSize.top,
            Width = trayButtonPositionAndSize.right - trayButtonPositionAndSize.left,
            Height = trayButtonPositionAndSize.bottom - trayButtonPositionAndSize.top,
            Parent = taskbarHandle.Value,
            //Param = ?,
        };

        // NOTE: CreateHandle can throw InvalidOperationException, OutOfMemoryException or Win32Exception
        try
        {
            result.CreateHandle(windowParams);
        }
        catch (PInvoke.Win32Exception ex)
        {
            return MorphicResult.ErrorResult<ICreateNewError>(new ICreateNewError.Win32Error((uint)ex.ErrorCode));
        }
        catch (Exception ex)
        {
            return MorphicResult.ErrorResult<ICreateNewError>(new ICreateNewError.OtherException(ex));
        }

        // set the window's background transparency to 0% (in the range of a 0 to 255 alpha channel, with 255 being 100%)
        // NOTE: an alpha value of 0 (0%) makes our window complete see-through but it has the side-effect of not capturing any mouse events; to counteract this,
        //       we set our "tranparent" alpha value to 1 instead.  We will only use an alpha value of 0 when we want our window to be invisible and also not capture mouse events
        var setBackgroundAlphaResult = TrayButtonNativeWindow.SetBackgroundAlpha((Windows.Win32.Foundation.HWND)result.Handle, ALPHA_VALUE_FOR_TRANSPARENT_BUT_HIT_TESTABLE);
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

        // since we are making the control visible by default, set its _visibility state
        result._visibility = System.Windows.Visibility.Visible;

        // create an instance of the ArgbImageNativeWindow to hold our icon; we cannot draw the bitmap directly on this window as the bitmap would then be alphablended the same % as our background (instead of being independently blended over our window)
        var argbImageNativeWindowResult = ArgbImageNativeWindow.CreateNew(result.Handle, windowParams.X, windowParams.Y, windowParams.Width, windowParams.Height);
        if (argbImageNativeWindowResult.IsError == true)
        {
            result.Dispose();
            return MorphicResult.ErrorResult(argbImageNativeWindowResult.Error!);
        }
        result._argbImageNativeWindow = argbImageNativeWindowResult.Value!;

        /* wire up windows event hook listeners, to watch for events which require adjusting the zorder of our window */

        // NOTE: we could provide the process handle and thread of processes/threads which we were interested in specifically, but for now we're interested in more than one window so we filter broadly
        var locationChangeWindowEventProc = new PInvokeExtensions.WinEventProc(result.LocationChangeWindowEventProc);
        var locationChangeWindowEventHook = PInvokeExtensions.SetWinEventHook(
             PInvokeExtensions.WinEventHookType.EVENT_OBJECT_LOCATIONCHANGE, // start index
             PInvokeExtensions.WinEventHookType.EVENT_OBJECT_LOCATIONCHANGE, // end index
             IntPtr.Zero,
             locationChangeWindowEventProc,
             0, // process handle (0 = all processes on current desktop)
             0, // thread (0 = all existing threads on current desktop)
             PInvokeExtensions.WinEventHookFlags.WINEVENT_OUTOFCONTEXT | PInvokeExtensions.WinEventHookFlags.WINEVENT_SKIPOWNPROCESS
        );
        Debug.Assert(locationChangeWindowEventHook != IntPtr.Zero, "Could not wire up location change window event listener for tray button");
        //
        result._locationChangeWindowEventHook = locationChangeWindowEventHook;
        // NOTE: we must capture the delegate so that it is not garbage collected; otherwise the native callbacks can crash the .NET execution engine
        result._locationChangeWindowEventProc = locationChangeWindowEventProc;
        //
        //
        //
        var objectReorderWindowEventProc = new PInvokeExtensions.WinEventProc(result.ObjectReorderWindowEventProc);
        var objectReorderWindowEventHook = PInvokeExtensions.SetWinEventHook(
             PInvokeExtensions.WinEventHookType.EVENT_OBJECT_REORDER, // start index
             PInvokeExtensions.WinEventHookType.EVENT_OBJECT_REORDER, // end index
             IntPtr.Zero,
             objectReorderWindowEventProc,
             0, // process handle (0 = all processes on current desktop)
             0, // thread (0 = all existing threads on current desktop)
             PInvokeExtensions.WinEventHookFlags.WINEVENT_OUTOFCONTEXT | PInvokeExtensions.WinEventHookFlags.WINEVENT_SKIPOWNPROCESS
        );
        Debug.Assert(objectReorderWindowEventHook != IntPtr.Zero, "Could not wire up object reorder window event listener for tray button");
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

    // NOTE: the built-in CreateHandle function couldn't accept our custom class (an ATOM rather than a string) as input, so we have overridden CreateHandle and are calling CreateWindowEx manually
    // NOTE: in some circumstances, it is possible that we are unable to create our window; our caller may want to consider retrying mechanism
    public override void CreateHandle(System.Windows.Forms.CreateParams cp)
    {
        // NOTE: if cp.ClassName is a string parseable as a short unsigned integer, parse it into an unsigned short; otherwise use the string as the classname
        IntPtr classNameAsIntPtr;
        bool classNameAsIntPtrRequiresFree = false;
        if (cp.ClassName is not null && ushort.TryParse(cp.ClassName, out var classNameAsUshort) == true)
        {
            classNameAsIntPtr = (IntPtr)classNameAsUshort;
        }
        else
        {
            if (cp.ClassName is not null)
            {
                classNameAsIntPtr = Marshal.StringToHGlobalUni(cp.ClassName);
                classNameAsIntPtrRequiresFree = true;
            }
            else
            {
                classNameAsIntPtr = IntPtr.Zero;
            }
        }
        //
        try
        {
            // NOTE: CreateWindowEx will return IntPtr.Zero ("NULL") if it fails
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
            if (handle == IntPtr.Zero)
            {
                var lastError = Marshal.GetLastWin32Error();
                throw new PInvoke.Win32Exception(lastError);
            }

            this.AssignHandle(handle);
        }
        finally
        {
            if (classNameAsIntPtrRequiresFree == true)
            {
                Marshal.FreeHGlobal(classNameAsIntPtr);
            }
        }
    }


    // Listen to when the handle changes to keep the argb image native window synced
    protected override void OnHandleChange()
    {
        base.OnHandleChange();

        // NOTE: if we ever need to update our children (or other owned windows) to let them know that our handle had changed, this is where we would add that code
    }

    // NOTE: during initial creation of the window, callbacks are sent to this delegated event; after creation, messages are captured by the WndProc function instead
    private IntPtr WndProcCallback(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch ((PInvoke.User32.WindowMessage)msg)
        {
            case PInvoke.User32.WindowMessage.WM_CREATE:
                // NOTE: it may not technically be necessary for us to use buffered painting for this control since we're effectively just painting it with a single fill color--but 
                //       we do so to maintain consistency with the ArgbImageNativeWindow class and other user-painted forms
                // see: https://learn.microsoft.com/en-us/windows/win32/api/uxtheme/nf-uxtheme-bufferedpaintinit
                if (Windows.Win32.PInvoke.BufferedPaintInit() != Windows.Win32.Foundation.HRESULT.S_OK)
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
        return PInvoke.User32.DefWindowProc(hWnd, (PInvoke.User32.WindowMessage)msg, wParam, lParam);
    }

    // NOTE: this WndProc method processes all messages after the initial creation of the window
    protected override void WndProc(ref System.Windows.Forms.Message m)
    {
        IntPtr? result = null;

        switch ((PInvoke.User32.WindowMessage)m.Msg)
        {
            case PInvoke.User32.WindowMessage.WM_LBUTTONDOWN:
                {
                    _visualState |= TrayButtonVisualStateFlags.LeftButtonPressed;
                    this.UpdateVisualStateAlpha();

                    result = IntPtr.Zero;
                }
                break;
            case PInvoke.User32.WindowMessage.WM_LBUTTONUP:
                {
                    _visualState &= ~TrayButtonVisualStateFlags.LeftButtonPressed;
                    this.UpdateVisualStateAlpha();

                    var convertLParamResult = this.ConvertMouseMessageLParamToScreenPoint(m.LParam);
                    if (convertLParamResult.IsSuccess)
                    {
                        var hitPoint = convertLParamResult.Value!;

                        var mouseArgs = new System.Windows.Forms.MouseEventArgs(System.Windows.Forms.MouseButtons.Left, 1, hitPoint.X, hitPoint.Y, 0);
                        Task.Run(() => this.MouseUp?.Invoke(this, mouseArgs));
                    }
                    else
                    {
                        Debug.Assert(false, "Could not map tray button hit point to screen coordinates");
                    }

                    result = IntPtr.Zero;
                }
                break;
            case PInvoke.User32.WindowMessage.WM_MOUSELEAVE:
                {
                    // the cursor has left our tray button's window area; remove the hover state from our visual state
                    _visualState &= ~TrayButtonVisualStateFlags.Hover;

                    // NOTE: as we aren't able to track mouseup when the cursor is outside of the button, we also remove the left/right button pressed states here
                    //       (and then we check them again when the mouse moves back over the button)
                    _visualState &= ~TrayButtonVisualStateFlags.LeftButtonPressed;
                    _visualState &= ~TrayButtonVisualStateFlags.RightButtonPressed;
                    this.UpdateVisualStateAlpha();

                    result = IntPtr.Zero;
                }
                break;
            case PInvoke.User32.WindowMessage.WM_MOUSEMOVE:
                {
                    // NOTE: this message is raised while we are tracking (whereas the SETCURSOR WM_MOUSEMOVE is captured when the mouse cursor first enters the window)
                    //
                    // NOTE: if the cursor moves off of the tray button while the button is pressed, we would have removed the "pressed" focus as well as the "hover" focus
                    //       because we can't track mouseup when the cursor is outside of the button; consequently we also need to check the mouse pressed state during
                    //       mousemove so that we can re-visualize (re-set flags for) the pressed state as appropriate.
                    if (((_visualState & TrayButtonVisualStateFlags.LeftButtonPressed) == 0) && ((m.WParam.ToInt64() & PInvokeExtensions.MK_LBUTTON) != 0))
                    {
                        _visualState |= TrayButtonVisualStateFlags.LeftButtonPressed;
                        this.UpdateVisualStateAlpha();
                    }
                    if (((_visualState & TrayButtonVisualStateFlags.RightButtonPressed) == 0) && ((m.WParam.ToInt64() & PInvokeExtensions.MK_RBUTTON) != 0))
                    {
                        _visualState |= TrayButtonVisualStateFlags.RightButtonPressed;
                        this.UpdateVisualStateAlpha();
                    }

                    result = IntPtr.Zero;
                }
                break;
            case PInvoke.User32.WindowMessage.WM_NCDESTROY:
                {
                    // NOTE: we are calling this in response to WM_NCDESTROY (instead of WM_DESTROY)
                    //       see: https://learn.microsoft.com/en-us/windows/win32/api/uxtheme/nf-uxtheme-bufferedpaintinit
                    _ = Windows.Win32.PInvoke.BufferedPaintUnInit();

                    // NOTE: we pass along this message (i.e. we don't return a "handled" result)
                }
                break;
            case PInvoke.User32.WindowMessage.WM_NCPAINT:
                {
                    // we suppress all painting of the non-client areas (so that we can have a transparent window)
                    // return zero, indicating that we processed the message
                    result = IntPtr.Zero;
                }
                break;
            case PInvoke.User32.WindowMessage.WM_PAINT:
                {
                    // NOTE: we override the built-in paint functionality with our own Paint function
                    this.OnPaintWindowsMessage((Windows.Win32.Foundation.HWND)m.HWnd);
                    //
                    // return zero, indicating that we processed the message
                    result = IntPtr.Zero;
                }
                break;
            case PInvoke.User32.WindowMessage.WM_RBUTTONDOWN:
                {
                    _visualState |= TrayButtonVisualStateFlags.RightButtonPressed;
                    this.UpdateVisualStateAlpha();

                    result = IntPtr.Zero;
                }
                break;
            case PInvoke.User32.WindowMessage.WM_RBUTTONUP:
                {
                    _visualState &= ~TrayButtonVisualStateFlags.RightButtonPressed;
                    this.UpdateVisualStateAlpha();

                    var convertLParamResult = this.ConvertMouseMessageLParamToScreenPoint(m.LParam);
                    if (convertLParamResult.IsSuccess)
                    {
                        var hitPoint = convertLParamResult.Value!;

                        var mouseArgs = new System.Windows.Forms.MouseEventArgs(System.Windows.Forms.MouseButtons.Right, 1, hitPoint.X, hitPoint.Y, 0);
                        Task.Run(() => this.MouseUp?.Invoke(this, mouseArgs));
                    }
                    else
                    {
                        Debug.Assert(false, "Could not map tray button hit point to screen coordinates");
                    }

                    result = IntPtr.Zero;
                }
                break;
            case PInvoke.User32.WindowMessage.WM_SETCURSOR:
                {
                    // wParam: window handle
                    // lParam: low-order word is the high-test result for the cursor position; high-order word specifies the mouse message that triggered this event

                    var hitTestResult = (uint)((m.LParam.ToInt64() >> 0) & 0xFFFF);
                    var mouseMsg = (uint)((m.LParam.ToInt64() >> 16) & 0xFFFF);

                    // NOTE: for messages which we handle, we return "TRUE" (1) to halt further message processing; this may not technically be necessary
                    //       see: https://learn.microsoft.com/en-us/windows/win32/menurc/wm-setcursor
                    switch ((PInvoke.User32.WindowMessage)mouseMsg)
                    {
                        case PInvoke.User32.WindowMessage.WM_LBUTTONDOWN:
                            {
                                _visualState |= TrayButtonVisualStateFlags.LeftButtonPressed;
                                this.UpdateVisualStateAlpha();

                                result = new IntPtr(1);
                            }
                            break;
                        case PInvoke.User32.WindowMessage.WM_LBUTTONUP:
                            {
                                _visualState &= ~TrayButtonVisualStateFlags.LeftButtonPressed;
                                this.UpdateVisualStateAlpha();

                                result = new IntPtr(1);
                            }
                            break;
                        case PInvoke.User32.WindowMessage.WM_MOUSEMOVE:
                            {
                                // if we are not yet tracking the mouse position (i.e. this is effectively "mouse enter"), then start tracking it now (so that we can capture its move out of our box)
                                // NOTE: we track whether or not we are tracking the mouse by analyzing the hover state of our visual state flags
                                if ((_visualState & TrayButtonVisualStateFlags.Hover) == 0)
                                {
                                    // track mousehover (for tooltips) and mouseleave (to remove hover effect)
                                    var eventTrack = PInvokeExtensions.TRACKMOUSEEVENT.CreateNew(PInvokeExtensions.TRACKMOUSEEVENTFlags.TME_LEAVE, this.Handle, PInvokeExtensions.HOVER_DEFAULT);
                                    var trackMouseEventSuccess = PInvokeExtensions.TrackMouseEvent(ref eventTrack);
                                    if (trackMouseEventSuccess == false)
                                    {
                                        // failed; we could capture the win32 error code via GetLastWin32Error
                                        Debug.Assert(false, "Could not set up tracking of tray button window area");
                                    }

                                    _visualState |= TrayButtonVisualStateFlags.Hover;
                                    this.UpdateVisualStateAlpha();
                                }
                                result = new IntPtr(1);
                            }
                            break;
                        case PInvoke.User32.WindowMessage.WM_RBUTTONDOWN:
                            {
                                _visualState |= TrayButtonVisualStateFlags.RightButtonPressed;
                                this.UpdateVisualStateAlpha();

                                result = new IntPtr(1);
                            }
                            break;
                        case PInvoke.User32.WindowMessage.WM_RBUTTONUP:
                            {
                                _visualState &= ~TrayButtonVisualStateFlags.RightButtonPressed;
                                this.UpdateVisualStateAlpha();

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

        // if we handled the message, return 'result'; otherwise, if we did not handle the message, call through to DefWindowProc to handle the message
        if (result is not null)
        {
            m.Result = result.Value!;
        }
        else
        {
            // NOTE: per the Microsoft .NET documentation, we should call base.WndProc to process any events which we have not handled; however,
            //       in our testing, this led to frequent crashes.  So instead, we follow the traditional pattern and call DefWindowProc to handle any events which we have not handled
            //       see: https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.nativewindow.wndproc?view=windowsdesktop-6.0
            m.Result = PInvoke.User32.DefWindowProc(m.HWnd, (PInvoke.User32.WindowMessage)m.Msg, m.WParam, m.LParam);
            //base.WndProc(ref m); // causes crashes (when other native windows are capturing/processing/passing along messages)
        }
    }

    // NOTE: this function may ONLY be called when responding to a WM_PAINT message
    private void OnPaintWindowsMessage(Windows.Win32.Foundation.HWND hWnd)
    {
        // create a device context for drawing; we must destroy this automatically in a finally block.  We are effectively replicating the functionality of C++'s CPaintDC.
        Windows.Win32.Graphics.Gdi.PAINTSTRUCT paintStruct;
        // NOTE: we experienced significant issues using PInvoke.User32.BeginPaint (possibly due to its IntPtr result wrapper), so we have redeclared the BeginPaint function ourselves
        var deviceContext = Windows.Win32.PInvoke.BeginPaint(hWnd, out paintStruct)!;
        if (deviceContext == IntPtr.Zero)
        {
            // no display context is available
            Debug.Assert(false, "Cannot paint TrayButton in response to WM_PAINT message; no display context is available.");
            return;
        }
        try
        {
            // NOTE: to avoid flickering, we use buffered painting to erase the background, fill the background with a single (white) brush, and then apply the painted area to the window in a single paint operation
            Windows.Win32.Graphics.Gdi.HDC bufferedPaintDc;
            var paintBufferHandle = Windows.Win32.PInvoke.BeginBufferedPaint(paintStruct.hdc, in paintStruct.rcPaint, Windows.Win32.UI.Controls.BP_BUFFERFORMAT.BPBF_TOPDOWNDIB, null, out bufferedPaintDc);
            if (paintBufferHandle == IntPtr.Zero)
            {
                Debug.Assert(false, "Cannot begin a buffered paint operation for TrayButton (when responding to a WM_PAINT message).");
                return;
            }
            try
            {
                // NOTE: this is the section where we call all of our actual (buffered) paint operations

                // clear our window's background (i.e. the buffer background)
                var bufferedPaintClearHresult = Windows.Win32.PInvoke.BufferedPaintClear(paintBufferHandle, paintStruct.rcPaint);
                if (bufferedPaintClearHresult != Windows.Win32.Foundation.HRESULT.S_OK)
                {
                    Debug.Assert(false, "Could not clear background of TrayButton window--using buffered clearing (when responding to a WM_Paint message).");
                    return;
                }

                // create a solid white brush
                var createSolidBrushResult = Windows.Win32.PInvoke.CreateSolidBrush((Windows.Win32.Foundation.COLORREF)0x00FFFFFF);
                if (createSolidBrushResult == IntPtr.Zero)
                {
                    Debug.Assert(false, "Could not create white brush to paint the background of the TrayButton window (when responding to a WM_Paint message).");
                    return;
                }
                var whiteBrush = createSolidBrushResult;
                try
                {
                    int fillRectResult;
                    unsafe
                    {
                        fillRectResult = Windows.Win32.PInvoke.FillRect(bufferedPaintDc, &paintStruct.rcPaint, whiteBrush);
                    }
                    Debug.Assert(fillRectResult != 0, "Could not fill highlight background of Tray icon with white brush");
                }
                finally
                {
                    // clean up the white solid brush we created for the fill operation
                    var deleteObjectSuccess = Windows.Win32.PInvoke.DeleteObject(whiteBrush);
                    Debug.Assert(deleteObjectSuccess == true, "Could not delete white brush object used to highlight Tray icon");
                }
            }
            finally
            {
                // complete the buffered paint operation and free the buffered paint handle
                var endBufferedPaintHresult = Windows.Win32.PInvoke.EndBufferedPaint(paintBufferHandle, true /* copy buffer to DC, completing the paint operation */);
                Debug.Assert(endBufferedPaintHresult == Windows.Win32.Foundation.HRESULT.S_OK, "Error while attempting to end buffered paint operation for TrayButton; hresult: " + endBufferedPaintHresult);
            }
        }
        finally
        {
            // mark the end of painting; this function must always be called when BeginPaint was called (and succeeded), and only after drawing is complete
            // NOTE: per the MSDN docs, this function never returns zero (so there is no result to check)
            _ = Windows.Win32.PInvoke.EndPaint(hWnd, in paintStruct);
        }
    }

    public System.Windows.Visibility Visibility
    {
        get
        {
            return _visibility;
        }
        set
        {
            if (_visibility != value)
            {
                _visibility = value;
                this.UpdateVisibility();
            }
        }
    }

    private void UpdateVisibility()
    {
        _argbImageNativeWindow?.SetVisible(this.ShouldWindowBeVisible());
        this.UpdateVisualStateAlpha();
    }

    private bool ShouldWindowBeVisible()
    {
        return (_visibility == System.Windows.Visibility.Visible) && (_taskbarIsTopmost == true);
    }

    private void UpdateVisualStateAlpha()
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
            TrayButtonNativeWindow.SetBackgroundAlpha((Windows.Win32.Foundation.HWND)this.Handle, Math.Max(alpha, ALPHA_VALUE_FOR_TRANSPARENT_BUT_HIT_TESTABLE));
        }
        else
        {
            // collapsed or hidden controls should be invisible
            TrayButtonNativeWindow.SetBackgroundAlpha((Windows.Win32.Foundation.HWND)this.Handle, 0);
        }
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

    private void LocationChangeWindowEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint idEventThread, uint dwmsEventTime)
    {
        // we cannot process a location change message if the hwnd is zero
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        // attempt to capture the class name for the window; if the window has already been destroyed, this will fail
        string? className = null;
        var getWindowClassNameResult = TrayButtonNativeWindow.GetWindowClassName(hwnd);
        if (getWindowClassNameResult.IsSuccess)
        {
            className = getWindowClassNameResult.Value!;
        }

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
            Debug.Assert(repositionResult.IsSuccess, "Could not reposition Tray Button window");
        }
    }

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

    // NOTE: just in case we miss any edge cases to resurface our button, we resurface it from time to time on a timer
    private void ResurfaceTaskButtonTimerCallback(object? state)
    {
        this.BringTaskButtonTopmostWithoutActivating();
    }

    private void BringTaskButtonTopmostWithoutActivating()
    {
        Windows.Win32.PInvoke.SetWindowPos((Windows.Win32.Foundation.HWND)this.Handle, Windows.Win32.Foundation.HWND.HWND_TOPMOST, 0, 0, 0, 0, Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOMOVE | Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOSIZE | Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
    }

    private void ObjectReorderWindowEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint idEventThread, uint dwmsEventTime)
    {
        // we cannot process an object reorder message if the hwnd is zero
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        // attempt to capture the class name for the window; if the window has already been destroyed, this will fail
        string? className = null;
        var getWindowClassNameResult = TrayButtonNativeWindow.GetWindowClassName(hwnd);
        if (getWindowClassNameResult.IsSuccess)
        {
            className = getWindowClassNameResult.Value!;
        }

        // capture the desktop handle
        var desktopHandle = Windows.Win32.PInvoke.GetDesktopWindow();

        // if the reordered window was either the taskbar or the desktop, update the _taskbarIsTopmost state; this will generally be triggered when an app goes full-screen (or full-screen mode is exited)
        if (className == "Shell_TrayWnd" || hwnd == desktopHandle.Value)
        {
            // whenever the window ordering changes, resurface our control
            this.BringTaskButtonTopmostWithoutActivating();

            // determine if the taskbar is topmost; the taskbar's topmost flag is removed when an app goes full-screen and should cover the taskbar (e.g. a full-screen video)
            _taskbarIsTopmost = TrayButtonNativeWindow.IsTaskbarTopmost(/*hwnd -- not passed in, since the handle could be the desktop */);
            //
            // NOTE: UpdateVisibility takes both the .Visibility property and the topmost state of the taskbar into consideration to determine whether or not to show the control
            this.UpdateVisibility();
        }
    }

    private static bool IsTaskbarTopmost(Windows.Win32.Foundation.HWND? taskbarHWnd = null)
    {
        var taskbarHandle = taskbarHWnd ?? TrayButtonNativeWindow.GetWindowsTaskbarHandle();

        var taskbarWindowExStyle = PInvokeExtensions.GetWindowLongPtr_IntPtr(taskbarHandle, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        var taskbarIsTopmost = ((nint)taskbarWindowExStyle & (nint)Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_TOPMOST) != 0;

        return taskbarIsTopmost;
    }

    private MorphicResult<MorphicUnit, MorphicUnit> RecalculatePositionAndRepositionWindow()
    {
        // first, reposition our control (NOTE: this will be required to subsequently determine the position of our bitmap)
        var calculatePositionResult = TrayButtonNativeWindow.CalculatePositionAndSizeForTrayButton(this.Handle);
        if (calculatePositionResult.IsError)
        {
            Debug.Assert(false, "Cannot calculate position for tray button");
            return MorphicResult.ErrorResult();
        }
        var trayButtonPositionAndSize = calculatePositionResult.Value!;
        //
        var size = new System.Drawing.Size(trayButtonPositionAndSize.right - trayButtonPositionAndSize.left, trayButtonPositionAndSize.bottom - trayButtonPositionAndSize.top);
        var setWindowPosResult = Windows.Win32.PInvoke.SetWindowPos((Windows.Win32.Foundation.HWND)this.Handle, (Windows.Win32.Foundation.HWND)IntPtr.Zero, trayButtonPositionAndSize.left, trayButtonPositionAndSize.top, size.Width, size.Height, Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOZORDER | Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
        if (setWindowPosResult == 0)
        {
            var win32Error = (uint)System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            Debug.Assert(false, "SetWindowPos failed while trying to reposition TrayButton native window; win32 error: " + win32Error.ToString() + ".");
            return MorphicResult.ErrorResult();
            //return MorphicResult.ErrorResult<Morphic.WindowsNative.IWin32ApiError>(new Morphic.WindowsNative.IWin32ApiError.Win32Error(win32Error));
        }
        //
        // capture our updated position and size
        _trayButtonPositionAndSize = trayButtonPositionAndSize;

        // once the control is repositioned, reposition the bitmap
        var bitmap = _argbImageNativeWindow?.GetBitmap();
        if (bitmap is not null)
        {
            var positionAndResizeBitmapResult = this.PositionAndResizeBitmap(bitmap);
            if (positionAndResizeBitmapResult.IsError == true)
            {
                Debug.Assert(false, "Could not position and resize bitmap.");
                return MorphicResult.ErrorResult();
            }
        }

        // also reposition the tooltip's tracking rectangle
        if (_tooltipText is not null)
        {
            this.UpdateTooltipTextAndTracking();
        }

        _ = Windows.Win32.PInvoke.BringWindowToTop((Windows.Win32.Foundation.HWND)this.Handle);

        return MorphicResult.OkResult();
    }

    private static MorphicResult<string, Morphic.WindowsNative.IWin32ApiError> GetWindowClassName(IntPtr hWnd)
    {
        System.Text.StringBuilder classNameBuilder = new(256);
        var getClassNameResult = PInvokeExtensions.GetClassName(hWnd, classNameBuilder, classNameBuilder.Capacity);
        if (getClassNameResult == 0)
        {
            var win32Error = Marshal.GetLastWin32Error();
            return MorphicResult.ErrorResult<Morphic.WindowsNative.IWin32ApiError>(new Morphic.WindowsNative.IWin32ApiError.Win32Error((uint)win32Error));
        }

        var classNameAsString = classNameBuilder.ToString();
        return MorphicResult.OkResult(classNameAsString);
    }

    //

    public MorphicResult<MorphicUnit, MorphicUnit> SetBitmap(System.Drawing.Bitmap? bitmap)
    {
        if (bitmap is not null)
        {
            var positionAndResizeBitmapResult = this.PositionAndResizeBitmap(bitmap);
            if (positionAndResizeBitmapResult.IsError == true)
            {
                Debug.Assert(false, "Could not position and resize bitmap.");
                return MorphicResult.ErrorResult();
            }
        }

        if (_argbImageNativeWindow is not null)
        {
            var setBitmapResult = _argbImageNativeWindow!.SetBitmap(bitmap);
            if (setBitmapResult.IsError == true)
            {
                // OBSERVATION: ArgbImageNativeWindow.SetBitmap returns several error options; we ignore them here.
                return MorphicResult.ErrorResult();
            }
        }

        return MorphicResult.OkResult();
    }

    private MorphicResult<MorphicUnit, MorphicUnit> PositionAndResizeBitmap(System.Drawing.Bitmap bitmap)
    {
        // then, reposition the bitmap
        Windows.Win32.Foundation.RECT positionAndSize;
        var getWindowRectResult = Windows.Win32.PInvoke.GetWindowRect((Windows.Win32.Foundation.HWND)this.Handle, out positionAndSize);
        if (getWindowRectResult == 0)
        {
            // NOTE: we could capture the last error here; see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowrect
            return MorphicResult.ErrorResult();
        }
        //
        var bitmapSize = bitmap.Size;

        var argbImageNativeWindowSize = TrayButtonNativeWindow.CalculateWidthAndHeightForBitmap(positionAndSize, bitmapSize);
        var bitmapRect = TrayButtonNativeWindow.CalculateCenterRectInsideRect(positionAndSize, argbImageNativeWindowSize);

        if (_argbImageNativeWindow is not null)
        {
            var setPositionAndSizeResult = _argbImageNativeWindow!.SetPositionAndSize(bitmapRect);
            if (setPositionAndSizeResult.IsError == true)
            {
                // OBSERVATION: ArgbImageNativeWindow.SetPositionAndSize returns several error options; we ignore them here.
                return MorphicResult.ErrorResult();
            }
        }

        return MorphicResult.OkResult();
    }

    public void SetText(string? text)
    {
        _tooltipText = text;

        this.UpdateTooltipTextAndTracking();
    }

    //

    private IntPtr CreateTooltipWindow()
    {
        if (_tooltipWindowHandle != IntPtr.Zero)
        {
            // tooltip window already exists
            return _tooltipWindowHandle;
        }

        var tooltipWindowHandle = PInvoke.User32.CreateWindowEx(
             0 /* no styles */,
             PInvokeExtensions.TOOLTIPS_CLASS,
             null,
             PInvoke.User32.WindowStyles.WS_POPUP | (PInvoke.User32.WindowStyles)PInvokeExtensions.TTS_ALWAYSTIP,
             PInvokeExtensions.CW_USEDEFAULT,
             PInvokeExtensions.CW_USEDEFAULT,
             PInvokeExtensions.CW_USEDEFAULT,
             PInvokeExtensions.CW_USEDEFAULT,
             this.Handle,
             IntPtr.Zero,
             IntPtr.Zero,
             IntPtr.Zero);

        // NOTE: Microsoft's documentation seems to indicate that we should set the tooltip as topmost, but in our testing this was unnecessary.  It's possible that using SendMessage to add/remove tooltip text automatically handles this when the system handles showing the tooltip
        //       see: https://learn.microsoft.com/en-us/windows/win32/controls/tooltip-controls
        //PInvoke.User32.SetWindowPos(tooltipWindowHandle, PInvokeExtensions.HWND_TOPMOST, 0, 0, 0, 0, PInvoke.User32.SetWindowPosFlags.SWP_NOMOVE | PInvoke.User32.SetWindowPosFlags.SWP_NOSIZE | PInvoke.User32.SetWindowPosFlags.SWP_NOACTIVATE);

        Debug.Assert(tooltipWindowHandle != IntPtr.Zero, "Could not create tooltip window.");

        return tooltipWindowHandle;
    }

    private bool DestroyTooltipWindow()
    {
        if (_tooltipWindowHandle == IntPtr.Zero)
        {
            return true;
        }

        // set the tooltip text to empty (so that UpdateTooltipText will clear out the tooltip), then update the tooltip text.
        _tooltipText = null;
        this.UpdateTooltipTextAndTracking();

        var result = Windows.Win32.PInvoke.DestroyWindow((Windows.Win32.Foundation.HWND)_tooltipWindowHandle);
        _tooltipWindowHandle = (Windows.Win32.Foundation.HWND)IntPtr.Zero;

        return result;
    }

    private void UpdateTooltipTextAndTracking()
    {
        if (_tooltipWindowHandle == IntPtr.Zero)
        {
            // tooltip window does not exist; failed; abort
            Debug.Assert(false, "Tooptip window does not exist; if this is an expected failure, remove this assert.");
            return;
        }

        var trayButtonNativeWindowHandle = this.Handle;
        if (trayButtonNativeWindowHandle == IntPtr.Zero)
        {
            // tray button window does not exist; there is no tool window to update
            return;
        }

        var getClientRectSuccess = PInvoke.User32.GetClientRect(this.Handle, out var trayButtonClientRect);
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
                    _ = PInvoke.User32.SendMessage(_tooltipWindowHandle, (PInvoke.User32.WindowMessage)PInvokeExtensions.TTM_ADDTOOL, IntPtr.Zero, pointerToToolinfo);
                    _tooltipInfoAdded = true;
                }
                else
                {
                    // delete and re-add the tooltipinfo; this will update all the info (including the text and tracking rect)
                    _ = PInvoke.User32.SendMessage(_tooltipWindowHandle, (PInvoke.User32.WindowMessage)PInvokeExtensions.TTM_DELTOOL, IntPtr.Zero, pointerToToolinfo);
                    _ = PInvoke.User32.SendMessage(_tooltipWindowHandle, (PInvoke.User32.WindowMessage)PInvokeExtensions.TTM_ADDTOOL, IntPtr.Zero, pointerToToolinfo);
                }
            }
            else
            {
                // NOTE: we might technically call "deltool" even when a tooltipinfo was already removed
                _ = PInvoke.User32.SendMessage(_tooltipWindowHandle, (PInvoke.User32.WindowMessage)PInvokeExtensions.TTM_DELTOOL, IntPtr.Zero, pointerToToolinfo);
                _tooltipInfoAdded = false;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(pointerToToolinfo);
        }
    }

    //

    /* helper functions */

    internal static Windows.Win32.Foundation.RECT CalculateCenterRectInsideRect(Windows.Win32.Foundation.RECT outerRect, System.Drawing.Size innerSize)
    {
        var outerWidth = outerRect.right - outerRect.left;
        var outerHeight = outerRect.bottom - outerRect.top;

        var innerWidth = innerSize.Width;
        var innerHeight = innerSize.Height;

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

    internal static MorphicResult<Windows.Win32.Foundation.RECT, MorphicUnit> CalculatePositionAndSizeForTrayButton(IntPtr? trayButtonHandle)
    {
        // NOTE: in this implementation, we simply place the tray button over the taskbar, directly to the left of the system tray
        //       in the future, we may want to consider searching for any children which might occupy the area--and any system windows which are owned by the taskbar or any of its children--and then try to find a place to the "left" of those

        // get the handles for the taskbar, task button container, and the notify tray
        //
        var taskbarHandle = TrayButtonNativeWindow.GetWindowsTaskbarHandle();
        if (taskbarHandle == IntPtr.Zero) { return MorphicResult.ErrorResult(); }
        //
        var taskButtonContainerHandle = TrayButtonNativeWindow.GetWindowsTaskbarTaskButtonContainerHandle(taskbarHandle);
        if (taskButtonContainerHandle == IntPtr.Zero) { return MorphicResult.ErrorResult(); }
        //
        var notifyTrayHandle = TrayButtonNativeWindow.GetWindowsTaskbarNotificationTrayHandle(taskbarHandle);
        if (notifyTrayHandle == IntPtr.Zero) { return MorphicResult.ErrorResult(); }

        // get the RECTs for the taskbar, task button container and the notify tray
        //
        var getTaskbarRectSuccess = Windows.Win32.PInvoke.GetWindowRect(taskbarHandle, out var taskbarRect);
        if (getTaskbarRectSuccess == false) { return MorphicResult.ErrorResult(); }
        //
        var getTaskButtonContainerRectSuccess = Windows.Win32.PInvoke.GetWindowRect(taskButtonContainerHandle, out var taskButtonContainerRect);
        if (getTaskButtonContainerRectSuccess == false) { return MorphicResult.ErrorResult(); }
        //
        var getNotifyTrayRectSuccess = Windows.Win32.PInvoke.GetWindowRect(notifyTrayHandle, out var notifyTrayRect);
        if (getNotifyTrayRectSuccess == false) { return MorphicResult.ErrorResult(); }

        // determine the taskbar's orientation
        //
        System.Windows.Forms.Orientation taskbarOrientation;
        if ((taskbarRect.right - taskbarRect.left) > (taskbarRect.bottom - taskbarRect.top))
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
        if (taskbarOrientation == System.Windows.Forms.Orientation.Horizontal)
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
        if (taskbarOrientation == System.Windows.Forms.Orientation.Horizontal)
        {
            if (isRightToLeft == false)
            {
                trayButtonX = notifyTrayRect.left - trayButtonWidth;
            }
            else
            {
                trayButtonX = notifyTrayRect.right;
            }
            // NOTE: if we have any issues with positioning, try to replace taskbarRect.bottom with taskButtoncontainerRect.bottom (if we chose option #1 for our size calculations above)
            trayButtonY = taskbarRect.bottom - trayButtonHeight;
        }
        else
        {
            // NOTE: if we have any issues with positioning, try to replace taskbarRect.bottom with taskButtoncontainerRect.right (if we chose option #1 for our size calculations above)
            trayButtonX = taskbarRect.right - trayButtonWidth;
            trayButtonY = notifyTrayRect.top - trayButtonHeight;
        }

        var result = new Windows.Win32.Foundation.RECT() { left = trayButtonX, top = trayButtonY, right = trayButtonX + trayButtonWidth, bottom = trayButtonY + trayButtonHeight };
        return MorphicResult.OkResult(result);
    }

    //

    private MorphicResult<System.Drawing.Point, MorphicUnit> ConvertMouseMessageLParamToScreenPoint(IntPtr lParam)
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
            return MorphicResult.ErrorResult();
        }

        var result = new System.Drawing.Point(hitPoint.x, hitPoint.y);
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

    private static Windows.Win32.Foundation.HWND GetWindowsTaskbarHandle()
    {
        return Windows.Win32.PInvoke.FindWindow("Shell_TrayWnd", null);
    }
    //
    private static Windows.Win32.Foundation.HWND GetWindowsTaskbarTaskButtonContainerHandle(Windows.Win32.Foundation.HWND taskbarHandle)
    {
        if (taskbarHandle.Value == IntPtr.Zero)
        {
            return (Windows.Win32.Foundation.HWND)IntPtr.Zero;
        }
        return Windows.Win32.PInvoke.FindWindowEx(taskbarHandle, (Windows.Win32.Foundation.HWND)IntPtr.Zero, "ReBarWindow32", null);
    }
    //
    private static Windows.Win32.Foundation.HWND GetWindowsTaskbarNotificationTrayHandle(Windows.Win32.Foundation.HWND taskbarHandle)
    {
        if (taskbarHandle.Value == IntPtr.Zero)
        {
            return (Windows.Win32.Foundation.HWND)IntPtr.Zero;
        }
        return Windows.Win32.PInvoke.FindWindowEx(taskbarHandle, (Windows.Win32.Foundation.HWND)IntPtr.Zero, "TrayNotifyWnd", null);
    }
}
