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
// state (true = filter on, false = filter off). NewValue follows the BCL convention.
public class ColorFiltersIsActiveChangedEventArgs(bool newValue) : EventArgs
{
    public bool NewValue { get; } = newValue;
}

public class ColorFilters
{
    private const string COLOR_FILTERING_REGISTRY_KEY_PATH = @"SOFTWARE\Microsoft\ColorFiltering";
    private const string ACTIVE_REGISTRY_VALUE_NAME = "Active";

    private static readonly object _colorFilteringKeyWatcherLock = new();
    private static Morphic.WindowsNative.Registry.RegistryKeyChangeWatcher? _colorFilteringKeyWatcher;
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

    //

    // Change-notification event
    //
    // Backed by a RegistryKeyChangeWatcher on HKCU\SOFTWARE\Microsoft\ColorFiltering; we
	// compare the new value to the latest-known value cached in _cachedInActive and fire
    // IsActiveChanged only when it actually changed -- delivering the new value (as a bool)
	// via event args.
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

        // Seed the initial cached value BEFORE wiring the watcher's Changed handler so the first
        // change-fire has a baseline to compare against. The key may not exist yet (the user has
        // never toggled color filtering, or some process has deleted the key); a missing key
        // MIGHT mean "filter is off" -- or might mean "registry deleted but the OS service still
        // has the filter on" (Windows feature services often cache their state in memory; the
        // registry value is just a representation). The synchronous seed assumes "off" for now;
        // the fire-and-forget refinement Task below queries SettingItemProxy to find the
        // actually-current state and updates the cache (firing IsActiveChanged) if it differs.
        using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(ColorFilters.COLOR_FILTERING_REGISTRY_KEY_PATH))
        {
            _cachedIsActive = key is null ? false : ColorFilters.ReadIsActiveFromKey(key);
        }

        var watcherCreateResult = Morphic.WindowsNative.Registry.RegistryKeyChangeWatcher.CreateForPath(
            Microsoft.Win32.RegistryHive.CurrentUser,
            ColorFilters.COLOR_FILTERING_REGISTRY_KEY_PATH);
        if (watcherCreateResult.IsError)
        {
            // Argument-validation failure -- means our constants are wrong (unsupported hive,
            // path with null chars). The only fix is a code change; assert in debug builds so
            // the cause is obvious during development, then leave the watcher null so the next
            // subscription cycle has a chance to retry (in case the failure mode is transient).
            Debug.Assert(false, $"Could not create ColorFilters key watcher: {watcherCreateResult.Error}");
            return;
        }
        _colorFilteringKeyWatcher = watcherCreateResult.Value!;
        _colorFilteringKeyWatcher.Changed += ColorFilters.OnColorFilteringKeyChanged;

        // Kick off the SettingItem-fallback refinement on a background Task. If the registry seed
        // was wrong (registry deleted but feature still on), this updates the cache to the live
        // state via SettingItemProxy and fires IsActiveChanged so subscribers see the correction.
        // No-op when the registry seed agrees with SettingItem (the common case).
        _ = Task.Run(() => ColorFilters.RecomputeAndUpdateCachedIsActiveAsync(TimeSpan.FromSeconds(2)));
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

    // RegistryKeyChangeWatcher.Changed callback. Routes through the unified compute-and-update
    // helper so registry-missing transitions get the SettingItem fallback applied. The event
    // kind (ValueChanged / TargetCreated / TargetDeleted) doesn't change the work we do here
    // -- the helper re-reads from the most authoritative source available regardless of why
    // the watcher fired.
    private static async void OnColorFilteringKeyChanged(object? sender, Morphic.WindowsNative.Registry.RegistryKeyChangedEventArgs e)
    {
        try
        {
            await ColorFilters.RecomputeAndUpdateCachedIsActiveAsync(TimeSpan.FromSeconds(2));
        }
        catch (Exception ex)
        {
            // async void: exceptions here would otherwise escape to the synchronization context
            // (and propagate to TaskScheduler.UnobservedTaskException via the WhenAny machinery).
            // Log instead so a bad SettingItem read doesn't kill the dispatcher.
            Debug.WriteLine($"OnColorFilteringKeyChanged threw: {ex}");
        }
    }

    // Computes the current "filter is active" state from the most authoritative source available
    // and updates _cachedIsActive accordingly. If the cached value changed, fires IsActiveChanged
    // with the new value.
    //
    // Source priority:
    //   1. Registry HKCU\SOFTWARE\Microsoft\ColorFiltering\Active -- when the key exists, this
    //      is the canonical source.
    //   2. SettingItemProxy -- when the registry key is missing, the OS may still have the
    //      feature on (services cache state in memory; registry value is a representation that
    //      can be deleted without changing the actual setting). SettingItem queries the live
    //      OS state, so it can correct the registry's "appears off because key missing" picture.
    //   3. Default `false` -- if both sources fail (e.g. SettingItem times out or also returns
    //      null), fall back to the Windows default.
    //
    // Lock discipline: the async SettingItem read happens OUTSIDE the lock; only the
    // cache-update + handler-snapshot happens under lock; the handler dispatch happens after
    // releasing the lock. Same pattern as NightLight.SettingItem_ValueChanged.
    private static async Task RecomputeAndUpdateCachedIsActiveAsync(TimeSpan settingItemFallbackTimeout)
    {
        // Step 1: read registry (sync, fast).
        bool? valueFromRegistry;
        using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(ColorFilters.COLOR_FILTERING_REGISTRY_KEY_PATH))
        {
            valueFromRegistry = key is null ? (bool?)null : ColorFilters.ReadIsActiveFromKey(key);
        }

        // Step 2: if registry didn't have a value, fall back to SettingItem (async).
        bool computedValue;
        if (valueFromRegistry is not null)
        {
            computedValue = valueFromRegistry.Value;
        }
        else
        {
            var settingItem = ColorFilters.ColorFilteringIsEnabledSettingItem;
            if (settingItem is null)
            {
                computedValue = false;
            }
            else
            {
                var settingItemReadResult = await Morphic.WindowsNative.SystemSettings.SettingItemProxy.GetSettingItemValueAsync<bool>(settingItem, settingItemFallbackTimeout);
                computedValue = settingItemReadResult.IsSuccess && settingItemReadResult.Value == true;
            }
        }

        // Step 3: update cache + snapshot handlers under lock; dispatch outside.
        EventHandler<ColorFiltersIsActiveChangedEventArgs>? handlersToFire = null;
        lock (_colorFilteringKeyWatcherLock)
        {
            if (computedValue == _cachedIsActive)
            {
                return;
            }
            _cachedIsActive = computedValue;
            handlersToFire = _isActiveChanged;
        }

        if (handlersToFire is not null)
        {
            var eventArgs = new ColorFiltersIsActiveChangedEventArgs(computedValue);
            foreach (EventHandler<ColorFiltersIsActiveChangedEventArgs> handler in handlersToFire.GetInvocationList())
            {
                _ = Task.Run(() => handler.Invoke(null, eventArgs));
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
}
