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
    // text size

    private static async Task<MorphicResult<MorphicUnit, MorphicUnit>> StepBarDisplayDpiOffsetAsync(string? actionTag, int step)
    {
        var barManager = ((App)Microsoft.UI.Xaml.Application.Current).MorphicBarManager;
        if (barManager is null)
        {
            return MorphicResult.ErrorResult();
        }
        var barHwnd = barManager.GetBarWindowHandle();
        if (barHwnd == IntPtr.Zero)
        {
            return MorphicResult.ErrorResult();
        }

        var displayResult = Morphic.WindowsNative.Display.Display.GetDisplayNearestWindowHandle(barHwnd);
        if (displayResult.IsError)
        {
            return MorphicResult.ErrorResult();
        }
        var display = displayResult.Value!;

        var rangeResult = display.GetCurrentDpiOffsetAndRange();
        if (rangeResult.IsError)
        {
            return MorphicResult.ErrorResult();
        }
        var range = rangeResult.Value;

        var newDpiOffset = range.CurrentDpiOffset + step;
        if (newDpiOffset < range.MinimumDpiOffset || newDpiOffset > range.MaximumDpiOffset)
        {
            return MorphicResult.ErrorResult();
        }

        // SetDpiOffsetAsync wraps the SPI call in Task.Run internally, so this is already off the
        // UI thread; no extra .ConfigureAwait dance needed.
        var setResult = await display.SetDpiOffsetAsync(newDpiOffset);
        if (setResult.IsError)
        {
            return MorphicResult.ErrorResult();
        }

        return MorphicResult.OkResult();
    }

    public static Task<MorphicResult<MorphicUnit, MorphicUnit>> IncreaseTextSizeButtonAction(string? actionTag, bool? isChecked)
    {
        return BarItemHandlers.StepBarDisplayDpiOffsetAsync(actionTag, +1);
    }

    public static Task<MorphicResult<MorphicUnit, MorphicUnit>> DecreaseTextSizeButtonAction(string? actionTag, bool? isChecked)
    {
        return BarItemHandlers.StepBarDisplayDpiOffsetAsync(actionTag, -1);
    }

    //

    // magnifier

    public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> ShowMagnifierButtonAction(string? actionTag, bool? isChecked)
    {
        // if the magnifier is already visible, do nothing (treat as success -- requested end-state is satisfied)
        var isMagnifierActiveResult = await Morphic.WindowsNative.Magnifier.Magnifier.IsMagnifierActiveAsync();
        if (isMagnifierActiveResult.IsError)
        {
            return MorphicResult.ErrorResult();
        }
        var isMagnifierActive = isMagnifierActiveResult.Value!;
        if (isMagnifierActive == true)
        {
            return MorphicResult.OkResult();
        }


        // step 1: re-center the mouse cursor

        // before showing the magnifier, move the cursor to the center of the screen where the mouse pointer currently resides

        var getCurrentPositionResult = Morphic.WindowsNative.Mouse.Mouse.GetCurrentPosition();
        if (getCurrentPositionResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        var currentMousePosition = getCurrentPositionResult.Value!;

        var getDisplayAtPointResult = Morphic.WindowsNative.Display.Display.GetDisplayAtPoint(currentMousePosition);
        if (getDisplayAtPointResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        var targetDisplay = getDisplayAtPointResult.Value!;

        var moveCursorToCenterOfDisplayResult = Morphic.WindowsNative.Mouse.Mouse.MoveCursorToCenterOfDisplay(targetDisplay);
        if (moveCursorToCenterOfDisplayResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }


        // step 2: show the magnifier

        // The SystemSettings calls can block before their first async yield, so keep them off the UI thread; .ConfigureAwait(false) also tells C# not to try to resume execution on the original thread
        var showMagnifierSucceeded = await Task.Run(async () =>
        {
            var showResult = await Morphic.WindowsNative.Magnifier.Magnifier.ShowMagnifierAsync().ConfigureAwait(false);
            return showResult.IsSuccess;
        });

        Debug.WriteLine($"[BarItem] {actionTag}: show -> {(showMagnifierSucceeded ? "ok" : "error")}");

        return showMagnifierSucceeded ? MorphicResult.OkResult() : MorphicResult.ErrorResult();
    }

    public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> HideMagnifierButtonAction(string? actionTag, bool? isChecked)
    {
        // The SystemSettings calls can block before their first async yield, so keep them off the UI thread; .ConfigureAwait(false) also tells C# not to try to resume execution on the original thread
        var hideMagnifierSucceeded = await Task.Run(async () =>
        {
            var hideResult = await Morphic.WindowsNative.Magnifier.Magnifier.HideMagnifierAsync().ConfigureAwait(false);
            return hideResult.IsSuccess;
        });

        Debug.WriteLine($"[BarItem] {actionTag}: hide -> {(hideMagnifierSucceeded ? "ok" : "error")}");

        return hideMagnifierSucceeded ? MorphicResult.OkResult() : MorphicResult.ErrorResult();
    }

    //

    // read selected

    // Placeholder action for Read Selected Play / Stop. The feature isn't implemented yet
    // in 2.x, so this hands the user a toast explaining that.
    public static Task<MorphicResult<MorphicUnit, MorphicUnit>> ReadSelectedButtonAction(string? actionTag, bool? isChecked)
    {
        Morphic.AppNotifications.ToastNotifications.ShowText(
            title: "Read Selected",
            body: "We are updating this feature to utilize the latest functionality from Microsoft.\n\nIt will be available in an upcoming preview release.");
        return Task.FromResult<MorphicResult<MorphicUnit, MorphicUnit>>(MorphicResult.OkResult());
    }

    //

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

        // The SystemSettings call can block before its first async yield, so keep it off the UI thread; 
        // .ConfigureAwait(false) also tells C# not to try to resume execution on the original thread.
        // NOTE A 5-second timeout covers the edge case where the user clicks before the factory's
        // startup prime (BarItemDataFactory.CreateContrastColorButtonGroup) has finished settling the
        // SettingItem's IsEnabled flag.
        var nightLightSucceeded = await Task.Run(async () =>
        {
            var setResult = await Morphic.WindowsNative.Display.NightLight.SetIsOnAsync(enableNightLight, TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            return setResult.IsSuccess;
        });

        return nightLightSucceeded ? MorphicResult.OkResult() : MorphicResult.ErrorResult();
    }
}
