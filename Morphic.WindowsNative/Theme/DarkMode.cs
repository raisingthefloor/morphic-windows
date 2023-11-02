// Copyright 2020-2022 Raising the Floor - US, Inc.
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
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Morphic.WindowsNative.Theme;

public class DarkMode
{
     public static class SystemSettingId
     {
          public const string APPS_USE_LIGHT_THEME = "SystemSettings_Personalize_Color_AppsUseLightTheme";
          public const string SYSTEM_USES_LIGHT_THEME = "SystemSettings_Personalize_Color_SystemUsesLightTheme";
     }

     private static SettingItemProxy? _appsUseLightThemeSettingItem;
     private static SettingItemProxy? AppsUseLightThemeSettingItem
     {
          get
          {
               if (_appsUseLightThemeSettingItem is null)
               {
                    _appsUseLightThemeSettingItem = SettingsDatabaseProxy.GetSettingItemOrNull(DarkMode.SystemSettingId.APPS_USE_LIGHT_THEME);
               }

               return _appsUseLightThemeSettingItem;
          }
     }
     //private const string APPS_USE_LIGHT_THEME_VALUE_NAME = "Value";

     //

     private static Morphic.WindowsNative.Registry.RegistryKey? s_ThemePersonalizationWatchKey = null;
     //
     private static EventHandler? _appsUseDarkModeChanged = null;
     public static event EventHandler AppsUseDarkModeChanged
     {
          add
          {
               if (s_ThemePersonalizationWatchKey is null)
               {
                    var openKeyResult = Morphic.WindowsNative.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                    if (openKeyResult.IsError == true)
                    {
                         switch (openKeyResult.Error!.Value)
                         {
                              case Win32ApiError.Values.Win32Error:
                                   Debug.Assert(false, "Could not open dark/light theme key for notifications; win32 error: " + openKeyResult.Error!.Win32ErrorCode.ToString());
                                   break;
                              default:
                                   throw new MorphicUnhandledErrorException();
                         }
                         return;
                    }
                    var watchKey = openKeyResult.Value!;

                    s_ThemePersonalizationWatchKey = watchKey;
                    s_ThemePersonalizationWatchKey.RegistryKeyChangedEvent += s_ThemePersonalizationWatchKey_RegistryKeyChangedEvent;
               }

               _appsUseDarkModeChanged += value;
          }
          remove
          {
               _appsUseDarkModeChanged -= value;

               if (_appsUseDarkModeChanged is null || _appsUseDarkModeChanged!.GetInvocationList().Length == 0)
               {
                    _appsUseDarkModeChanged = null;

                    s_ThemePersonalizationWatchKey?.Dispose();
                    s_ThemePersonalizationWatchKey = null;
               }
          }
     }
     //
     private static EventHandler? _systemUsesDarkModeChanged = null;
     public static event EventHandler SystemUsesDarkModeChanged
     {
          add
          {
               if (s_ThemePersonalizationWatchKey is null)
               {
                    var openKeyResult = Morphic.WindowsNative.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                    if (openKeyResult.IsError == true)
                    {
                         switch (openKeyResult.Error!.Value)
                         {
                              case Win32ApiError.Values.Win32Error:
                                   Debug.Assert(false, "Could not open dark/light theme key for notifications; win32 error: " + openKeyResult.Error!.Win32ErrorCode.ToString());
                                   break;
                              default:
                                   throw new MorphicUnhandledErrorException();
                         }
                         return;
                    }
                    var watchKey = openKeyResult.Value!;

                    s_ThemePersonalizationWatchKey = watchKey;
                    s_ThemePersonalizationWatchKey.RegistryKeyChangedEvent += s_ThemePersonalizationWatchKey_RegistryKeyChangedEvent;
               }

               _systemUsesDarkModeChanged += value;
          }
          remove
          {
               _systemUsesDarkModeChanged -= value;

               if (_systemUsesDarkModeChanged is null || _systemUsesDarkModeChanged!.GetInvocationList().Length == 0)
               {
                    _systemUsesDarkModeChanged = null;

                    s_ThemePersonalizationWatchKey?.Dispose();
                    s_ThemePersonalizationWatchKey = null;
               }
          }
     }
     //
     private static void s_ThemePersonalizationWatchKey_RegistryKeyChangedEvent(Registry.RegistryKey sender, EventArgs e)
     {
          // apps use dark mode
          var invocationList = _appsUseDarkModeChanged?.GetInvocationList();
          if (invocationList is not null)
          {
               foreach (EventHandler element in invocationList!)
               {
                    Task.Run(() => {
                         element.Invoke(null /* static class, no so type instance */, EventArgs.Empty);
                    });
               }
          }
          //Task.Run(() =>
          //{
          //    _appsUseDarkModeChanged?.Invoke(null /* static class, no so type instance */, new EventArgs());
          //});

          // system uses dark mode
          invocationList = _systemUsesDarkModeChanged?.GetInvocationList();
          if (invocationList is not null)
          {
               foreach (EventHandler element in invocationList!)
               {
                    Task.Run(() => {
                         element.Invoke(null /* static class, no so type instance */, EventArgs.Empty);
                    });
               }
          }
          //Task.Run(() =>
          //{
          //    _systemUsesDarkModeChanged?.Invoke(null /* static class, no so type instance */, new EventArgs());
          //});
     }

