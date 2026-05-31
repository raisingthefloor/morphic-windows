// Copyright 2026 Raising the Floor - US, Inc.
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

using Microsoft.UI.Windowing;
using System;
using System.Threading.Tasks;

namespace Morphic.MorphicBar;

// Owns the lifetime, event subscriptions, and operations for ONE MorphicBarWindow.
internal sealed class MorphicBarManager : IDisposable
{
    private readonly MorphicBarWindow _morphicBarWindow;
    private bool _disposed;

    private readonly Microsoft.UI.Dispatching.DispatcherQueue _uiDispatcherQueue;
    private EventHandler<Morphic.SettingsUtils.CachedDarkModeStateChangedEventArgs>? _barIconRefreshHandler;

    // Watches for a foreground window going full-screen on any monitor and, when that monitor is
    // the bar's own, drops the bar's always-on-top z-order so the full-screen content (e.g. a
    // full-screen video) can cover it. See OnFullScreenMonitorChanged.
    private FullScreenMonitorWatcher? _fullScreenWatcher;

    public MorphicBarManager(MorphicBarWindow morphicBarWindow)
    {
        _morphicBarWindow = morphicBarWindow;
        _uiDispatcherQueue = morphicBarWindow.DispatcherQueue;
        _morphicBarWindow.AppWindow.Changed += this.OnBarAppWindowChanged;
        _morphicBarWindow.RasterizationScaleChangedExternal += this.OnBarRasterizationScaleChanged;
        _morphicBarWindow.CurrentMonitorChanged += this.OnBarCurrentMonitorChanged;
        //
        _morphicBarWindow.DockingLocationChanged += this.OnBarWindowDockingLocationChanged;
        _morphicBarWindow.OrientationChanged += this.OnBarWindowOrientationChanged;

        // Seed the bar icon for the current system theme (HC variant or non-HC), then subscribe
        // so any future HC transition swaps the icon. CachedDarkModeState's StateChanged fires
        // from a worker thread; marshal the refresh onto the bar's DispatcherQueue (UI thread)
        // before calling SetIconFromFile.
        this.RefreshBarIcon();
        _barIconRefreshHandler = (_, _) =>
        {
            _uiDispatcherQueue.TryEnqueue(this.RefreshBarIcon);
        };
        Morphic.SettingsUtils.CachedDarkModeState.StateChanged += _barIconRefreshHandler;

        // Install the full-screen watcher on this (UI) thread: its WinEvent hooks deliver on the
        // installing thread and OnFullScreenMonitorChanged touches the bar window, so both must be
        // the UI thread. A start failure is non-fatal (the bar simply won't yield to full-screen
        // content), so log and continue rather than throw out of the constructor.
        _fullScreenWatcher = new FullScreenMonitorWatcher(_uiDispatcherQueue);
        _fullScreenWatcher.FullScreenMonitorChanged += this.OnFullScreenMonitorChanged;
        var startWatcherResult = _fullScreenWatcher.Start();
        if (startWatcherResult.IsError == true)
        {
            Morphic.RmTraceLog.Log("MorphicBarManager: FullScreenMonitorWatcher.Start() failed; bar will not yield to full-screen windows.");
        }
    }

    // Picks the correct contrast-variant icon for the current system theme and applies it to
    // the bar window. Driven by CachedDarkModeState (HC on/off + HC variant dark/light)
    private void RefreshBarIcon()
    {
        try
        {
            var relativePath = App.GetIconRelativePathForCurrentSystemTheme();
            var absolutePath = System.IO.Path.Combine(AppContext.BaseDirectory, relativePath);
            _ = _morphicBarWindow.SetIconFromFile(absolutePath, 256, 256);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // if MorphicBarWindows's native window is mid-teardown (HC change fired between
            // Dispose's unsubscribe + bar's Close); swallow rather than crash. Subsequent
            // state changes won't fire because we'll be disposed.
        }
    }

    public event EventHandler? BarVisibilityChanged;

    // Re-raised from the bar so App-level persistence (AppRegistrySettings) can observe re-docks
    // without depending on MorphicBarWindow directly.
    public event EventHandler<Morphic.MorphicBar.DockingLocation>? DockingLocationChanged;

    // Re-raised from the bar (companion to DockingLocationChanged) so App-level persistence can
    // observe orientation flips. Orientation + docking location are a persisted pair.
    public event EventHandler<Microsoft.UI.Xaml.Controls.Orientation>? OrientationChanged;

