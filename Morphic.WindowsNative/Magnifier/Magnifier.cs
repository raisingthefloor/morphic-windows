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
using Morphic.WindowsNative.Theme;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Win32;

namespace Morphic.WindowsNative.Magnifier;

public class Magnifier
{
    // System Setting Ids (for SettingItem settings)
    public static class SystemSettingIds
    {
        public const string IS_ENABLED = "SystemSettings_Accessibility_Magnifier_IsEnabled";
    }

    private static SettingItemProxy? _isEnabledSettingItem;
    private static SettingItemProxy? IsEnabledSettingItem
    {
        get
        {
            if (_isEnabledSettingItem is null)
            {
                _isEnabledSettingItem = SettingsDatabaseProxy.GetSettingItemOrNull(Magnifier.SystemSettingIds.IS_ENABLED);
            }

            return _isEnabledSettingItem;
        }
    }
    //private const string IS_ENABLED_VALUE_NAME = "Value";

    public static async Task<MorphicResult<bool, MorphicUnit>> IsMagnifierActiveAsync(TimeSpan? timeout = null)
    {
        var getIsEnabledResult = await Magnifier.GetIsEnabledAsync(timeout);
        if (getIsEnabledResult.IsError)
        {
            return MorphicResult.ErrorResult();
        }
        var isEnabledAsNullable = getIsEnabledResult.Value;

        if (isEnabledAsNullable is not null)
        {
            return MorphicResult.OkResult(isEnabledAsNullable!.Value);
        }
        else
        {
            return MorphicResult.ErrorResult();
        }
    }

    public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> ShowMagnifierAsync(TimeSpan? timeout = null)
    {
        return await Magnifier.SetIsEnabledAsync(true, timeout);
    }

    public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> HideMagnifierAsync(TimeSpan? timeout = null)
    {
        return await Magnifier.SetIsEnabledAsync(false, timeout);
    }
    //
    private static async Task<MorphicResult<bool?, MorphicUnit>> GetIsEnabledAsync(TimeSpan? timeout = null)
    {
        var isEnabledSettingItem = Magnifier.IsEnabledSettingItem;
        if (isEnabledSettingItem is null)
        {
            return MorphicResult.ErrorResult();
        }
        //
        var getValueResult = await SettingItemProxy.GetSettingItemValueAsync<bool>(isEnabledSettingItem, /*Magnifier.IS_ENABLED_VALUE_NAME, */timeout);
        if (getValueResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        var result = getValueResult.Value;

        return MorphicResult.OkResult(result);
    }

    private static async Task<MorphicResult<MorphicUnit, MorphicUnit>> SetIsEnabledAsync(bool isEnabled, TimeSpan? timeout = null)
    {
        var isEnabledSettingItem = Magnifier.IsEnabledSettingItem;
        if (isEnabledSettingItem is null)
        {
            return MorphicResult.ErrorResult();
        }
        //
        var setValueResult = await SettingItemProxy.SetSettingItemValueAsync<bool>(isEnabledSettingItem, /*Magnifier.IS_ENABLED_VALUE_NAME, */isEnabled, timeout);
        if (setValueResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }

        return MorphicResult.OkResult();
    }
}
