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

using Morphic.Core;
using Morphic.WindowsNative.SystemSettings;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Morphic.WindowsNative.Display;

public class NightLight
{
    private const string IS_ON_SETTING_ID = "SystemSettings_Display_BlueLight_ManualToggleQuickAction";
    private static SettingItemProxy? _isOnSettingItem;
    private static SettingItemProxy? IsOnSettingItem
    {
        get
        {
            if (_isOnSettingItem is null)
            {
                var getSettingItemResult = SettingsDatabaseProxy.GetSettingItem(NightLight.IS_ON_SETTING_ID);
                if (getSettingItemResult.IsError == true)
                {
                    switch (getSettingItemResult.Error!.Value)
                    {
                        case SettingsDatabaseProxy.GetSettingItemError.Values.CouldNotInstantiateSettingsDatabase:
                            return null;
                        case SettingsDatabaseProxy.GetSettingItemError.Values.SettingNotFound:
                            return null;
                        case SettingsDatabaseProxy.GetSettingItemError.Values.ExceptionError:
                            return null;
                        default:
                            throw new MorphicUnhandledErrorException();
                    }
                }
                _isOnSettingItem = getSettingItemResult.Value;
            }

            return _isOnSettingItem;
        }
    }
    //private const string IS_ON_VALUE_NAME = "Value";

    private static bool _isOnSettingChangedEventIsSubscribed = false;
    private static object _isOnSettingChangedEventLock = new();

    private static EventHandler? _isOnChanged = null;
    public static event EventHandler IsOnChanged
    {
        add
        {
            SettingItemProxy isOnSettingItem;
            if (_isOnChanged is null)
            {
                isOnSettingItem = NightLight.IsOnSettingItem;
                if (isOnSettingItem is null)
                {
                    Debug.Assert(false, "Could not get setting item for NightLight");
                }
                else
                {
                    lock (_isOnSettingChangedEventLock)
                    {
                        if (_isOnSettingChangedEventIsSubscribed == false)
                        {
                            isOnSettingItem.ValueChanged += SettingItem_ValueChanged;
                            _isOnSettingChangedEventIsSubscribed = true;
                        }
                    }
                }
            }

            _isOnChanged += value;
        }
        remove
        {
            _isOnChanged -= value;

            if (_isOnChanged is null || _isOnChanged!.GetInvocationList().Length == 0)
            {
                _isOnChanged = null;

                var isOnSettingItem = NightLight.IsOnSettingItem;
                if (isOnSettingItem is null)
                {
                    Debug.Assert(false, "Could not get setting item for NightLight");
                }
                else
                {
                    lock (_isOnSettingChangedEventLock)
                    {
                        if (_isOnSettingChangedEventIsSubscribed == true)
                        {
                            isOnSettingItem.ValueChanged -= SettingItem_ValueChanged;
                            _isOnSettingChangedEventIsSubscribed = false;
                        }
                    }
                }
            }
        }
    }

    private static void SettingItem_ValueChanged(object? sender, EventArgs e)
    {
        var invocationList = _isOnChanged?.GetInvocationList();
        if (invocationList is not null)
        {
            foreach (EventHandler element in invocationList!)
            {
                Task.Run(() => {
                    element.Invoke(null /* static class, no so type instance */, EventArgs.Empty);
                });
            }
        }
        //Task.Run(() => {
        //    _isOnChanged?.Invoke(null /* static class, no so type instance */, EventArgs.Empty);
        //});
    }

    //

    public async static Task<MorphicResult<bool?, MorphicUnit>> GetIsOnAsync(TimeSpan? timeout = null)
    {
        var settingItem = NightLight.IsOnSettingItem;
        if (settingItem is null)
        {
            return MorphicResult.ErrorResult();
        }

        var getSettingResult = await settingItem.GetValueAsync<bool>(/*NightLight.IS_ON_VALUE_NAME, */timeout);
        if (getSettingResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        var result = getSettingResult.Value;

        return MorphicResult.OkResult(result);
    }

    public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> SetIsOnAsync(bool value, TimeSpan? timeout = null)
    {
        var settingItem = NightLight.IsOnSettingItem;
        if (settingItem is null)
        {
            return MorphicResult.ErrorResult();
        }

        var setValueResult = await settingItem.SetValueAsync(/*NightLight.IS_ON_VALUE_NAME, */value);
        if (setValueResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }

        return MorphicResult.OkResult();
    }
}
