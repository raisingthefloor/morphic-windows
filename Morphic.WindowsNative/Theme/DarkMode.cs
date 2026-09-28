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
using Morphic.WindowsNative.SystemSettings;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Morphic.WindowsNative.Theme;

public class DarkModeChangedEventArgs(bool newValue) : EventArgs
{
    // The new "uses dark mode" state (true = dark, false = light). "NewValue" follows the BCL
    // convention (WinUI's IsEnabledChangedEventArgs.NewValue, WPF's 
	// RoutedPropertyChangedEventArgs.NewValue) and leaves room to add OldValue alongside it later 
	// without renaming.
    public bool NewValue { get; } = newValue;
}

public class DarkMode
{
    // System Setting Ids (for SettingItem settings)
    public static class SystemSettingIds
    {
        public const string APPS_USE_LIGHT_THEME = "SystemSettings_Personalize_Color_AppsUseLightTheme";
        public const string SYSTEM_USES_LIGHT_THEME = "SystemSettings_Personalize_Color_SystemUsesLightTheme";
        public const string SYSTEM_THEME = "SystemSettings_Personalize_Color_SystemTheme";
    }

    private const string PERSONALIZE_REGISTRY_KEY_PATH = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string APPS_USE_LIGHT_THEME_REGISTRY_VALUE_NAME = "AppsUseLightTheme";
    private const string SYSTEM_USES_LIGHT_THEME_REGISTRY_VALUE_NAME = "SystemUsesLightTheme";

    private static readonly object _personalizeKeyWatcherLock = new();
    private static Morphic.WindowsNative.Registry.OpenedRegistryKeyChangeWatcher? _personalizeKeyWatcher;

    private static bool _cachedAppsUseDarkMode;
    private static bool _cachedSystemUsesDarkMode;
    private static EventHandler<DarkModeChangedEventArgs>? _appsUseDarkModeChanged;
    private static EventHandler<DarkModeChangedEventArgs>? _systemUsesDarkModeChanged;

    private static SettingItemProxy? _appsUseLightThemeSettingItem;
    private static SettingItemProxy? AppsUseLightThemeSettingItem
    {
        get
        {
            if (_appsUseLightThemeSettingItem is null)
            {
                _appsUseLightThemeSettingItem = SettingsDatabaseProxy.GetSettingItemOrNull(DarkMode.SystemSettingIds.APPS_USE_LIGHT_THEME);
            }

            return _appsUseLightThemeSettingItem;
        }
    }
    //private const string APPS_USE_LIGHT_THEME_VALUE_NAME = "Value";
    //
    //
    //
    private static SettingItemProxy? _systemUsesLightThemeSettingItem;
    private static SettingItemProxy? SystemUsesLightThemeSettingItem
    {
        get
        {
            if (_systemUsesLightThemeSettingItem is null)
            {
                _systemUsesLightThemeSettingItem = SettingsDatabaseProxy.GetSettingItemOrNull(DarkMode.SystemSettingIds.SYSTEM_USES_LIGHT_THEME);
            }

            return _systemUsesLightThemeSettingItem;
        }
    }
    //private const string SYSTEM_USES_LIGHT_THEME_VALUE_NAME = "Value";
    //
    //
    //
    private static SettingItemProxy? _systemThemeSettingItem;
    private static SettingItemProxy? SystemThemeSettingItem
    {
        get
        {
            if (_systemThemeSettingItem is null)
            {
                _systemThemeSettingItem = SettingsDatabaseProxy.GetSettingItemOrNull(DarkMode.SystemSettingIds.SYSTEM_THEME);
            }

            return _systemThemeSettingItem;
        }
    }
    //private const string SYSTEM_THEME_VALUE_NAME = "Value";

    //

