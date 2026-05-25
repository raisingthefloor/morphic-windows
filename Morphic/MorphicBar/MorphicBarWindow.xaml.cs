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
    private uint _logicalLength = MorphicBarWindowMetrics.DefaultLogicalLength;
    private uint _logicalThickness = MorphicBarWindowMetrics.DefaultLogicalThickness;

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

    // Master registry of all bar item controls created from the last InitializeBarItems call. Items
    // here are NOT necessarily currently present in BarItemsPanel.Children -- we trim (move)
    // controls between "displayed" (those that fit within the current screen's working area, in a single 
	// bar) and controls that need to be hidden (or overflowed onto an overflow panel)
    private readonly System.Collections.Generic.List<Microsoft.UI.Xaml.FrameworkElement> _allBarItemControls = new();


    //
    // Item lengths are NOT cached: they depend on the bar's effective thickness (since a narrower
    // bar can cause text wrapping that makes items taller). MeasureBarForOrientation performs a
    // fresh two-pass measurement each time it's called.

    public MorphicBarWindow()
    {
        InitializeComponent();

        // apply the initial orientation-specific layout via the same helpers used on orientation
        // change; the Orientation setter only fires when the value changes, so without this call
        // the bar items panel and logo button would initially render at default Margin
        this.UpdateBarItemsPanelLayout(_orientation);
        this.UpdateMorphicMenuButtonLayout(_orientation);

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

        // materialize the input so we can length-check it; enforce MorphicBarWindowMetrics.MaxBarItemCount by silently
        // truncating excess.
        var itemsList = items.ToList();
        if (itemsList.Count > MorphicBarWindowMetrics.MaxBarItemCount)
        {
            itemsList = itemsList.GetRange(0, MorphicBarWindowMetrics.MaxBarItemCount);
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

        // schedule the bar-level measure-and-resize for the moment every new item has fired Loaded.
        // Loaded fires once an element is in a live visual tree and its initial measure pass is
        // complete, which is the strongest guarantee WinUI gives us that DesiredSize is meaningful.
        int pendingLoadedCount = _allBarItemControls.Count;
        if (pendingLoadedCount == 0)
        {
            // no items to wait for: still re-fit the bar (collapses chrome around an empty panel)
            this.MeasureAndResize();
            return;
        }
        foreach (var control in _allBarItemControls)
        {
            RoutedEventHandler? handler = null;
            handler = (sender, e) =>
            {
                // unwire this handler (as it should only be called once)
                ((FrameworkElement)sender).Loaded -= handler;

                pendingLoadedCount--;
                if (pendingLoadedCount == 0)
                {
                    // every control is now in the live visual tree; MeasureAndResize runs the
                    // full pipeline (two-pass measure -> trim -> resize) via AnimateMoveTo
                    this.MeasureAndResize();
                }
            };
            control.Loaded += handler;

            // add to BarItemsPanel so the control loads and we can measure it. The trim step (above)
            // moves anything that doesn't fit out of BarItemsPanel into the cache.
            this.BarItemsPanel.Children.Add(control);
        }
    }

    // Returns true once every control in _allBarItemControls has fired its Loaded event (and is
    // therefore safely measurable). Until then, measurements may return zero or template-pending
    // values, so any code that makes sizing/trimming decisions should bail and let the eventual
    // post-Loaded MeasureAndResize handle it.
    private bool AllBarItemsLoaded()
    {
        foreach (var control in _allBarItemControls)
        {
            if (!control.IsLoaded) { return false; }
        }
        return true;
    }

    //
	
    // Sets each item's Visibility so the first `fittingCount` entries of _allBarItemControls are
    // Visible and the rest are Collapsed. Items remain parented to BarItemsPanel.Children at all
    // times so they stay loaded and measurable (otherwise BarButtonControl's MeasureForOrientation
    // would return zero for unparented items, and Pass 2 trim math would silently let everything
    // through).
    //
    // BarItemsPanel uses StackPanel.Spacing, which only adds spacing between consecutive Visible
    // children, so Collapsed items contribute neither layout space nor spacing.
    private void SyncDisplayedItemPrefix(int fittingCount)
    {
        if (fittingCount < 0) { fittingCount = 0; }
        if (fittingCount > _allBarItemControls.Count) { fittingCount = _allBarItemControls.Count; }

        for (int i = 0; i < _allBarItemControls.Count; i++)
        {
            _allBarItemControls[i].Visibility = (i < fittingCount) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void MorphicBarWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        // handle any post-load code here
    }

    private void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        // record the current rasterization scale and subscribe to changes (e.g. monitor switch)
        _lastRasterizationScale = this.Content.XamlRoot.RasterizationScale;
        this.Content.XamlRoot.Changed += (s, e) =>
        {
            this.RasterizationScaleChanged();
        };

        // do a fresh measure-and-resize now that the visual tree is fully loaded; any earlier
        // measurement during construction or InitializeBarItems may have been against partially-
        // realized children, so this is the authoritative initial sizing pass
        this.MeasureAndResize();
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
    /// Brings the bar to the target (monitor, orientation, docking location) state. Measures the
    /// bar for the target orientation on the target monitor, computes the docking rect, and animates
    /// the AppWindow to it. Size and position animate together via AnimationUtils.AnimateMoveTo.
    /// A duration of TimeSpan.Zero snaps instantly (no animation). If called while a previous
    /// animation is in progress, that animation is cancelled and a new one starts from the window's
    /// current state.
    ///
    /// This is the single entry point for any change to monitor / orientation / docking location.
    /// MeasureAndResize is a convenience wrapper for the common "stay where we are, just re-fit"
    /// case.
    /// </summary>
    internal void AnimateMoveTo(Windows.Win32.Graphics.Gdi.HMONITOR hMonitor, Microsoft.UI.Xaml.Controls.Orientation targetOrientation, DockingLocation targetDockingLocation, TimeSpan duration)
    {
        // stop any existing timer
        _moveAnimationTimer?.Stop();
        _moveAnimationTimer = null;

        // record the destination monitor; this is the single normal path that legitimately changes
        // which monitor we are on (both intentional moves and drag-release end up here)
        _currentMonitorHandle = hMonitor;

        // apply the target orientation via the property setter so its layout helpers run
        // (UpdateMorphicMenuButtonLayout / UpdateBarItemsPanelLayout); the setter is a no-op
        // when the orientation already matches
        this.Orientation = targetOrientation;

        // record the destination docking location
        _dockingLocation = targetDockingLocation;

        // single combined two-pass measurement: returns the bar's logical size AND the count of
        // items that fit. Pass 1 of MeasureBarForOrientation determines bar thickness; Pass 2
        // re-measures each item with that thickness as the cross-axis constraint, capturing any
        // text wrapping in headers/buttons that the unconstrained Pass 1 would have missed.
        var (logicalLength, logicalThickness, fittingCount) = this.MeasureBarForOrientation(hMonitor, targetOrientation);
        _logicalLength = logicalLength;
        _logicalThickness = logicalThickness;

        // sync BarItemsPanel.Children to the leading prefix of _allBarItemControls that fits.
        // Trimmed items remain in the _allBarItemControls cache (unparented for now; will move
        // into the overflow window once that exists). Items that fit but were previously trimmed
        // get inserted back into BarItemsPanel.
        this.SyncDisplayedItemPrefix(fittingCount);

        // compute the target rect (already in physical pixels; GetRectForDockingLocation multiplies
        // by the monitor's rasterization scale internally)
        var getRectForDockingLocationResult = LayoutUtils.GetRectForDockingLocation(targetDockingLocation, targetOrientation, logicalLength, logicalThickness, hMonitor);
        if (getRectForDockingLocationResult.IsError)
        {
            Debug.Assert(false);
            return;
        }
        var targetRect = getRectForDockingLocationResult.Value!;
        var targetPosition = new Windows.Graphics.PointInt32(targetRect.X, targetRect.Y);
        var targetSize = new Windows.Graphics.SizeInt32(targetRect.Width, targetRect.Height);

        // start the new animation (size + position interpolate together; TimeSpan.Zero snaps)
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

                // reposition the inner chrome to match the new orientation; the bar window's
                // own size and position are NOT touched here. Callers who want the bar to
                // re-fit and re-dock for the new orientation should go through AnimateMoveTo
                // (or MeasureAndResize for the in-place case); AnimateMoveTo itself uses this
                // setter, so internal orientation changes get the full flow automatically.
                this.UpdateMorphicMenuButtonLayout(value);
                this.UpdateBarItemsPanelLayout(value);

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

        // dispatch the re-fit asynchronously so it runs after WinUI finishes its own DPI handling.
        // MeasureAndResize uses GetVerifiedCurrentMonitorHandle internally, so a DPI change is
        // interpreted as "same monitor, new scale" rather than "follow the cursor to a new monitor"
        // (unless the cached handle has actually gone stale).
        this.DispatcherQueue.TryEnqueue(() =>
        {
            this.MeasureAndResize();
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
    // UpdateBarItemsPanelLayout (to apply the live Margin) and by MeasureBarForOrientation
    // (to compose the bar's desired size for an arbitrary orientation without mutating the live
    // panel). If we ever expose these to XAML too, route XAML through the same source.
    //
    // Vertical mode's 25px top reserves space for the CloseButton, which overlaps the items
    // panel area at the top-right corner.
    private static Thickness GetBarItemsPanelMargin(Orientation orientation)
    {
        return orientation switch
        {
            Orientation.Horizontal => MorphicBarWindowMetrics.ItemsPanelMarginHorizontal,
            Orientation.Vertical => MorphicBarWindowMetrics.ItemsPanelMarginVertical,
            _ => throw new Morphic.Core.MorphicUnhandledCaseException(orientation),
        };
    }

    // Single-source margin values for the Morphic logo (menu) button per orientation. Same
    // consumer story as GetBarItemsPanelMargin above.
    private static Thickness GetMorphicMenuButtonMargin(Orientation orientation)
    {
        return orientation switch
        {
            Orientation.Horizontal => MorphicBarWindowMetrics.MenuButtonMarginHorizontal,
            Orientation.Vertical => MorphicBarWindowMetrics.MenuButtonMarginVertical,
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

    // Measures the bar (via MeasureBarForOrientation) for the supplied monitor at the bar's
    // current orientation, then applies the result via Resize. Use this wherever the bar needs to
    // re-fit its contents: after items change, after orientation change, after a DPI/rasterization
    // change on the same monitor, or before/after a monitor switch (the caller is responsible for
    // having already set _orientation to the desired value).
	//
    // Convenience wrapper around AnimateMoveTo for the common case of "stay where we are, just
    // re-fit to whatever size the bar's contents now want." Uses the cached current monitor,
    // current orientation, and current docking location, so the bar visually stays put except
    // for the size change and any docking-edge realignment caused by the size change.
    //
    // Duration defaults to TimeSpan.Zero (snap, no animation). Pass a non-zero duration for a
    // smooth transition (e.g. ~150ms for an orientation flip).
    internal void MeasureAndResize(TimeSpan duration = default)
    {
        this.AnimateMoveTo(this.GetVerifiedCurrentMonitorHandle(), _orientation, _dockingLocation, duration);
    }

    // Two-pass measurement of the bar's items, returning both the bar's logical size and the
    // count of items that fit on the target monitor at the target orientation.
    //
    // Why two passes: a naive single-pass measurement with availableSize = (infinity, infinity)
    // assumes items don't wrap. But once the bar's actual thickness is determined (from the
    // widest item), any item whose text exceeds that thickness wraps to multiple lines and
    // becomes TALLER than the single-pass measurement reported. Trimming based on the underestimate
    // lets too many items in, and the rendered bar overflows.
    //
    // Pass 1: measure every item with availableSize = (infinity, infinity) -> get the natural
    //         maximum item thickness. Combine with chrome to determine the bar's effective
    //         thickness, capped to the working area.
    // Pass 2: re-measure every item with availableSize constrained on the thickness axis (the
    //         bar's effective thickness minus margins). Items wrap as they would in the live bar.
    //         Sum the resulting per-item lengths and compute how many fit.
    //
    // The orientation argument may differ from this.Orientation (e.g. during drag-preview), in
    // which case items are measured via IBarItemControl.MeasureForOrientation, which composes
    // for the requested orientation without mutating the live items.
    //
    // If items aren't all Loaded yet (their templates may not have applied, so Measure would
    // return zero/incomplete values), returns the current cached size fields and a "no trim"
    // fitting count -- the eventual post-Loaded MeasureAndResize will run the real measurement.
    internal (uint logicalLength, uint logicalThickness, int fittingCount) MeasureBarForOrientation(
        Windows.Win32.Graphics.Gdi.HMONITOR hMonitor,
        Microsoft.UI.Xaml.Controls.Orientation orientation)
    {
        if (this.AllBarItemsLoaded() == false)
        {
            // measurements would be unreliable; preserve current size and tell caller everything fits
            return (_logicalLength, _logicalThickness, _allBarItemControls.Count);
        }

        // ----- monitor working area in logical pixels (the per-axis caps) -----
        var monitorInfo = new Windows.Win32.Graphics.Gdi.MONITORINFO();
        monitorInfo.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Windows.Win32.Graphics.Gdi.MONITORINFO>();
        var getMonitorInfoResult = Windows.Win32.PInvoke.GetMonitorInfo(hMonitor, ref monitorInfo);
        double rasterizationScale = 1.0;
        double logicalAvailableWidth = double.PositiveInfinity;
        double logicalAvailableHeight = double.PositiveInfinity;
        if (getMonitorInfoResult != 0)
        {
            var getRasterizationScaleResult = LayoutUtils.GetRasterizationScaleForMonitor(hMonitor);
            if (getRasterizationScaleResult.IsSuccess) { rasterizationScale = getRasterizationScaleResult.Value; }
            int keepaway = LayoutUtils.WINDOW_CORNER_DOCKING_DISTANCE_FROM_SCREEN_EDGE_IN_DEVICE_UNITS;
            logicalAvailableWidth = System.Math.Max(0, (monitorInfo.rcWork.Width / rasterizationScale) - (2 * keepaway));
            logicalAvailableHeight = System.Math.Max(0, (monitorInfo.rcWork.Height / rasterizationScale) - (2 * keepaway));
        }
        else
        {
            Debug.Assert(false, "Could not get current monitor (to measure working area); fell back to 'infinite' screen size and 100% rasterization scale");
        }
		
        double logicalAvailableLength = (orientation == Orientation.Horizontal) ? logicalAvailableWidth : logicalAvailableHeight;
        double logicalAvailableThickness = (orientation == Orientation.Horizontal) ? logicalAvailableHeight : logicalAvailableWidth;
        var fullAvailableSize = new Windows.Foundation.Size(logicalAvailableWidth, logicalAvailableHeight);

        // ----- chrome measurement (logo + close button); same approach as before -----
        this.MorphicMenuButton.Measure(fullAvailableSize);
        var liveLogoMargin = this.MorphicMenuButton.Margin;
        double logoNaturalWidth = System.Math.Max(0, this.MorphicMenuButton.DesiredSize.Width - (liveLogoMargin.Left + liveLogoMargin.Right));
        double logoNaturalHeight = System.Math.Max(0, this.MorphicMenuButton.DesiredSize.Height - (liveLogoMargin.Top + liveLogoMargin.Bottom));
        var requestedLogoMargin = GetMorphicMenuButtonMargin(orientation);
        double logoWidthWithMargin = logoNaturalWidth + requestedLogoMargin.Left + requestedLogoMargin.Right;
        double logoHeightWithMargin = logoNaturalHeight + requestedLogoMargin.Top + requestedLogoMargin.Bottom;

        this.CloseButton.Measure(fullAvailableSize);
        double closeWidth = this.CloseButton.DesiredSize.Width;
        double closeHeight = this.CloseButton.DesiredSize.Height;

        // ----- items panel margin (added once to bar's length and thickness) -----
        var itemsPanelMargin = GetBarItemsPanelMargin(orientation);
        double itemsPanelMarginAlongLength = (orientation == Orientation.Horizontal)
            ? itemsPanelMargin.Left + itemsPanelMargin.Right
            : itemsPanelMargin.Top + itemsPanelMargin.Bottom;
        double itemsPanelMarginAlongThickness = (orientation == Orientation.Horizontal)
            ? itemsPanelMargin.Top + itemsPanelMargin.Bottom
            : itemsPanelMargin.Left + itemsPanelMargin.Right;

        // ----- Pass 1: measure each item at full available size to find max natural thickness -----
        // NOTE: Visibility=Collapsed elements return DesiredSize=(0,0) from Measure (the framework
        // short-circuits MeasureCore for them). To get accurate measurements for currently-Collapsed
        // (trimmed-out) items, we temporarily flip them to Visible, measure, then restore. The
        // toggles happen synchronously within this method so no layout pass runs in between,
        // and the user-visible Visibility state ends up identical to what it was on entry.
        double maxItemNaturalThickness = 0;
        foreach (var control in _allBarItemControls)
        {
            if (control is IBarItemControl item)
            {
                var savedVisibility = control.Visibility;
                if (savedVisibility != Visibility.Visible) { control.Visibility = Visibility.Visible; }

                var natural = item.MeasureForOrientation(fullAvailableSize, orientation);
                double thicknessContribution = (orientation == Orientation.Horizontal) ? natural.Height : natural.Width;
                if (thicknessContribution > maxItemNaturalThickness) { maxItemNaturalThickness = thicknessContribution; }

                if (control.Visibility != savedVisibility) { control.Visibility = savedVisibility; }
            }
        }

        // ----- bar's outer Border eats inner space on each axis (BorderThickness=1 logical, so
        //       2 total per axis). Items, chrome, and margins all live INSIDE that border, so
        //       both Pass 2's thickness constraint and the trim's length cap have to subtract it.
        //       The returned outer dimensions (barLength, barThickness) add it back so AppWindow
        //       is sized to include the border. -----
        double barBorderThicknessPerSide = MorphicBarWindowMetrics.BarBorderThicknessPerSide;
        double barBorderThicknessBothSides = barBorderThicknessPerSide * 2;

        // ----- compose the bar's effective INNER thickness (capped to working area minus border) -----
        // thickness axis composition: max(items + items panel margin, logo with margin, close)
        double logoThickness = (orientation == Orientation.Horizontal) ? logoHeightWithMargin : logoWidthWithMargin;
        double closeThickness = (orientation == Orientation.Horizontal) ? closeHeight : closeWidth;
        double itemsPanelNaturalThickness = maxItemNaturalThickness + itemsPanelMarginAlongThickness;
        double innerBarThickness = System.Math.Max(itemsPanelNaturalThickness, System.Math.Max(logoThickness, closeThickness));
        double innerThicknessCap = System.Math.Max(0, logicalAvailableThickness - barBorderThicknessBothSides);
        if (innerBarThickness > innerThicknessCap) { innerBarThickness = innerThicknessCap; }

        // ----- Pass 2: re-measure each item with the bar's effective thickness as cross-axis cap -----
        // items get availableSize.thickness = bar's INNER thickness minus items-panel margin
        double itemsAvailableThickness = System.Math.Max(0, innerBarThickness - itemsPanelMarginAlongThickness);
        var constrainedAvailableSize = (orientation == Orientation.Horizontal)
            ? new Windows.Foundation.Size(double.PositiveInfinity, itemsAvailableThickness)
            : new Windows.Foundation.Size(itemsAvailableThickness, double.PositiveInfinity);

        // ----- chrome length (for the bar's length-axis composition + the trim cap) -----
        double chromeLength = (orientation == Orientation.Horizontal)
            ? logoWidthWithMargin + closeWidth   // horizontal: logo and close both add to length
            : logoHeightWithMargin;              // vertical: close overlaps items area via top margin
        // items get inner length = working-area-cap - border - chrome - margin
        double availableForItemsContent = System.Math.Max(0, logicalAvailableLength - barBorderThicknessBothSides - chromeLength - itemsPanelMarginAlongLength);

        // walk items in order, accumulating length and counting how many fit.
        // NOTE: same Visibility-toggle pattern as Pass 1: Collapsed elements return DesiredSize=(0,0)
        // from Measure, so trim would silently let everything through if we measured them collapsed.
        // Toggle synchronously so no layout pass runs with the intermediate state visible.
        double spacing = this.BarItemsPanel.Spacing;
        double cumulativeItemsLength = 0;
        int fittingCount = 0;
        for (int i = 0; i < _allBarItemControls.Count; i++)
        {
            var control = _allBarItemControls[i];
            if (control is not IBarItemControl item) { continue; }

            var savedVisibility = control.Visibility;
            if (savedVisibility != Visibility.Visible) { control.Visibility = Visibility.Visible; }

            var constrained = item.MeasureForOrientation(constrainedAvailableSize, orientation);

            if (control.Visibility != savedVisibility) { control.Visibility = savedVisibility; }

            // Math.Ceiling per item: WinUI's LayoutRounding rounds each item's painted position to
            // an integer pixel at the current rasterization scale. With fractional per-item widths
            // (e.g. an AutoSize multi-button group whose buttons measure to non-integer DesiredSize
            // values), the cumulative offset of items can drift up to ~0.5 logical px per item.
            // Across several items that drift accumulates enough to push the rightmost item's
            // painted right edge past the bar's reserved length, clipping the last item's right-side
            // rounded corner. Ceiling here absorbs that drift -- each item gets up to 1 logical px
            // of slack in the bar's overall budget, so the bar reserves a touch more than the raw
            // DesiredSize sum and the last item paints fully.
            double rawItemLength = (orientation == Orientation.Horizontal) ? constrained.Width : constrained.Height;
            double itemLength = System.Math.Ceiling(rawItemLength);
            double additional = itemLength + (i > 0 ? spacing : 0);
            if (cumulativeItemsLength + additional <= availableForItemsContent)
            {
                cumulativeItemsLength += additional;
                fittingCount = i + 1;
            }
            else
            {
                break;
            }
        }

        // bar's OUTER logical length (= AppWindow size): inner content + border
        double innerBarLength = cumulativeItemsLength + chromeLength + itemsPanelMarginAlongLength;
        double outerBarLength = innerBarLength + barBorderThicknessBothSides;
        double outerBarThickness = innerBarThickness + barBorderThicknessBothSides;

        return ((uint)System.Math.Ceiling(outerBarLength), (uint)System.Math.Ceiling(outerBarThickness), fittingCount);
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
            // pick the target orientation and docking location: prefer whatever the layout preview
            // had locked in during the drag; fall back to the current values if the user didn't drag
            // far enough to trigger a preview change
            var targetOrientation = _layoutPreviewWindowOrientation ?? _orientation;
            var targetDockingLocation = _layoutPreviewDockingLocation ?? _dockingLocation;

            // if the orientation is flipping, rotate the window dimensions around the cursor first
            // so the animation that follows starts from a sensible visual state (otherwise the bar
            // would visibly "swap axes" in mid-flight)
            if (_orientation != targetOrientation)
            {
                this.Rotate90DegreesAroundPoint(currentPointerPosition);
            }

            // AnimateMoveTo handles the orientation change (via the property setter), updates the
            // docking location, measures for the new state, and animates size + position together
            this.AnimateMoveTo(hMonitor, targetOrientation, targetDockingLocation, new TimeSpan(0, 0, 1));

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

        // measure the bar's desired size for the TARGET (monitor, orientation), not the bar's
        // current state -- the preview must show what the bar will look like after release:
        //   - if orientation differs, IBarItemControl.MeasureForOrientation gives the cross-orientation
        //     answer without mutating the live items
        //   - if the target monitor's working area is tighter than the current monitor's, the
        //     working-area cap inside MeasureBarForOrientation keeps the preview within bounds
        var (previewLogicalLength, previewLogicalThickness, _) = this.MeasureBarForOrientation(hMonitor, newPreviewOrientation);
        var getRectForDockingLocationResult = LayoutUtils.GetRectForDockingLocation(newPreviewDockingLocation, newPreviewOrientation, previewLogicalLength, previewLogicalThickness, hMonitor);
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
