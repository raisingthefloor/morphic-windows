// Copyright 2026 Raising the Floor - US, Inc.
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

using System;
using System.Threading.Tasks;

namespace Morphic.Controls.TrayButton;

// EventArgs payload for CachedTaskbarThemeState.StateChanged. Carries the new IsTaskbarLight
// and IsHighContrast values as a snapshot so a single fire covers any combination of changes.
public class CachedTaskbarThemeStateChangedEventArgs(bool isTaskbarLight, bool isHighContrast) : EventArgs
{
    // Whether the Windows taskbar background is light. See CachedTaskbarThemeState for the
    // precise semantic (SystemUsesDarkMode-based when HC is off; GetSysColor-luminance-based
    // when HC is on).
    public bool IsTaskbarLight { get; } = isTaskbarLight;

    // Whether high contrast is currently active. Exposed alongside IsTaskbarLight because the
    // two are tracked from the same cache (HC active determines how IsTaskbarLight is computed).
    public bool IsHighContrast { get; } = isHighContrast;
}

// Threading: handlers fire from a Task.Run dispatch (matching CachedDarkModeState's shape) so
// a slow/throwing handler doesn't block the next. UI subscribers must marshal back via their
// dispatcher (e.g. PostMessage / InvalidateRect on the appropriate window).
internal static class CachedTaskbarThemeState
{
    private static readonly object _lock = new();
    private static bool _isSubscribed;
    //
    private static bool _cachedIsTaskbarLight;
    private static bool _cachedIsHighContrast;
    //
    private static EventHandler<CachedTaskbarThemeStateChangedEventArgs>? _stateChanged;

    public static event EventHandler<CachedTaskbarThemeStateChangedEventArgs> StateChanged
    {
        add
        {
            lock (_lock)
            {
                CachedTaskbarThemeState.EnsureSubscribedLocked();
                _stateChanged += value;
            }
        }
        remove
        {
            lock (_lock)
            {
                _stateChanged -= value;
                CachedTaskbarThemeState.UnsubscribeIfNoSubscribersLocked();
            }
        }
    }

    // Snapshot accessors for callers that want the current value without subscribing.
    // Returns the cached value when a subscriber is keeping the cache current; otherwise
    // computes fresh from the underlying sources.
    public static bool GetCurrentIsTaskbarLight()
    {
        lock (_lock)
        {
            if (_isSubscribed)
            {
                return _cachedIsTaskbarLight;
            }
            var (isTaskbarLight, _) = CachedTaskbarThemeState.ComputeState();
            return isTaskbarLight;
        }
    }

    public static bool GetCurrentIsHighContrast()
    {
        lock (_lock)
        {
            if (_isSubscribed)
            {
                return _cachedIsHighContrast;
            }
            var (_, isHighContrast) = CachedTaskbarThemeState.ComputeState();
            return isHighContrast;
        }
    }

    // Pre-requisite: caller MUST hold _lock.
    private static void EnsureSubscribedLocked()
    {
        if (_isSubscribed)
        {
            return;
        }

        var (isTaskbarLight, isHighContrast) = CachedTaskbarThemeState.ComputeState();
        _cachedIsTaskbarLight = isTaskbarLight;
        _cachedIsHighContrast = isHighContrast;

        // Subscribe to every change source that can affect either value. Overlapping fires
        // are deduplicated by the cache-compare in OnAnyChange.
        Morphic.WindowsNative.Theme.DarkMode.SystemUsesDarkModeChanged += CachedTaskbarThemeState.OnAnyChange;
        Morphic.WindowsNative.SystemSettings.SystemSettingsListener.Shared.HighContrastChanged += CachedTaskbarThemeState.OnAnyChange;

        _isSubscribed = true;
    }

    // Pre-requisite: caller MUST hold _lock.
    private static void UnsubscribeIfNoSubscribersLocked()
    {
        if (_stateChanged is not null)
        {
            return;
        }
        if (_isSubscribed == false)
        {
            return;
        }

        Morphic.WindowsNative.Theme.DarkMode.SystemUsesDarkModeChanged -= CachedTaskbarThemeState.OnAnyChange;
        Morphic.WindowsNative.SystemSettings.SystemSettingsListener.Shared.HighContrastChanged -= CachedTaskbarThemeState.OnAnyChange;

        _isSubscribed = false;
    }

