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
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Morphic.WindowsNative.Theme;

public class DarkModeChangedEventArgs(bool newValue) : EventArgs
{
    // The new "uses dark mode" state (true = dark, false = light). "NewValue" follows the BCL convention
    public bool NewValue { get; } = newValue;
}

public class DarkMode
{
    private static readonly object _personalizeKeyWatcherLock = new();
    private static Morphic.WindowsNative.Registry.RegistryKeyChangeWatcher? _personalizeKeyWatcher;

    private static bool _cachedAppsUseDarkMode;
    private static bool _cachedSystemUsesDarkMode;
    private static EventHandler<DarkModeChangedEventArgs>? _appsUseDarkModeChanged;
    private static EventHandler<DarkModeChangedEventArgs>? _systemUsesDarkModeChanged;

    //

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
    private const string SYSTEM_THEME_REGISTRY_VALUE_NAME = "SystemTheme";

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
        //
        if (personalizeKey is null)
        {
            return MorphicResult.OkResult<bool?>(null);
        }

        object? rawAppsUseLightThemeValue;
        try
        {
            rawAppsUseLightThemeValue = personalizeKey.GetValue("AppsUseLightTheme");
        }
        catch
        {
            return MorphicResult.ErrorResult();
        }
        finally
        {
            personalizeKey.Dispose();
        }
        //
        bool? appsUseLightThemeAsBool = null;
        if (rawAppsUseLightThemeValue is null)
        {
            return MorphicResult.OkResult<bool?>(null);
        }
        else if (rawAppsUseLightThemeValue is int appsUseLightThemeAsInt)
        {
            appsUseLightThemeAsBool = (appsUseLightThemeAsInt != 0) ? true : false;
        }
        else
        {
            return MorphicResult.ErrorResult();
        }

        // NOTE dark mode states are the inverse of AppsUseLightTheme/SystemUsesLightTheme
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
        // Open the Personalize key. We deliberately do NOT branch on OS version up front to
        // decide which value name to read -- instead, we try both formats below. The newer
        // SystemTheme (REG_SZ, "Light"/"Dark") was added around Win11 23H2 build 22631.4037,
        // but in practice some installations on newer versions only have the traditional value 
		// SystemUsesLightTheme (REG_DWORD) populated: so we need the fallback regardless of
        // version.
        Microsoft.Win32.RegistryKey? personalizeKey;
        try
        {
            personalizeKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(DarkMode.PERSONALIZE_REGISTRY_KEY_PATH, writable: false);
        }
        catch
        {
            return MorphicResult.ErrorResult();
        }
        //
        if (personalizeKey is null)
        {
            return MorphicResult.OkResult<bool?>(null);
        }
        //
        try
        {
            // Try the newer SystemTheme (REG_SZ "Light"/"Dark") first. If present and
            // recognized, it's authoritative.
            object? rawSystemTheme;
            try
            {
                rawSystemTheme = personalizeKey.GetValue(DarkMode.SYSTEM_THEME_REGISTRY_VALUE_NAME);
            }
            catch
            {
                return MorphicResult.ErrorResult();
            }
            if (rawSystemTheme is string systemThemeAsString)
            {
                bool? systemThemeIsDarkMode = DarkMode.TryConvertSystemThemeNameToDarkModeState(systemThemeAsString);
                if (systemThemeIsDarkMode is not null)
                {
                    return MorphicResult.OkResult<bool?>(systemThemeIsDarkMode);
                }
                // unrecognized theme name -- fall through to the int-format attempt below (while will return Error if that is not present)
            }

            // Fall back to the older SystemUsesLightTheme (REG_DWORD: 1 = light, 0 = dark).
            object? rawSystemUsesLightTheme;
            try
            {
                rawSystemUsesLightTheme = personalizeKey.GetValue(DarkMode.SYSTEM_USES_LIGHT_THEME_REGISTRY_VALUE_NAME);
            }
            catch
            {
                return MorphicResult.ErrorResult();
            }
            if (rawSystemUsesLightTheme is null)
            {
                // Neither value is set -- the user has never explicitly toggled the system theme.
                return MorphicResult.OkResult<bool?>(null);
            }
            if (rawSystemUsesLightTheme is int systemUsesLightThemeAsInt)
            {
                // Dark mode state is the inverse of the LightTheme value.
                return MorphicResult.OkResult<bool?>(systemUsesLightThemeAsInt == 0);
            }
            return MorphicResult.ErrorResult();
        }
        finally
        {
            personalizeKey.Dispose();
        }
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
            // Windows 11 v23H2 build 22631.4037+, Windows 11 v24H2+

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

