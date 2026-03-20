// Copyright 2020-2026 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-windows/blob/master/LICENSE.txt
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

using ABI.Windows.Foundation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Morphic.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Win32.Foundation;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Morphic.MorphicBar;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MorphicBarWindow : Window, IDisposable
{
    private bool disposedValue;

    private IntPtr _hIconRawHandle = IntPtr.Zero;
    Window _dummyParentWindow;

    private const int WINDOW_CORNER_RADIUS_IN_DEVICE_UNITS = 15;

    // variables to enable full-window click-and-drag
    private Windows.Graphics.PointInt32 _dragStartWindowPosition;
    private Windows.Foundation.Point _dragStartPointerPosition;
    private bool _isDraggingWindow = false;

    public MorphicBarWindow()
    {
        InitializeComponent();

        var hwnd = (Windows.Win32.Foundation.HWND)WinRT.Interop.WindowNative.GetWindowHandle(this);

        // create a dummy "parent window" for the layout preview window (so that this window doesn't show up in the taskbar)
        _dummyParentWindow = new Window();
        IntPtr dummyParentHwndAsIntPtr = WindowNative.GetWindowHandle(_dummyParentWindow);
        Windows.Win32.PInvoke.SetWindowLongPtr(hwnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWLP_HWNDPARENT, dummyParentHwndAsIntPtr);

(this.Content as Grid)!.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DarkGreen);

        this.InitializeBorderlessRoundWindowProperties(hwnd);

        // when the window changes, we need to apply our rounded corners etc.
        this.SizeChanged += this.MorphicBarWindow_SizeChanged;

        // if the user clicks on the window, let them drag it (i.e. release the pointer capture and forward the left-click as if it's a "caption bar" left-click instead)
        this.InitializePointerPressAndDrag(this.Content);
    }

    private void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
            }

            // free unmanaged resources (unmanaged objects) and override finalizer
            if (_hIconRawHandle != IntPtr.Zero)
            {
                _ = Windows.Win32.PInvoke.DestroyIcon((Windows.Win32.UI.WindowsAndMessaging.HICON)_hIconRawHandle);
            }

            // set large fields to null
            // [none]

            disposedValue = true;
        }
    }

    // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    ~MorphicBarWindow()
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

    /* public methods */

    public MorphicResult<MorphicUnit, MorphicUnit> SetIconFromFile(string filePath, int width, int height)
    {
        IntPtr oldIconRawHandle = _hIconRawHandle;
        try
        {
            var hwnd = (Windows.Win32.Foundation.HWND)WinRT.Interop.WindowNative.GetWindowHandle(this);

            // load the .ico file as an HICON using LoadImage
            var hIcon = Windows.Win32.PInvoke.LoadImage(
                null,
                filePath,
                Windows.Win32.UI.WindowsAndMessaging.GDI_IMAGE_TYPE.IMAGE_ICON,
                width,
                height,
                Windows.Win32.UI.WindowsAndMessaging.IMAGE_FLAGS.LR_LOADFROMFILE
            );
            if (hIcon.IsInvalid)
            {
                Debug.Assert(false, "Could not load icon from file: " + filePath);
                return MorphicResult.ErrorResult();
            }
            // NOTE: there's a bug in SafeFileHandle which tries to clean up icons incorrectly, so prevent the safe handle from trying to free it
            // NOTE: this icon MUST be cleaned up in the Dispose pattern, in the unmanaged resource section
            _hIconRawHandle = hIcon.DangerousGetHandle();
            hIcon.SetHandleAsInvalid();

            // update the icons (big and small) for the window
            _ = Windows.Win32.PInvoke.SendMessage(hwnd, Windows.Win32.PInvoke.WM_SETICON, Windows.Win32.PInvoke.ICON_BIG, (Windows.Win32.Foundation.LPARAM)_hIconRawHandle);
            _ = Windows.Win32.PInvoke.SendMessage(hwnd, Windows.Win32.PInvoke.WM_SETICON, Windows.Win32.PInvoke.ICON_SMALL, (Windows.Win32.Foundation.LPARAM)_hIconRawHandle);
        }
        finally
        {
            // if the icon is changing, destroy the previous icon
            if (oldIconRawHandle != IntPtr.Zero)
            {
                _ = Windows.Win32.PInvoke.DestroyIcon((Windows.Win32.UI.WindowsAndMessaging.HICON)oldIconRawHandle);
            }
        }

        return MorphicResult.OkResult();
    }

    /* events */

    private void MorphicBarWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args)
    {
        // whenever the window size changes, re-apply the custom corner radius
        _ = this.ApplyCornerRadius(WINDOW_CORNER_RADIUS_IN_DEVICE_UNITS);
    }

    /* callbacks */
    // [none]


    /* helper methods */

    private void InitializeBorderlessRoundWindowProperties(Windows.Win32.Foundation.HWND hwnd)
    {
        // remove window chrome (minimize/maximize/close buttons); set the window to be 'always on top'; turn off the border and titlebar
        var presenter = this.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
        if (presenter is not null)
        {
            presenter.IsResizable = false;
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
            presenter.IsAlwaysOnTop = true;
            presenter.SetBorderAndTitleBar(false, false);
        }

        // update window style (i.e. remove the dialog frame)
        var style = (Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE)Windows.Win32.PInvoke.GetWindowLongPtr(hwnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        style &= ~Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_DLGFRAME;
        //
        // NOTE: SetWindowLongPtr can return 0 even if there is no error; see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowlongptrw
        System.Runtime.InteropServices.Marshal.SetLastPInvokeError(0);
        var setWindowLongPtrResult = Windows.Win32.PInvoke.SetWindowLongPtr(hwnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_STYLE, (nint)style);
        if (setWindowLongPtrResult == 0)
        {
            var win32ErrorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            if (win32ErrorCode != 0)
            {
                System.Diagnostics.Debug.Assert(false);
            }
        }

        // notify Windows that the frame has changed
        var setWindowPosResult = Windows.Win32.PInvoke.SetWindowPos(hwnd, Windows.Win32.Foundation.HWND.Null, 0, 0, 0, 0,
            Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED | Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOMOVE |
            Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOSIZE | Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOZORDER);
        System.Diagnostics.Debug.Assert(setWindowPosResult != 0);

        // turn off DWM corner rounding (since we'll round the corner ourselves, for compatibility with Windows 10 etc.)
        int cornerPreference = 1; // DWMWCP_DONOTROUND
        Span<byte> cornerPreferenceAsSpan = MemoryMarshal.AsBytes(new Span<int>(ref cornerPreference));
        var setAttributeResult = Windows.Win32.PInvoke.DwmSetWindowAttribute(hwnd, Windows.Win32.Graphics.Dwm.DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, cornerPreferenceAsSpan);
        System.Diagnostics.Debug.Assert(setAttributeResult == HRESULT.S_OK);

        // apply our rounded corners; these will be reapplied whenever the window gets resized as well
        _ = this.ApplyCornerRadius(WINDOW_CORNER_RADIUS_IN_DEVICE_UNITS);
    }

    private void InitializePointerPressAndDrag(UIElement rootDragElement)
    {
        rootDragElement.PointerPressed += (s, e) =>
        {
            var isLeftButtonPressed = e.GetCurrentPoint(null).Properties.IsLeftButtonPressed;
            if (isLeftButtonPressed)
            {
                _isDraggingWindow = true;

                // capture the window's current position (i.e. at the time that we start the drag)
                _dragStartWindowPosition = this.AppWindow.Position;
                //
                // capture the mouse cursor's position in virtual screen coordinate space
                var getCursorPosResult = Windows.Win32.PInvoke.GetCursorPos(out var startPointerPosition);
                System.Diagnostics.Debug.Assert(getCursorPosResult != 0);
                _dragStartPointerPosition = new Windows.Foundation.Point(startPointerPosition.X, startPointerPosition.Y);
                //
                // capture the pointer with WinUI (so that we can capture PointerMoved and PointerReleased events
                rootDragElement.CapturePointer(e.Pointer);
            }
        };
        rootDragElement.PointerMoved += (s, e) =>
        {
            if (_isDraggingWindow)
            {
                // get the updated cursor position
                var getCursorPosResult = Windows.Win32.PInvoke.GetCursorPos(out var currentPointerPosition);

                var deltaX = currentPointerPosition.X - _dragStartPointerPosition.X;
                var deltaY = currentPointerPosition.Y - _dragStartPointerPosition.Y;
                this.AppWindow.Move(new Windows.Graphics.PointInt32(
                    (int)(_dragStartWindowPosition.X + deltaX),
                    (int)(_dragStartWindowPosition.Y + deltaY)
                ));
            }
        };
        rootDragElement.PointerReleased += (s, e) =>
        {
            _isDraggingWindow = false;
            rootDragElement.ReleasePointerCapture(e.Pointer);
        };
    }

    private MorphicResult<MorphicUnit, MorphicUnit> ApplyCornerRadius(int radiusInDeviceUnits)
    {
        var hwnd = (Windows.Win32.Foundation.HWND)WindowNative.GetWindowHandle(this);

        bool getWindowRectSuccess = Windows.Win32.PInvoke.GetWindowRect(hwnd, out Windows.Win32.Foundation.RECT rect);
        if (getWindowRectSuccess == false)
        {
            return MorphicResult.ErrorResult();
        }

        int width = rect.right - rect.left;
        int height = rect.bottom - rect.top;
        var roundRectRegion = Windows.Win32.PInvoke.CreateRoundRectRgn(0, 0, width + 1 /*x2=left+width+1*/, height + 1/*y2=top+height+1*/, radiusInDeviceUnits, radiusInDeviceUnits);
        if (roundRectRegion == Windows.Win32.Graphics.Gdi.HRGN.Null)
        {
            return MorphicResult.ErrorResult();
        }
        try
        {
            var setWindowRgnSuccess = Windows.Win32.PInvoke.SetWindowRgn(hwnd, roundRectRegion, true);
            if (setWindowRgnSuccess == 0)
            {
                return MorphicResult.ErrorResult();
            }
        }
        finally
        {
            _ = Windows.Win32.PInvoke.DeleteObject((Windows.Win32.Graphics.Gdi.HGDIOBJ)roundRectRegion);
        }

        return MorphicResult.OkResult();
    }

    //



}
