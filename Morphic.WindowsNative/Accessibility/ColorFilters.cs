// Copyright 2022 Raising the Floor - US, Inc.
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
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Services.Maps;

using SystemSettingsDataModel = SystemSettings.DataModel;

namespace Morphic.WindowsNative.Accessibility;

public class ColorFilters
{
    public static class SystemSettingId
    {
        public const string COLOR_FILTERING_IS_ENABLED_SETTING_ID = "SystemSettings_Accessibility_ColorFiltering_IsEnabled";
    }

    private static SettingItemProxy? _colorFilteringIsEnabledSettingItem;
    private static SettingItemProxy? ColorFilteringIsEnabledSettingItem
    {
        get
        {
            if (_colorFilteringIsEnabledSettingItem is null)
            {
                _colorFilteringIsEnabledSettingItem = SettingsDatabaseProxy.GetSettingItemOrNull(ColorFilters.SystemSettingId.COLOR_FILTERING_IS_ENABLED_SETTING_ID);
            }

            return _colorFilteringIsEnabledSettingItem;
        }
    }
    // private const string COLOR_FILTERING_IS_ENABLED_VALUE = "Value";

    //

    private static Morphic.WindowsNative.Registry.RegistryKey? s_colorFilteringIsActiveWatchKey = null;
    //
    private static EventHandler? _isActiveChanged = null;
    public static event EventHandler IsActiveChanged
    {
        add
        {
            if (s_colorFilteringIsActiveWatchKey is null) 
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

                s_colorFilteringIsActiveWatchKey = watchKey;
                s_colorFilteringIsActiveWatchKey.RegistryKeyChangedEvent += s_colorFilteringIsActiveWatchKey_RegistryKeyChangedEvent;
            }

            _isActiveChanged += value;
        }
        remove
        {
            _isActiveChanged -= value;

            if (_isActiveChanged is null || _isActiveChanged!.GetInvocationList().Length == 0)
            {
                _isActiveChanged = null;

                s_colorFilteringIsActiveWatchKey?.Dispose();
                s_colorFilteringIsActiveWatchKey = null;
            }
        }
    }

    private static void s_colorFilteringIsActiveWatchKey_RegistryKeyChangedEvent(Registry.RegistryKey sender, EventArgs e)
    {
        var invocationList = _isActiveChanged?.GetInvocationList();
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
        //    _isActiveChanged?.Invoke(null /* static class, no so type instance */, new EventArgs());
        //});
    }

    //

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
    //public static async Task<MorphicResult<bool?, MorphicUnit>> GetIsActiveAsync(TimeSpan? timeout = null)
    //{
    //    var getValueResult = await SettingItemProxy.GetSettingItemValueAsync<bool>(ColorFilters.ColorFilteringIsEnabledSettingItem, /*ColorFilters.COLOR_FILTERING_IS_ENABLED_VALUE, */timeout);
    //    if (getValueResult.IsError == true)
    //    {
    //        return MorphicResult.ErrorResult();
    //    }
    //    var result = getValueResult.Value;

    //    return MorphicResult.OkResult(result);
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

    public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> SetIsActiveAsync(bool value, TimeSpan? timeout = null)
    {
        var setValueResult = await SettingItemProxy.SetSettingItemValueAsync<bool>(ColorFilters.ColorFilteringIsEnabledSettingItem, /*ColorFilters.COLOR_FILTERING_IS_ENABLED_VALUE, */value, timeout);
        if (setValueResult.IsError == true)
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