    public static async Task<MorphicResult<MorphicUnit, IMorphicTimeoutError>> BroadcastChangeMessageAsync(TimeSpan? timeout = null)
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
            return MorphicResult.ErrorResult<IMorphicTimeoutError>(new IMorphicTimeoutError.Timeout());
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

        // Seed the initial cached values BEFORE wiring the watcher's Changed handler, so the
        // first change-fire has a baseline to compare against. The Personalize key is OS-owned
        // and effectively always present on a normal Windows install, but a missing key MIGHT
        // mean "neither preference set" -- or might mean "registry deleted but the OS theme
        // service still has dark mode on" (services often cache state in memory; the registry
        // value is just a representation that can be deleted without changing the actual state).
        // The synchronous seed below assumes "off" if the key is missing; the fire-and-forget
        // refinement Task that follows queries SettingItemProxy to find the actually-current
        // state and updates the cache (firing the appropriate *Changed event) if it differs.
        using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(DarkMode.PERSONALIZE_REGISTRY_KEY_PATH))
        {
            _cachedAppsUseDarkMode = key is null ? false : DarkMode.ReadAppsUseDarkModeFromKey(key);
            _cachedSystemUsesDarkMode = key is null ? false : DarkMode.ReadSystemUsesDarkModeFromKey(key);
        }

        var watcherCreateResult = Morphic.WindowsNative.Registry.RegistryKeyChangeWatcher.CreateForPath(
            Microsoft.Win32.RegistryHive.CurrentUser,
            DarkMode.PERSONALIZE_REGISTRY_KEY_PATH);
        if (watcherCreateResult.IsError)
        {
            // Argument-validation failure -- means our constants are wrong (unsupported hive,
            // path with null chars). The only fix is a code change; assert in debug builds so
            // the cause is obvious during development, then leave the watcher null so the next
            // subscription cycle has a chance to retry (in case the failure mode is transient).
            Debug.Assert(false, $"Could not create DarkMode key watcher: {watcherCreateResult.Error}");
            return;
        }
        _personalizeKeyWatcher = watcherCreateResult.Value!;
        _personalizeKeyWatcher.Changed += DarkMode.OnPersonalizeKeyChanged;

        // Kick off the SettingItem-fallback refinement on a background Task. If the registry
        // seed was wrong (registry deleted but theme still applied), this updates the cache to
        // the live state via SettingItemProxy and fires the appropriate *Changed events so
        // subscribers see the correction.
        _ = Task.Run(() => DarkMode.RecomputeAndUpdateCachedDarkModeAsync(TimeSpan.FromSeconds(2)));
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

    // RegistryKeyChangeWatcher.Changed callback. Routes through the unified compute-and-update
    // helper so registry-missing transitions get the SettingItem fallback applied. The event
    // kind (ValueChanged / TargetCreated / TargetDeleted) doesn't change the work we do here
    // -- the helper re-reads from the most authoritative source available regardless of why
    // the watcher fired.
    private static async void OnPersonalizeKeyChanged(object? sender, Morphic.WindowsNative.Registry.RegistryKeyChangedEventArgs e)
    {
        try
        {
            await DarkMode.RecomputeAndUpdateCachedDarkModeAsync(TimeSpan.FromSeconds(2));
        }
        catch (Exception ex)
        {
            // async void: exceptions here would otherwise escape to the synchronization context.
            // Log instead so a bad SettingItem read doesn't kill the dispatcher.
            Debug.WriteLine($"OnPersonalizeKeyChanged threw: {ex}");
        }
    }

    // Computes the current AppsUseDarkMode and SystemUsesDarkMode values from the most
    // authoritative source available and updates the caches accordingly. Fires the appropriate
    // *Changed events for each value that actually changed.
    //
    // Source priority (per value):
    //   1. Registry HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize -- when
    //      the key exists, this is the canonical source.
    //   2. SettingItemProxy -- when the registry key is missing, the OS may still have the
    //      themes applied (services cache state in memory; registry value is a representation
    //      that can be deleted without changing the actual setting). SettingItem queries the
    //      live OS state, so it can correct the registry's "appears default because key
    //      missing" picture.
    //   3. Default `false` -- if both sources fail, fall back to the Windows default.
    //
    // Lock discipline: the async SettingItem reads happen OUTSIDE the lock; only the cache
    // updates + handler snapshots happen under lock; the handler dispatch happens after
    // releasing the lock.
    private static async Task RecomputeAndUpdateCachedDarkModeAsync(TimeSpan settingItemFallbackTimeout)
    {
        // Step 1: read registry (sync, fast). Track whether each value came from the key.
        bool? appsUseDarkModeFromRegistry;
        bool? systemUsesDarkModeFromRegistry;
        using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(DarkMode.PERSONALIZE_REGISTRY_KEY_PATH))
        {
            appsUseDarkModeFromRegistry = key is null ? (bool?)null : DarkMode.ReadAppsUseDarkModeFromKey(key);
            systemUsesDarkModeFromRegistry = key is null ? (bool?)null : DarkMode.ReadSystemUsesDarkModeFromKey(key);
        }

        // Step 2: for whichever values the registry didn't provide, fall back to SettingItem.
        // Both reads run sequentially -- they share the same SettingsDatabase backing and the
        // second call is essentially free once the first has primed the proxy.
        bool computedAppsUseDarkMode;
        if (appsUseDarkModeFromRegistry is not null)
        {
            computedAppsUseDarkMode = appsUseDarkModeFromRegistry.Value;
        }
        else
        {
            computedAppsUseDarkMode = await DarkMode.ReadAppsUseDarkModeViaSettingItemAsync(settingItemFallbackTimeout);
        }
        //
        bool computedSystemUsesDarkMode;
        if (systemUsesDarkModeFromRegistry is not null)
        {
            computedSystemUsesDarkMode = systemUsesDarkModeFromRegistry.Value;
        }
        else
        {
            computedSystemUsesDarkMode = await DarkMode.ReadSystemUsesDarkModeViaSettingItemAsync(settingItemFallbackTimeout);
        }

        // Step 3: update caches + snapshot handlers under lock; dispatch outside.
        EventHandler<DarkModeChangedEventArgs>? appsDarkModeHandlersToFire = null;
        EventHandler<DarkModeChangedEventArgs>? systemDarkModeHandlersToFire = null;
        lock (_personalizeKeyWatcherLock)
        {
            if (computedAppsUseDarkMode != _cachedAppsUseDarkMode)
            {
                _cachedAppsUseDarkMode = computedAppsUseDarkMode;
                appsDarkModeHandlersToFire = _appsUseDarkModeChanged;
            }
            if (computedSystemUsesDarkMode != _cachedSystemUsesDarkMode)
            {
                _cachedSystemUsesDarkMode = computedSystemUsesDarkMode;
                systemDarkModeHandlersToFire = _systemUsesDarkModeChanged;
            }
        }

        // Dispatch outside the lock so a handler that subscribes/unsubscribes can't deadlock.
        // Each handler runs on its own Task so a slow/throwing handler doesn't block the others.
        bool newAppsUseDarkMode = computedAppsUseDarkMode;
        bool newSystemUsesDarkMode = computedSystemUsesDarkMode;

        // Dispatch outside the lock so a handler that subscribes/unsubscribes can't deadlock.
        // Each handler runs on its own Task so a slow/throwing handler doesn't block the others.
        // Sender is null -- DarkMode is a static class with no instance.
        if (appsDarkModeHandlersToFire is not null)
        {
            foreach (EventHandler<DarkModeChangedEventArgs> handler in appsDarkModeHandlersToFire.GetInvocationList())
            {
                _ = Task.Run(() => handler.Invoke(null, new DarkModeChangedEventArgs(newAppsUseDarkMode)));
            }
        }
        if (systemDarkModeHandlersToFire is not null)
        {
            foreach (EventHandler<DarkModeChangedEventArgs> handler in systemDarkModeHandlersToFire.GetInvocationList())
            {
                _ = Task.Run(() => handler.Invoke(null, new DarkModeChangedEventArgs(newSystemUsesDarkMode)));
            }
        }
    }

    // Reads the apps' "uses dark mode" state from an open Personalize key. The registry stores
    // AppsUseLightTheme as REG_DWORD (1 = light, 0 = dark); we return the inverted semantic
    // ("uses dark mode") so callers don't have to think about LightTheme vs. DarkMode every time.
    // A missing or unexpected-type value is treated as light (not dark), matching the Windows
    // default when the user has not explicitly set a theme.
    private static bool ReadAppsUseDarkModeFromKey(Microsoft.Win32.RegistryKey key)
    {
        var raw = key.GetValue(APPS_USE_LIGHT_THEME_REGISTRY_VALUE_NAME);
        if (raw is int intValue)
        {
            return intValue == 0;
        }
        return false;
    }

    // Reads the system's "uses dark mode" state from an open Personalize key. Tries the newer
    // SystemTheme value (REG_SZ, "Light" / "Dark") first -- Win11 v23H2 build 4037+ / v24H2+
    // exposes the system theme this way -- and falls back to SystemUsesLightTheme (REG_DWORD,
    // 1 = light, 0 = dark) for older Windows. Trying both in this order (rather than branching
    // on OS version) keeps the hot path simple and works across the whole supported range, and
    // it correctly handles versions where the OS writes both for back-compat. A missing or
    // unrecognized value falls through to "not dark" (the Windows default).
    private static bool ReadSystemUsesDarkModeFromKey(Microsoft.Win32.RegistryKey key)
    {
        var rawSystemTheme = key.GetValue(SYSTEM_THEME_REGISTRY_VALUE_NAME);
        if (rawSystemTheme is string systemThemeAsString)
        {
            bool? systemThemeIsDarkMode = DarkMode.TryConvertSystemThemeNameToDarkModeState(systemThemeAsString);
            if (systemThemeIsDarkMode is not null)
            {
                return systemThemeIsDarkMode.Value;
            }
            // unrecognized theme name -- fall through to the int-format attempt
        }

        var rawSystemUsesLightTheme = key.GetValue(SYSTEM_USES_LIGHT_THEME_REGISTRY_VALUE_NAME);
        if (rawSystemUsesLightTheme is int intValue)
        {
            return intValue == 0;
        }

        return false;
    }

    // SettingItem-based fallback readers used by RecomputeAndUpdateCachedDarkModeAsync when the
    // Personalize registry key is missing. SettingItems return the "uses light theme" boolean;
    // we invert to "uses dark mode" so the rest of the class works in dark-mode semantics. A
    // failed read (timeout, SettingItem not available, etc.) returns false -- the Windows
    // default if no preference is reachable from either source.
    private static async Task<bool> ReadAppsUseDarkModeViaSettingItemAsync(TimeSpan timeout)
    {
        var settingItem = DarkMode.AppsUseLightThemeSettingItem;
        if (settingItem is null)
        {
            return false;
        }
        var result = await Morphic.WindowsNative.SystemSettings.SettingItemProxy.GetSettingItemValueAsync<bool>(settingItem, timeout);
        if (result.IsError || result.Value is null)
        {
            return false;
        }
        // SettingItem returns the "apps use LIGHT theme" boolean; invert for the dark-mode caller.
        return !result.Value.Value;
    }

    // Same pattern for system theme. Reads via the bool-typed SystemUsesLightThemeSettingItem
    // exclusively (skipping the Win11-modern SystemThemeSettingItem string variant): both proxies
    // reflect the same underlying Settings-framework state, and the static GetSettingItemValueAsync
    // helper is constrained to value types, so the bool variant is the simpler path.
    private static async Task<bool> ReadSystemUsesDarkModeViaSettingItemAsync(TimeSpan timeout)
    {
        var settingItem = DarkMode.SystemUsesLightThemeSettingItem;
        if (settingItem is null)
        {
            return false;
        }
        var result = await Morphic.WindowsNative.SystemSettings.SettingItemProxy.GetSettingItemValueAsync<bool>(settingItem, timeout);
        if (result.IsError || result.Value is null)
        {
            return false;
        }
        // SettingItem returns the "system uses LIGHT theme" boolean; invert.
        return !result.Value.Value;
    }
}
