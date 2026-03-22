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
using System.Threading;
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
    DummyWindow _dummyParentWindow;

    private Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue;

    // animation timer for moving the window
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _moveAnimationTimer;

    private const int WINDOW_CORNER_RADIUS_IN_DEVICE_UNITS = 10;

    // logical (96 DPI) window size — scaled by the current monitor's DPI
    private uint _logicalLength = 67; // 100 pixels at 150% zoom
    private uint _logicalThickness = 67; // 100 pixels at 150% zoom

    // variables to enable full-window click-and-drag
    private Windows.Graphics.PointInt32 _dragStartWindowPosition;
    private Windows.Foundation.Point _dragStartPointerPosition;
    private bool _isDraggingWindow = false;

    // the layout preview window lets us show the user where the window will move to if they release the mouse cursor
    private Morphic.MorphicBar.LayoutPreviewWindow.LayoutPreviewWindow _layoutPreviewWindow = null!;
    private Orientation? _layoutPreviewWindowOrientation = null;
    private DockingLocation? _layoutPreviewDockingLocation = null;
    private Windows.Win32.Foundation.RECT? _lastLayoutPreviewTargetPosition = null;

    // orientation of the MorphicBar
    private Microsoft.UI.Xaml.Controls.Orientation _orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal;
    public event EventHandler<Microsoft.UI.Xaml.Controls.Orientation>? OrientationChanged;

    private Morphic.MorphicBar.DockingLocation _dockingLocation = DockingLocation.FloatingBottomRight; // default location
    public event EventHandler<Morphic.MorphicBar.DockingLocation>? DockingLocationChanged;

    // NOTE: as we are handling sizing ourselves, we need to manage size scaling ourselves; this tracks the latest screen scale (so that we know if we need to resize our window)
    private double? _lastRasterizationScale = null;

    public MorphicBarWindow()
    {
        InitializeComponent();

        _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        var hwnd = (Windows.Win32.Foundation.HWND)WinRT.Interop.WindowNative.GetWindowHandle(this);

        // create a dummy "parent window" for the layout preview window (so that this window doesn't show up in the taskbar)
        _dummyParentWindow = new DummyWindow();
        _ = _dummyParentWindow.SetAsParentHwnd(hwnd);

(this.Content as Grid)!.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DarkGreen);

        // create a layout preview window; we'll need this whenever the MorphicBar is moved; this is created up front, as it can take a little time to create the window
        _layoutPreviewWindow = new();

        this.InitializeBorderlessWindowProperties(hwnd);

        // if the user clicks on the window, let them drag it (i.e. release the pointer capture and forward the left-click as if it's a "caption bar" left-click instead)
        this.InitializePointerPressAndDrag(this.Content);

        this.Activated += MorphicBarWindow_Activated;
        (this.Content as Grid)!.Loaded += RootGrid_Loaded;
    }

    private void MorphicBarWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        // handle any post-load code here
    }

    private void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        // set the initial size based on current DPI, and resize whenever DPI changes (e.g. moving to another monitor)
        var lastRasterizationScale = this.Content.XamlRoot.RasterizationScale;
        _lastRasterizationScale = lastRasterizationScale;
        this.Content.XamlRoot.Changed += (s, e) =>
        {
            this.RasterizationScaleChanged();
        };
        // and update the window size
        this.UpdateAppWindowSizeUsingRasterizationScale(lastRasterizationScale);
    }

    private void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // dispose managed state (managed objects)
                _dummyParentWindow.Dispose();
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

    public void Resize(uint logicalLength, uint logicalThickness)
    {
        _logicalLength = logicalLength;
        _logicalThickness = logicalThickness;

        if (_lastRasterizationScale is not null)
        {
            this.UpdateAppWindowSizeUsingRasterizationScale(_lastRasterizationScale!.Value);
        }
    }

    /// <summary>
    /// Smoothly animates the window to the target position and optionally to a target size,
    /// using ease-out cubic interpolation. If called while a previous animation is in progress,
    /// the current animation is cancelled and a new one starts from the window's current state.
    /// </summary>
    internal void AnimateMoveTo(Windows.Win32.Graphics.Gdi.HMONITOR hMonitor, Microsoft.UI.Xaml.Controls.Orientation targetOrientation, DockingLocation targetDockingLocation, TimeSpan duration, Action? afterEachStepAction = null)
    {
        // stop any existing timer
        _moveAnimationTimer?.Stop();
        _moveAnimationTimer = null;

        // calculate the target location
        var getRectForDockingLocationResult = LayoutUtils.GetRectForDockingLocation(targetDockingLocation, targetOrientation, _logicalLength, _logicalThickness, hMonitor);
        if (getRectForDockingLocationResult.IsError)
        {
            Debug.Assert(false);
            return;
        }
        var targetRect = getRectForDockingLocationResult.Value!;
        var targetPosition = new Windows.Graphics.PointInt32(targetRect.X, targetRect.Y);
        var targetSize = new Windows.Graphics.SizeInt32(targetRect.Width, targetRect.Height);

        // start the new animation
        _moveAnimationTimer = AnimationUtils.AnimateMoveTo(_dispatcherQueue, this.AppWindow, targetPosition, targetSize, duration, afterEachStepAction);
    }

    internal void AnimateStop()
    {
        _moveAnimationTimer?.Stop();
        _moveAnimationTimer = null;
    }

    /* properties */

    public Microsoft.UI.Xaml.Controls.Orientation Orientation
    {
        get => _orientation;
        set
        {
            if (_orientation != value)
            {
                _orientation = value;

                if (_lastRasterizationScale is not null)
                {
                    this.UpdateAppWindowSizeUsingRasterizationScale(_lastRasterizationScale!.Value);
                }

                OrientationChanged?.Invoke(this, value);
            }
        }
    }

    /* events */

    private void RasterizationScaleChanged()
    {
        var rasterizationScale = this.Content.XamlRoot.RasterizationScale;
        if (rasterizationScale == _lastRasterizationScale)
        {
            return;
        }
        _lastRasterizationScale = rasterizationScale;

        // dispatch the resize asynchronously so it runs after WinUI finishes its own DPI handling
        this.DispatcherQueue.TryEnqueue(() =>
        {
            this.UpdateAppWindowSizeUsingRasterizationScale(rasterizationScale);
        });
    }

    /* callbacks */
    // [none]


    /* helper methods */

    private void UpdateAppWindowSizeUsingRasterizationScale(double rasterizationScale)
    {
        int physicalWidth;
        int physicalHeight;
        switch (_orientation)
        {
            case Orientation.Horizontal:
                physicalWidth = (int)(_logicalLength * rasterizationScale);
                physicalHeight = (int)(_logicalThickness * rasterizationScale);
                break;
            case Orientation.Vertical:
                physicalWidth = (int)(_logicalThickness * rasterizationScale);
                physicalHeight = (int)(_logicalLength * rasterizationScale);
                break;
            default:
                throw new Exception("invalid code path");
        }

        this.AppWindow.Resize(new Windows.Graphics.SizeInt32(physicalWidth, physicalHeight));

        // every time we update the window size, reapply the corner radius (which will also resize the visible area)
        var cornerRadiusInDeviceUnits = (int)(WINDOW_CORNER_RADIUS_IN_DEVICE_UNITS * rasterizationScale);
        _ = this.ApplyCornerRadius(cornerRadiusInDeviceUnits);
    }

    private void InitializeBorderlessWindowProperties(Windows.Win32.Foundation.HWND hwnd)
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
                var newLeft = (int)(_dragStartWindowPosition.X + deltaX);
                var newTop = (int)(_dragStartWindowPosition.Y + deltaY);
                this.AppWindow.Move(new Windows.Graphics.PointInt32(
                    newLeft,
                    newTop
                ));

                // determine if/where we should show the layout preview window
                var newCenterX = newLeft + (this.AppWindow.Size.Width / 2);
                var newCenterY = newTop + (this.AppWindow.Size.Height / 2);
                this.UpdateLayoutPreviewState(currentPointerPosition, new System.Drawing.Point(newCenterX, newCenterY), _orientation);
            }
        };
        rootDragElement.PointerReleased += (s, e) =>
        {
            _isDraggingWindow = false;
            rootDragElement.ReleasePointerCapture(e.Pointer);

            if (_layoutPreviewWindow.Visible == true)
            {
                _layoutPreviewWindow.AppWindow.Hide();
            }

            // capture the mouse cursor's position in virtual screen coordinate space
            _ = Windows.Win32.PInvoke.GetCursorPos(out var currentPointerPosition);
            var hMonitor = Windows.Win32.PInvoke.MonitorFromPoint(currentPointerPosition, Windows.Win32.Graphics.Gdi.MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
            if (hMonitor.IsNull)
            {
                Debug.Assert(false, "No monitor handle; this might be a headless system; aborting.");
                return;
            }
            var verticalBarDockingHitAreaWidth = _logicalThickness;
            Action applyCornerRadiusAction = () => { this.ApplyCornerRadius(WINDOW_CORNER_RADIUS_IN_DEVICE_UNITS); };
            //
            if (_layoutPreviewWindowOrientation is not null)
            {
                if (_orientation != _layoutPreviewWindowOrientation)
                {
                    this.Rotate90DegreesAroundPoint(currentPointerPosition);
                }
                _orientation = _layoutPreviewWindowOrientation!.Value;
            }
            if (_layoutPreviewDockingLocation is not null)
            {
                _dockingLocation = _layoutPreviewDockingLocation!.Value;
            }
            this.AnimateMoveTo(hMonitor, _orientation, _dockingLocation, new TimeSpan(0, 0, 1), applyCornerRadiusAction);

            _layoutPreviewWindowOrientation = null;
            _layoutPreviewDockingLocation = null;
        };
    }

    private void Rotate90DegreesAroundPoint(System.Drawing.Point centerPoint)
    {
        var windowPosition = this.AppWindow.Position;
        var windowSize = this.AppWindow.Size;

        bool cursorIsOverWindow = centerPoint.X >= windowPosition.X && centerPoint.X < windowPosition.X + windowSize.Width && 
                                  centerPoint.Y >= windowPosition.Y && centerPoint.Y < windowPosition.Y + windowSize.Height;

        // swap dimensions
        var newWidth = windowSize.Height;
        var newHeight = windowSize.Width;

        if (cursorIsOverWindow == false)
        {
            // fallback position: if the center point isn't within the window, just rotate the MorphicBar 90 degrees in place (i.e. rotate around window center)
            var centerX = windowPosition.X + windowSize.Width / 2;
            var centerY = windowPosition.Y + windowSize.Height / 2;
            var newX = centerX - newWidth / 2;
            var newY = centerY - newHeight / 2;
            this.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(newX, newY, newWidth, newHeight));
        }
        else
        {
            // cursor is over window; rotate around the cursor

            // calculate center point's proportional position within the window (0.0 to 1.0)
            var proportionX = (double)(centerPoint.X - windowPosition.X) / windowSize.Width;
            var proportionY = (double)(centerPoint.Y - windowPosition.Y) / windowSize.Height;

            // reposition so the cursor stays at the same proportional point in the new dimensions
            var newX = centerPoint.X - (int)(proportionX * newWidth);
            var newY = centerPoint.Y - (int)(proportionY * newHeight);

            this.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(newX, newY, newWidth, newHeight));
        }

        this.ApplyCornerRadius(WINDOW_CORNER_RADIUS_IN_DEVICE_UNITS);
    }

    private void UpdateLayoutPreviewState(System.Drawing.Point currentPointerPosition, System.Drawing.Point windowCenterPoint, Microsoft.UI.Xaml.Controls.Orientation orientation)
    {
        var ANIMATION_DURATION = new TimeSpan(0, 0, 0, 0, 500);

        // get the current monitor, based on the current mouse cursor relative position
        var hMonitor = Windows.Win32.PInvoke.MonitorFromPoint(currentPointerPosition, Windows.Win32.Graphics.Gdi.MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        if (hMonitor.IsNull)
        {
            Debug.Assert(false, "No monitor handle; this might be a headless system; aborting.");
            return;
        }

        // get the monitor's info (including dimensions)
        var monitorInfo = new Windows.Win32.Graphics.Gdi.MONITORINFO();
        monitorInfo.cbSize = (uint)Marshal.SizeOf<Windows.Win32.Graphics.Gdi.MONITORINFO>();
        var getMonitorInfoResult = Windows.Win32.PInvoke.GetMonitorInfo(hMonitor, ref monitorInfo);
        if (getMonitorInfoResult == 0)
        {
            Debug.Assert(false);
            return;
        }

        // get the absolute monitor size (including taskbar, etc.)
        var monitorFullRect = monitorInfo.rcMonitor;
        // and get the working area (excluding taskbar, etc.)
        var monitorWorkingArea = monitorInfo.rcWork;

        // get the RasterizationScale for this monitor
        var getRasterizationScaleResult = LayoutUtils.GetRasterizationScaleForMonitor(hMonitor);
        if (getRasterizationScaleResult.IsError)
        {
            return;
        }
        double rasterizationScale = getRasterizationScaleResult!.Value;

        /* determine target area on monitor (in coords) where the bar will go when released */
        
        // select which 'center' point will be used to determine whether a MorphicBar will be docked left/right or top/bottom depends on the current MorphicBar orientation
        System.Drawing.Point horizontalDockingCenterPoint;
        System.Drawing.Point verticalDockingCenterPoint;
        if (orientation == Orientation.Horizontal)
        {
            // if the orientation is horiontal:
            // - windowCenterPoint determines HORIZONTAL DOCKING
            // - currentPointerPos determines VERTICAL DOCKING
            horizontalDockingCenterPoint = windowCenterPoint;
            verticalDockingCenterPoint = currentPointerPosition;
        }
        else
        {
            // if the orientation is vertical:
            // - windowCenterPoint determines VERTICAL DOCKING
            // - currentPointerPos determines HORIZONTAL DOCKING
            horizontalDockingCenterPoint = currentPointerPosition;
            verticalDockingCenterPoint = windowCenterPoint;
        }

        // calculate the preview window rect using the already-gathered data points
        var verticalBarDockingHitAreaWidth = _logicalThickness;
        var calculatePreviewWindowRectResult = LayoutUtils.CalculatePreviewDockingLocation(monitorFullRect, monitorWorkingArea, currentPointerPosition, horizontalDockingCenterPoint, verticalDockingCenterPoint, rasterizationScale, verticalBarDockingHitAreaWidth);
        if (calculatePreviewWindowRectResult.IsError)
        {
            return;
        }
        var newPreviewDockingLocation = calculatePreviewWindowRectResult.Value!.DockingLocation;
        var newPreviewOrientation = calculatePreviewWindowRectResult.Value!.Orientation;
        var getRectForDockingLocationResult = LayoutUtils.GetRectForDockingLocation(newPreviewDockingLocation, newPreviewOrientation, _logicalLength, _logicalThickness, hMonitor);
        if (getRectForDockingLocationResult.IsError)
        {
            return;
        }
        var newPreviewRect = getRectForDockingLocationResult!.Value;

		// NOTE: we should only show the preview window once the MorphicBarWindow window has moved far enough to dock in a different location; therefore, don't
		//       show the layout preview window until the MorphicBar has been moved far enough to warrant it (i.e. docking location or orientation changes)
        if (_layoutPreviewWindow.Visible == false && _layoutPreviewDockingLocation == null && (newPreviewDockingLocation == _dockingLocation && newPreviewOrientation == _orientation))
        {
            // if the window hasn't moved far enough to have a new docking location, don't show the layout window yet
            return;
        }

        _layoutPreviewDockingLocation = newPreviewDockingLocation;
        _layoutPreviewWindowOrientation = newPreviewOrientation;

        // if the layout preview is not already visible, create a small preview window (which will "expand out") and set its initial orientation
        if (_layoutPreviewWindow.Visible == false)
        {
            // calculate a preview "10%-sized" window that can grow into the animated full-size preview window
            var initialSizePercent = 0.10;
            var smallPreviewWidth = (int)(newPreviewRect.Width * initialSizePercent);
            var smallPreviewHeight = (int)(newPreviewRect.Height * initialSizePercent);
            var smallPreviewLeft = newPreviewRect.left + ((newPreviewRect.Width  - smallPreviewWidth) / 2);
            var smallPreviewTop = newPreviewRect.top + ((newPreviewRect.Height - smallPreviewHeight) / 2);

            _lastLayoutPreviewTargetPosition = null;

            _layoutPreviewWindow.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(smallPreviewLeft, smallPreviewTop, smallPreviewWidth, smallPreviewHeight));
            _layoutPreviewWindow.AppWindow.Show();
            Debug.Assert(_layoutPreviewWindow.AppWindow.Position.Y == smallPreviewTop);
        }

        // resize/move the window to its new position (with animation)
        if (_lastLayoutPreviewTargetPosition is null || Windows.Win32.PInvoke.EqualRect(newPreviewRect, _lastLayoutPreviewTargetPosition!.Value) == false)
        {
            _layoutPreviewWindow.AnimateMoveTo(new Windows.Graphics.PointInt32(newPreviewRect.left, newPreviewRect.top), new Windows.Graphics.SizeInt32(newPreviewRect.Width, newPreviewRect.Height), ANIMATION_DURATION);
            // update the layout position
            _lastLayoutPreviewTargetPosition = newPreviewRect;
        }
    }

    //

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
        bool mustCleanupRoundRectRegion = true;
        try
        {
            // NOTE: the system owns the roundRectRegion (and will dispose of it) once we call this function; we should only delete it if the SetWindowRgn call fails
            var setWindowRgnSuccess = Windows.Win32.PInvoke.SetWindowRgn(hwnd, roundRectRegion, true);
            if (setWindowRgnSuccess == 0)
            {
                return MorphicResult.ErrorResult();
            }

            // since SetWindowRgn succeeded, it now owns the RoundRectRegion
            mustCleanupRoundRectRegion = false;
        }
        finally
        {
            if (mustCleanupRoundRectRegion == true)
            {
                _ = Windows.Win32.PInvoke.DeleteObject((Windows.Win32.Graphics.Gdi.HGDIOBJ)roundRectRegion);
            }
        }

        return MorphicResult.OkResult();
    }

    //



}
