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


public class ColorFilters
{
    private const string COLOR_FILTERING_REGISTRY_KEY_PATH = @"SOFTWARE\Microsoft\ColorFiltering";
    private const string ACTIVE_REGISTRY_VALUE_NAME = "Active";

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
}