     //

     private static SettingItemProxy? _systemUsesLightThemeSettingItem;
     private static SettingItemProxy? SystemUsesLightThemeSettingItem
     {
          get
          {
               if (_systemUsesLightThemeSettingItem is null)
               {
                    _systemUsesLightThemeSettingItem = SettingsDatabaseProxy.GetSettingItemOrNull(DarkMode.SystemSettingId.SYSTEM_USES_LIGHT_THEME);
               }

               return _systemUsesLightThemeSettingItem;
          }
     }
     //private const string SYSTEM_USES_LIGHT_THEME_VALUE_NAME = "Value";

     //// NOTE: this is an alternate implementation; we don't currently use this, but the code remains here just in case
     //public static async Task<MorphicResult<bool?, MorphicUnit>> GetAppsUseDarkModeAsync(TimeSpan? timeout = null)
     //{
     //    var getValueResult = await SettingItemProxy.GetSettingItemValueAsync<bool>(DarkMode.AppsUseLightThemeSettingItem, /*DarkMode.APPS_USE_LIGHT_THEME_VALUE_NAME, */timeout);
     //    if (getValueResult.IsError == true)
     //    {
     //        return MorphicResult.ErrorResult();
     //    }
     //    var result = getValueResult.Value;

     //    // NOTE: to the caller, we return the inverted result (since the caller is asking about dark mode, not about light mode)
     //    return MorphicResult.OkResult(!result);
     //}

     public static MorphicResult<bool?, MorphicUnit> GetAppsUseDarkMode()
     {
          var openPersonalizeKeyResult = Morphic.WindowsNative.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false);
          if (openPersonalizeKeyResult.IsError == true)
          {
               return MorphicResult.ErrorResult();
          }
          var personalizeKey = openPersonalizeKeyResult.Value!;

          // get the current light theme settings for apps
          bool? appsUseLightThemeAsBool = null;
          var getAppsUseLightThemeResult = personalizeKey.GetValueDataOrNull<uint>("AppsUseLightTheme");
          if (getAppsUseLightThemeResult.IsError == true)
          {
               return MorphicResult.ErrorResult();
          }
          var appsUseLightThemeAsUInt32 = getAppsUseLightThemeResult.Value;
          if (appsUseLightThemeAsUInt32 is not null)
          {
               appsUseLightThemeAsBool = (appsUseLightThemeAsUInt32 != 0) ? true : false;
          }

          // dark mode states are the inverse of AppsUseLightTheme/SystemUsesLightTheme
          bool? result = appsUseLightThemeAsBool is null ? null : !appsUseLightThemeAsBool;