    public bool IsBarVisible => _morphicBarWindow.Visible;

    public Morphic.MorphicBar.DockingLocation CurrentDockingLocation => _morphicBarWindow.CurrentDockingLocation;

    public Microsoft.UI.Xaml.Controls.Orientation CurrentOrientation => _morphicBarWindow.Orientation;

    // The bar's flow direction, forwarded so App-level persistence can resolve logical docks to
    // physical ones (for position-equality comparisons) without referencing MorphicBarWindow directly.
    public bool IsRightToLeft => _morphicBarWindow.IsRightToLeft;

    // Shows the bar. Pass activateWindow: false for the startup case so we don't interrupt the
    // user's previous foreground app AND don't put the bar into a sticky Win32 "active" state from
    // which the first Alt+Tab to it would fail to fire WM_ACTIVATE (OS sees no state change).
    //
    // When activateWindow=true (the normal user-driven path -- tray click, "Show MorphicBar" menu
    // item, etc.), defuse the focus ring BEFORE showing: the bar's XAML FocusState is preserved
    // across Hide/Show, so without this a button that previously had Keyboard focus would re-
    // display its keyboard ring as if the user had Tab'd there. Downgrading Keyboard -> Programmatic
    // + suppressing the post-Show WM_ACTIVATE upgrade ensures the bar reappears with no stale ring.
    public void ShowBar(bool activateWindow = true)
    {
        if (activateWindow == true)
        {
            // Bundle: defuse any stale Keyboard ring left from a prior session AND suppress
            // the upgrade timer that would otherwise re-arm a ring from the post-Show
            // WM_ACTIVATE. The controller knows the right durations; the manager just says
            // "I'm about to Show, prepare focus."
            _morphicBarWindow.FocusController.PrepareForShow();
        }
        _morphicBarWindow.AppWindow.Show(activateWindow: activateWindow);
        if (activateWindow == true)
        {
            // Close the suppression scope opened by PrepareForShow. Deferred (enqueued) so the
            // post-Show WM_ACTIVATE focus update drains while still suppressed, then clears.
            _morphicBarWindow.FocusController.EndSuppressUpgradeAfterPendingActivations();
        }

        // Re-assert each button's compound visual state after the show transition. Without
        // this, an in-progress action (notably the Dark toggle) whose button has been
        // hidden + shown mid-flight reappears showing the toggled state instead of the
        // in-progress visual, until a hover event triggers the compound-state update. See
        // MorphicBarWindow.RefreshAllButtonCompoundStatesAfterShow for the full rationale.
        _morphicBarWindow.RefreshAllButtonCompoundStatesAfterShow();
    }

    public void HideBar() => _morphicBarWindow.AppWindow.Hide();

    public void ActivateBar() => _morphicBarWindow.Activate();

    // Animates the bar to the given orientation + docking location on its current monitor. Used by
    // App-level persistence (AppRegistrySettings) to apply a registry-driven placement change with
    // the same animated transition the user sees when re-docking or flipping orientation by drag.
    // The pair is applied in a single animated move.
    public void MoveToPlacement(Microsoft.UI.Xaml.Controls.Orientation orientation, Morphic.MorphicBar.DockingLocation dockingLocation) => _morphicBarWindow.MoveToCurrentMonitorPlacement(orientation, dockingLocation);

    public IntPtr GetBarWindowHandle() => WinRT.Interop.WindowNative.GetWindowHandle(_morphicBarWindow);

    // The bar's authoritative CURRENT (destination) monitor handle, as IntPtr. Unlike deriving the
    // monitor from the live window position, this is animation-independent: during a drag-release the
    // window can straddle the monitor boundary at the moment we read it, so display-dependent state
    // (Text Size +/- enablement) must key off the bar's intended monitor, not where the window is mid-move.
    public IntPtr GetBarCurrentMonitorHandle() => _morphicBarWindow.GetVerifiedCurrentMonitorHandle();

