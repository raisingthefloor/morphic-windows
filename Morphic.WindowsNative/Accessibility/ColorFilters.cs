// Copyright 2022 Raising the Floor - US, Inc.
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

namespace Morphic.WindowsNative.Accessibility
{
    using Morphic.Core;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Windows.Services.Maps;

    public class ColorFilters
    {
        private static SystemSettings.DataModel.SettingsDatabase? _settingsDatabase;
        private static SystemSettings.DataModel.SettingsDatabase? SettingsDatabase
        {
            get
            {
                if (_settingsDatabase is null)
                {
                    try
                    {
                        _settingsDatabase = new SystemSettings.DataModel.SettingsDatabase();
                    }
                    catch
                    {
                        return null;
                    }
                }
                return _settingsDatabase!;
            }
        }
        //
        private const string IS_ENABLED_SETTING_ID = "SystemSettings_Accessibility_ColorFiltering_IsEnabled";
        private static SystemSettings.DataModel.ISettingItem? _isEnabledSettingItem;
        private static SystemSettings.DataModel.ISettingItem? IsEnabledSettingItem
        {
            get
            {
                if (_isEnabledSettingItem is null)
                {
                    try
                    {
                        _isEnabledSettingItem = ColorFilters.SettingsDatabase?.GetSetting(ColorFilters.IS_ENABLED_SETTING_ID);
                    }
                    catch
                    {
                        return null;
                    }
                }

                return _isEnabledSettingItem;
            }
        }
        private const string IS_ENABLED_VALUE_NAME = "Value";

        private static Morphic.WindowsNative.Registry.RegistryKey? s_watchKey = null;
        //
        private static EventHandler? _isActiveChanged = null;
        public static event EventHandler IsActiveChanged
        {
            add
            {
                if (s_watchKey is null)
                {
                    var openKeyResult = Morphic.WindowsNative.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\ColorFiltering");
                    if (openKeyResult.IsError == true)
                    {
                        switch (openKeyResult.Error!.Value)
                        {
                            case Win32ApiError.Values.Win32Error:
                                Debug.Assert(false, "Could not open color filtering key for notifications; win32 error: " + openKeyResult.Error!.Win32ErrorCode.ToString());
                                break;
                            default:
                                throw new MorphicUnhandledErrorException();
                        }
                        return;
                    }
                    var watchKey = openKeyResult.Value!;

                    var getIsActiveResult = ColorFilters.GetIsActive();
                    if (getIsActiveResult.IsError == true)
                    {
                        switch (getIsActiveResult.Error!.Value)
                        {
                            case Win32ApiError.Values.Win32Error:
                                Debug.Assert(false, "Could not get current active state (to set initial active state value); win32 error: " + getIsActiveResult.Error!.Win32ErrorCode.ToString());
                                break;
                            default:
                                throw new MorphicUnhandledErrorException();
                        }
                        return;
                    }
                    var isActive = getIsActiveResult.Value!.Value;

                    s_watchKey = watchKey;
                    _lastIsActiveValue = isActive;

                    s_watchKey.RegistryKeyChangedEvent += s_watchKey_RegistryKeyChangedEvent;
                }

                _isActiveChanged += value;
            }
            remove
            {
                _isActiveChanged -= value;

                if (_isActiveChanged is null)
                {
                    _lastIsActiveValue = null;

                    if (_isActiveChanged!.GetInvocationList().Length == 0)
                    {
                        s_watchKey?.Dispose();
                        s_watchKey = null;
                    }
                }
            }
        }
        private static bool? _lastIsActiveValue;

        private static void s_watchKey_RegistryKeyChangedEvent(Registry.RegistryKey sender, EventArgs e)
        {
            var getIsActiveResult = ColorFilters.GetIsActive();
            if (getIsActiveResult.IsError == true)
            {
                switch (getIsActiveResult.Error!.Value)
                {
                    case Win32ApiError.Values.Win32Error:
                        Debug.Assert(false, "Could not get current active state (to check value when registry key value(s) changed); win32 error: " + getIsActiveResult.Error!.Win32ErrorCode.ToString());
                        break;
                    default:
                        throw new MorphicUnhandledErrorException();
                }
                return;
            }
            var isActive = getIsActiveResult.Value!.Value;

            if (_lastIsActiveValue != isActive)
            {
                _lastIsActiveValue = isActive;
                _isActiveChanged?.Invoke(null /* static class, no so type instance */, new EventArgs());
            }
        }

