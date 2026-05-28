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

        using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(ColorFilters.COLOR_FILTERING_REGISTRY_KEY_PATH))
        {
            _cachedIsActive = key is null ? false : ColorFilters.ReadIsActiveFromKey(key);
        }

        var watcherCreateResult = Morphic.WindowsNative.Registry.RegistryKeyChangeWatcher.CreateForPath(
            Microsoft.Win32.RegistryHive.CurrentUser,
            ColorFilters.COLOR_FILTERING_REGISTRY_KEY_PATH);
        if (watcherCreateResult.IsError)
        {
            Debug.Assert(false, $"Could not create ColorFilters key watcher: {watcherCreateResult.Error}");
            return;
        }
        _colorFilteringKeyWatcher = watcherCreateResult.Value!;
        _colorFilteringKeyWatcher.Changed += ColorFilters.OnColorFilteringKeyChanged;

        _ = Task.Run(() => ColorFilters.RecomputeAndUpdateCachedIsActiveAsync(TimeSpan.FromSeconds(2)));
    }

    private static void StopWatcherIfNoSubscribersLocked()
    {
        if (_isActiveChanged is not null)
        {
            return;
        }

        _colorFilteringKeyWatcher?.Dispose();
        _colorFilteringKeyWatcher = null;
    }

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
