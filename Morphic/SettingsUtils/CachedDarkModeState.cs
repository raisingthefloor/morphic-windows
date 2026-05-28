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
using System;
using System.Threading.Tasks;

namespace Morphic.SettingsUtils;

public class CachedDarkModeStateChangedEventArgs(bool isDark, bool isHighContrast) : EventArgs
{
    public bool IsDark { get; } = isDark;

    public bool IsHighContrast { get; } = isHighContrast;
}

public static class CachedDarkModeState
{
    private static readonly object _lock = new();
    private static bool _isSubscribed;
    //
    private static bool _cachedIsDark;
    private static bool _cachedIsHighContrast;
    private static EventHandler<CachedDarkModeStateChangedEventArgs>? _stateChanged;

    public static event EventHandler<CachedDarkModeStateChangedEventArgs> StateChanged
    {
        add
        {
            lock (_lock)
            {
                CachedDarkModeState.EnsureSubscribedLocked();
                _stateChanged += value;
            }
        }
        remove
        {
            lock (_lock)
            {
                _stateChanged -= value;
                CachedDarkModeState.UnsubscribeIfNoSubscribersLocked();
            }
        }
    }

    // Snapshot accessors for callers that want the current value without subscribing (e.g. the
    // factory's initial seed for the Dark button's IsChecked and IsEnabled). Returns the cached
    // value when a subscriber is keeping the cache current; otherwise computes fresh.
    public static bool GetCurrentIsDark()
    {
        lock (_lock)
        {
            if (_isSubscribed)
            {
                return _cachedIsDark;
            }
            var (isDark, _) = CachedDarkModeState.ComputeState();
            return isDark;
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
            var (_, isHighContrast) = CachedDarkModeState.ComputeState();
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

        var (isDark, isHighContrast) = CachedDarkModeState.ComputeState();
        _cachedIsDark = isDark;
        _cachedIsHighContrast = isHighContrast;

        // Subscribe to every change source that can affect either value. Overlapping fires
        // are deduplicated by the cache-compare in OnAnyChange.
        Morphic.WindowsNative.Theme.DarkMode.AppsUseDarkModeChanged += CachedDarkModeState.OnAnyChange;
        Morphic.WindowsNative.Theme.DarkMode.SystemUsesDarkModeChanged += CachedDarkModeState.OnAnyChange;
        Morphic.WindowsNative.SystemSettings.SystemSettingsListener.Shared.HighContrastChanged += CachedDarkModeState.OnAnyChange;

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

        Morphic.WindowsNative.Theme.DarkMode.AppsUseDarkModeChanged -= CachedDarkModeState.OnAnyChange;
        Morphic.WindowsNative.Theme.DarkMode.SystemUsesDarkModeChanged -= CachedDarkModeState.OnAnyChange;
        Morphic.WindowsNative.SystemSettings.SystemSettingsListener.Shared.HighContrastChanged -= CachedDarkModeState.OnAnyChange;

        _isSubscribed = false;
    }

    // Bridge for the various per-source change events. We don't use the args -- the new value
    // is computed fresh from all sources in ComputeState.
    private static void OnAnyChange(object? sender, EventArgs e)
    {
        CachedDarkModeState.RecomputeAndFireIfChanged();
    }

    private static void RecomputeAndFireIfChanged()
    {
        EventHandler<CachedDarkModeStateChangedEventArgs>? handlersToFire = null;
        bool newIsDark;
        bool newIsHighContrast;

        lock (_lock)
        {
            var (isDark, isHighContrast) = CachedDarkModeState.ComputeState();
            if (isDark == _cachedIsDark && isHighContrast == _cachedIsHighContrast)
            {
                return;
            }
            _cachedIsDark = isDark;
            _cachedIsHighContrast = isHighContrast;
            newIsDark = isDark;
            newIsHighContrast = isHighContrast;
            handlersToFire = _stateChanged;
        }

        // Dispatch outside the lock so a handler that subscribes/unsubscribes can't deadlock.
        // Each handler runs on its own Task so a slow/throwing handler doesn't block the others.
        if (handlersToFire is not null)
        {
            foreach (EventHandler<CachedDarkModeStateChangedEventArgs> handler in handlersToFire.GetInvocationList())
            {
                Task.Run(() => handler.Invoke(null, new CachedDarkModeStateChangedEventArgs(newIsDark, newIsHighContrast)));
            }
        }
    }

    private static (bool IsDark, bool IsHighContrast) ComputeState()
    {
        var highContrastResult = Morphic.WindowsNative.Theme.HighContrast.GetIsOn();
        bool isHighContrastOn = highContrastResult.IsSuccess && highContrastResult.Value;

        if (isHighContrastOn)
        {
            var textColorref = Windows.Win32.PInvoke.GetSysColor(Windows.Win32.Graphics.Gdi.SYS_COLOR_INDEX.COLOR_WINDOWTEXT);
            var isDark = CachedDarkModeState.IsColorrefLight((uint)textColorref);
            return (IsDark: isDark, IsHighContrast: true);
        }
        else
        {
            // Non-HC: aggregate the per-app and per-system registry-backed preferences using an
            // "either is dark = effectively dark" semantic. Missing or errored reads are treated
            // as "light" (the Windows default when the user has not explicitly toggled).
            var appsResult = Morphic.WindowsNative.Theme.DarkMode.GetAppsUseDarkMode();
            bool appsIsDark = appsResult.IsSuccess && appsResult.Value == true;
            //
            var systemResult = Morphic.WindowsNative.Theme.DarkMode.GetSystemUsesDarkMode();
            bool systemIsDark = systemResult.IsSuccess && systemResult.Value == true;

            return (IsDark: appsIsDark || systemIsDark, IsHighContrast: false);
        }
    }

    // courtesy of Microsoft documentation: https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/apply-windows-themes#know-when-dark-mode-is-enabled
    //
    // NOTE: we invert the result (caller takes !IsColorrefLight) when checking the *background* color,
    // since a light background = light theme = NOT dark.
    private static bool IsColorrefLight(uint colorref)
    {
        byte r = (byte)(colorref & 0xFF);
        byte g = (byte)((colorref >> 8) & 0xFF);
        byte b = (byte)((colorref >> 16) & 0xFF);
        return ((5 * g) + (2 * r) + b) > (8 * 128);
    }
}
