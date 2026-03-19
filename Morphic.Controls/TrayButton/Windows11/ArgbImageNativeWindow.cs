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
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Morphic.Controls.TrayButton.Windows11;

internal class ArgbImageNativeWindow : IDisposable
{
    private bool disposedValue;

    // NOTE: SourceHBitmap is the GDI bitmap handle for the original bitmap passed into our object instance
    // NOTE: SizedHBitmap is the GDI bitmap handle for the resized bitmap which we paint to our window
    private record BitmapInfo
    {
        public IntPtr hSourceBitmap { get; set; }
        public int SourceWidth { get; set; }
        public int SourceHeight { get; set; }
        public IntPtr hSizedBitmap { get; set; }
    }
    private BitmapInfo? _bitmapInfo = null;

    private Windows.Win32.Foundation.HWND _hwnd = Windows.Win32.Foundation.HWND.Null;
    // NOTE: a GC handle to the class instance is stored as userdata for each native window's hwnd (so that we can trampoline from the static wndproc to the instance-specific WndProc callback)
    private GCHandle _gcHandle;

    private bool _visible;

    // NOTE: s_morphicArgbImageClassInfoExAtom and s_wndProcDelegate are initialized together
    private static ushort? s_morphicArgbImageClassInfoExAtom = null;
    // create a static wndproc delegate (which will work as a trampoline to a window's wndproc function, using the hwnd-specific userdata which stores a reference to each instance)
    // [this is done to prevent the delegate from being GC'd while the window class is registered]
    private static Windows.Win32.UI.WindowsAndMessaging.WNDPROC? s_wndProcDelegate;

    private ArgbImageNativeWindow()
    {
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // dispose managed state (managed objects)
                // [none]
            }

            // free unmanaged resources (unmanaged objects) and override finalizer
            // NOTE: the source bitmap is owned by the caller, so we do not free it here
            if (_bitmapInfo is not null)
            {
                // free sized GDI bitmap
                if (_bitmapInfo.hSizedBitmap != IntPtr.Zero)
                {
                    Windows.Win32.PInvoke.DeleteObject((Windows.Win32.Graphics.Gdi.HGDIOBJ)_bitmapInfo.hSizedBitmap);
                }
                _bitmapInfo = null;
            }
			//
            // free window handle
            if (_hwnd != IntPtr.Zero)
            {
                _ = Windows.Win32.PInvoke.DestroyWindow((Windows.Win32.Foundation.HWND)_hwnd);
                _hwnd = Windows.Win32.Foundation.HWND.Null;
            }

            // clean up the _gcHandle if it's already allocated; we allocate the GC Handle to make our event handler trampoline possible
            if (_gcHandle.IsAllocated)
            {
                _gcHandle.Free();
            }

