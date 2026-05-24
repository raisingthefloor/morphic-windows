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

public class NightLight
{
    public static class SystemSettingIds
    {
        // The SystemSettings "quick action" toggle for Night Light. This is the same setting
        // surfaced by the Windows Action Center's Night Light tile and by the OS Setting app's
		// "System > Display > Night light" corresponding on/off switch
        public const string NIGHT_LIGHT_IS_ON = "SystemSettings_Display_BlueLight_ManualToggleQuickAction";
    }

    private static Morphic.WindowsNative.SystemSettings.SettingItemProxy? _nightLightIsOnSettingItem;
    private static Morphic.WindowsNative.SystemSettings.SettingItemProxy? NightLightIsOnSettingItem
    {
        get
        {
            if (_nightLightIsOnSettingItem is null)
            {
                _nightLightIsOnSettingItem = Morphic.WindowsNative.SystemSettings.SettingsDatabaseProxy.GetSettingItemOrNull(NightLight.SystemSettingIds.NIGHT_LIGHT_IS_ON);
            }

            return _nightLightIsOnSettingItem;
        }
    }

    public static async Task<MorphicResult<bool?, MorphicUnit>> GetIsOnAsync(TimeSpan? timeout = null)
    {
        var getValueResult = await Morphic.WindowsNative.SystemSettings.SettingItemProxy.GetSettingItemValueAsync<bool>(NightLight.NightLightIsOnSettingItem, timeout);
        if (getValueResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }

        return MorphicResult.OkResult(getValueResult.Value);
    }

    public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> SetIsOnAsync(bool value, TimeSpan? timeout = null)
    {
        var setValueResult = await Morphic.WindowsNative.SystemSettings.SettingItemProxy.SetSettingItemValueAsync<bool>(NightLight.NightLightIsOnSettingItem, value, timeout);
        if (setValueResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }

        return MorphicResult.OkResult();
    }

}
