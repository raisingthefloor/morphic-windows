// Copyright 2022-2026 Raising the Floor - US, Inc.
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

namespace Morphic.WindowsNative.Display;

// EventArgs payload for ColorFilters.IsActiveChanged. Carries the new "color filtering is active"
// state (true = filter on, false = filter off). NewValue follows the BCL convention -- see the
// matching note in Morphic.WindowsNative.Theme.DarkModeChangedEventArgs.
public class ColorFiltersIsActiveChangedEventArgs(bool newValue) : EventArgs
{
    public bool NewValue { get; } = newValue;
}

public class ColorFilters
{
    private const string COLOR_FILTERING_REGISTRY_KEY_PATH = @"SOFTWARE\Microsoft\ColorFiltering";
    private const string ACTIVE_REGISTRY_VALUE_NAME = "Active";

    private static readonly object _colorFilteringKeyWatcherLock = new();
    private static Morphic.WindowsNative.Registry.OpenedRegistryKeyChangeWatcher? _colorFilteringKeyWatcher;
    private static bool _cachedIsActive;
    private static EventHandler<ColorFiltersIsActiveChangedEventArgs>? _isActiveChanged;

    //

    public static class SystemSettingIds
    {
        public const string COLOR_FILTERING_IS_ENABLED_SETTING_ID = "SystemSettings_Accessibility_ColorFiltering_IsEnabled";
    }

    private static Morphic.WindowsNative.SystemSettings.SettingItemProxy? _colorFilteringIsEnabledSettingItem;
    private static Morphic.WindowsNative.SystemSettings.SettingItemProxy? ColorFilteringIsEnabledSettingItem
    {
        get
        {
            if (_colorFilteringIsEnabledSettingItem is null)
            {
                _colorFilteringIsEnabledSettingItem = Morphic.WindowsNative.SystemSettings.SettingsDatabaseProxy.GetSettingItemOrNull(ColorFilters.SystemSettingIds.COLOR_FILTERING_IS_ENABLED_SETTING_ID);
            }

            return _colorFilteringIsEnabledSettingItem;
        }
    }
    // private const string COLOR_FILTERING_IS_ENABLED_VALUE = "Value";

    //

    // Reads the current "color filtering is active" state from HKCU\SOFTWARE\Microsoft\ColorFiltering.
    // Returns null if the key or value doesn't exist (the value isn't created until the user has
    // toggled color filtering at least once; Windows treats absent as inactive). Returns an error
    // only for unexpected registry failures (permission denied, unexpected value type, etc.).
    public static MorphicResult<bool?, MorphicUnit> GetIsActive()
    {
        Microsoft.Win32.RegistryKey? colorFilteringKey;
        try
        {
            colorFilteringKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(ColorFilters.COLOR_FILTERING_REGISTRY_KEY_PATH, writable: false);
        }
        catch
        {
            return MorphicResult.ErrorResult();
        }
        //
        if (colorFilteringKey is null)
        {
            return MorphicResult.OkResult<bool?>(null);
        }

        object? rawActiveValue;
        try
        {
            rawActiveValue = colorFilteringKey.GetValue(ColorFilters.ACTIVE_REGISTRY_VALUE_NAME);
        }
        catch
        {
            return MorphicResult.ErrorResult();
        }
        finally
        {
            colorFilteringKey.Dispose();
        }
        //
        if (rawActiveValue is null)
        {
            return MorphicResult.OkResult<bool?>(null);
        }
        else if (rawActiveValue is int activeAsInt)
        {
            return MorphicResult.OkResult<bool?>(activeAsInt != 0);
        }
        else
        {
            return MorphicResult.ErrorResult();
        }
    }

    //// NOTE: this is an alternate implementation of GetIsActive (saved as a backup plan, just in case the registry entries aren't a reliable (or preferred) source of truth for the value
    //public static async Task<MorphicResult<bool?, MorphicUnit>> GetIsActiveAsync(TimeSpan? timeout = null)
    //{
    //    var getValueResult = await Morphic.WindowsNative.SystemSettings.SettingItemProxy.GetSettingItemValueAsync<bool>(ColorFilters.ColorFilteringIsEnabledSettingItem, /*ColorFilters.COLOR_FILTERING_IS_ENABLED_VALUE, */timeout);
    //    if (getValueResult.IsError == true)
    //    {
    //        return MorphicResult.ErrorResult();
    //    }
    //    var result = getValueResult.Value;

