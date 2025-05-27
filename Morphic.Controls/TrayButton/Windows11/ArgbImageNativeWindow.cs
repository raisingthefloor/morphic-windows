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

namespace Morphic.Controls.TrayButton.Windows11;

internal class ArgbImageNativeWindow : System.Windows.Forms.NativeWindow, IDisposable
{
    private bool disposedValue;

    // NOTE: SourceBitmap is the original bitmap passed into our object instance
    private System.Drawing.Bitmap? _sourceBitmap = null;
    //
    // NOTE: SizedBitmap is the resized bitmap which we paint to our window
    private System.Drawing.Bitmap? _sizedBitmap = null;

    private bool _visible;

    private static ushort? s_morphicArgbImageClassInfoExAtom = null;

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
            this.DestroyHandle();

            // set large fields to null
            // [none]

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

    public static MorphicResult<ArgbImageNativeWindow, Morphic.Controls.TrayButton.Windows11.ICreateNewError> CreateNew(IntPtr parentHWnd, int x, int y, int width, int height)
    {
        var result = new ArgbImageNativeWindow();

        /* register a custom native window class for our ARGB Image window (or refer to the already-registered class, if we captured it earlier in the application's execution) */
        const string nativeWindowClassName = "Morphic-ArgbImage";
        //
        if (s_morphicArgbImageClassInfoExAtom is null)
        {
            // register our control's custom native window class
            nint pointerToWndProcCallback;
            try
            {
                pointerToWndProcCallback = Marshal.GetFunctionPointerForDelegate(new PInvokeExtensions.WndProc(result.WndProcCallback));
            }
            catch (Exception ex)
            {
                return MorphicResult.ErrorResult<ICreateNewError>(new ICreateNewError.OtherException(ex));
            }
            //
            var hCursor = Windows.Win32.PInvoke.LoadCursor(Windows.Win32.Foundation.HINSTANCE.Null, Windows.Win32.PInvoke.IDC_ARROW);
            if (hCursor.IsNull == true)
            {
                Debug.Assert(false, "Could not load arrow cursor");
                var win32ErrorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                return MorphicResult.ErrorResult<ICreateNewError>(new ICreateNewError.Win32Error((uint)win32ErrorCode));
            }
            //
            var lpWndClassEx = new PInvokeExtensions.WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf(typeof(PInvokeExtensions.WNDCLASSEX)),
                lpfnWndProc = pointerToWndProcCallback,
                lpszClassName = nativeWindowClassName,
                hCursor = hCursor,
            };

            // NOTE: RegisterClassEx returns an ATOM (or 0 if the call failed)
            var registerClassResult = PInvokeExtensions.RegisterClassEx(ref lpWndClassEx);
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

        var windowParams = new System.Windows.Forms.CreateParams()
        {
            ClassName = s_morphicArgbImageClassInfoExAtom.ToString(), // for simplicity, we pass the value of the custom class as its integer self but in string form; our CreateWindow function will parse this and convert it to an int
            Caption = nativeWindowClassName,
            Style = unchecked((int)(/*PInvoke.User32.WindowStyles.WS_CLIPSIBLINGS | */PInvoke.User32.WindowStyles.WS_POPUP | PInvoke.User32.WindowStyles.WS_VISIBLE)),
            ExStyle = (int)(PInvoke.User32.WindowStylesEx.WS_EX_LAYERED/* | PInvoke.User32.WindowStylesEx.WS_EX_TOOLWINDOW*/ | PInvoke.User32.WindowStylesEx.WS_EX_TRANSPARENT),
            //ClassStyle = ?,
            X = x,
            Y = y,
            Width = width,
            Height = height,
            Parent = parentHWnd,
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

        // since we are making the image visible by default, set its visible state to true
        result._visible = true;

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
                var win32ErrorCode = Marshal.GetLastWin32Error();
                throw new System.ComponentModel.Win32Exception(win32ErrorCode);
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

    //

    // NOTE: during initial creation of the window, callbacks are sent to this delegated event; after creation, messages are captured by the WndProc function instead
    private IntPtr WndProcCallback(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch ((PInvoke.User32.WindowMessage)msg)
        {
            case PInvoke.User32.WindowMessage.WM_CREATE:
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
            //base.WndProc(ref m); // DO NOT USE: this causes crashes (when other native windows are capturing/processing/passing along messages)
        }
    }

    //

    public System.Drawing.Bitmap? GetBitmap()
    {
        return _sourceBitmap;
    }

    public interface ISetBitmapError
    {
        public record CannotRequestWindowRedraw(IRequestRedrawError InnerError) : ISetBitmapError;
        public record OtherException(Exception Exception) : ISetBitmapError;
        public record Win32Error(uint Win32ErrorCode) : ISetBitmapError;
        public record WindowSizeIsZero : ISetBitmapError;
    }
    public MorphicResult<MorphicUnit, ISetBitmapError> SetBitmap(System.Drawing.Bitmap? bitmap)
    {
        _sourceBitmap = bitmap;
        var recreateSizedBitmapResult = this.CreateAndCacheSizedBitmap(bitmap);
        if (recreateSizedBitmapResult.IsError == true)
        {
            switch (recreateSizedBitmapResult.Error!)
            {
                case ICreateAndCacheSizedBitmapError.OtherException(var ex):
                    return MorphicResult.ErrorResult<ISetBitmapError>(new ISetBitmapError.OtherException(ex));
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
        var setWindowPosResult = Windows.Win32.PInvoke.SetWindowPos((Windows.Win32.Foundation.HWND)this.Handle, (Windows.Win32.Foundation.HWND)IntPtr.Zero, rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top, Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOZORDER | Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
        if (setWindowPosResult == false)
        {
            var win32ErrorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            return MorphicResult.ErrorResult<ISetPositionAndSizeError>(new ISetPositionAndSizeError.CouldNotResizeWindow((uint)win32ErrorCode));
        }

        var createAndCacheSizedBitmapResult = this.CreateAndCacheSizedBitmap(_sourceBitmap);
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

            var windowStyle = PInvokeExtensions.GetWindowLongPtr_IntPtr((Windows.Win32.Foundation.HWND)this.Handle, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_STYLE);
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
            var setWindowLongPtrResult = PInvokeExtensions.SetWindowLongPtr_IntPtr((Windows.Win32.Foundation.HWND)this.Handle, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_STYLE, newWindowStyle);
            if (setWindowLongPtrResult == 0)
            {
                var win32ErrorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                return MorphicResult.ErrorResult<ISetVisibleError>(new ISetVisibleError.Win32Error((uint)win32ErrorCode));
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
    private MorphicResult<MorphicUnit, ICreateAndCacheSizedBitmapError> CreateAndCacheSizedBitmap(System.Drawing.Bitmap? bitmap)
    {
        if (bitmap is not null)
        {
            var getCurrentSizeResult = this.GetCurrentSize();
            if (getCurrentSizeResult.IsSuccess == true)
            {
                var currentSize = getCurrentSizeResult.Value!;
                if (currentSize.Width == 0 || currentSize.Height == 0)
                {
                    return MorphicResult.ErrorResult<ICreateAndCacheSizedBitmapError>(new ICreateAndCacheSizedBitmapError.WindowSizeIsZero());
                }
                //
                try
                {
                    var sizedBitmap = new System.Drawing.Bitmap(bitmap, currentSize);
                    _sizedBitmap = sizedBitmap;
                }
                catch (Exception ex)
                {
                    return MorphicResult.ErrorResult<ICreateAndCacheSizedBitmapError>(new ICreateAndCacheSizedBitmapError.OtherException(ex));
                }

                return MorphicResult.OkResult();
            }
            else
            {
                switch (getCurrentSizeResult.Error!)
                {
                    case Morphic.WindowsNative.IWin32ApiError.Win32Error(var win32ErrorCode):
                        return MorphicResult.ErrorResult<ICreateAndCacheSizedBitmapError>(new ICreateAndCacheSizedBitmapError.Win32Error(win32ErrorCode));
                    default:
                        throw new MorphicUnhandledErrorException();
                }
            }
        }
        else
        {
            _sizedBitmap = null;
            return MorphicResult.OkResult();
        }
    }

    private MorphicResult<System.Drawing.Size, Morphic.WindowsNative.IWin32ApiError> GetCurrentSize()
    {
        var getWindowRectResult = Windows.Win32.PInvoke.GetWindowRect((Windows.Win32.Foundation.HWND)this.Handle, out var rect);
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
        var redrawWindowSuccess = PInvokeExtensions.RedrawWindow(this.Handle, IntPtr.Zero, IntPtr.Zero, /*Windows.Win32.Graphics.Gdi.REDRAW_WINDOW_FLAGS.RDW_ERASE | */Windows.Win32.Graphics.Gdi.REDRAW_WINDOW_FLAGS.RDW_INVALIDATE/* | Windows.Win32.Graphics.Gdi.REDRAW_WINDOW_FLAGS.RDW_ALLCHILDREN*/);
        if (redrawWindowSuccess == false)
        {
            return MorphicResult.ErrorResult<IRequestRedrawError>(new IRequestRedrawError.CouldNotInvalidateWindow());
        }

        return MorphicResult.OkResult();
    }

    internal interface IUpdateLayeredPaintingError
    {
        public record CouldNotCreateCompatibleDeviceContext : IUpdateLayeredPaintingError;
        public record CouldNotCreateGdiBitmap(Exception InnerException) : IUpdateLayeredPaintingError;
        public record CouldNotGetCurrentSize(uint Win32ErrorCode) : IUpdateLayeredPaintingError;
        public record CouldNotGetDeviceContextOfOwner : IUpdateLayeredPaintingError;
        public record CouldNotGetOwner : IUpdateLayeredPaintingError;
        public record CouldNotSelectSizedBitmapInSourceDeviceContext : IUpdateLayeredPaintingError;
        public record CouldNotUpdateLayeredWindow(uint Win32ErrorCode) : IUpdateLayeredPaintingError;
    }
    private MorphicResult<MorphicUnit, IUpdateLayeredPaintingError> UpdateLayeredPainting()
    {
        var ownerHWnd = Windows.Win32.PInvoke.GetWindow((Windows.Win32.Foundation.HWND)this.Handle, Windows.Win32.UI.WindowsAndMessaging.GET_WINDOW_CMD.GW_OWNER);
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
        //
        var sizedBitmap = _sizedBitmap;

        if (sizedBitmap is not null)
        {
            // create a GDI bitmap from the Bitmap (using (0, 0, 0, 0) as the color of the ARGB background i.e. transparent)
            Windows.Win32.Graphics.Gdi.HGDIOBJ sizedBitmapPointer;
            try
            {
                sizedBitmapPointer = (Windows.Win32.Graphics.Gdi.HGDIOBJ)sizedBitmap.GetHbitmap(System.Drawing.Color.FromArgb(0));
            }
            catch (Exception ex)
            {
                Debug.Assert(false, "Could not create GDI bitmap object from the sized bitmap.");
                return MorphicResult.ErrorResult<IUpdateLayeredPaintingError>(new IUpdateLayeredPaintingError.CouldNotCreateGdiBitmap(ex));
            }
            try
            {
                // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getdc
                var ownerDC = Windows.Win32.PInvoke.GetDC(ownerHWnd);
                if (ownerDC.Value == IntPtr.Zero)
                {
                    Debug.Assert(false, "Could not get owner DC so that we can draw the icon bitmap.");
                    return MorphicResult.ErrorResult<IUpdateLayeredPaintingError>(new IUpdateLayeredPaintingError.CouldNotGetDeviceContextOfOwner());
                }
                try
                {
                    // see: https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-createcompatibledc
                    var sourceDC = Windows.Win32.PInvoke.CreateCompatibleDC(ownerDC);
                    if (sourceDC.Value == IntPtr.Zero)
                    {
                        Debug.Assert(false, "Could not get create compatible DC for screen DC so that we can draw the icon bitmap.");
                        return MorphicResult.ErrorResult<IUpdateLayeredPaintingError>(new IUpdateLayeredPaintingError.CouldNotCreateCompatibleDeviceContext());
                    }
                    try
                    {
                        // select our bitmap in the source DC
                        // see: https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-selectobject
                        var oldSourceDCObject = Windows.Win32.PInvoke.SelectObject(sourceDC, sizedBitmapPointer);
                        if (oldSourceDCObject.Value == new IntPtr(-1) /*HGDI_ERROR*/)
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
                                                                                                                    //var updateLayeredWindowSuccess = Windows.Win32.PInvoke.UpdateLayeredWindow((Windows.Win32.Foundation.HWND)this.Handle, ownerDC, position/* captured position of our window */, size, sourceDC, sourcePoint, (Windows.Win32.Foundation.COLORREF)0/* unused COLORREF*/, blendfunction, flags);
                            // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-updatelayeredwindow
                            var updateLayeredWindowSuccess = Windows.Win32.PInvoke.UpdateLayeredWindow((Windows.Win32.Foundation.HWND)this.Handle, ownerDC, null/* current position is not changing */, size, sourceDC, sourcePoint, (Windows.Win32.Foundation.COLORREF)0/* unused COLORREF*/, blendfunction, flags);
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
                            if (selectObjectResult == new IntPtr(-1) /*HGDI_ERROR*/)
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
            finally
            {
                // see: https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-deleteobject
                var deleteObjectSuccess = Windows.Win32.PInvoke.DeleteObject(sizedBitmapPointer);
                Debug.Assert(deleteObjectSuccess == true, "Could not delete the GDI bitmap object which was created from the icon bitmap");
            }
        }
        else
        {
            // NOTE: we do not support erasing the bitmap once it's created, so there is nothing to do here; the caller may hide the image by setting its visible state to true
        }

        // if we reach here, the operation was successful          
        return MorphicResult.OkResult();
    }
}