    public static MorphicResult<bool?, MorphicUnit> GetAppsUseDarkMode()
    {
        Microsoft.Win32.RegistryKey? personalizeKey;
        try
        {
            personalizeKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", writable: false);
        }
        catch
        {
            return MorphicResult.ErrorResult();
        }
        if (personalizeKey is null)
        {
            return MorphicResult.OkResult<bool?>(null);
        }

        // get the current light theme settings for apps
        bool? appsUseLightThemeAsBool = null;
        var appsUseLightThemeAsNullableObject = personalizeKey.GetValue("AppsUseLightTheme");
        if (appsUseLightThemeAsNullableObject is null)
        {
            return MorphicResult.OkResult<bool?>(null);
        }
        else if (appsUseLightThemeAsNullableObject is int appsUseLightThemeAsInt)
        {
            appsUseLightThemeAsBool = (appsUseLightThemeAsInt != 0) ? true : false;
        }
        else
        {
            return MorphicResult.ErrorResult();
        }

        // dark mode states are the inverse of AppsUseLightTheme/SystemUsesLightTheme
        bool? result = appsUseLightThemeAsBool is null ? null : !appsUseLightThemeAsBool;

        return MorphicResult.OkResult(result);
    }

    public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> SetAppsUseDarkModeAndBroadcastChangeMessageAsync(bool value, TimeSpan? timeout = null)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        var setAppsUseDarkModeResult = await DarkMode.SetAppsUseDarkModeAsync(value, timeout);
        if (setAppsUseDarkModeResult.IsError)
        {
            return MorphicResult.ErrorResult();
        }

        TimeSpan? remainingTimeout = null;
        if (timeout is not null)
        {
            remainingTimeout = timeout!.Value.Subtract(new TimeSpan(stopwatch.ElapsedMilliseconds));
        }

        var broadcastChangeMessageResult = await DarkMode.BroadcastChangeMessageAsync(remainingTimeout);
        if (broadcastChangeMessageResult.IsError)
        {
            return MorphicResult.ErrorResult();
        }