    //    return MorphicResult.OkResult(result);
    //}

    public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> SetIsActiveAsync(bool value, TimeSpan? timeout = null)
    {
        var colorFilteringIsEnabledSettingItem = ColorFilters.ColorFilteringIsEnabledSettingItem;
        if (colorFilteringIsEnabledSettingItem is null)
        {
            return MorphicResult.ErrorResult();
        }
        //
        var setValueResult = await Morphic.WindowsNative.SystemSettings.SettingItemProxy.SetSettingItemValueAsync<bool>(colorFilteringIsEnabledSettingItem, /*ColorFilters.COLOR_FILTERING_IS_ENABLED_VALUE, */value, timeout);
        if (setValueResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }

        return MorphicResult.OkResult();
    }

    //// NOTE: this is an alternate implementation of SetIsActive (saved as a backup plan, just in case ISettingItem.SetValue(...) stops working at some point)
    //public static async Task<MorphicResult<MorphicUnit, Win32ApiError>> SetIsActiveAsync(bool value)
    //{
    //    var openKeyResult = Morphic.WindowsNative.Registry.CurrentUser.OpenSubKey(COLOR_FILTERING_REGISTRY_KEY_PATH, true);
    //    if (openKeyResult.IsError == true)
    //    {
    //        switch (openKeyResult.Error!.Value)
    //        {
    //            case Win32ApiError.Values.Win32Error:
    //                return MorphicResult.ErrorResult(openKeyResult.Error!);
    //            default:
    //                throw new MorphicUnhandledErrorException();
    //        }
    //    }
    //    var colorFilteringKey = openKeyResult.Value!;

    //    // set the active state of color filtering
    //    uint valueAsUInt32 = value ? (uint)1 : (uint)0;
    //    var setValueResult = colorFilteringKey.SetValue<uint>(ColorFilters.ACTIVE_REGISTRY_VALUE_NAME, valueAsUInt32);
    //    if (setValueResult.IsError == true)
    //    {
    //        switch (setValueResult.Error!.Value)
    //        {
    //            case Registry.RegistryKey.RegistrySetValueError.Values.Win32Error:
    //                return MorphicResult.ErrorResult(Win32ApiError.Win32Error((uint)setValueResult.Error!.Win32ErrorCode!));
    //            case Registry.RegistryKey.RegistrySetValueError.Values.UnsupportedType:
    //            default:
    //                throw new MorphicUnhandledErrorException();
    //        }
    //    }

    //    // run AtBroker (from the Windows System folder) to update the at settings (which we just wrote out to the registry) in real-time
    //    // NOTE: we may want to queue up the atbroker request until after we've done all of our registry writes (i.e. during an "apply settings" batch function), combining
    //    //       arguments if possible between runs of the executable
    //    var atbroker = new Process();
    //    atbroker.StartInfo.FileName = Path.Combine(Environment.SystemDirectory, "AtBroker.exe");
    //    // NOTE: we found these arguments on the Internet; we do not know if they are the correct keys but in our brief testing they worked; before using this in production,
    //    //       we should try to understand what "resettransferkeys" does exactly
    //    atbroker.StartInfo.Arguments = "/colorfiltershortcut /resettransferkeys";
    //    atbroker.StartInfo.UseShellExecute = false;
    //    atbroker.StartInfo.RedirectStandardOutput = true;
    //    try
    //    {
    //        atbroker.Start();
    //        //
    //        // we'll wait up to 250 milliseconds for the atbroker to timeout
    //        var ATBROKER_ASYNC_WAIT_TIMEOUT = new TimeSpan(0, 0, 0, 0, 250);
    //        CancellationTokenSource waitCancellationTokenSource = new(ATBROKER_ASYNC_WAIT_TIMEOUT);
    //        //
    //        await atbroker.WaitForExitAsync(waitCancellationTokenSource.Token);
    //    }
    //    catch
    //    {
    //        return MorphicResult.ErrorResult(Win32ApiError.Win32Error((uint)PInvoke.Win32ErrorCode.ERROR_TIMEOUT));
    //    }