            disposedValue = true;
        }
    }

    // NOTE: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    ~ArgbImageNativeWindow()
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

    // create a new ArgbImageNativeWindow (a child of the parent window); this control will draw the supplied image (which will be supplied later) on its surface using alpha blending
    public static MorphicResult<ArgbImageNativeWindow, Morphic.Controls.TrayButton.Windows11.ICreateNewError> CreateNew(Windows.Win32.Foundation.HWND parentHWnd, int x, int y, int width, int height)
    {
        var result = new ArgbImageNativeWindow();

        /* register a custom native window class for our ARGB Image window (or refer to the already-registered class, if we captured it earlier in the application's execution) */
        const string nativeWindowClassName = "Morphic-ArgbImage";
        //
        if (s_morphicArgbImageClassInfoExAtom is null)
        {
            // register our control's custom native window class using a static wndproc (which will act as a trampoline to an instance-specific WndProc callback)
            s_wndProcDelegate = ArgbImageNativeWindow.StaticWndProc;
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
            unsafe
            {
                fixed (char* pointerToNativeWindowClassName = nativeWindowClassName)
                {
                    lpWndClassEx = new Windows.Win32.UI.WindowsAndMessaging.WNDCLASSEXW
                    {
                        cbSize = (uint)Marshal.SizeOf<Windows.Win32.UI.WindowsAndMessaging.WNDCLASSEXW>(),
                        lpfnWndProc = ArgbImageNativeWindow.StaticWndProc,
                        lpszClassName = pointerToNativeWindowClassName,
                        hCursor = hCursor,
                    };
                }
            }

            // NOTE: RegisterClassEx returns an ATOM (or 0 if the call failed)
            var registerClassResult = Windows.Win32.PInvoke.RegisterClassEx(lpWndClassEx);
            if (registerClassResult == 0) // failure
            {
                var win32ErrorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                if (win32ErrorCode == (int)Windows.Win32.Foundation.WIN32_ERROR.ERROR_CLASS_ALREADY_EXISTS)
                {
                    Debug.Assert(false, "Class was already registered; we should have recorded this ATOM, and we cannot proceed");
                }
                return MorphicResult.ErrorResult<ICreateNewError>(new ICreateNewError.Win32Error((uint)win32ErrorCode));
            }
            s_morphicArgbImageClassInfoExAtom = registerClassResult;
        }

        /* create an instance of our native window */

        Windows.Win32.Foundation.HWND handle;
        unsafe
        {
            var atomAsString = new Windows.Win32.Foundation.PCWSTR((char*)(nint)s_morphicArgbImageClassInfoExAtom!.Value);
            fixed (char* pointerToNativeWindowClassName = nativeWindowClassName)
            {
                handle = Windows.Win32.PInvoke.CreateWindowEx(
                    dwExStyle: Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_LAYERED/* | Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_TOOLWINDOW*/ | Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_TRANSPARENT,
                    lpClassName: atomAsString,
                    lpWindowName: pointerToNativeWindowClassName,
                    dwStyle:/*Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_CLIPSIBLINGS | */Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_POPUP | Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_VISIBLE,
                    X: x,
                    Y: y,
                    nWidth: width,
                    nHeight: height,
                    hWndParent: parentHWnd,
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
        var setWindowLongPtrResult = PInvokeExtensions.SetWindowLongPtr_IntPtr((Windows.Win32.Foundation.HWND)handle, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_USERDATA, (nint)(IntPtr)result._gcHandle);
        if (setWindowLongPtrResult == 0)
        {
            var win32ErrorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            if (win32ErrorCode != 0)
            {
                return MorphicResult.ErrorResult<ICreateNewError>(new ICreateNewError.Win32Error((uint)win32ErrorCode));
            }
        }

        // since we are making the image visible by default, set its visible state to true
        result._visible = true;

        return MorphicResult.OkResult(result);
    }

    //

    // static wndproc (registered with the window class); this static wndproc callback routes messages to instance-specific callbacks (using the instance reference stored in GWL_USERDATA); also handles creation-time (pre-window-fully-init'd) messages
    private static Windows.Win32.Foundation.LRESULT StaticWndProc(Windows.Win32.Foundation.HWND hWnd, uint msg, Windows.Win32.Foundation.WPARAM wParam, Windows.Win32.Foundation.LPARAM lParam)
    {
        // try to retrieve the instance from GWL_USERDATA
        var userData = PInvokeExtensions.GetWindowLongPtr_IntPtr((Windows.Win32.Foundation.HWND)hWnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_USERDATA);
        ArgbImageNativeWindow? instance = null;
        if (userData != IntPtr.Zero)
        {
            try
            {
                var gcHandle = GCHandle.FromIntPtr(userData);
                instance = (ArgbImageNativeWindow?)gcHandle.Target;
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
        switch (msg)
        {
            case Windows.Win32.PInvoke.WM_NCDESTROY:
                {
                    // NOTE: we are calling this in response to WM_NCDESTROY (instead of WM_DESTROY)
                    //       see: https://learn.microsoft.com/en-us/windows/win32/api/uxtheme/nf-uxtheme-bufferedpaintinit
                    _ = Windows.Win32.PInvoke.BufferedPaintUnInit();

                    // NOTE: we pass along this message (i.e. we don't return a "handled" result)

                    // clear GWL_USERDATA so no more messages are routed to this instance
                    // NOTE: SetWindowLongPtr can return 0 even if there is no error; see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowlongptrw
                    System.Runtime.InteropServices.Marshal.SetLastPInvokeError(0);
                    var setWindowLongPtrResult = PInvokeExtensions.SetWindowLongPtr_IntPtr((Windows.Win32.Foundation.HWND)hWnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_USERDATA, 0);
                    Debug.Assert(setWindowLongPtrResult != 0 || System.Runtime.InteropServices.Marshal.GetLastWin32Error() == 0);
                }
                break;
            case Windows.Win32.PInvoke.WM_NCPAINT:
                // we suppress all painting of the non-client areas (so that we can have a transparent window)
                // return zero, indicating that we processed the message
                return (Windows.Win32.Foundation.LRESULT)0;
            default:
                break;
        }

        // if we did not handle the message, call through to DefWindowProc to handle the message
        return Windows.Win32.PInvoke.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    //

    public System.Drawing.Size? GetBitmapSize()
    {
        if (_bitmapInfo is null)
        {
            return null;
        }
        return new System.Drawing.Size(_bitmapInfo.SourceWidth, _bitmapInfo.SourceHeight);
    }

    public interface ISetBitmapError
    {
        public record CannotRequestWindowRedraw(IRequestRedrawError InnerError) : ISetBitmapError;
        public record Win32Error(uint Win32ErrorCode) : ISetBitmapError;
        public record WindowSizeIsZero : ISetBitmapError;
    }
    /// <summary>
    /// Sets the source bitmap from a GDI HBITMAP handle. This class does NOT take ownership of the handle;
    /// the caller is responsible for keeping the HBITMAP valid for the lifetime of this object (or until
    /// SetBitmap is called again). Pass IntPtr.Zero to clear.
    /// </summary>
    public MorphicResult<MorphicUnit, ISetBitmapError> SetBitmap(IntPtr hBitmap, int width, int height)
    {
        // free previous sized bitmap (which we own); the source bitmap is owned by the caller so we cannot and should not free it here
        if (_bitmapInfo is not null)
        {
            if (_bitmapInfo.hSizedBitmap != IntPtr.Zero)
            {
                Windows.Win32.PInvoke.DeleteObject((Windows.Win32.Graphics.Gdi.HGDIOBJ)_bitmapInfo.hSizedBitmap);
            }
            _bitmapInfo = null;
        }

        if (hBitmap != IntPtr.Zero)
        {
            _bitmapInfo = new() { hSourceBitmap = hBitmap, SourceWidth = width, SourceHeight = height, hSizedBitmap = IntPtr.Zero };
        }

        var recreateSizedBitmapResult = this.CreateAndCacheSizedBitmap();
        if (recreateSizedBitmapResult.IsError == true)
        {
            switch (recreateSizedBitmapResult.Error!)
            {
                case ICreateAndCacheSizedBitmapError.Win32Error(var win32ErrorCode):
                    return MorphicResult.ErrorResult<ISetBitmapError>(new ISetBitmapError.Win32Error(win32ErrorCode));
                case ICreateAndCacheSizedBitmapError.WindowSizeIsZero:
                    return MorphicResult.ErrorResult<ISetBitmapError>(new ISetBitmapError.WindowSizeIsZero());
                default:
                    throw new MorphicUnhandledErrorException();
            }
        }

        var requestRedrawResult = this.RequestRedraw();
        if (requestRedrawResult.IsError == true)
        {
            var innerError = requestRedrawResult.Error!;
            return MorphicResult.ErrorResult<ISetBitmapError>(new ISetBitmapError.CannotRequestWindowRedraw(innerError));
        }

        return MorphicResult.OkResult();
    }

    public interface ISetPositionAndSizeError
    {
        public record CannotRequestWindowRedraw(IRequestRedrawError InnerError) : ISetPositionAndSizeError;
        public record CouldNotResizeWindow(uint Win32ErrorCode) : ISetPositionAndSizeError;
        public record OtherException(Exception Exception) : ISetPositionAndSizeError;
        public record WindowSizeIsZero : ISetPositionAndSizeError;
        public record Win32Error(uint Win32ErrorCode) : ISetPositionAndSizeError;
    }
    //
    public MorphicResult<MorphicUnit, ISetPositionAndSizeError> SetPositionAndSize(Windows.Win32.Foundation.RECT rect)
    {
        // set the new window position (including size); we must resize the window before recreating the sized bitmap (which will be sized to the updated size)
        var setWindowPosResult = Windows.Win32.PInvoke.SetWindowPos((Windows.Win32.Foundation.HWND)_hwnd, (Windows.Win32.Foundation.HWND)IntPtr.Zero, rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top, Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOZORDER | Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
        if (setWindowPosResult == false)
        {
            var win32ErrorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            return MorphicResult.ErrorResult<ISetPositionAndSizeError>(new ISetPositionAndSizeError.CouldNotResizeWindow((uint)win32ErrorCode));
        }

        var createAndCacheSizedBitmapResult = this.CreateAndCacheSizedBitmap();
        if (createAndCacheSizedBitmapResult.IsError == true)
        {
            switch (createAndCacheSizedBitmapResult.Error!)
            {
                case ICreateAndCacheSizedBitmapError.OtherException(var ex):
                    return MorphicResult.ErrorResult<ISetPositionAndSizeError>(new ISetPositionAndSizeError.OtherException(ex));
                case ICreateAndCacheSizedBitmapError.WindowSizeIsZero:
                    return MorphicResult.ErrorResult<ISetPositionAndSizeError>(new ISetPositionAndSizeError.WindowSizeIsZero());
                case ICreateAndCacheSizedBitmapError.Win32Error(var win32ErrorCode):
                    return MorphicResult.ErrorResult<ISetPositionAndSizeError>(new ISetPositionAndSizeError.Win32Error(win32ErrorCode));
                default:
                    throw new MorphicUnhandledErrorException();
            }
        }

        var requestRedrawResult = this.RequestRedraw();
        if (requestRedrawResult.IsError == true)
        {
            var innerError = requestRedrawResult.Error!;
            return MorphicResult.ErrorResult<ISetPositionAndSizeError>(new ISetPositionAndSizeError.CannotRequestWindowRedraw(innerError));
        }

        return MorphicResult.OkResult();
    }

    public interface ISetVisibleError
    {
        public record Win32Error(uint Win32ErrorCode) : ISetVisibleError;
    }
    //
    public MorphicResult<MorphicUnit, ISetVisibleError> SetVisible(bool value)
    {
        if (_visible != value)
        {
            _visible = value;

            var windowStyle = PInvokeExtensions.GetWindowLongPtr_IntPtr((Windows.Win32.Foundation.HWND)_hwnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_STYLE);
            if (windowStyle == IntPtr.Zero)
            {
                var win32ErrorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                return MorphicResult.ErrorResult<ISetVisibleError>(new ISetVisibleError.Win32Error((uint)win32ErrorCode));
            }
            nint newWindowStyle;
            if (_visible == true)
            {
                newWindowStyle = (nint)windowStyle | (nint)Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_VISIBLE;
            }
            else
            {
                newWindowStyle = (nint)windowStyle & ~(nint)Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_VISIBLE;
            }
            //
            // NOTE: SetWindowLongPtr can return 0 even if there is no error; see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowlongptrw
            System.Runtime.InteropServices.Marshal.SetLastPInvokeError(0);
            var setWindowLongPtrResult = PInvokeExtensions.SetWindowLongPtr_IntPtr((Windows.Win32.Foundation.HWND)_hwnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_STYLE, newWindowStyle);
            if (setWindowLongPtrResult == 0)
            {
                var win32ErrorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                if (win32ErrorCode != 0)
                {
                    return MorphicResult.ErrorResult<ISetVisibleError>(new ISetVisibleError.Win32Error((uint)win32ErrorCode));
                }
            }
        }

        return MorphicResult.OkResult();
    }

    public interface ICreateAndCacheSizedBitmapError
    {
        public record OtherException(Exception Ex) : ICreateAndCacheSizedBitmapError;
        public record Win32Error(uint Win32ErrorCode) : ICreateAndCacheSizedBitmapError;
        public record WindowSizeIsZero : ICreateAndCacheSizedBitmapError;
    }
    private MorphicResult<MorphicUnit, ICreateAndCacheSizedBitmapError> CreateAndCacheSizedBitmap()
    {
        // free previous sized bitmap
        if (_bitmapInfo is not null)
        {
            if (_bitmapInfo.hSizedBitmap != IntPtr.Zero)
            {
                _ = Windows.Win32.PInvoke.DeleteObject((Windows.Win32.Graphics.Gdi.HGDIOBJ)_bitmapInfo.hSizedBitmap);
                _bitmapInfo.hSizedBitmap = IntPtr.Zero;
            }

        }
        //
        if (_bitmapInfo is null || _bitmapInfo.hSourceBitmap == IntPtr.Zero)
        {
            return MorphicResult.OkResult();
        }

        // NOTE: this will get the current size of our native window
        var getCurrentSizeResult = this.GetCurrentSize();
        if (getCurrentSizeResult.IsError == true)
        {
            switch (getCurrentSizeResult.Error!)
            {
                case Morphic.WindowsNative.IWin32ApiError.Win32Error(var win32ErrorCode):
                    return MorphicResult.ErrorResult<ICreateAndCacheSizedBitmapError>(new ICreateAndCacheSizedBitmapError.Win32Error(win32ErrorCode));
                default:
                    throw new MorphicUnhandledErrorException();
            }
        }
        var targetSize = getCurrentSizeResult.Value!;
        if (targetSize.Width == 0 || targetSize.Height == 0)
        {
            return MorphicResult.ErrorResult<ICreateAndCacheSizedBitmapError>(new ICreateAndCacheSizedBitmapError.WindowSizeIsZero());
        }

        // resize the source GDI bitmap (via its handle) to the window size using GDI
        var resizeResult = ArgbImageNativeWindow.ResizeGdiBitmap(_bitmapInfo!.hSourceBitmap, _bitmapInfo!.SourceWidth, _bitmapInfo!.SourceHeight, targetSize.Width, targetSize.Height);
        if (resizeResult.IsError == true)
        {
            var win32ErrorCode = (uint)System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            return MorphicResult.ErrorResult<ICreateAndCacheSizedBitmapError>(new ICreateAndCacheSizedBitmapError.Win32Error(win32ErrorCode));
        }
        _bitmapInfo.hSizedBitmap = resizeResult.Value!;

        return MorphicResult.OkResult();
    }

    private MorphicResult<System.Drawing.Size, Morphic.WindowsNative.IWin32ApiError> GetCurrentSize()
    {
        var getWindowRectResult = Windows.Win32.PInvoke.GetWindowRect((Windows.Win32.Foundation.HWND)_hwnd, out var rect);
        if (getWindowRectResult == 0)
        {
            var win32Error = (uint)System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            return MorphicResult.ErrorResult<Morphic.WindowsNative.IWin32ApiError>(new Morphic.WindowsNative.IWin32ApiError.Win32Error(win32Error));
        }

        var result = new System.Drawing.Size(rect.right - rect.left, rect.bottom - rect.top);
        return MorphicResult.OkResult(result);
    }

    //

    internal interface IRequestRedrawError
    {
        public record CouldNotInvalidateWindow : IRequestRedrawError;
        public record CouldNotUpdateLayeredPainting(IUpdateLayeredPaintingError InnerError) : IRequestRedrawError;
    }
    private MorphicResult<MorphicUnit, IRequestRedrawError> RequestRedraw()
    {
        // update our layered bitmap
        var updateLayeredPaintingResult = this.UpdateLayeredPainting();
        if (updateLayeredPaintingResult.IsError == true)
        {
            var innerError = updateLayeredPaintingResult.Error!;
            return MorphicResult.ErrorResult<IRequestRedrawError>(new IRequestRedrawError.CouldNotUpdateLayeredPainting(innerError));
        }

        // invalidate the window
        Windows.Win32.Foundation.BOOL redrawWindowSuccess;
        unsafe { redrawWindowSuccess = Windows.Win32.PInvoke.RedrawWindow(_hwnd, null, Windows.Win32.Graphics.Gdi.HRGN.Null, /*Windows.Win32.Graphics.Gdi.REDRAW_WINDOW_FLAGS.RDW_ERASE | */Windows.Win32.Graphics.Gdi.REDRAW_WINDOW_FLAGS.RDW_INVALIDATE/* | Windows.Win32.Graphics.Gdi.REDRAW_WINDOW_FLAGS.RDW_ALLCHILDREN*/); }
        if (redrawWindowSuccess == false)
        {
            return MorphicResult.ErrorResult<IRequestRedrawError>(new IRequestRedrawError.CouldNotInvalidateWindow());
        }

        return MorphicResult.OkResult();
    }

    internal interface IUpdateLayeredPaintingError
    {
        public record CouldNotCreateCompatibleDeviceContext : IUpdateLayeredPaintingError;
        public record CouldNotGetCurrentSize(uint Win32ErrorCode) : IUpdateLayeredPaintingError;
        public record CouldNotGetDeviceContextOfOwner : IUpdateLayeredPaintingError;
        public record CouldNotGetOwner : IUpdateLayeredPaintingError;
        public record CouldNotSelectSizedBitmapInSourceDeviceContext : IUpdateLayeredPaintingError;
        public record CouldNotUpdateLayeredWindow(uint Win32ErrorCode) : IUpdateLayeredPaintingError;
    }
    private MorphicResult<MorphicUnit, IUpdateLayeredPaintingError> UpdateLayeredPainting()
    {
        var ownerHWnd = Windows.Win32.PInvoke.GetWindow((Windows.Win32.Foundation.HWND)_hwnd, Windows.Win32.UI.WindowsAndMessaging.GET_WINDOW_CMD.GW_OWNER);
        if (ownerHWnd == Windows.Win32.Foundation.HWND.Null)
        {
            return MorphicResult.ErrorResult<IUpdateLayeredPaintingError>(new IUpdateLayeredPaintingError.CouldNotGetOwner());
        }
        //
        var getCurrentSizeResult = this.GetCurrentSize();
        if (getCurrentSizeResult.IsError == true)
        {
            switch (getCurrentSizeResult.Error!)
            {
                case Morphic.WindowsNative.IWin32ApiError.Win32Error(var win32ErrorCode):
                    Debug.Assert(false, "Could not get current size of native window; win32 error: " + win32ErrorCode.ToString());
                    return MorphicResult.ErrorResult<IUpdateLayeredPaintingError>(new IUpdateLayeredPaintingError.CouldNotGetCurrentSize(win32ErrorCode));
                default:
                    throw new MorphicUnhandledErrorException();
            }
        }
        var size = getCurrentSizeResult.Value!;

        if (_bitmapInfo is not null && _bitmapInfo.hSizedBitmap != IntPtr.Zero)
        {
            // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getdc
            var ownerDC = Windows.Win32.PInvoke.GetDC(ownerHWnd);
            if (ownerDC == IntPtr.Zero)
            {
                Debug.Assert(false, "Could not get owner DC so that we can draw the icon bitmap.");
                return MorphicResult.ErrorResult<IUpdateLayeredPaintingError>(new IUpdateLayeredPaintingError.CouldNotGetDeviceContextOfOwner());
            }
            try
            {
                // see: https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-createcompatibledc
                var sourceDC = Windows.Win32.PInvoke.CreateCompatibleDC(ownerDC);
                if (sourceDC == IntPtr.Zero)
                {
                    Debug.Assert(false, "Could not get create compatible DC for screen DC so that we can draw the icon bitmap.");
                    return MorphicResult.ErrorResult<IUpdateLayeredPaintingError>(new IUpdateLayeredPaintingError.CouldNotCreateCompatibleDeviceContext());
                }
                try
                {
                    // select our sized bitmap in the source DC
                    // see: https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-selectobject
                    var oldSourceDCObject = Windows.Win32.PInvoke.SelectObject(sourceDC, (Windows.Win32.Graphics.Gdi.HGDIOBJ)_bitmapInfo.hSizedBitmap);
                    if (oldSourceDCObject == PInvokeExtensions.HGDI_ERROR)
                    {
                        Debug.Assert(false, "Could not select the icon bitmap GDI object to update the layered window with the alpha-blended bitmap.");
                        return MorphicResult.ErrorResult<IUpdateLayeredPaintingError>(new IUpdateLayeredPaintingError.CouldNotSelectSizedBitmapInSourceDeviceContext());
                    }
                    try
                    {
                        // configure our blend function to blend the bitmap into its background
                        // see: https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-blendfunction
                        var blendfunction = new Windows.Win32.Graphics.Gdi.BLENDFUNCTION()
                        {
                            BlendOp = (byte)Windows.Win32.PInvoke.AC_SRC_OVER, /* the only available blend op, this will place the source bitmap over the destination bitmap based on the alpha values of the source pixels */
                            BlendFlags = 0, /* must be zero */
                            SourceConstantAlpha = 255, /* use per-pixel alpha values */
                            AlphaFormat = (byte)Windows.Win32.PInvoke.AC_SRC_ALPHA, /* the bitmap has an alpha channel; it MUST be a 32bpp bitmap */
                        };
                        var sourcePoint = new System.Drawing.Point(0, 0);
                        var flags = Windows.Win32.UI.WindowsAndMessaging.UPDATE_LAYERED_WINDOW_FLAGS.ULW_ALPHA; // this flag indicates the blendfunction should be used as the blend function
                        // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-updatelayeredwindow
                        var updateLayeredWindowSuccess = Windows.Win32.PInvoke.UpdateLayeredWindow((Windows.Win32.Foundation.HWND)_hwnd, ownerDC, null/* current position is not changing */, size, sourceDC, sourcePoint, (Windows.Win32.Foundation.COLORREF)0/* unused COLORREF*/, blendfunction, flags);
                        if (updateLayeredWindowSuccess == false)
                        {
                            var win32ErrorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                            Debug.Assert(false, "Could not update the layered window with the alpha-blended bitmap; win32 error code: " + win32ErrorCode.ToString());
                            return MorphicResult.ErrorResult<IUpdateLayeredPaintingError>(new IUpdateLayeredPaintingError.CouldNotUpdateLayeredWindow((uint)win32ErrorCode));
                        }
                    }
                    finally
                    {
                        // restore the old source object for the source DC
                        // see: https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-selectobject
                        var selectObjectResult = Windows.Win32.PInvoke.SelectObject(sourceDC, oldSourceDCObject);
                        if (selectObjectResult == PInvokeExtensions.HGDI_ERROR)
                        {
                            Debug.Assert(false, "Could not restore the screen's compatible DC to its previous object after attempting to update the layered window.");
                        }
                    }
                }
                finally
                {
                    // see: https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-deletedc
                    var deleteDCSuccess = Windows.Win32.PInvoke.DeleteDC(sourceDC);
                    Debug.Assert(deleteDCSuccess == true, "Could not delete the compatible DC for the owner DC.");
                }
            }
            finally
            {
                //see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-releasedc
                var releaseDcResult = Windows.Win32.PInvoke.ReleaseDC(ownerHWnd, ownerDC);
                Debug.Assert(releaseDcResult == 1, "Could not release owner DC.");
            }
        }
        else
        {
            // NOTE: we do not support erasing the bitmap once it's created, so there is nothing to do here; the caller may hide the image by setting its visible state to true
        }

        // if we reach here, the operation was successful
        return MorphicResult.OkResult();
    }

    /// <summary>Creates a new 32bpp top-down DIB section at (destW x destH) by stretching the source HBITMAP.</summary>
    /// NOTE: this function returns a new DIB handle which must be cleaned up (freed) by the caller eventually
    private static MorphicResult<IntPtr, MorphicUnit> ResizeGdiBitmap(IntPtr source, int sourceWidth, int sourceHeight, int destWidth, int destHeight)
    {
        // get handle to the device context for the whole screen
        var screenDC = Windows.Win32.PInvoke.GetDC(Windows.Win32.Foundation.HWND.Null /* entire screen */);
        if (screenDC == IntPtr.Zero)
        {
            return MorphicResult.ErrorResult();
        }
        try
        {
            var srcDC = Windows.Win32.PInvoke.CreateCompatibleDC(screenDC);
            if (srcDC == IntPtr.Zero)
            {
                return MorphicResult.ErrorResult();
            }
            try
            {
                var dstDC = Windows.Win32.PInvoke.CreateCompatibleDC(screenDC);
                if (dstDC == IntPtr.Zero)
                {
                    return MorphicResult.ErrorResult();
                }
                try
                {
                    // create destination 32bpp top-down DIB section
                    IntPtr destHBitmapFromDIB;
                    uint sizeOfBitmapInfoHeader;
                    unsafe
                    {
                        sizeOfBitmapInfoHeader = (uint)sizeof(Windows.Win32.Graphics.Gdi.BITMAPINFOHEADER);
                    }
                    var bmi = new Windows.Win32.Graphics.Gdi.BITMAPINFO()
                    {
                        bmiHeader = new()
                        {
                            biSize = sizeOfBitmapInfoHeader,
                            biWidth = destWidth,
                            biHeight = -destHeight, // negative = top-down
                            biPlanes = 1,
                            biBitCount = 32,
                            biCompression = (int)Windows.Win32.Graphics.Gdi.BI_COMPRESSION.BI_RGB,
                        }
                    };

                    // create a DIB (to write to); this is what we'll return the handle to
                    Windows.Win32.DeleteObjectSafeHandle destBitmapSafeHandle;
                    unsafe
                    {
                        void* destBits;
                        destBitmapSafeHandle = Windows.Win32.PInvoke.CreateDIBSection(screenDC, &bmi, Windows.Win32.Graphics.Gdi.DIB_USAGE.DIB_RGB_COLORS, out destBits, null, 0);
                    }
                    if (destBitmapSafeHandle is null || destBitmapSafeHandle.IsInvalid)
                    {
                        return MorphicResult.ErrorResult();
                    }
                    destHBitmapFromDIB = destBitmapSafeHandle.DangerousGetHandle(); // capture handle as IntPtr
                    destBitmapSafeHandle.SetHandleAsInvalid();
                    //
                    var oldSrc = Windows.Win32.PInvoke.SelectObject(srcDC, (Windows.Win32.Graphics.Gdi.HGDIOBJ)source);
                    var oldDst = Windows.Win32.PInvoke.SelectObject(dstDC, (Windows.Win32.Graphics.Gdi.HGDIOBJ)destHBitmapFromDIB);
                    try
                    {
                        // copy the source GDI bitmap into the new target GDI bitmap (and resize it simultaneously) using AlphaBlend
                        var blendFunction = new Windows.Win32.Graphics.Gdi.BLENDFUNCTION()
                        {
                            BlendOp = (byte)Windows.Win32.PInvoke.AC_SRC_OVER,
                            BlendFlags = 0,
                            SourceConstantAlpha = 255, // use per-pixel alpha
                            AlphaFormat = (byte)Windows.Win32.PInvoke.AC_SRC_ALPHA,
                        };
                        var alphaBlendResult = Windows.Win32.PInvoke.AlphaBlend(dstDC, 0, 0, destWidth, destHeight, srcDC, 0, 0, sourceWidth, sourceHeight, blendFunction);
                        Debug.Assert(alphaBlendResult != 0);
                    }
                    finally
                    {
                        // restore the old src and dest object in the current device context
                        Windows.Win32.Graphics.Gdi.HGDIOBJ selectObjectResult;
                        selectObjectResult = Windows.Win32.PInvoke.SelectObject(dstDC, oldDst);
                        Debug.Assert(selectObjectResult.IsNull == false && selectObjectResult != PInvokeExtensions.HGDI_ERROR);
                        selectObjectResult = Windows.Win32.PInvoke.SelectObject(srcDC, oldSrc);
                        Debug.Assert(selectObjectResult.IsNull == false && selectObjectResult != PInvokeExtensions.HGDI_ERROR);
                    }

                    return MorphicResult.OkResult(destHBitmapFromDIB);
                }
                finally
                {
                    var deleteDcResult = Windows.Win32.PInvoke.DeleteDC(dstDC);
                    Debug.Assert(deleteDcResult != 0);
                }
            }
            finally
            {
                var deleteDcResult = Windows.Win32.PInvoke.DeleteDC(srcDC);
                Debug.Assert(deleteDcResult != 0);
            }
        }
        finally
        {
            _ = Windows.Win32.PInvoke.ReleaseDC((Windows.Win32.Foundation.HWND)IntPtr.Zero, screenDC);
        }
    }
}