        return MorphicResult.OkResult();
    }

    public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> SetAppsUseDarkModeAsync(bool value, TimeSpan? timeout = null)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        // NOTE: we invert the caller's supplied argument (since the caller is setting the 'uses dark mode' value, but we need to set the corresponding 'uses light mode' value)
        var appsUseLightThemeSettingItem = DarkMode.AppsUseLightThemeSettingItem;
        if (appsUseLightThemeSettingItem is null)
        {
            return MorphicResult.ErrorResult();
        }
        //
        var setValueResult = await SettingItemProxy.SetSettingItemValueAsync<bool>(appsUseLightThemeSettingItem, /*DarkMode.APPS_USE_LIGHT_THEME_VALUE_NAME, */!value, timeout);
        if (setValueResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }

        return MorphicResult.OkResult();
    }

    //

    public static MorphicResult<bool?, MorphicUnit> GetSystemUsesDarkMode()
    {
        Microsoft.Win32.RegistryKey? personalizeKey;
        try
        {
            personalizeKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", writable: false);
        }
        catch
        {
            return MorphicResult.ErrorResult();
        }
        if (personalizeKey is null)
        {
            return MorphicResult.OkResult<bool?>(null);
        }

        // get the current light theme setting for Windows (i.e. the system light theme setting)
        bool? systemUsesLightThemeAsBool = null;
        var isEqualOrNewerThanVersionResult = Morphic.WindowsNative.OsVersion.OsVersion.IsEqualOrNewerThanVersion(WindowsNative.OsVersion.WindowsVersion.Win11_v23H2, 4037 /* not required in build 22631.3447, but required in build 22631.4037 */);
        if (isEqualOrNewerThanVersionResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        var isEqualOrNewerThanVersion = isEqualOrNewerThanVersionResult.Value!;
        if (isEqualOrNewerThanVersion == true)
        {
            // Windows 11 v23H2 revision 4037+, Windows 11 v24H2+

            var systemThemeAsNullableObject = personalizeKey.GetValue("SystemTheme");
            if (systemThemeAsNullableObject is null)
            {
                return MorphicResult.OkResult<bool?>(null);
            }
            else if (systemThemeAsNullableObject is string systemThemeAsString)
            {
                // NOTE: if the theme name is not recognized, TryConvertSystemThemeNameToDarkModeState will return null
                bool? systemThemeIsDarkMode = DarkMode.TryConvertSystemThemeNameToDarkModeState(systemThemeAsString);
                if (systemThemeIsDarkMode is not null)
                { 
                    systemUsesLightThemeAsBool = systemThemeIsDarkMode;
                }
                else
                {
                    return MorphicResult.ErrorResult();
                }
            }
            else
            {
                return MorphicResult.ErrorResult();
            }
        }
        else
        {
            // Windows 10 1903+

            var systemUsesLightThemeAsNullableObject = personalizeKey.GetValue("SystemUsesLightTheme");
            if (systemUsesLightThemeAsNullableObject is null)
            {
                return MorphicResult.OkResult<bool?>(null);
            }
            if (systemUsesLightThemeAsNullableObject is int systemUsesLightThemeAsInt)
            {
                systemUsesLightThemeAsBool = (systemUsesLightThemeAsInt != 0) ? true : false;
            }
            else
            {
                return MorphicResult.ErrorResult();
            }
        }

        // dark mode states are the inverse of AppsUseLightTheme/SystemUsesLightTheme
        bool? result = systemUsesLightThemeAsBool is null ? null : !systemUsesLightThemeAsBool;

        return MorphicResult.OkResult(result);
    }

    public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> SetSystemUsesDarkModeAndBroadcastChangeMessageAsync(bool value, TimeSpan? timeout = null)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        var setSystemUsesDarkModeResult = await DarkMode.SetSystemUsesDarkModeAsync(value, timeout);
        if (setSystemUsesDarkModeResult.IsError)
        {
            return MorphicResult.ErrorResult();
        }

        TimeSpan? remainingTimeout = null;
        if (timeout is not null)
        {
            remainingTimeout = timeout!.Value.Subtract(new TimeSpan(stopwatch.ElapsedMilliseconds));
        }

        var broadcastChangeMessageResult = await DarkMode.BroadcastChangeMessageAsync(remainingTimeout);
        if (broadcastChangeMessageResult.IsError)
        {
            return MorphicResult.ErrorResult();
        }

        return MorphicResult.OkResult();
    }

    public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> SetSystemUsesDarkModeAsync(bool value, TimeSpan? timeout = null)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        var isEqualOrNewerThanVersionResult = Morphic.WindowsNative.OsVersion.OsVersion.IsEqualOrNewerThanVersion(WindowsNative.OsVersion.WindowsVersion.Win11_v23H2, 4037 /* not required in build 22631.3447, but required in build 22631.4037 */);
        if (isEqualOrNewerThanVersionResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        var isEqualOrNewerThanVersion = isEqualOrNewerThanVersionResult.Value!;
        if (isEqualOrNewerThanVersion == true)
        {
            // Windows 11 v23H2 revision 4037+, Windows 11 v24H2+

            var themeName = value switch
            {
                true => "Dark",
                false => "Light"
            };
            var systemThemeSettingItem = DarkMode.SystemThemeSettingItem;
            if (systemThemeSettingItem is null)
            {
                return MorphicResult.ErrorResult();
            }
            //
            var setValueResult = await SettingItemProxy.SetSettingItemValueAsync<string>(systemThemeSettingItem, themeName, timeout);
            if (setValueResult.IsError == true)
            {
                return MorphicResult.ErrorResult();
            }
        }
        else
        {
            // Windows 10 1903+

            var systemUsesLightThemeSettingItem = DarkMode.SystemUsesLightThemeSettingItem;
            if (systemUsesLightThemeSettingItem is null)
            {
                return MorphicResult.ErrorResult();
            }
            //
            var setValueResult = await SettingItemProxy.SetSettingItemValueAsync<bool>(systemUsesLightThemeSettingItem, /*DarkMode.SYSTEM_USES_LIGHT_THEME_VALUE_NAME, */!value, timeout);
            if (setValueResult.IsError == true)
            {
                return MorphicResult.ErrorResult();
            }
        }

        return MorphicResult.OkResult();
    }

    //

    // NOTE: this function handles system themes as written at: HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\SystemTheme (as string)
    // this function returns null if the provided theme is not recognized as either light or dark
    public static bool? TryConvertSystemThemeNameToDarkModeState(string systemTheme)
    {
        if (systemTheme is null)
        {
            throw new ArgumentNullException(nameof(systemTheme));
        }

        bool systemUsesLightThemeAsBool;

        switch (systemTheme)
        {
            case "Light":
                systemUsesLightThemeAsBool = true;
                break;
            case "Dark":
                systemUsesLightThemeAsBool = false;
                break;
            default:
                // NOTE: if "Contrast" or another theme were ever returned, we might need to return a different result
                Debug.Assert(false, "SystemTheme is neither Light nor Dark; this is an unexpected reponse; we are assuming 'light' mode -- but we should return an 'unknown theme' result ideally.");
                return null;
        }

        // NOTE: dark mode state is the inverse of SystemTheme's light state
        return !systemUsesLightThemeAsBool;
    }

    //

    public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> BroadcastChangeMessageAsync(TimeSpan? timeout = null)
    {
        // broadcast a message that the "INI" (registry) setting has been changed for dark mode
        MorphicResult<MorphicUnit, MorphicUnit> broadcastMessageResult;
        if (timeout is not null)
        {
            broadcastMessageResult = await DarkMode.BroadcastImmersiveColorSetMessageAsync().WaitAsync(timeout.Value!);
        }
        else
        {
            broadcastMessageResult = await DarkMode.BroadcastImmersiveColorSetMessageAsync();
        }
        if (broadcastMessageResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }

        return MorphicResult.OkResult();
    }

    private static async Task<MorphicResult<MorphicUnit, MorphicUnit>> BroadcastImmersiveColorSetMessageAsync(uint? timeoutPerTopLevelWindowInMilliseconds = null)
    {
        if (timeoutPerTopLevelWindowInMilliseconds is null)
        {
            // NOTE: This default timeout period is arbitrary; we may want to tune it
            uint DEFAULT_TIMEOUT_PER_TOP_LEVEL_WINDOW_IN_MILLISECONDS = 1000;
            timeoutPerTopLevelWindowInMilliseconds = DEFAULT_TIMEOUT_PER_TOP_LEVEL_WINDOW_IN_MILLISECONDS;
        }

        // see: https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-settingchange
        var pointerToImmersiveColorSetString = Marshal.StringToHGlobalUni("ImmersiveColorSet");
        bool success;
        try
        {
            // notify all windows that we have changed a setting
            // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendmessagetimeoutw
            UIntPtr messageProcessingResult;
            var sendMessageTimeoutResult = await Task.Run(() =>
            {
                // NOTE: SendMessageTimeout can return 0 when the error is generic; see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendmessagetimeoutw
                System.Runtime.InteropServices.Marshal.SetLastPInvokeError(0);

                return Windows.Win32.PInvoke.SendMessageTimeout(
                    Windows.Win32.Foundation.HWND.HWND_BROADCAST,
                    Windows.Win32.PInvoke.WM_WININICHANGE,
                    (Windows.Win32.Foundation.WPARAM)0,
                    pointerToImmersiveColorSetString,
                    Windows.Win32.UI.WindowsAndMessaging.SEND_MESSAGE_TIMEOUT_FLAGS.SMTO_ABORTIFHUNG,
                    timeoutPerTopLevelWindowInMilliseconds!.Value,
                    out messageProcessingResult);
            });
            if (sendMessageTimeoutResult != IntPtr.Zero)
            {
                // succeeded, did not time out
                // NOTE: we currently ignore the result of the windows message captured `messageProcessingResult`; this result is specific to WM_WININICHANGE
                success = true;
            }
            else
            {
                // failed, timed out, etc.
                var win32ErrorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                if (win32ErrorCode != 0)
                {
                    // specific error
                    // [we may filter based on the the errors, such as timeout, if desired]
                }
                else
                {
                    // generic error
                }
                success = false;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(pointerToImmersiveColorSetString);
        }

        return success ? MorphicResult.OkResult() : MorphicResult.ErrorResult();
    }

    //

    // Change-notification events
    //
    // Backed by a single RegistryKeyChangeWatcher on
    // HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize. On any change to the
    // key we re-read both dark-mode values, compare them to a cached copy, and fire each event
    // ONLY when its corresponding value actually changed -- delivering the new value (as a bool)
    // via EventHandler<DarkModeChangedEventArgs>. Subscribers get a precise signal with no need to re-query, and no
    // spurious fires for unrelated value changes inside the same key.
    //
    // Naming note: the registry stores `*UseLightTheme` (1=light, 0=dark), but the events expose
    // the inverted semantic ("uses dark mode") to match the bar action and the v1.x API. The
    // ReadDarkModeFromValue helper does the inversion.
    //
    // Threading: handlers fire from the watcher's ThreadPool callback (NOT the UI thread).
    // Subscribers that touch UI must marshal back to their dispatcher.
    //
    // Lifecycle: the watcher starts lazily on the first subscription (across either event), and
    // tears down when the last subscriber detaches. No registry handle is held while there are
    // no subscribers.

    public static event EventHandler<DarkModeChangedEventArgs> AppsUseDarkModeChanged
    {
        add
        {
            lock (_personalizeKeyWatcherLock)
            {
                DarkMode.EnsureWatcherStartedLocked();
                _appsUseDarkModeChanged += value;
            }
        }
        remove
        {
            lock (_personalizeKeyWatcherLock)
            {
                _appsUseDarkModeChanged -= value;
                DarkMode.StopWatcherIfNoSubscribersLocked();
            }
        }
    }

    public static event EventHandler<DarkModeChangedEventArgs> SystemUsesDarkModeChanged
    {
        add
        {
            lock (_personalizeKeyWatcherLock)
            {
                DarkMode.EnsureWatcherStartedLocked();
                _systemUsesDarkModeChanged += value;
            }
        }
        remove
        {
            lock (_personalizeKeyWatcherLock)
            {
                _systemUsesDarkModeChanged -= value;
                DarkMode.StopWatcherIfNoSubscribersLocked();
            }
        }
    }

    // Pre-requisite: caller MUST hold _watcherLock.
    private static void EnsureWatcherStartedLocked()
    {
        if (_personalizeKeyWatcher is not null)
        {
            return;
        }

        // Open via the BCL Microsoft.Win32.Registry. Default permissions (KEY_READ) include
        // KEY_NOTIFY (required by RegistryKeyChangeWatcher) and KEY_QUERY_VALUE (required for the
        // initial-state read below), so no explicit-rights overload is needed. OpenSubKey returns
        // null if the key is missing -- vanishingly unlikely for this OS-owned key, but defended.
        var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(DarkMode.PERSONALIZE_REGISTRY_KEY_PATH);
        if (key is null)
        {
            Debug.Assert(false, "Personalize registry key could not be opened for change notifications");
            return;
        }

        // Cache the initial values BEFORE wiring the watcher's Changed handler, so the first
        // change-fire has a baseline to compare against. Read from this same open key while we
        // still have direct access (after we hand it to the watcher, the watcher owns it).
        _cachedAppsUseDarkMode = DarkMode.ReadDarkModeFromValue(key, DarkMode.APPS_USE_LIGHT_THEME_REGISTRY_VALUE_NAME);
        _cachedSystemUsesDarkMode = DarkMode.ReadDarkModeFromValue(key, DarkMode.SYSTEM_USES_LIGHT_THEME_REGISTRY_VALUE_NAME);

        // RegistryKeyChangeWatcher takes ownership of the key and disposes it on its own Dispose.
        _personalizeKeyWatcher = new Morphic.WindowsNative.Registry.OpenedRegistryKeyChangeWatcher(key);
        _personalizeKeyWatcher.Changed += DarkMode.OnPersonalizeKeyChanged;
    }

    // Pre-requisite: caller MUST hold _watcherLock.
    private static void StopWatcherIfNoSubscribersLocked()
    {
        if (_appsUseDarkModeChanged is not null || _systemUsesDarkModeChanged is not null)
        {
            return;
        }

        _personalizeKeyWatcher?.Dispose();
        _personalizeKeyWatcher = null;
    }

    // RegistryKeyChangeWatcher.Changed callback. Re-reads both Personalize values, compares to
    // the cache, fires per-event ONLY for values that actually changed (delivering the new bool
    // via EventHandler<DarkModeChangedEventArgs>).
    private static void OnPersonalizeKeyChanged(object? sender, EventArgs e)
    {
        bool newAppsUseDarkMode;
        bool newSystemUsesDarkMode;
        EventHandler<DarkModeChangedEventArgs>? appsDarkModeHandlersToFire = null;
        EventHandler<DarkModeChangedEventArgs>? systemDarkModeHandlersToFire = null;

        // Hold the lock across read+compare+cache-update so concurrent OnPersonalizeKeyChanged
        // invocations don't race the cache. (ThreadPool.RegisterWaitForSingleObject with
        // executeOnlyOnce=false can re-fire the callback on a new Task before the previous one
        // returns, if registry changes arrive rapidly.) Re-open the key here instead of holding
        // one open between fires -- registry reads are cheap and it keeps the watcher's "I own
        // the key" invariant intact.
        lock (_personalizeKeyWatcherLock)
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(DarkMode.PERSONALIZE_REGISTRY_KEY_PATH);
            if (key is null)
            {
                return;
            }
            newAppsUseDarkMode = DarkMode.ReadDarkModeFromValue(key, DarkMode.APPS_USE_LIGHT_THEME_REGISTRY_VALUE_NAME);
            newSystemUsesDarkMode = DarkMode.ReadDarkModeFromValue(key, DarkMode.SYSTEM_USES_LIGHT_THEME_REGISTRY_VALUE_NAME);

            if (newAppsUseDarkMode != _cachedAppsUseDarkMode)
            {
                _cachedAppsUseDarkMode = newAppsUseDarkMode;
                appsDarkModeHandlersToFire = _appsUseDarkModeChanged;
            }
            if (newSystemUsesDarkMode != _cachedSystemUsesDarkMode)
            {
                _cachedSystemUsesDarkMode = newSystemUsesDarkMode;
                systemDarkModeHandlersToFire = _systemUsesDarkModeChanged;
            }
        }

        // Dispatch outside the lock so a handler that subscribes/unsubscribes can't deadlock.
        // Each handler runs on its own Task so a slow/throwing handler doesn't block the others.
        // Sender is null -- DarkMode is a static class with no instance.
        if (appsDarkModeHandlersToFire is not null)
        {
            foreach (EventHandler<DarkModeChangedEventArgs> handler in appsDarkModeHandlersToFire.GetInvocationList())
            {
                Task.Run(() => handler.Invoke(null, new DarkModeChangedEventArgs(newAppsUseDarkMode)));
            }
        }
        if (systemDarkModeHandlersToFire is not null)
        {
            foreach (EventHandler<DarkModeChangedEventArgs> handler in systemDarkModeHandlersToFire.GetInvocationList())
            {
                Task.Run(() => handler.Invoke(null, new DarkModeChangedEventArgs(newSystemUsesDarkMode)));
            }
        }
    }

    // Reads a Personalize-key value where 1 = light theme, 0 = dark theme. Missing or
    // unexpected-type values are treated as light (not dark), matching the Windows default when
    // the user has not explicitly set a theme. The bool we return is the INVERTED semantic ("uses
    // dark mode") so callers don't have to think about LightTheme vs. DarkMode every time.
    private static bool ReadDarkModeFromValue(Microsoft.Win32.RegistryKey key, string valueName)
    {
        var raw = key.GetValue(valueName);
        if (raw is int intValue)
        {
            return intValue == 0;
        }
        return false;
    }
}