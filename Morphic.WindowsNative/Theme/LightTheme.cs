// Copyright 2020-2024 Raising the Floor - US, Inc.
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
using Windows.UI.ViewManagement;

namespace Morphic.WindowsNative.Theme;

public static class LightTheme
{
    private static readonly string HKCU_THEMES_PERSONALIZE_PATH = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private static Morphic.WindowsNative.Registry.RegistryKey? s_ThemePersonalizationWatchKey = null;
    private static object s_ThemePersonalizationWatchKeyLock = new();

    public class LightThemeChangedEventArgs(bool state) : EventArgs
    {
        public bool State = state;
    }
    public delegate void LightThemeChangedEventHandler(object? sender, LightThemeChangedEventArgs args);
    //
    private static LightThemeChangedEventHandler? s_appsUseLightThemeSettingChanged = null;
    private static LightThemeChangedEventHandler? s_systemUsesLightThemeSettingChanged = null;

    private static bool? _appsUseLightThemeSetting = null;
    private static bool? _systemUsesLightThemeSetting = null;

    private static bool s_AppSettingsThemeWatchEventIsActive = false;
    private static object s_AppSettingsThemeWatchEventLock = new();

    public static MorphicResult<bool, MorphicUnit> GetAppsUseLightThemeSetting()
    {
        var openPersonalizeKeyResult = Morphic.WindowsNative.Registry.CurrentUser.OpenSubKey(LightTheme.HKCU_THEMES_PERSONALIZE_PATH, false);
        if (openPersonalizeKeyResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        var personalizeKey = openPersonalizeKeyResult.Value!;

        // get the current light theme settings for apps
        bool? appsUseLightThemeSettingAsBool = null;
        var getAppsUseLightThemeSettingResult = personalizeKey.GetValueDataOrNull<uint>("AppsUseLightTheme");
        if (getAppsUseLightThemeSettingResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        var appsUseLightThemeSettingAsUInt32 = getAppsUseLightThemeSettingResult.Value;
        if (appsUseLightThemeSettingAsUInt32 is not null)
        {
            appsUseLightThemeSettingAsBool = (appsUseLightThemeSettingAsUInt32 != 0) ? true : false;
        }

        // NOTE: the default light theme state (if no registry value is present) is true
        bool result = appsUseLightThemeSettingAsBool is null ? true : appsUseLightThemeSettingAsBool.Value!;

        return MorphicResult.OkResult(result);
    }

    // NOTE: it is the target event's responsibility to run any UI-related code on the main UI thread; this event should be considered to be fired from a background thread
    public static event LightThemeChangedEventHandler AppsUseLightThemeSettingChanged
    {
        add
        {
            var createWatchKeyResult = LightTheme.CreateThemePersonalizationWatchKeyIfUninitialized();
            if (createWatchKeyResult.IsError == true)
            {
                return;
            }

            s_appsUseLightThemeSettingChanged += value;
        }
        remove
        {
            s_appsUseLightThemeSettingChanged -= value;

            if (s_appsUseLightThemeSettingChanged is null || s_appsUseLightThemeSettingChanged!.GetInvocationList().Length == 0)
            {
                s_appsUseLightThemeSettingChanged = null;

                LightTheme.DestroyThemePersonalizationWatchKeyIfUnused();
            }
        }
    }

    //

    public static MorphicResult<bool, MorphicUnit> GetSystemUsesLightThemeSetting()
    {
        var openPersonalizeKeyResult = Morphic.WindowsNative.Registry.CurrentUser.OpenSubKey(LightTheme.HKCU_THEMES_PERSONALIZE_PATH, false);
        if (openPersonalizeKeyResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        var personalizeKey = openPersonalizeKeyResult.Value!;

        // get the current light theme settings for Windows (i.e. the system)
        bool? systemUsesLightThemeSettingAsBool = null;
        var getSystemUsesLightThemeSettingResult = personalizeKey.GetValueDataOrNull<uint>("SystemUsesLightTheme");
        if (getSystemUsesLightThemeSettingResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        var systemUsesLightThemeSettingAsUInt32 = getSystemUsesLightThemeSettingResult.Value;
        if (systemUsesLightThemeSettingAsUInt32 is not null)
        {
            systemUsesLightThemeSettingAsBool = (systemUsesLightThemeSettingAsUInt32 != 0) ? true : false;
        }

        // NOTE: the default light theme state (if no registry value is present) is true
        bool result = systemUsesLightThemeSettingAsBool is null ? true : systemUsesLightThemeSettingAsBool.Value!;

        return MorphicResult.OkResult(result);
    }

    // NOTE: it is the target event's responsibility to run any UI-related code on the main UI thread; this event should be considered to be fired from a background thread
    public static event LightThemeChangedEventHandler SystemUsesLightThemeSettingChanged
    {
        add
        {
            var createWatchKeyResult = LightTheme.CreateThemePersonalizationWatchKeyIfUninitialized();
            if (createWatchKeyResult.IsError == true)
            {
                return;
            }

            s_systemUsesLightThemeSettingChanged += value;
        }
        remove
        {
            s_systemUsesLightThemeSettingChanged -= value;

            if (s_systemUsesLightThemeSettingChanged is null || s_systemUsesLightThemeSettingChanged!.GetInvocationList().Length == 0)
            {
                s_systemUsesLightThemeSettingChanged = null;

                LightTheme.DestroyThemePersonalizationWatchKeyIfUnused();
            }
        }
    }

    //

    private static void s_ThemePersonalizationWatchKey_RegistryKeyChangedEvent(Registry.RegistryKey sender, EventArgs e)
    {
        // apps use light mode
        var getAppsUseLightThemeSettingResult = LightTheme.GetAppsUseLightThemeSetting();
        if (getAppsUseLightThemeSettingResult.IsSuccess == true)
        {
            var appsUseLightThemeSetting = getAppsUseLightThemeSettingResult.Value!;
            if (_appsUseLightThemeSetting != appsUseLightThemeSetting)
            {
                _appsUseLightThemeSetting = appsUseLightThemeSetting;

                var lightThemeChangedEventArgs = new LightThemeChangedEventArgs(appsUseLightThemeSetting);

                // NOTE: to ensure that each event handler runs (even if one throws an exception), we send an event to each window separately in parallel
                var invocationList = s_appsUseLightThemeSettingChanged?.GetInvocationList();
                if (invocationList is not null)
                {
                    foreach (LightThemeChangedEventHandler element in invocationList!)
                    {
                        Task.Run(() => {
                            // NOTE: it is the target event's responsibility to run any UI-related code on the main UI thread; this event should be considered to be fired from a background thread
                            element.Invoke(null /* static class, no so type instance */, lightThemeChangedEventArgs);
                        });
                    }
                }
                //Task.Run(() =>
                //{
                //    _appsUseLightThemeChanged?.Invoke(null /* static class, no so type instance */, lightThemeChangedEventArgs);
                //});
            }
        }
        else
        {
            Debug.Assert(false, "Captured event that apps light theme changed, but could not read updated state");
        }

        // system uses light mode
        var getSystemUsesLightThemeSettingResult = LightTheme.GetSystemUsesLightThemeSetting();
        if (getSystemUsesLightThemeSettingResult.IsSuccess == true)
        {
            var systemUsesLightThemeSetting = getSystemUsesLightThemeSettingResult.Value!;
            if (_systemUsesLightThemeSetting != systemUsesLightThemeSetting)
            {
                _systemUsesLightThemeSetting = systemUsesLightThemeSetting;

                var lightThemeChangedEventArgs = new LightThemeChangedEventArgs(systemUsesLightThemeSetting);

                // NOTE: to ensure that each event handler runs (even if one throws an exception), we send an event to each window separately in parallel
                var invocationList = s_systemUsesLightThemeSettingChanged?.GetInvocationList();
                if (invocationList is not null)
                {
                    foreach (LightThemeChangedEventHandler element in invocationList!)
                    {
                        Task.Run(() => {
                            // NOTE: it is the target event's responsibility to run any UI-related code on the main UI thread; this event should be considered to be fired from a background thread
                            element.Invoke(null /* static class, no so type instance */, lightThemeChangedEventArgs);
                        });
                    }
                }
                //Task.Run(() =>
                //{
                //    _systemUsesLightThemeChanged?.Invoke(null /* static class, no so type instance */, new EventArgs());
                //});
            }
        }
        else
        {
            Debug.Assert(false, "Received notification that system light theme changed, but could not read updated state");
        }
    }

    private static MorphicResult<MorphicUnit, MorphicUnit> CreateThemePersonalizationWatchKeyIfUninitialized()
    {
        // NOTE: we could alternatively wire up the UserPreferenceChanged handler to capture theme state changes
        // ex: Microsoft.Win32.SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
        // ### NOTE: if we use the UserPreferenceChanged event handler, we must also detach our event handler when the application is disposed (see: https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.systemevents.displaysettingschanged?view=dotnet-plat-ext-3.1#microsoft-win32-systemevents-displaysettingschanged)
        // ### ex: Microsoft.Win32.SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged; // detach the event handler

        lock (s_ThemePersonalizationWatchKeyLock)
        {
            if (s_ThemePersonalizationWatchKey is null)
            {
                var openKeyResult = Morphic.WindowsNative.Registry.CurrentUser.OpenSubKey(LightTheme.HKCU_THEMES_PERSONALIZE_PATH);
                if (openKeyResult.IsError == true)
                {
                    switch (openKeyResult.Error!)
                    {
                        case IWin32ApiError.Win32Error(Win32ErrorCode: var win32ErrorCode):
                            Debug.Assert(false, "Could not open theme personalization registry key for notifications; win32 error: " + win32ErrorCode.ToString());
                            break;
                        default:
                            throw new MorphicUnhandledErrorException();
                    }
                    return MorphicResult.ErrorResult();
                }
                var watchKey = openKeyResult.Value!;

                s_ThemePersonalizationWatchKey = watchKey;
                s_ThemePersonalizationWatchKey.RegistryKeyChangedEvent += s_ThemePersonalizationWatchKey_RegistryKeyChangedEvent;
            }
        }

        return MorphicResult.OkResult();
    }

    private static void DestroyThemePersonalizationWatchKeyIfUnused()
    {
        lock (s_ThemePersonalizationWatchKeyLock)
        {
            if ((s_appsUseLightThemeSettingChanged is null || s_appsUseLightThemeSettingChanged!.GetInvocationList().Length == 0) &&
                (s_systemUsesLightThemeSettingChanged is null || s_systemUsesLightThemeSettingChanged!.GetInvocationList().Length == 0))
            {
                s_ThemePersonalizationWatchKey?.Dispose();
                s_ThemePersonalizationWatchKey = null;
            }
        }
    }

    //

}