    //    return MorphicResult.OkResult();
    //}

    //

    // Change-notification event
    //
    // Backed by a RegistryKeyChangeWatcher on HKCU\SOFTWARE\Microsoft\ColorFiltering. On any
    // change to the key we re-read the Active value, compare to the cached copy, and fire
    // IsActiveChanged only when it actually changed -- delivering the new value (as a bool) via
    // ColorFiltersIsActiveChangedEventArgs. Subscribers get a precise signal with no need to
    // re-query, and no spurious fires for unrelated value changes inside the same key (e.g. the
    // FilterType value also lives there).
    //
    // Threading: handlers fire from the watcher's ThreadPool callback (NOT the UI thread).
    // Subscribers that touch UI must marshal back to their dispatcher.
    //
    // Lifecycle: the watcher starts lazily on the first subscription, and tears down when the
    // last subscriber detaches. No registry handle is held while there are no subscribers.

    public static event EventHandler<ColorFiltersIsActiveChangedEventArgs> IsActiveChanged
    {
        add
        {
            lock (_colorFilteringKeyWatcherLock)
            {
                ColorFilters.EnsureWatcherStartedLocked();
                _isActiveChanged += value;
            }
        }
        remove
        {
            lock (_colorFilteringKeyWatcherLock)
            {
                _isActiveChanged -= value;
                ColorFilters.StopWatcherIfNoSubscribersLocked();
            }
        }
    }

    // Pre-requisite: caller MUST hold _colorFilteringKeyWatcherLock.
    private static void EnsureWatcherStartedLocked()
    {
        if (_colorFilteringKeyWatcher is not null)
        {
            return;
        }

        // Open via the BCL Microsoft.Win32.Registry. Default permissions (KEY_READ) include
        // KEY_NOTIFY (required by RegistryKeyChangeWatcher) and KEY_QUERY_VALUE (required for the
        // initial-state read below), so the default open path is sufficient. OpenSubKey returns
        // null if the key doesn't exist (i.e. the user has never enabled color filtering); in that
        // case we treat the initial value as inactive and skip wiring the watcher -- the next
        // subscription cycle will retry, and by then if the user has enabled the feature the key
        // will exist.
        var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(ColorFilters.COLOR_FILTERING_REGISTRY_KEY_PATH);
        if (key is null)
        {
            Debug.Assert(false, "Color filtering registry key could not be opened for change notifications");
            return;
        }

        // Cache the initial value BEFORE wiring the watcher's Changed handler so the first
        // change-fire has a baseline to compare against. Read from this same open key while we
        // still have direct access (after we hand it to the watcher, the watcher owns it).
        _cachedIsActive = ColorFilters.ReadIsActiveFromKey(key);

        // RegistryKeyChangeWatcher takes ownership of the key and disposes it on its own Dispose.
        _colorFilteringKeyWatcher = new Morphic.WindowsNative.Registry.OpenedRegistryKeyChangeWatcher(key);
        _colorFilteringKeyWatcher.Changed += ColorFilters.OnColorFilteringKeyChanged;
    }

    // Pre-requisite: caller MUST hold _colorFilteringKeyWatcherLock.
    private static void StopWatcherIfNoSubscribersLocked()
    {
        if (_isActiveChanged is not null)
        {
            return;
        }

        _colorFilteringKeyWatcher?.Dispose();
        _colorFilteringKeyWatcher = null;
    }

