// Copyright 2020-2026 Raising the Floor - US, Inc.
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
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Morphic.MorphicBar;

internal class BarItemHandlers
{
    // contrast and color buttons

    public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> ContrastButtonAction(string? actionTag, bool? isChecked)
    {
        if (isChecked is null)
        {
            // non-toggle invocation makes no sense for high contrast -- treat as error so caller doesn't pretend it took effect
            return MorphicResult.ErrorResult();
        }

        var enableHighContrast = isChecked.Value;

        // HighContrast.SetIsOn is a synchronous SPI call (it broadcasts WM_SETTINGCHANGE inline,
        // which can block briefly); wrap in Task.Run so the click handler stays off the UI thread.
        var highContrastSucceeded = await Task.Run(() =>
        {
            var setResult = Morphic.WindowsNative.Theme.HighContrast.SetIsOn(enableHighContrast);
            return setResult.IsSuccess;
        });

        return highContrastSucceeded ? MorphicResult.OkResult() : MorphicResult.ErrorResult();
    }

    public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> DarkButtonAction(string? actionTag, bool? isChecked)
    {
        if (isChecked is null)
        {
            // non-toggle invocation makes no sense for dark-mode -- treat as error so caller doesn't pretend it took effect
            return MorphicResult.ErrorResult();
        }

        var useDarkMode = isChecked.Value;

        // The SystemSettings calls can block before their first async yield, so keep them off the UI thread; .ConfigureAwait(false) also tells C# not to try to resume execution on the original thread.
        // Run all three calls and report overall success only if every step succeeded; partial-failure cases (e.g. system flag flipped but broadcast failed) are treated as failure so the toggle reverts.
        var darkModeSucceeded = await Task.Run(async () =>
        {
            // update the two dark states first, and then send the broadcast ONCE for efficiency
            var systemResult = await Morphic.WindowsNative.Theme.DarkMode.SetSystemUsesDarkModeAsync(useDarkMode).ConfigureAwait(false);
            if (systemResult.IsError) { return false; }
            var appsResult = await Morphic.WindowsNative.Theme.DarkMode.SetAppsUseDarkModeAsync(useDarkMode).ConfigureAwait(false);
            if (appsResult.IsError) { return false; }
            //
            // broadcast the change to all apps
            var broadcastResult = await Morphic.WindowsNative.Theme.DarkMode.BroadcastChangeMessageAsync().ConfigureAwait(false);
            if (broadcastResult.IsError) { return false; }
			
            return true; // set operation succeeded
        });

        return darkModeSucceeded ? MorphicResult.OkResult() : MorphicResult.ErrorResult();
    }

    public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> ColorButtonAction(string? actionTag, bool? isChecked)
    {
        if (isChecked is null)
        {
            // non-toggle invocation makes no sense for color filtering -- treat as error so caller doesn't pretend it took effect
            return MorphicResult.ErrorResult();
        }

        var enableColorFiltering = isChecked.Value;

        // The SystemSettings call can block before its first async yield, so keep it off the UI thread; .ConfigureAwait(false) also tells C# not to try to resume execution on the original thread.
        var colorFilteringSucceeded = await Task.Run(async () =>
        {
            var setResult = await Morphic.WindowsNative.Display.ColorFilters.SetIsActiveAsync(enableColorFiltering).ConfigureAwait(false);
            return setResult.IsSuccess;
        });

        return colorFilteringSucceeded ? MorphicResult.OkResult() : MorphicResult.ErrorResult();
    }

    public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> NightButtonAction(string? actionTag, bool? isChecked)
    {
        if (isChecked is null)
        {
            // non-toggle invocation makes no sense for night light -- treat as error so caller doesn't pretend it took effect
            return MorphicResult.ErrorResult();
        }

        var enableNightLight = isChecked.Value;

        // The SystemSettings call can block before its first async yield, so keep it off the UI thread; .ConfigureAwait(false) also tells C# not to try to resume execution on the original thread.
        // NOTE A 5-second timeout covers the edge case where the user clicks before the factory's
        // startup prime (BarItemDataFactory.CreateContrastColorButtonGroup) has finished settling the
        // SettingItem's IsEnabled flag. Without the timeout, SetIsOnAsync's default TimeSpan.Zero
        // could race the OS's IsEnabled=true signal and fail silently on the very first click.
        var nightLightSucceeded = await Task.Run(async () =>
        {
            var setResult = await Morphic.WindowsNative.Display.NightLight.SetIsOnAsync(enableNightLight, TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            return setResult.IsSuccess;
        });

        return nightLightSucceeded ? MorphicResult.OkResult() : MorphicResult.ErrorResult();
    }

}
