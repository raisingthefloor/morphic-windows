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

    public MorphicBarManager(MorphicBarWindow morphicBarWindow)
    {
        _morphicBarWindow = morphicBarWindow;
        _uiDispatcherQueue = morphicBarWindow.DispatcherQueue;
        _morphicBarWindow.AppWindow.Changed += this.OnBarAppWindowChanged;
        _morphicBarWindow.RasterizationScaleChangedExternal += this.OnBarRasterizationScaleChanged;

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

    public bool IsBarVisible => _morphicBarWindow.Visible;

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

        _morphicBarWindow.RefreshAllButtonCompoundStatesAfterShow();
    }

    public void HideBar() => _morphicBarWindow.AppWindow.Hide();

    public void ActivateBar() => _morphicBarWindow.Activate();

    public IntPtr GetBarWindowHandle() => WinRT.Interop.WindowNative.GetWindowHandle(_morphicBarWindow);

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

    public async Task RunWithBarHiddenAsync(Func<Task> action)
    {
        bool wasVisible = _morphicBarWindow.Visible;
        var focusSnapshot = this.CaptureBarFocus();
        // Suppress focus upgrades for the WHOLE flow: covers click activation's deferred update,
        // the action duration, and the re-Show's WM_ACTIVATE. 30s is generous enough for any
        // realistic action (snip overlay etc.) and short enough to expire before any user action.
        _morphicBarWindow.FocusController.SuppressUpgradeFor(TimeSpan.FromSeconds(30));
        if (wasVisible) { _morphicBarWindow.AppWindow.Hide(); }
        try
        {
            await action();
        }
        finally
        {
            if (wasVisible) { _morphicBarWindow.AppWindow.Show(); }
            this.RestoreBarFocus(focusSnapshot);
        }
    }

    public MorphicBarFocusController.FocusSnapshot CaptureBarFocus()
    {
        return _morphicBarWindow.FocusController.Capture(MorphicBarFocusController.SnapshotScope.AnyInBarXamlRoot);
    }

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

    private void OnBarRasterizationScaleChanged(object? sender, EventArgs e)
    {
        BarItemDataFactory.RefreshTextSizeButtonState();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _morphicBarWindow.AppWindow.Changed -= this.OnBarAppWindowChanged;
        _morphicBarWindow.RasterizationScaleChangedExternal -= this.OnBarRasterizationScaleChanged;
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
