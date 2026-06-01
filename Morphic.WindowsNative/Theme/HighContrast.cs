// Copyright 2020-2026 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-windowsnative-lib-cs/blob/main/LICENSE
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
using System.Threading.Tasks;

namespace Morphic.WindowsNative.Theme;

// EventArgs payload for HighContrast.IsOnChanged. Carries the new "high contrast is on" state.
// NewValue follows the BCL convention -- see Morphic.WindowsNative.Theme.DarkModeChangedEventArgs.
public class HighContrastIsOnChangedEventArgs(bool newValue) : EventArgs
{
    public bool NewValue { get; } = newValue;
}

public class HighContrast
{
    // Lock used only by SetIsOn to ensure two concurrent setters don't interleave their
    // read-modify-write SPI sequences and clobber each other's flag updates. GetIsOn is a
    // single SPI call and needs no locking (SPI reads are thread-safe).
    private static readonly object _setIsOnLock = new();

    // Separate lock for the event-state (cache + subscription lifecycle), so the event path
    // doesn't block on SetIsOn's read-modify-write window.
    private static readonly object _isOnChangedLock = new();
    private static bool _isOnChangedIsSubscribed;
	//
    // Nullable on purpose: null means "we couldn't determine the initial state" (the seed read
    // in EnsureSubscribedLocked failed). With null, the next OnHighContrastChanged comparison
    // (newIsOn == _cachedIsOn) is guaranteed false regardless of which way HC moved, so the
    // first observed transition always fires the event -- even if the change is TO the same
    // default `false` we'd have otherwise assumed. Without this, a failed seed silently asserts
    // "HC is off" and subscribers miss a real false-to-true transition (or stay believing the
    // wrong state forever if the reality is true and no further changes happen).
    private static bool? _cachedIsOn;
    private static EventHandler<HighContrastIsOnChangedEventArgs>? _isOnChanged;

    // Reads the current "high contrast is on" state by inspecting the HCF_HIGHCONTRASTON bit
    // of the HIGHCONTRASTW struct returned by SystemParametersInfo(SPI_GETHIGHCONTRAST).
    public static MorphicResult<bool, MorphicUnit> GetIsOn()
    {
        var highContrastInfo = new Windows.Win32.UI.Accessibility.HIGHCONTRASTW
        {
            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(Windows.Win32.UI.Accessibility.HIGHCONTRASTW)),
        };

        Windows.Win32.Foundation.BOOL getResult;
        unsafe
        {
            getResult = Windows.Win32.PInvoke.SystemParametersInfo(
                Windows.Win32.UI.WindowsAndMessaging.SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETHIGHCONTRAST,
                highContrastInfo.cbSize,
                &highContrastInfo,
                /* fWinIni unused for GET operations */ 0);
        }
        if (getResult == 0)
        {
            // Debug.Assert pops a dialog when a debugger is attached; Debug.WriteLine logs to
            // the debug output stream unconditionally (so the failure is visible in attached
            // debuggers AND in non-debugger debug-build runs).
            var win32Error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            Debug.WriteLine($"[HighContrast.GetIsOn] SystemParametersInfo(SPI_GETHIGHCONTRAST) failed with win32 error {win32Error}");
            Debug.Assert(false, $"SystemParametersInfo(SPI_GETHIGHCONTRAST) failed with win32 error {win32Error}");
            return MorphicResult.ErrorResult();
        }