    // RegistryKeyChangeWatcher.Changed callback. Re-reads the Active value, compares to cache,
    // fires IsActiveChanged ONLY if it actually changed (delivering the new bool via
    // ColorFiltersIsActiveChangedEventArgs).
    private static void OnColorFilteringKeyChanged(object? sender, EventArgs e)
    {
        bool newIsActive;
        EventHandler<ColorFiltersIsActiveChangedEventArgs>? handlersToFire = null;

        // Hold the lock across read+compare+cache-update so concurrent OnColorFilteringKeyChanged
        // invocations don't race the cache. (ThreadPool.RegisterWaitForSingleObject with
        // executeOnlyOnce=false can re-fire the callback on a new Task before the previous one
        // returns, if registry changes arrive rapidly.) Re-open the key here instead of holding
        // one open between fires -- registry reads are cheap and it keeps the watcher's "I own
        // the key" invariant intact.
        lock (_colorFilteringKeyWatcherLock)
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(ColorFilters.COLOR_FILTERING_REGISTRY_KEY_PATH);
            if (key is null)
            {
                return;
            }
            newIsActive = ColorFilters.ReadIsActiveFromKey(key);

            if (newIsActive == _cachedIsActive)
            {
                return;
            }
            _cachedIsActive = newIsActive;
            handlersToFire = _isActiveChanged;
        }

        // Dispatch outside the lock so a handler that subscribes/unsubscribes can't deadlock.
        // Each handler runs on its own Task so a slow/throwing handler doesn't block the others.
        // Sender is null -- ColorFilters is a static class with no instance.
        if (handlersToFire is not null)
        {
            foreach (EventHandler<ColorFiltersIsActiveChangedEventArgs> handler in handlersToFire.GetInvocationList())
            {
                Task.Run(() => handler.Invoke(null, new ColorFiltersIsActiveChangedEventArgs(newIsActive)));
            }
        }
    }

    // Internal helper for the change-event path: returns true iff Active=1 in the key. Missing
    // or unexpected-type values are treated as false (inactive), matching the Windows default
    // when the user has not explicitly enabled color filtering. Differs from GetIsActive() in
    // that this never returns null -- the change-event cache always has a concrete bool.
    private static bool ReadIsActiveFromKey(Microsoft.Win32.RegistryKey key)
    {
        var rawActiveValue = key.GetValue(ColorFilters.ACTIVE_REGISTRY_VALUE_NAME);
        if (rawActiveValue is int activeAsInt)
        {
            return activeAsInt != 0;
        }
        return false;
    }

    //

    // NOTE: these color filter types are current as of Windows 11 v22H2
    public enum FilterType : uint
    {
        Greyscale = 0,
        Invert = 1,
        GreyscaleInverted = 2,
        Deuteranopia = 3,
        Protanopia = 4,
        Tritanopia = 5
    }
    public static MorphicResult<FilterType?, IWin32ApiError> GetFilterType()
    {
        var openKeyResult = Morphic.WindowsNative.Registry.CurrentUser.OpenSubKey(COLOR_FILTERING_REGISTRY_KEY_PATH);
        if (openKeyResult.IsError == true)
        {
            switch (openKeyResult.Error!)
            {
                case IWin32ApiError.Win32Error(Win32ErrorCode: var win32ErrorCode):
                    var win32ApiError = new IWin32ApiError.Win32Error(unchecked((uint)win32ErrorCode));
                    return MorphicResult.ErrorResult<IWin32ApiError>(win32ApiError);
                default:
                    throw new MorphicUnhandledErrorException();
            }
        }
        var colorFilteringKey = openKeyResult.Value!;

        // get the current light theme settings for both apps and the system
        FilterType? filterType = null;
        var getValueResult = colorFilteringKey.GetValueDataOrNull<uint>("FilterType");
        if (getValueResult.IsError == true)
        {
            switch (getValueResult.Error!)
            {
                case Registry.RegistryKey.IRegistryGetValueError.Win32Error(Win32ErrorCode: var win32ErrorCode):
                    return MorphicResult.ErrorResult<IWin32ApiError>(new IWin32ApiError.Win32Error(unchecked((uint)win32ErrorCode!)));
                case Registry.RegistryKey.IRegistryGetValueError.TypeMismatch:
                case Registry.RegistryKey.IRegistryGetValueError.UnsupportedType:
                default:
                    throw new MorphicUnhandledErrorException();
            }
        }
        var filterTypeAsUInt32 = getValueResult.Value;
        if (filterTypeAsUInt32 is not null)
        {
            filterType = (FilterType)filterTypeAsUInt32;
        }

        return MorphicResult.OkResult(filterType);
    }
}