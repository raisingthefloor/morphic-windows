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
}