        var isOn = (highContrastInfo.dwFlags & Windows.Win32.UI.Accessibility.HIGHCONTRASTW_FLAGS.HCF_HIGHCONTRASTON) == Windows.Win32.UI.Accessibility.HIGHCONTRASTW_FLAGS.HCF_HIGHCONTRASTON;
        return MorphicResult.OkResult(isOn);
    }

    // Toggles the system-wide high-contrast mode by flipping the HCF_HIGHCONTRASTON bit on the
    // current HIGHCONTRASTW struct. We do read-modify-write because we want to leave the other
    // flags (hotkey configuration, etc.) untouched.
    //
    // updateUserProfile (default false) controls SPIF_UPDATEINIFILE: when true, Windows also
    // persists the change to the user's profile so it survives logoff/login. When false, only
    // the current session is affected.
    public static MorphicResult<MorphicUnit, MorphicUnit> SetIsOn(bool isOn, bool updateUserProfile = false)
    {
        lock (_setIsOnLock)
        {
            var highContrastInfo = new Windows.Win32.UI.Accessibility.HIGHCONTRASTW
            {
                cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(Windows.Win32.UI.Accessibility.HIGHCONTRASTW)),
            };

            // Read the current HIGHCONTRASTW state.
            Windows.Win32.Foundation.BOOL getResult;
            unsafe
            {
                getResult = Windows.Win32.PInvoke.SystemParametersInfo(
                    Windows.Win32.UI.WindowsAndMessaging.SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETHIGHCONTRAST,
                    highContrastInfo.cbSize,
                    &highContrastInfo,
                    /* fWinIni unused for GET operations */ 0);
            }
            if (getResult == 0)
            {
                Debug.Assert(false, $"SystemParametersInfo(SPI_GETHIGHCONTRAST) failed with win32 error {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}");
                return MorphicResult.ErrorResult();
            }

            // Flip the on/off bit, leaving the other flags untouched.
            //
            // NOTE: per Microsoft's documentation we do NOT set HCF_OPTION_NOTHEMECHANGE when
            // toggling high contrast mode -- the theme change is the desired side-effect.
            if (isOn)
            {
                highContrastInfo.dwFlags |= Windows.Win32.UI.Accessibility.HIGHCONTRASTW_FLAGS.HCF_HIGHCONTRASTON;
            }
            else
            {
                highContrastInfo.dwFlags &= ~Windows.Win32.UI.Accessibility.HIGHCONTRASTW_FLAGS.HCF_HIGHCONTRASTON;
            }

            // Write the updated state back.
            // SPIF_SENDWININICHANGE broadcasts WM_SETTINGCHANGE to top-level windows so that
            // anyone watching (including our own IsOnChanged via SystemSettingsListener) sees the
            // update. SPIF_UPDATEINIFILE additionally persists the change to the user profile.
            var fWinIni = Windows.Win32.UI.WindowsAndMessaging.SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS.SPIF_SENDWININICHANGE;
            if (updateUserProfile)
            {
                fWinIni |= Windows.Win32.UI.WindowsAndMessaging.SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS.SPIF_UPDATEINIFILE;
            }

            Windows.Win32.Foundation.BOOL setResult;
            unsafe
            {
                setResult = Windows.Win32.PInvoke.SystemParametersInfo(
                    Windows.Win32.UI.WindowsAndMessaging.SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETHIGHCONTRAST,
                    highContrastInfo.cbSize,
                    &highContrastInfo,
                    fWinIni);
            }
            if (setResult == 0)
            {
                Debug.Assert(false, $"SystemParametersInfo(SPI_SETHIGHCONTRAST) failed with win32 error {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}");
                return MorphicResult.ErrorResult();
            }

            return MorphicResult.OkResult();
        }
    }

    //

    // Change-notification event
	//
    // Lifecycle: lazy. First subscription wires SystemSettingsListener.Shared.HighContrastChanged
    // and seeds the cache via GetIsOn(); last unsubscribe detaches. The shared SystemSettingsListener
    // owns its broadcast-catching window for the process lifetime, so there's no underlying resource
    // we need to tear down.
	
    public static event EventHandler<HighContrastIsOnChangedEventArgs> IsOnChanged
    {
        add
        {
            lock (_isOnChangedLock)
            {
                HighContrast.EnsureSubscribedLocked();
                _isOnChanged += value;
            }
        }
        remove
        {
            lock (_isOnChangedLock)
            {
                _isOnChanged -= value;
                HighContrast.UnsubscribeIfNoSubscribersLocked();
            }
        }
    }

    // Pre-requisite: caller MUST hold _isOnChangedLock.
    private static void EnsureSubscribedLocked()
    {
        if (_isOnChangedIsSubscribed)
        {
            return;
        }

        // capture the current high contrast isOn state before wiring the handlers to seed the
        // cached value (as a baseline for the event to compare against); if the call fails,
        // gracefully fail by setting the cached value to null (in which case any high contrast
        // on/off state event will trigger as a change).
        var firstReadResult = HighContrast.GetIsOn();
        if (firstReadResult.IsError)
        {
            Debug.WriteLine("[HighContrast.EnsureSubscribedLocked] seed GetIsOn() failed; cache will start as null");
        }
        _cachedIsOn = firstReadResult.IsSuccess ? firstReadResult.Value : (bool?)null;

        Morphic.WindowsNative.SystemSettings.SystemSettingsListener.Shared.HighContrastChanged += HighContrast.OnHighContrastChanged;
        _isOnChangedIsSubscribed = true;

        // re-capture the current high contrast isOn state in case it changed while we were wiring
        // up the event; if the state cannot be read, set the cache to null again (which could result
        // in an extra non-transition event, but that's better than missing an event)
        // no event needs to fire here even if the two reads disagree, because no subscriber has
        // been added prior to this wiring
        var secondReadResult = HighContrast.GetIsOn();
        if (secondReadResult.IsError)
        {
            Debug.WriteLine("[HighContrast.EnsureSubscribedLocked] post-subscribe GetIsOn() failed; cache will be set to null");
        }
        _cachedIsOn = secondReadResult.IsSuccess ? secondReadResult.Value : (bool?)null;
    }

    // Pre-requisite: caller MUST hold _isOnChangedLock.
    private static void UnsubscribeIfNoSubscribersLocked()
    {
        if (_isOnChanged is not null)
        {
            return;
        }
        if (_isOnChangedIsSubscribed == false)
        {
            return;
        }

        Morphic.WindowsNative.SystemSettings.SystemSettingsListener.Shared.HighContrastChanged -= HighContrast.OnHighContrastChanged;
        _isOnChangedIsSubscribed = false;
    }

    // SystemSettingsListener has already filtered to "high-contrast specifically changed", so we
    // just re-read via SPI, compare to cache, and then only fire IsOnChanged on actual transitions.
    private static void OnHighContrastChanged(object? sender, EventArgs e)
    {
        // The read+compare+update must be atomic relative to other invocations: lock-acquisition
        // order has no relationship to lock-free-read order, so a slower-reading thread could win
        // the lock later and clobber the cache with a stale value (e.g., T1 reads ON pre-flip,
        // T2 reads OFF post-flip, T2 locks first and no-ops against cache(OFF), T1 locks next
        // and updates cache to a now-wrong ON). Doing the read INSIDE the lock keeps each
        // invocation's view of reality consistent with its cache-decision.
        //
        // Retry covers transient SPI failures (essentially never happens for SPI_GETHIGHCONTRAST).
        // We re-acquire the lock per attempt rather than holding it across Thread.Sleep, so
        // subscribe/unsubscribe on the UI thread isn't blocked during the retry budget. On the
        // FINAL failed attempt we null the cache INSIDE that same lock acquisition so that a
        // concurrent successful invocation that updated the cache between our attempts has its
        // update preserved (until our null-set wins, at which point further events will still
        // re-establish the cache from a fresh read).
        const int MaxAttempts = 3;
        const int RetryDelayMilliseconds = 10;

        // toFire encodes "I have a value iff I have handlers" together; null by default
        (bool isOn, EventHandler<HighContrastIsOnChangedEventArgs>? handlers)? toFire = null;
        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            lock (_isOnChangedLock)
            {
                var attemptResult = HighContrast.GetIsOn();
                if (attemptResult.IsSuccess)
                {
                    var newIsOn = attemptResult.Value;
                    // If a second OnHighContrastChanged committed the same fresh value first,
                    // _cachedIsOn already reflects it; we no-op and one event fires for one
                    // change, which is correct.
                    if (newIsOn != _cachedIsOn)
                    {
                        _cachedIsOn = newIsOn;
                        toFire = (newIsOn, _isOnChanged);
                    }
                    break;
                }

                Debug.WriteLine($"[HighContrast.OnHighContrastChanged] GetIsOn() failed on attempt {attempt}/{MaxAttempts}");
                if (attempt == MaxAttempts)
                {
                    // The event fired BECAUSE high-contrast state changed (SystemSettingsListener
                    // already filtered to that signal), so reality is now different from whatever
                    // we have cached -- but we can't read the new value to know which way. Null
                    // the cache so the next OnHighContrastChanged doesn't mis-compare against a
                    // known-stale value (if we left it as-is, a later successful read that happens
                    // to MATCH the stale cache would silently no-op, and subscribers would never
                    // learn about the transition we had to skip).
                    Debug.WriteLine($"[HighContrast.OnHighContrastChanged] GetIsOn() failed all {MaxAttempts} attempts; setting cache to null and skipping this transition");
                    _cachedIsOn = null;
                    return;
                }
            }

            // Sleep OUTSIDE the lock so UI-thread subscribers aren't blocked while we wait.
            // Only reached on non-final failure -- success breaks out above, final-attempt
            // failure returns from inside the lock.
            System.Threading.Thread.Sleep(RetryDelayMilliseconds);
        }

        // Dispatch outside the lock so a handler that subscribes/unsubscribes can't deadlock.
        // Each handler runs on its own Task so a slow/throwing handler doesn't block the others.
        //
        // The pattern unwraps both the outer Nullable and the inner handlers reference in one
        // step: skipped silently if either is null (i.e. no transition, or no subscribers).
        if (toFire is { isOn: var isOn, handlers: { } handlers })
        {
            foreach (EventHandler<HighContrastIsOnChangedEventArgs> handler in handlers.GetInvocationList())
            {
                _ = Task.Run(() => handler.Invoke(null, new HighContrastIsOnChangedEventArgs(isOn)));
            }
        }
    }
}
