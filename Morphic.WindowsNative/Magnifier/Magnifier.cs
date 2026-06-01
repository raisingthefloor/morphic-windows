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

// Values match the Windows Magnifier registry value HKCU\Software\Microsoft\ScreenMagnifier\MagnificationMode.
public enum MagnifierMode
{
    Docked = 1,
    Fullscreen = 2,
    Lens = 3,
}

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

    //

    // magnifier mode (lens vs fullscreen vs docked)

    // Windows Magnifier reads its mode from this value at startup. Because our show flow only runs while the magnifier is off, writing it before launch makes the magnifier come up in the mode we want.
    private const string SCREEN_MAGNIFIER_REGISTRY_KEY_PATH = @"Software\Microsoft\ScreenMagnifier";
    private const string MAGNIFICATION_MODE_VALUE_NAME = "MagnificationMode";

    // The mode to put the magnifier back into after it is hidden; null means nothing to restore (already lens at show time, or we could not switch to lens).
    private static MagnifierMode? _modeToRestoreAfterHide = null;

    // Returns .IsSuccess if the caller should re-center the cursor (the magnifier will come up in lens mode); returns .IsError if lens mode could not be guaranteed, in which case the cursor must be left alone.
    public static MorphicResult<MorphicUnit, MorphicUnit> EnsureLensModeForShow()
    {
        _modeToRestoreAfterHide = null;

        var currentModeAsNullable = Magnifier.GetCurrentMode();
        if (currentModeAsNullable is null)
        {
            // mode unknown: we cannot switch to (and later restore from) lens mode safely
            return MorphicResult.ErrorResult();
        }
        var currentMode = currentModeAsNullable.Value;

        if (currentMode == MagnifierMode.Lens)
        {
            return MorphicResult.OkResult();
        }

        var setCurrentModeResult = Magnifier.SetCurrentMode(MagnifierMode.Lens);
        if (setCurrentModeResult.IsError)
        {
            return MorphicResult.ErrorResult();
        }

        _modeToRestoreAfterHide = currentMode;
        return MorphicResult.OkResult();
    }

    // Restores the pre-show magnifier mode, but only if the magnifier is still in lens mode. If the user switched it to another mode while it was running, we honor that and leave it alone.
    public static void RestoreModePriorToShowIfNeeded()
    {
        var modeToRestore = _modeToRestoreAfterHide;
        if (modeToRestore is null)
        {
            return;
        }

        // Assumes Magnifier writes runtime mode changes to the registry synchronously and does not re-write the mode on exit; a live mode other than lens therefore means the user changed it.
        var currentModeAsNullable = Magnifier.GetCurrentMode();
        if (currentModeAsNullable == MagnifierMode.Lens)
        {
            _ = Magnifier.SetCurrentMode(modeToRestore.Value);
        }

        _modeToRestoreAfterHide = null;
    }

    // Returns Fullscreen (the Windows default) when the key or value is absent; null when the value cannot be read or is not a recognized mode.
    private static MagnifierMode? GetCurrentMode()
    {
        try
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(Magnifier.SCREEN_MAGNIFIER_REGISTRY_KEY_PATH))
            {
                if (key is null)
                {
                    return MagnifierMode.Fullscreen;
                }

                var rawValue = key.GetValue(Magnifier.MAGNIFICATION_MODE_VALUE_NAME);
                if (rawValue is null)
                {
                    return MagnifierMode.Fullscreen;
                }

                if (rawValue is int valueAsInt)
                {
                    switch (valueAsInt)
                    {
                        case (int)MagnifierMode.Fullscreen:
                            return MagnifierMode.Fullscreen;
                        case (int)MagnifierMode.Lens:
                            return MagnifierMode.Lens;
                        case (int)MagnifierMode.Docked:
                            return MagnifierMode.Docked;
                        default:
                            return null;
                    }
                }

                return null;
            }
        }
        catch
        {
            return null;
        }
    }

    private static MorphicResult<MorphicUnit, MorphicUnit> SetCurrentMode(MagnifierMode mode)
    {
        try
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(Magnifier.SCREEN_MAGNIFIER_REGISTRY_KEY_PATH))
            {
                if (key is null)
                {
                    return MorphicResult.ErrorResult();
                }

                key.SetValue(Magnifier.MAGNIFICATION_MODE_VALUE_NAME, (int)mode, Microsoft.Win32.RegistryValueKind.DWord);
                return MorphicResult.OkResult();
            }
        }
        catch
        {
            return MorphicResult.ErrorResult();
        }
    }
}