          return MorphicResult.OkResult(result);
     }

     public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> SetAppsUseDarkModeAsync(bool value, TimeSpan? timeout = null)
     {
          Stopwatch stopwatch = Stopwatch.StartNew();

          // NOTE: we invert the caller's supplied argument (since the caller is setting the 'uses dark mode' value, not the 'uses light mode' value)
          var setValueResult = await SettingItemProxy.SetSettingItemValueAsync<bool>(DarkMode.AppsUseLightThemeSettingItem, /*DarkMode.APPS_USE_LIGHT_THEME_VALUE_NAME, */!value, timeout);
          if (setValueResult.IsError == true)
          {
               return MorphicResult.ErrorResult();
          }

          TimeSpan? remainingTimeout = null;
          if (timeout is not null)
          {
               remainingTimeout = timeout!.Value.Subtract(new TimeSpan(stopwatch.ElapsedMilliseconds));
          }

          // broadcast a message that the "INI" (registry) setting has been changed for dark mode
          MorphicResult<MorphicUnit, MorphicUnit> broadcastMessageResult;
          if (remainingTimeout is not null)
          {
               broadcastMessageResult = await DarkMode.BroadcastImmersiveColorSetMessageAsync().WaitAsync(remainingTimeout.Value!);
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

     //// NOTE: this is an alternate implementation; we don't currently use this, but the code remains here just in case
     //public static async Task<MorphicResult<bool?, MorphicUnit>> GetSystemUsesDarkModeAsync(TimeSpan? timeout = null)
     //{
     //    var getValueResult = await SettingItemProxy.GetSettingItemValueAsync<bool>(DarkMode.SystemUsesLightThemeSettingItem, /*DarkMode.SYSTEM_USES_LIGHT_THEME_VALUE_NAME, */timeout);
     //    if (getValueResult.IsError == true)
     //    {
     //        return MorphicResult.ErrorResult();
     //    }
     //    var result = getValueResult.Value;

     //    // NOTE: to the caller, we return the inverted result (since the caller is asking about dark mode, not about light mode)
     //    return MorphicResult.OkResult(!result);
     //}

     public static MorphicResult<bool?, MorphicUnit> GetSystemUsesDarkMode()
     {
          var openPersonalizeKeyResult = Morphic.WindowsNative.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false);
          if (openPersonalizeKeyResult.IsError == true)
          {
               return MorphicResult.ErrorResult();
          }
          var personalizeKey = openPersonalizeKeyResult.Value!;

          // get the current light theme settings for Windows (i.e. the system)
          bool? systemUsesLightThemeAsBool = null;
          var getSystemUsesLightThemeResult = personalizeKey.GetValueDataOrNull<uint>("SystemUsesLightTheme");
          if (getSystemUsesLightThemeResult.IsError == true)
          {
               return MorphicResult.ErrorResult();
          }
          var systemUsesLightThemeAsUInt32 = getSystemUsesLightThemeResult.Value;
          if (systemUsesLightThemeAsUInt32 is not null)
          {
               systemUsesLightThemeAsBool = (systemUsesLightThemeAsUInt32 != 0) ? true : false;
          }

          // dark mode states are the inverse of AppsUseLightTheme/SystemUsesLightTheme
          bool? result = systemUsesLightThemeAsBool is null ? null : !systemUsesLightThemeAsBool;

          return MorphicResult.OkResult(result);
     }

     public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> SetSystemUsesDarkModeAsync(bool value, TimeSpan? timeout = null)
     {
          Stopwatch stopwatch = Stopwatch.StartNew();

          // NOTE: we invert the caller's supplied argument (since the caller is setting the 'uses dark mode' value, not the 'uses light mode' value)
          var setValueResult = await SettingItemProxy.SetSettingItemValueAsync<bool>(DarkMode.SystemUsesLightThemeSettingItem, /*DarkMode.SYSTEM_USES_LIGHT_THEME_VALUE_NAME, */!value, timeout);
          if (setValueResult.IsError == true)
          {
               return MorphicResult.ErrorResult();
          }

          TimeSpan? remainingTimeout = null;
          if (timeout is not null)
          {
               remainingTimeout = timeout!.Value.Subtract(new TimeSpan(stopwatch.ElapsedMilliseconds));
          }

          // broadcast a message that the "INI" (registry) setting has been changed for dark mode
          MorphicResult<MorphicUnit, MorphicUnit> broadcastMessageResult;
          if (remainingTimeout is not null)
          {
               broadcastMessageResult = await DarkMode.BroadcastImmersiveColorSetMessageAsync().WaitAsync(remainingTimeout.Value!);
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

     private static async Task<MorphicResult<MorphicUnit, MorphicUnit>> BroadcastImmersiveColorSetMessageAsync(int? timeoutPerTopLevelWindowInMilliseconds = null)
     {
          if (timeoutPerTopLevelWindowInMilliseconds is null)
          {
               // NOTE: This default timeout period is arbitrary; we may want to tune it
               var DEFAULT_TIMEOUT_PER_TOP_LEVEL_WINDOW_IN_MILLISECONDS = 1000;
               timeoutPerTopLevelWindowInMilliseconds = DEFAULT_TIMEOUT_PER_TOP_LEVEL_WINDOW_IN_MILLISECONDS;
          }

          // see: https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-settingchange
          var pointerToImmersiveColorSetString = Marshal.StringToHGlobalUni("ImmersiveColorSet");
          bool success;
          try
          {
               // notify all windows that we have changed a setting
               // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendmessagetimeoutw
               IntPtr sendMessageResult;
               var result = await Task.Run(() =>
               {
                    var result = PInvoke.User32.SendMessageTimeout(PInvoke.User32.HWND_BROADCAST, PInvoke.User32.WindowMessage.WM_WININICHANGE, IntPtr.Zero, pointerToImmersiveColorSetString, PInvoke.User32.SendMessageTimeoutFlags.SMTO_ABORTIFHUNG, timeoutPerTopLevelWindowInMilliseconds!.Value, out sendMessageResult);
                    return result;
               });
               success = (result != IntPtr.Zero);
          }
          finally
          {
               Marshal.FreeHGlobal(pointerToImmersiveColorSetString);
          }

          return success ? MorphicResult.OkResult() : MorphicResult.ErrorResult();
     }
}