    /// <summary>
    /// Awaits the next RasterizationScaleChangedExternal on the bar, or until <paramref name="timeout"/>
    /// elapses, whichever comes first. Returns true if the event arrived, false on timeout.
    /// Used by handlers that issue a system change which will cause the bar to relayout (e.g.,
    /// a DPI scale change) and need to wait for the relayout before measuring or further acting.
    /// Subscribes BEFORE the timeout starts so an event arriving immediately is not missed.
    /// </summary>
    public async Task<bool> WaitForBarRasterizationScaleChangeAsync(TimeSpan timeout)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler handler = (_, _) => tcs.TrySetResult(true);
        _morphicBarWindow.RasterizationScaleChangedExternal += handler;
        try
        {
            var firstCompleted = await Task.WhenAny(tcs.Task, Task.Delay(timeout));
            return firstCompleted == tcs.Task;
        }
        finally
        {
            _morphicBarWindow.RasterizationScaleChangedExternal -= handler;
        }
    }

    /// <summary>
    /// Hides the bar (if visible) for the duration of <paramref name="action"/>, then restores the
    /// prior visibility state. Used by handlers that issue a system change which would cause the
    /// bar to flash or appear in the wrong place during the change (e.g., DPI / theme transitions
    /// that briefly relayout all top-level windows). Preserves the original Visible state across
    /// the action and restores even if the action throws. Also captures + restores the XAML
    /// focused element across the Hide/Show -- AppWindow.Hide drops focus state.
    /// </summary>
    public async Task RunWithBarHiddenAsync(Func<Task> action)
    {
        bool wasVisible = _morphicBarWindow.Visible;
        var focusSnapshot = this.CaptureBarFocus();
        // Open a suppression scope for the WHOLE flow: covers the click activation's deferred
        // update, the action duration, and the re-Show's WM_ACTIVATE. A depth scope (not a
        // timer) so it can't be "exceeded" by a slow action -- it stays open until the finally
        // closes it after the re-Show, regardless of how long the action ran.
        _morphicBarWindow.FocusController.BeginSuppressUpgrade();
        if (wasVisible) { _morphicBarWindow.AppWindow.Hide(); }
        try
        {
            await action();
        }
        finally
        {
            if (wasVisible) { _morphicBarWindow.AppWindow.Show(); }
            this.RestoreBarFocus(focusSnapshot);
            _morphicBarWindow.FocusController.EndSuppressUpgradeAfterPendingActivations();
        }
    }

    /// <summary>
    /// Captures the currently-focused bar control + its FocusState so it can be restored after an
    /// operation that would drop XAML focus (AppWindow.Hide/Show, rasterization-scale relayout
    /// triggered by a DPI change, etc.). Thin wrapper around the focus controller's Capture using
    /// AnyInBarXamlRoot scope (matches the legacy unfiltered behavior callers depend on -- the
    /// DPI / hide-show paths don't drop popup-style subtrees, so the popup-filter version isn't
    /// required here). Returns (null, Unfocused) if no element inside the bar is focused or the
    /// bar's XamlRoot isn't available.
    /// </summary>
    public MorphicBarFocusController.FocusSnapshot CaptureBarFocus()
    {
        return _morphicBarWindow.FocusController.Capture(MorphicBarFocusController.SnapshotScope.AnyInBarXamlRoot);
    }

    /// <summary>
    /// Restores focus from a snapshot taken by CaptureBarFocus. Thin wrapper around the focus
    /// controller's Restore. No-op for an empty snapshot.
    /// </summary>
    public void RestoreBarFocus(MorphicBarFocusController.FocusSnapshot snapshot)
    {
        _morphicBarWindow.FocusController.Restore(snapshot);
    }

    // AppWindow.Changed fires for several reasons (position, size, visibility, etc.); filter on
    // DidVisibilityChange so subscribers only get the relevant transitions, no matter which code
    // path (menu item, tray click, etc.) triggered the show/hide.
    private void OnBarAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidVisibilityChange)
        {
            this.BarVisibilityChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // Driven by FullScreenMonitorWatcher (UI thread). fullScreenMonitor is the monitor that now
    // has a full-screen foreground window, or HMONITOR.Null when none does. Suppress the bar's
    // always-on-top z-order only when the full-screen window is on the BAR's own monitor; a
    // full-screen video on another monitor must not affect the bar. The bar's logical visibility
    // is untouched (see MorphicBarWindow.SetTopmostSuppressedForFullScreen).
    private void OnFullScreenMonitorChanged(object? sender, Windows.Win32.Graphics.Gdi.HMONITOR fullScreenMonitor)
    {
        var barHandle = (Windows.Win32.Foundation.HWND)WinRT.Interop.WindowNative.GetWindowHandle(_morphicBarWindow);
        var barMonitor = Windows.Win32.PInvoke.MonitorFromWindow(barHandle, Windows.Win32.Graphics.Gdi.MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        bool suppress = (fullScreenMonitor != Windows.Win32.Graphics.Gdi.HMONITOR.Null) && (fullScreenMonitor == barMonitor);
        _morphicBarWindow.SetTopmostSuppressedForFullScreen(suppress);
    }

    // The bar's RasterizationScaleChangedExternal fires when the monitor DPI changes (or the bar
    // moves to a different-DPI monitor). Refresh Text Size +/- button state so it reflects the
    // bar's current monitor. The factory's Display.DisplayChanged subscription covers monitor
    // add/remove and resolution changes, but Windows does NOT reliably fire WM_DISPLAYCHANGE for
    // DPI-scaling changes -- the WinUI XamlRoot.Changed signal (which the bar wraps as
    // RasterizationScaleChangedExternal) is the authoritative source for those. Subscribing both
    // gives full coverage with idempotent recomputation.
    private void OnBarRasterizationScaleChanged(object? sender, EventArgs e)
    {
        BarItemDataFactory.RefreshTextSizeButtonState();
    }

    // The bar moved to a different monitor. Refresh Text Size +/- button state so it reflects the new
    // monitor's DPI-offset range. A same-scale cross-monitor move does NOT fire
    // RasterizationScaleChangedExternal, and Display.DisplayChanged only fires for system-wide config
    // changes (not a bar-window move), so without this the buttons keep the prior monitor's state.
    private void OnBarCurrentMonitorChanged(object? sender, EventArgs e)
    {
        BarItemDataFactory.RefreshTextSizeButtonState();
    }

    // Re-raise the bar's docking-location change so App-level persistence can observe re-docks
    // without taking a direct dependency on MorphicBarWindow. The bar raises this from its single
    // _dockingLocation mutation point (AnimateMoveTo), so this fires for drag re-docks AND for the
    // registry-driven moves we initiate via MoveToPlacement.
    private void OnBarWindowDockingLocationChanged(object? sender, Morphic.MorphicBar.DockingLocation dockingLocation)
    {
        this.DockingLocationChanged?.Invoke(this, dockingLocation);
    }

    // Re-raise the bar's orientation change (companion to OnBarWindowDockingLocationChanged). The bar
    // raises OrientationChanged from its Orientation setter, which AnimateMoveTo drives, so this fires
    // for drag-driven flips AND for the registry-driven moves we initiate via MoveToPlacement.
    private void OnBarWindowOrientationChanged(object? sender, Microsoft.UI.Xaml.Controls.Orientation orientation)
    {
        this.OrientationChanged?.Invoke(this, orientation);
    }

    /// <summary>
    /// Tears down the manager and closes the bar. Unsubscribes the manager's event handlers
    /// from the bar FIRST and then closes the bar -- order matters: closing the bar fires
    /// AppWindow.Changed (visibility transition during teardown), which the manager's still-
    /// active handler would translate into BarVisibilityChanged and propagate to subscribers
    /// (e.g., App's tray-tooltip refresh) whose targets are themselves mid-teardown -- COMException
    /// territory ("WinUI Desktop Window object has already been closed"). Unwiring before the
    /// close eliminates that. Callers just call Dispose; the safe order is internal.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _morphicBarWindow.AppWindow.Changed -= this.OnBarAppWindowChanged;
        _morphicBarWindow.RasterizationScaleChangedExternal -= this.OnBarRasterizationScaleChanged;
        _morphicBarWindow.CurrentMonitorChanged -= this.OnBarCurrentMonitorChanged;
        _morphicBarWindow.DockingLocationChanged -= this.OnBarWindowDockingLocationChanged;
        _morphicBarWindow.OrientationChanged -= this.OnBarWindowOrientationChanged;
        if (_fullScreenWatcher is not null)
        {
            _fullScreenWatcher.FullScreenMonitorChanged -= this.OnFullScreenMonitorChanged;
            _fullScreenWatcher.Dispose();
            _fullScreenWatcher = null;
        }
        if (_barIconRefreshHandler is not null)
        {
            Morphic.SettingsUtils.CachedDarkModeState.StateChanged -= _barIconRefreshHandler;
            _barIconRefreshHandler = null;
        }
        // The bar intercepts WM_CLOSE to turn Alt+F4 into a Hide; re-enable user-close before
        // Close so this programmatic-shutdown path actually destroys the window.
        _morphicBarWindow.SetUserCloseEnabled(true);
        _morphicBarWindow.Close();
    }
}
