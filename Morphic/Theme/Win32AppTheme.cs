// Copyright 2024-2026 Raising the Floor - US, Inc.
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

using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Morphic.Theme;

internal class Win32AppTheme
{
    private static Windows.UI.ViewManagement.UISettings s_uiSettings = new();
    private static bool s_isWatchingUiSettingsColorValuesChangedEvent = false;
    private static object s_UiSettingsColorValuesChangedEventLock = new();

    private static EventHandler<ElementTheme>? s_themeChanged = null;

    public static ElementTheme GetAppTheme()
    {
        // capture current (active) system colors for foreground color
        var foregroundColor = s_uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Foreground);

        // NOTE: a light foreground color means that the theme is a dark theme (and vice-versa)
        return Win32AppTheme.IsColorLight(foregroundColor) ? ElementTheme.Dark : ElementTheme.Light;
    }

    // returns whether or not a foreground color belongs to a light or dark mode theme
    // courtesy of Microsoft documentation: https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/apply-windows-themes#know-when-dark-mode-is-enabled
    private static bool IsColorLight(Windows.UI.Color color)
    {
        return (((5 * (ushort)color.G) + (2 * (ushort)color.R) + (ushort)color.B) > (ushort)(8 * 128));
    }

    public static Windows.UI.Color GetBackgroundColor()
    {
        return s_uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Background);
    }

    public static Windows.UI.Color GetForegroundColor()
    {
        return s_uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Foreground);
    }

    #region ThemeChanged event support

    // NOTE: it is the target event's responsibility to run any UI-related code on the main UI thread; this event should be considered to be fired from a background thread
    public static event EventHandler<ElementTheme> ThemeChanged
    {
        add
        {
            Win32AppTheme.ConnectUISettingsColorValuesChangedEventIfUninitialized();

            s_themeChanged += value;
        }
        remove
        {
            s_themeChanged -= value;

            if (s_themeChanged is null || s_themeChanged!.GetInvocationList().Length == 0)
            {
                s_themeChanged = null;

                Win32AppTheme.DestroyUISettingsColorValuesChangedEventIfUnused();
            }
        }
    }

    private static void ConnectUISettingsColorValuesChangedEventIfUninitialized()
    {
        lock (s_UiSettingsColorValuesChangedEventLock)
        {
            if (s_isWatchingUiSettingsColorValuesChangedEvent == false)
            {
                s_uiSettings.ColorValuesChanged += UISettings_ColorValuesChanged;
                s_isWatchingUiSettingsColorValuesChangedEvent = true;
            }
        }
    }

    private static void DestroyUISettingsColorValuesChangedEventIfUnused()
    {
        lock (s_UiSettingsColorValuesChangedEventLock)
        {
            if (s_themeChanged is null || s_themeChanged!.GetInvocationList().Length == 0)
            {
                s_uiSettings.ColorValuesChanged -= UISettings_ColorValuesChanged;
                s_isWatchingUiSettingsColorValuesChangedEvent = false;
            }
        }
    }

    // NOTE: UISettings_ColorValuesChanged gets called even when it's just theme colors changing (i.e. not the overall theme itself), so subscribers should ignore 'duplicate' theme changed events
    private static void UISettings_ColorValuesChanged(Windows.UI.ViewManagement.UISettings sender, object args)
    {
        var appTheme = Win32AppTheme.GetAppTheme();

        // NOTE: to ensure that each event handler runs (even if one throws an exception), we send an event to each window separately in parallel
        var invocationList = s_themeChanged?.GetInvocationList();
        if (invocationList is not null)
        {
            foreach (EventHandler<ElementTheme> element in invocationList!)
            {
                Task.Run(() => {
                    // NOTE: it is the target event's responsibility to run any UI-related code on the main UI thread; this event should be considered to be fired from a background thread
                    element.Invoke(null /* static class, no so type instance */, appTheme);
                });
            }
        }

        // alternate invocation strategy (if exceptions from theme change handler is not a concern)
        //Task.Run(() =>
        //{
        //    s_themeChanged?.Invoke(null /* static class, no so type instance */, appTheme);
        //});
    }

    #endregion ThemeChanged event support

}
