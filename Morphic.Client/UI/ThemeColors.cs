// Copyright 2020-2024 Raising the Floor - US, Inc.
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

using System;
using System.Threading.Tasks;

namespace Morphic.Client.UI;

internal static class ThemeColors
{
    public class ThemeColorsChangedEventArgs : EventArgs
    {
        public Windows.UI.Color BackgroundColor { get; init; }
        public Windows.UI.Color ForegroundColor { get; init; }
        public bool IsDarkColorTheme { get;  init; }

        internal ThemeColorsChangedEventArgs()
        {
        }
    }
    public delegate void ThemeColorsChangedEventHandler(object? sender, ThemeColorsChangedEventArgs e);

    private static ThemeColorsChangedEventHandler? s_themeColorsChanged = null;

    private static Windows.UI.ViewManagement.UISettings s_uiSettings = new();
    private static bool s_isWatchingUiSettingsColorValuesChangedEvent = false;
    private static object s_UiSettingsColorValuesChangedEventLock = new();

    //

    public static Windows.UI.Color GetBackgroundColor()
    {
        return s_uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Background);
    }

    public static Windows.UI.Color GetForegroundColor()
    {
        return s_uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Foreground);
    }

    //

    public static bool GetIsDarkColorTheme() 
    {
        // capture current (active) system colors for foreground color
        var foregroundColor = s_uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Foreground);

        return ThemeColors.IsColorLight(foregroundColor);
    }

    // courtesy of Microsoft documentation: https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/apply-windows-themes#know-when-dark-mode-is-enabled
    private static bool IsColorLight(Windows.UI.Color color)
    {
        return (((5 * (ushort)color.G) + (2 * (ushort)color.R) + (ushort)color.B) > (ushort)(8 * 128));
    }

    //

    // NOTE: it is the target event's responsibility to run any UI-related code on the main UI thread; this event should be considered to be fired from a background thread
    public static event ThemeColorsChangedEventHandler ThemeColorsChanged
    {
        add
        {
            ThemeColors.ConnectUISettingsColorValuesChangedEventIfUninitialized();

            s_themeColorsChanged += value;
        }
        remove
        {
            s_themeColorsChanged -= value;

            if (s_themeColorsChanged is null || s_themeColorsChanged!.GetInvocationList().Length == 0)
            {
                s_themeColorsChanged = null;

                ThemeColors.DestroyUISettingsColorValuesChangedEventIfUnused();
            }
        }
    }

    private static void UISettings_ColorValuesChanged(Windows.UI.ViewManagement.UISettings sender, object args)
    {
        // capture current (active) system colors for foreground and background color
        var foregroundColor = s_uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Foreground);
        var backgroundColor = s_uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Background);
        ////
        //var accentColor = s_uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
        //var complementColor = s_uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Complement);
        ////
        //var accentLight1Color = s_uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.AccentLight1);
        //var accentLight2Color = s_uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.AccentLight2);
        //var accentLight3Color = s_uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.AccentLight3);
        ////
        //var accentDark1Color = s_uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.AccentDark1);
        //var accentDark2Color = s_uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.AccentDark2);
        //var accentDark3Color = s_uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.AccentDark3);

        bool isDarkColorTheme = ThemeColors.IsColorLight(foregroundColor);

        var themeColorsChangedEventArgs = new ThemeColorsChangedEventArgs()
        {
            ForegroundColor = foregroundColor,
            BackgroundColor = backgroundColor,
            IsDarkColorTheme = isDarkColorTheme,
        };

        // NOTE: to ensure that each event handler runs (even if one throws an exception), we send an event to each window separately in parallel
        var invocationList = s_themeColorsChanged?.GetInvocationList();
        if (invocationList is not null)
        {
            foreach (ThemeColorsChangedEventHandler element in invocationList!)
            {
                Task.Run(() => {
                    // NOTE: it is the target event's responsibility to run any UI-related code on the main UI thread; this event should be considered to be fired from a background thread
                    element.Invoke(null /* static class, no so type instance */, themeColorsChangedEventArgs);
                });
            }
        }
        //Task.Run(() =>
        //{
        //    s_themeColorsChanged?.Invoke(null /* static class, no so type instance */, new EventArgs());
        //});
    }

    //

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
            if (s_themeColorsChanged is null || s_themeColorsChanged!.GetInvocationList().Length == 0)
            {
                s_uiSettings.ColorValuesChanged -= UISettings_ColorValuesChanged;
                s_isWatchingUiSettingsColorValuesChangedEvent = false;
            }
        }
    }

}