        public static MorphicResult<bool?, Win32ApiError> GetIsActive()
        {
            var openKeyResult = Morphic.WindowsNative.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\ColorFiltering");
            if (openKeyResult.IsError == true)
            {
                switch (openKeyResult.Error!.Value)
                {
                    case Win32ApiError.Values.Win32Error:
                        return MorphicResult.ErrorResult(openKeyResult.Error!);
                    default:
                        throw new MorphicUnhandledErrorException();
                }
            }
            var colorFilteringKey = openKeyResult.Value!;

            // get the active state of color fitlering
            var getValueResult = colorFilteringKey.GetValueDataOrNull<uint>("Active");
            if (getValueResult.IsError == true)
            {
                switch (getValueResult.Error!.Value)
                {
                    case Registry.RegistryKey.RegistryGetValueError.Values.Win32Error:
                        return MorphicResult.ErrorResult(Win32ApiError.Win32Error((uint)getValueResult.Error!.Win32ErrorCode!));
                    case Registry.RegistryKey.RegistryGetValueError.Values.TypeMismatch:
                    case Registry.RegistryKey.RegistryGetValueError.Values.UnsupportedType:
                    default:
                        throw new MorphicUnhandledErrorException();
                }
            }
            var activeAsUInt32 = getValueResult.Value;
            bool? activeAsBool;
            if (activeAsUInt32 is not null)
            {
                activeAsBool = (activeAsUInt32 != 0) ? true : false;
            }
            else
            {
                activeAsBool = null;
            }

            return MorphicResult.OkResult(activeAsBool);
        }

        //// NOTE: this is an alternate implementation of GetIsActive (saved as a backup plan, just in case the registry entries aren't a reliable (or preferred) source of truth for the value
        //public static MorphicResult<bool?, MorphicUnit> GetIsActive()
        //{
        //    var settingItem = ColorFilters.IsEnabledSettingItem;
        //    if (settingItem is null)
        //    {
        //        return MorphicResult.ErrorResult();
        //    }

        //    object? resultAsObject;
        //    try
        //    {
        //        resultAsObject = settingItem.GetValue(ColorFilters.IS_ENABLED_VALUE_NAME);
        //    }
        //    catch
        //    {
        //        return MorphicResult.ErrorResult();
        //    }
        //    var resultAsBool = resultAsObject as bool?;
        //    if (resultAsBool is null)
        //    {
        //        return MorphicResult.ErrorResult();
        //    }

        //    return MorphicResult.OkResult<bool?>(resultAsBool!.Value);
        //}

        //// NOTE: this is an alternate implementation of SetIsActive (saved as a backup plan, just in case ISettingItem.SetValue(...) stops working at some point)
        //public static async Task<MorphicResult<MorphicUnit, Win32ApiError>> SetIsActiveAsync(bool value)
        //{
        //    var openKeyResult = Morphic.WindowsNative.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\ColorFiltering", true);
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
        //    var setValueResult = colorFilteringKey.SetValue<uint>("Active", valueAsUInt32);
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

        public static MorphicResult<MorphicUnit, MorphicUnit> SetIsActive(bool value)
        {
            var settingItem = ColorFilters.IsEnabledSettingItem;
            if (settingItem is null)
            {
                return MorphicResult.ErrorResult();
            }

            try
            {
                settingItem.SetValue(ColorFilters.IS_ENABLED_VALUE_NAME, value);
            }
            catch
            {
                return MorphicResult.ErrorResult();
            }

            return MorphicResult.OkResult();
        }

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
        public static MorphicResult<FilterType?, Win32ApiError> GetFilterType()
        {
            var openKeyResult = Morphic.WindowsNative.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\ColorFiltering");
            if (openKeyResult.IsError == true)
            {
                switch (openKeyResult.Error!.Value)
                {
                    case Win32ApiError.Values.Win32Error:
                        return MorphicResult.ErrorResult(openKeyResult.Error!);
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
                switch (getValueResult.Error!.Value)
                {
                    case Registry.RegistryKey.RegistryGetValueError.Values.Win32Error:
                        return MorphicResult.ErrorResult(Win32ApiError.Win32Error((uint)getValueResult.Error!.Win32ErrorCode!));
                    case Registry.RegistryKey.RegistryGetValueError.Values.TypeMismatch:
                    case Registry.RegistryKey.RegistryGetValueError.Values.UnsupportedType:
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
}