    // Bridge for the per-source change events. We don't use the args -- the new values are
    // computed fresh from all sources in ComputeState.
    private static void OnAnyChange(object? sender, EventArgs e)
    {
        CachedTaskbarThemeState.RecomputeAndFireIfChanged();
    }

    private static void RecomputeAndFireIfChanged()
    {
        EventHandler<CachedTaskbarThemeStateChangedEventArgs>? handlersToFire = null;
        bool newIsTaskbarLight;
        bool newIsHighContrast;

        lock (_lock)
        {
            var (isTaskbarLight, isHighContrast) = CachedTaskbarThemeState.ComputeState();
            if (isTaskbarLight == _cachedIsTaskbarLight && isHighContrast == _cachedIsHighContrast)
            {
                return;
            }
            _cachedIsTaskbarLight = isTaskbarLight;
            _cachedIsHighContrast = isHighContrast;
            newIsTaskbarLight = isTaskbarLight;
            newIsHighContrast = isHighContrast;
			
            handlersToFire = _stateChanged;
        }

        // Dispatch outside the lock so a handler that subscribes/unsubscribes can't deadlock.
        // Each handler runs on its own Task so a slow/throwing handler doesn't block the others.
        if (handlersToFire is not null)
        {
            foreach (EventHandler<CachedTaskbarThemeStateChangedEventArgs> handler in handlersToFire.GetInvocationList())
            {
                Task.Run(() => handler.Invoke(null, new CachedTaskbarThemeStateChangedEventArgs(newIsTaskbarLight, newIsHighContrast)));
            }
        }
    }

    private static (bool IsTaskbarLight, bool IsHighContrast) ComputeState()
    {
        var highContrastResult = Morphic.WindowsNative.Theme.HighContrast.GetIsOn();
        bool isHighContrastOn = highContrastResult.IsSuccess && highContrastResult.Value;

        if (isHighContrastOn)
        {
            // HC: read the actual HC theme's window background color and check its luminance.
            // The taskbar uses COLOR_WINDOW under HC, so this directly answers "is the taskbar
            // light or dark" for any HC theme (HC Black / HC White / Aquatic / Desert / etc.).
            var windowColorref = Windows.Win32.PInvoke.GetSysColor(Windows.Win32.Graphics.Gdi.SYS_COLOR_INDEX.COLOR_WINDOW);
            var isTaskbarLight = CachedTaskbarThemeState.IsColorrefLight((uint)windowColorref);
            return (IsTaskbarLight: isTaskbarLight, IsHighContrast: true);
        }
        else
        {
            // Non-HC: the taskbar tracks the system theme (SystemUsesDarkMode), not the per-app
            // theme. A failed/missing read is treated as light (the Windows default when the
            // user has not explicitly toggled).
            var systemResult = Morphic.WindowsNative.Theme.DarkMode.GetSystemUsesDarkMode();
            bool systemIsDark = systemResult.IsSuccess && systemResult.Value == true;
            return (IsTaskbarLight: !systemIsDark, IsHighContrast: false);
        }
    }

    // Decodes a Win32 COLORREF (0x00BBGGRR packing) and returns true if its perceived luminance
    // is light. Matches the formula Microsoft documents (and that Win32AppTheme.IsColorLight uses)
    // for "is this color the foreground of a light theme or a dark theme":
    //   perceived = 5*G + 2*R + B
    //   threshold = 8*128 = 1024
    private static bool IsColorrefLight(uint colorref)
    {
        byte r = (byte)(colorref & 0xFF);
        byte g = (byte)((colorref >> 8) & 0xFF);
        byte b = (byte)((colorref >> 16) & 0xFF);
        return ((5 * g) + (2 * r) + b) > (8 * 128);
    }
}
