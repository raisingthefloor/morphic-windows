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

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Morphic.Core;
using Morphic.MorphicBar.BarControls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Morphic.MorphicBar;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MorphicBarWindow : Morphic.Controls.Windowing.TransparentBaseWindow, IDisposable
{
    private bool disposedValue;

    private IntPtr _hIconRawHandle = IntPtr.Zero;
    DummyWindow _dummyParentWindow;

    private Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue;

    // animation timer for moving the window
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _moveAnimationTimer;

    // logical (96 DPI) window size -- scaled by the current monitor's DPI
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

    // Tracks which monitor the bar belongs to. Kept in sync at the entry of AnimateMoveTo and
    // re-verified on demand via GetVerifiedCurrentMonitorHandle. Holding our own handle (instead
    // of re-querying the window's current position each time) means a DPI/rasterization-scale
    // change does not silently relocate us to a different display.
    private Windows.Win32.Graphics.Gdi.HMONITOR _currentMonitorHandle;

    // Maximum number of bar items the MorphicBar can hold. If a caller supplies more, excess items
    // are silently dropped during InitializeBarItems.
    public const int MaxBarItemCount = 128;

    // Master registry of all bar item controls created from the last InitializeBarItems call. Items
    // here are NOT necessarily currently present in BarItemsPanel.Children -- we trim (move)
    // controls between "displayed" (those that fit within the current screen's working area, in a single 
	// bar) and controls that need to be hidden (or overflowed onto an overflow panel)
    private readonly System.Collections.Generic.List<Microsoft.UI.Xaml.FrameworkElement> _allBarItemControls = new();

    public MorphicBarWindow()
    {
        InitializeComponent();

        _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        var hwnd = (Windows.Win32.Foundation.HWND)WinRT.Interop.WindowNative.GetWindowHandle(this);

        // record the initial monitor (wherever the window-manager placed us at creation time);
        // this is a placeholder that gets overwritten by the first AnimateMoveTo call from App.xaml.cs
        _currentMonitorHandle = Windows.Win32.PInvoke.MonitorFromWindow(
            hwnd,
            Windows.Win32.Graphics.Gdi.MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);

        // create a dummy "parent window" for the layout preview window (so that this window doesn't show up in the taskbar)
        _dummyParentWindow = new DummyWindow();
        _ = _dummyParentWindow.SetAsParentHwnd(hwnd);

        // create a layout preview window; we'll need this whenever the MorphicBar is moved; this is created up front, as it can take a little time to create the window
        _layoutPreviewWindow = new();

        this.InitializeBorderlessWindowProperties(hwnd);

        // if the user clicks on the window, let them drag it (i.e. release the pointer capture and forward the left-click as if it's a "caption bar" left-click instead)
        this.InitializePointerPressAndDrag(this.Content);

        this.Activated += MorphicBarWindow_Activated;
        (this.Content as Grid)!.Loaded += RootGrid_Loaded;
    }

    /// <summary>
    /// Replaces the bar's items with controls built from the supplied data list. The bar window
    /// owns control creation and orientation propagation; the caller (typically App) only supplies
    /// data classes, keeping bar contents authoring outside the window class.
    /// </summary>
    /// <param name="items">Heterogeneous list of bar item data (BarButtonData, BarMultiButtonData, ...).</param>
    public void InitializeBarItems(IEnumerable<IBarItemData> items)
    {
        // tear down: clear the live bar AND the master cache; the previous controls (if any) are
        // dropped and will be GC'd once their event subscriptions release
        this.BarItemsPanel.Children.Clear();
        _allBarItemControls.Clear();

        // materialize the input so we can length-check it; enforce MaxBarItemCount by silently
        // truncating excess.
        var itemsList = items.ToList();
        if (itemsList.Count > MaxBarItemCount)
        {
            itemsList = itemsList.GetRange(0, MaxBarItemCount);
        }

        // build all the controls up front; each one's Orientation is synced with the bar's current
        // orientation (and lays out correctly for that orientation when it loads)
        foreach (var data in itemsList)
        {
            IBarItemControl control;
            switch (data)
            {
                case BarButtonData buttonData:
                    control = new BarButtonControl { Data = buttonData };
                    break;
                case BarMultiButtonData multiButtonData:
                    control = new BarMultiButtonControl { Data = multiButtonData };
                    break;
                default:
                    throw new MorphicUnhandledCaseException(data);
            }
            control.Orientation = _orientation;
            _allBarItemControls.Add((FrameworkElement)control);
        }

        foreach (var control in _allBarItemControls)
        {
            // add to BarItemsPanel so the control loads and we can measure it. The trim step (above)
            // moves anything that doesn't fit out of BarItemsPanel into the cache.
            this.BarItemsPanel.Children.Add(control);
        }
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
    internal void AnimateMoveTo(Windows.Win32.Graphics.Gdi.HMONITOR hMonitor, Microsoft.UI.Xaml.Controls.Orientation targetOrientation, DockingLocation targetDockingLocation, TimeSpan duration)
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

        // record the destination monitor; this is the single normal path that legitimately changes
        // which monitor we are on (both intentional moves and drag-release end up here)
        _currentMonitorHandle = hMonitor;

        // start the new animation
        _moveAnimationTimer = AnimationUtils.AnimateMoveTo(_dispatcherQueue, this.AppWindow, targetPosition, targetSize, duration);
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

                // reposition elements to match the new MorphicBar orientation
                this.UpdateMorphicMenuButtonLayout(value);
                this.UpdateBarItemsPanelLayout(value);

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

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        this.AppWindow.Hide();
    }
	
	/* Morphic logo menu handling */

    // handle both click (left-click) and right tapping (right-click) of the Morphic logo button
    private void MorphicMenuButton_Click(object sender, RoutedEventArgs e)
    {
        this.ShowMorphicMenu();
    }

    private void MorphicMenuButton_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
    {
        e.Handled = true;
        this.ShowMorphicMenu();
    }

    private void ShowMorphicMenu()
    {
        var hwnd = new Windows.Win32.Foundation.HWND(WinRT.Interop.WindowNative.GetWindowHandle(this));
        var buttonPosition = this.MorphicMenuButton.TransformToVisual(this.Content).TransformPoint(new Windows.Foundation.Point(0, 0));
        var rasterizationScale = this.Content.XamlRoot.RasterizationScale;
        var isRtl = this.MorphicMenuButton.FlowDirection == FlowDirection.RightToLeft;

        // determine which half of the monitor the bar is on, so we can open the menu _away_ from the bar
        var windowPos = this.AppWindow.Position;
        var windowSize = this.AppWindow.Size;
        var windowCenterX = windowPos.X + (windowSize.Width / 2);
        var windowCenterY = windowPos.Y + (windowSize.Height / 2);
        //
        var hMonitor = Windows.Win32.PInvoke.MonitorFromWindow(hwnd, Windows.Win32.Graphics.Gdi.MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        var monitorInfo = new Windows.Win32.Graphics.Gdi.MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Windows.Win32.Graphics.Gdi.MONITORINFO>() };
        Windows.Win32.PInvoke.GetMonitorInfo(hMonitor, ref monitorInfo);
        var monitorCenterX = monitorInfo.rcWork.left + (monitorInfo.rcWork.Width / 2);
        var monitorCenterY = monitorInfo.rcWork.top + (monitorInfo.rcWork.Height / 2);

        // anchor the menu to the corner of the button that opens the menu away from the bar
        double anchorX;
        double anchorY;
        switch (this._orientation)
        {
            case Orientation.Horizontal:
                {
                    // open above if bar is in the bottom half, below if in the top half
                    bool openAbove = windowCenterY > monitorCenterY;
                    anchorX = isRtl
                        ? buttonPosition.X + this.MorphicMenuButton.ActualWidth
                        : buttonPosition.X;
                    anchorY = openAbove
                        ? buttonPosition.Y
                        : buttonPosition.Y + this.MorphicMenuButton.ActualHeight;
                }
                break;
            case Orientation.Vertical:
                {
                    // open to the left if bar is on the right half, to the right if on the left half
                    bool openLeft = windowCenterX > monitorCenterX;
                    anchorX = openLeft
                        ? buttonPosition.X
                        : buttonPosition.X + this.MorphicMenuButton.ActualWidth;
                    anchorY = buttonPosition.Y;
                }
                break;
            default:
                anchorX = buttonPosition.X;
                anchorY = buttonPosition.Y;
                break;
        }

        // convert from DIPs to physical pixels, then to screen coordinates
        var clientPoint = new System.Drawing.Point(
            (int)(anchorX * rasterizationScale),
            (int)(anchorY * rasterizationScale));
        _ = Windows.Win32.PInvoke.ClientToScreen(hwnd, ref clientPoint);

        // NOTE: always show the menu via the transparent window to avoid XamlRoot conflicts
        // (a MenuFlyout can only be associated with one XamlRoot at a time)
        App.MainMenu.Show(App.MenuOwnerWindow, this.Visible, clientPoint.X, clientPoint.Y);
    }

    /* layout methods */

    // Single-source margin values for the bar items panel per orientation. Consumed by
    // UpdateBarItemsPanelLayout (to apply the live Margin) and by MeasureDesiredBarLogicalSize
    // (to compose the bar's desired size for an arbitrary orientation without mutating the live
    // panel). If we ever expose these to XAML too, route XAML through the same source.
    //
    // Vertical mode's 25px top reserves space for the CloseButton, which overlaps the items
    // panel area at the top-right corner.
    private static Thickness GetBarItemsPanelMargin(Orientation orientation)
    {
        return orientation switch
        {
            Orientation.Horizontal => new Thickness(10, 5, 5, 5),
            Orientation.Vertical => new Thickness(5, 25, 5, 5),
            _ => throw new Morphic.Core.MorphicUnhandledCaseException(orientation),
        };
    }

    // Single-source margin values for the Morphic logo (menu) button per orientation. Same
    // consumer story as GetBarItemsPanelMargin above.
    private static Thickness GetMorphicMenuButtonMargin(Orientation orientation)
    {
        return orientation switch
        {
            Orientation.Horizontal => new Thickness(0, 0, 5, 0),
            Orientation.Vertical => new Thickness(0, 0, 0, 10),
            _ => throw new Morphic.Core.MorphicUnhandledCaseException(orientation),
        };
    }
	
	//

    // Flows the bar items vertically or horizontally to follow the bar's orientation, and propagates
    // the orientation down to each IBarItemControl child so the items can adapt their own layout.
    private void UpdateBarItemsPanelLayout(Orientation orientation)
    {
        switch (orientation)
        {
            case Orientation.Horizontal:
                this.BarItemsPanel.Orientation = Orientation.Horizontal;
                // column 0 only (leave column 1 for the logo button, column 2 for the close button)
                Grid.SetColumn(this.BarItemsPanel, 0);
                Grid.SetColumnSpan(this.BarItemsPanel, 1);
                Grid.SetRow(this.BarItemsPanel, 0);
                Grid.SetRowSpan(this.BarItemsPanel, 1);
                break;
            case Orientation.Vertical:
                this.BarItemsPanel.Orientation = Orientation.Vertical;
                // span all three columns on row 0; logo button sits below in row 1
                Grid.SetColumn(this.BarItemsPanel, 0);
                Grid.SetColumnSpan(this.BarItemsPanel, 3);
                Grid.SetRow(this.BarItemsPanel, 0);
                Grid.SetRowSpan(this.BarItemsPanel, 1);
                break;
            default:
                throw new Morphic.Core.MorphicUnhandledCaseException(orientation);
        }
        this.BarItemsPanel.Margin = GetBarItemsPanelMargin(orientation);

        foreach (var child in this.BarItemsPanel.Children)
        {
            if (child is IBarItemControl barItem)
            {
                barItem.Orientation = orientation;
            }
        }
    }

    // NOTE: this was attempted using pure XAML and an 'orientation' trigger, but ultimately this layout worked better when set manually from code
    private void UpdateMorphicMenuButtonLayout(Orientation orientation)
    {
        switch (orientation)
        {
            case Orientation.Horizontal:
                // Morphic logo (menu) button in column 1, to the left of the close button (right if rtl)
                Grid.SetColumn(this.MorphicMenuButton, 1);
                Grid.SetColumnSpan(this.MorphicMenuButton, 1);
                Grid.SetRow(this.MorphicMenuButton, 0);
                this.LogoColumn.Width = GridLength.Auto;
                this.LogoRow.Height = new GridLength(0);
                this.MorphicMenuButton.HorizontalAlignment = HorizontalAlignment.Center;
                this.MorphicMenuButton.VerticalAlignment = VerticalAlignment.Center;
                break;
            case Orientation.Vertical:
                // Morphic logo (menu) button in row 1 at the bottom, spanning all columns
                Grid.SetColumn(this.MorphicMenuButton, 0);
                Grid.SetColumnSpan(this.MorphicMenuButton, 3);
                Grid.SetRow(this.MorphicMenuButton, 1);
                this.LogoColumn.Width = new GridLength(0);
                this.LogoRow.Height = GridLength.Auto;
                this.MorphicMenuButton.HorizontalAlignment = HorizontalAlignment.Center;
                this.MorphicMenuButton.VerticalAlignment = VerticalAlignment.Center;
                break;
        }
        this.MorphicMenuButton.Margin = GetMorphicMenuButtonMargin(orientation);
    }

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
    }

    // Measures the bar's desired size in logical pixels for the given orientation, capped to the
    // target monitor's working area (minus the keepaway padding LayoutUtils already applies for
    // docking). Returned as (length, thickness) following the same convention as _logicalLength /
    // _logicalThickness elsewhere in this file: in horizontal mode length is the width and
    // thickness is the height; in vertical mode length is the height and thickness is the width.
    //
    // The orientation argument may differ from this.Orientation (e.g. during drag-preview), in
    // which case the bar items are measured via IBarItemControl.MeasureForOrientation, which
    // composes for the requested orientation without mutating the live items.
    internal (uint logicalLength, uint logicalThickness) MeasureDesiredBarLogicalSize(
        Windows.Win32.Graphics.Gdi.HMONITOR hMonitor,
        Microsoft.UI.Xaml.Controls.Orientation orientation)
    {
        // get the monitor's working area and rasterization scale to compute the logical-pixel cap
        var monitorInfo = new Windows.Win32.Graphics.Gdi.MONITORINFO();
        monitorInfo.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Windows.Win32.Graphics.Gdi.MONITORINFO>();
        var getMonitorInfoResult = Windows.Win32.PInvoke.GetMonitorInfo(hMonitor, ref monitorInfo);
        double rasterizationScale = 1.0;
        double logicalAvailWidth = double.PositiveInfinity;
        double logicalAvailHeight = double.PositiveInfinity;
        if (getMonitorInfoResult != 0)
        {
            var getRasterizationScaleResult = LayoutUtils.GetRasterizationScaleForMonitor(hMonitor);
            if (getRasterizationScaleResult.IsSuccess) { rasterizationScale = getRasterizationScaleResult.Value; }

            var workingArea = monitorInfo.rcWork;
            int keepaway = LayoutUtils.WINDOW_CORNER_DOCKING_DISTANCE_FROM_SCREEN_EDGE_IN_DEVICE_UNITS;
            logicalAvailWidth = System.Math.Max(0, (workingArea.Width / rasterizationScale) - (2 * keepaway));
            logicalAvailHeight = System.Math.Max(0, (workingArea.Height / rasterizationScale) - (2 * keepaway));
        }
        else
        {
            Debug.Assert(false, "Could not get current monitor (to measure working area); fell back to 'infinite' screen size and 100% rasterization scale");
        }

        // available size passed down to each item's measurement; conservatively use the full
        // working area minus keepaway (children will not be larger than this even with chrome)
        var measureAvailableSize = new Windows.Foundation.Size(logicalAvailWidth, logicalAvailHeight);

        // compose the items panel desired size from per-item measurements (the panel itself is a
        // StackPanel so we sum along the layout axis and take max on the cross axis, plus the
        // StackPanel's Spacing between adjacent items)
        double itemsPanelLength = 0;
        double itemsPanelThickness = 0;
        int itemCount = 0;
        foreach (var child in this.BarItemsPanel.Children)
        {
            if (child is IBarItemControl item)
            {
                var desired = item.MeasureForOrientation(measureAvailableSize, orientation);
                if (orientation == Orientation.Horizontal)
                {
                    itemsPanelLength += desired.Width;
                    if (desired.Height > itemsPanelThickness) { itemsPanelThickness = desired.Height; }
                }
                else
                {
                    itemsPanelLength += desired.Height;
                    if (desired.Width > itemsPanelThickness) { itemsPanelThickness = desired.Width; }
                }
                itemCount++;
            }
        }
        if (itemCount > 1)
        {
            itemsPanelLength += (itemCount - 1) * this.BarItemsPanel.Spacing;
        }

        // add the items panel's own Margin for the requested orientation
        var itemsPanelMargin = GetBarItemsPanelMargin(orientation);
        if (orientation == Orientation.Horizontal)
        {
            itemsPanelLength += itemsPanelMargin.Left + itemsPanelMargin.Right;
            itemsPanelThickness += itemsPanelMargin.Top + itemsPanelMargin.Bottom;
        }
        else
        {
            itemsPanelLength += itemsPanelMargin.Top + itemsPanelMargin.Bottom;
            itemsPanelThickness += itemsPanelMargin.Left + itemsPanelMargin.Right;
        }

        // measure the Morphic logo button and back out the live Margin so we can apply the margin
        // for the REQUESTED orientation (which may differ from the live one)
        this.MorphicMenuButton.Measure(measureAvailableSize);
        var liveLogoMargin = this.MorphicMenuButton.Margin;
        double logoNaturalWidth = System.Math.Max(0, this.MorphicMenuButton.DesiredSize.Width - (liveLogoMargin.Left + liveLogoMargin.Right));
        double logoNaturalHeight = System.Math.Max(0, this.MorphicMenuButton.DesiredSize.Height - (liveLogoMargin.Top + liveLogoMargin.Bottom));
        var requestedLogoMargin = GetMorphicMenuButtonMargin(orientation);
        double logoWidthWithMargin = logoNaturalWidth + requestedLogoMargin.Left + requestedLogoMargin.Right;
        double logoHeightWithMargin = logoNaturalHeight + requestedLogoMargin.Top + requestedLogoMargin.Bottom;

        // measure the close button (it has explicit Width/Height in XAML and no Margin; its
        // DesiredSize reflects those directly and doesn't vary with orientation)
        this.CloseButton.Measure(measureAvailableSize);
        double closeWidth = this.CloseButton.DesiredSize.Width;
        double closeHeight = this.CloseButton.DesiredSize.Height;

        // compose the final bar size based on the requested orientation
        double barLength;
        double barThickness;
        if (orientation == Orientation.Horizontal)
        {
            // layout: [items col 0] [logo col 1] [close col 2 fixed-width, top-aligned]
            // width  = items + logo (with their margins) + close width
            // height = max of items / logo / close (close at top, doesn't push if smaller)
            barLength = itemsPanelLength + logoWidthWithMargin + closeWidth;
            barThickness = System.Math.Max(itemsPanelThickness, System.Math.Max(logoHeightWithMargin, closeHeight));
        }
        else // orientation == Orientation.Vertical
        {
            // layout: [items spans cols 0..2 row 0] [logo spans cols 0..2 row 1]
            //         [close col 2 row 0, top-right; overlaps items area via the 25px top margin]
            // height = items + logo (with their margins); close doesn't add since it sits inside
            //   the items panel area thanks to that top margin reserved in GetBarItemsPanelMargin
            // width  = max of items / logo / close
            barLength = itemsPanelLength + logoHeightWithMargin;
            barThickness = System.Math.Max(itemsPanelThickness, System.Math.Max(logoWidthWithMargin, closeWidth));
        }

        // cap to working area logical pixels (per-axis, swapped per orientation)
        double maxLength = (orientation == Orientation.Horizontal) ? logicalAvailWidth : logicalAvailHeight;
        double maxThickness = (orientation == Orientation.Horizontal) ? logicalAvailHeight : logicalAvailWidth;
        if (barLength > maxLength) { barLength = maxLength; }
        if (barThickness > maxThickness) { barThickness = maxThickness; }

        return ((uint)System.Math.Ceiling(barLength), (uint)System.Math.Ceiling(barThickness));
    }

    // Returns the cached current-monitor handle if it still points at a live monitor (probed via
    // GetMonitorInfo, which returns zero when the monitor is no longer attached). If the cache is
    // stale (e.g. the display was disconnected), falls back to whatever monitor the window is
    // currently on, refreshes the cache, and returns the new handle.
    private Windows.Win32.Graphics.Gdi.HMONITOR GetVerifiedCurrentMonitorHandle()
    {
        var monitorInfo = new Windows.Win32.Graphics.Gdi.MONITORINFO();
        monitorInfo.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Windows.Win32.Graphics.Gdi.MONITORINFO>();
        if (Windows.Win32.PInvoke.GetMonitorInfo(_currentMonitorHandle, ref monitorInfo) != 0)
        {
            return _currentMonitorHandle;
        }

        var hwnd = (Windows.Win32.Foundation.HWND)WinRT.Interop.WindowNative.GetWindowHandle(this);
        _currentMonitorHandle = Windows.Win32.PInvoke.MonitorFromWindow(
            hwnd,
            Windows.Win32.Graphics.Gdi.MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        return _currentMonitorHandle;
    }

    private void InitializeBorderlessWindowProperties(Windows.Win32.Foundation.HWND hwnd)
    {
        // remove window chrome (minimize/maximize/close buttons); set the window to be 'always on top'
        var presenter = this.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
        if (presenter is not null)
        {
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
            presenter.IsResizable = false;
            presenter.IsAlwaysOnTop = true;
        }

        // update window style (i.e. remove the dialog frame); with our styling, this is required to prevent the window from growing beyond our target size
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

        // turn off DWM corner rounding
        int cornerPreference = (int)Windows.Win32.Graphics.Dwm.DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND;
        Span<byte> cornerPreferenceAsSpan = System.Runtime.InteropServices.MemoryMarshal.AsBytes(new Span<int>(ref cornerPreference));
        var setAttributeResult = Windows.Win32.PInvoke.DwmSetWindowAttribute(hwnd, Windows.Win32.Graphics.Dwm.DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, cornerPreferenceAsSpan);
        System.Diagnostics.Debug.Assert(setAttributeResult == Windows.Win32.Foundation.HRESULT.S_OK);
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
            //
            if (_layoutPreviewWindowOrientation is not null)
            {
                if (_orientation != _layoutPreviewWindowOrientation)
                {
                    this.Rotate90DegreesAroundPoint(currentPointerPosition);
                }
                this.Orientation = _layoutPreviewWindowOrientation!.Value;
            }
            if (_layoutPreviewDockingLocation is not null)
            {
                _dockingLocation = _layoutPreviewDockingLocation!.Value;
            }
            this.AnimateMoveTo(hMonitor, _orientation, _dockingLocation, new TimeSpan(0, 0, 1));

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
        monitorInfo.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Windows.Win32.Graphics.Gdi.MONITORINFO>();
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
}
