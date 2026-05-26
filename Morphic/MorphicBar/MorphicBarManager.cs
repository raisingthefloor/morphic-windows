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
    private readonly MorphicBarWindow _bar;
    private bool _disposed;

    private readonly Microsoft.UI.Dispatching.DispatcherQueue _uiDispatcherQueue;

    public MorphicBarManager(MorphicBarWindow bar)
    {
        _bar = bar;
        _uiDispatcherQueue = bar.DispatcherQueue;
        _bar.AppWindow.Changed += this.OnBarAppWindowChanged;

        // Seed the bar icon for the current system theme (HC variant or non-HC), then subscribe
        // so any future HC transition swaps the icon. CachedDarkModeState's StateChanged fires
        // from a worker thread; marshal the refresh onto the bar's DispatcherQueue (UI thread)
        // before calling SetIconFromFile.
        this.RefreshBarIcon();
        _barIconRefreshHandler = (_, _) =>
        {
            _uiDispatcherQueue.TryEnqueue(this.RefreshBarIcon);
        };
    }

    // Picks the correct contrast-variant icon for the current system theme and applies it to
    // the bar window. Driven by CachedDarkModeState (HC on/off + HC variant dark/light)
    private void RefreshBarIcon()
    {
        try
        {
            var relativePath = App.GetIconRelativePathForCurrentSystemTheme();
            var absolutePath = System.IO.Path.Combine(AppContext.BaseDirectory, relativePath);
            _ = _bar.SetIconFromFile(absolutePath, 256, 256);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // if MorphicBarWindows's native window is mid-teardown (HC change fired between
            // Dispose's unsubscribe + bar's Close); swallow rather than crash. Subsequent
            // state changes won't fire because we'll be disposed.
        }
    }

    public event EventHandler? BarVisibilityChanged;

    public bool IsBarVisible => _bar.Visible;

    public void ShowBar() => _bar.AppWindow.Show();

    public void HideBar() => _bar.AppWindow.Hide();

    public void ActivateBar() => _bar.Activate();

    public IntPtr GetBarWindowHandle() => WinRT.Interop.WindowNative.GetWindowHandle(_bar);


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
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _bar.AppWindow.Changed -= this.OnBarAppWindowChanged;
        _bar.Close();
    }
}
