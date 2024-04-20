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
using System.Windows.Media;
using System.Windows;
using System.Diagnostics;
using System.Reflection;

namespace Morphic.Theme;

internal class ThemeManager : IDisposable
{
    private bool disposedValue;

    private readonly Uri HIGHCONTRASTBLACK_THEME_URI = new("Themes/HighContrastBlack.xaml", UriKind.Relative);
    private readonly Uri HIGHCONTRASTWHITE_THEME_URI = new("Themes/HighContrastWhite.xaml", UriKind.Relative);
    private readonly Uri LIGHT_THEME_URI = new("Themes/Light.xaml", UriKind.Relative);
    private readonly Uri DARK_THEME_URI = new("Themes/Dark.xaml", UriKind.Relative);

    private const string THEME_AWARE_BACKGROUND_KEY = "ThemeAwareBackground";
    private const string THEME_AWARE_FOREGROUND_KEY = "ThemeAwareForeground";

    public static ThemeOption GetCurrentAppTheme()
    {
        // capture the initial light/dark theme state
        var isDarkColorTheme = Morphic.UI.ThemeColors.GetIsDarkColorTheme();
        //
        // capture the initial high contrast on/off state
        bool highContrastIsOn;
        var getHighContrastIsOnResult = Morphic.WindowsNative.Theme.HighContrast.GetIsOn();
        if (getHighContrastIsOnResult.IsSuccess == true)
        {
            highContrastIsOn = getHighContrastIsOnResult.Value!;
        }
        else
        {
            Debug.Assert(false, "Could not get high contrast on/off state");
            highContrastIsOn = false; // default to "high contrast is not on"
        }

        // set the theme (i.e. theme dictionary, application icon, etc.)
        ThemeOption theme;
        if (highContrastIsOn == true)
        {
            if (isDarkColorTheme == true)
            {
                theme = ThemeOption.HighContrastBlack;
            }
            else
            {
                theme = ThemeOption.HighContrastWhite;
            }
        }
        else
        {
            if (isDarkColorTheme == true)
            {
                theme = ThemeOption.Dark;
            }
            else
            {
                theme = ThemeOption.Light;
            }
        }

        return theme;
    }

    public ThemeManager()
    {
        // capture the current app theme
        var currentAppTheme = Morphic.Theme.ThemeManager.GetCurrentAppTheme();

        // set the initial theme for our application
        this.SetTheme(currentAppTheme);

        // wire up theme color change event (to detect dark/light mode changes as well as high contrast-related color changes and other theme color changes)
        Morphic.UI.ThemeColors.ThemeColorsChanged += this.ThemeColors_ThemeColorsChanged;

        // wire up high contrast on/off change event
        Morphic.WindowsNative.Theme.HighContrast.HighContrastIsOnChanged += this.HighContrast_HighContrastIsOnChanged;
    }

    //

    public void SetTheme(ThemeOption theme)
    {
        // determine the index of the "theme" resource dictionary entry
        int? indexOfThemeDictionary = null;
        for (var index = 0; index < App.Current.Resources.MergedDictionaries.Count; index += 1)
        {
            if (App.Current.Resources.MergedDictionaries[index].Contains(THEME_AWARE_BACKGROUND_KEY) == true)
            {
                indexOfThemeDictionary = index;
                break;
            }
        }
        if (indexOfThemeDictionary is null)
        {
            throw new Exception("Could not detect merged resource dictionary index of theme resource dictionary");
        }

        switch (theme)
        {
            case ThemeOption.Dark:
            case ThemeOption.HighContrastBlack:
            case ThemeOption.HighContrastWhite:
            case ThemeOption.Light:
                {
                    ResourceDictionary themeResourceDictionary;
                    switch (theme)
                    {
                        case ThemeOption.Dark:
                            themeResourceDictionary = new ResourceDictionary() { Source = DARK_THEME_URI };
                            break;
                        case ThemeOption.HighContrastBlack:
                            themeResourceDictionary = new ResourceDictionary() { Source = HIGHCONTRASTBLACK_THEME_URI };
                            break;
                        case ThemeOption.HighContrastWhite:
                            themeResourceDictionary = new ResourceDictionary() { Source = HIGHCONTRASTWHITE_THEME_URI };
                            break;
                        case ThemeOption.Light:
                            themeResourceDictionary = new ResourceDictionary() { Source = LIGHT_THEME_URI };
                            break;
                        default:
                            throw new Exception("invalid case");
                    }

                    // capture current system colors for background/foreground colors
                    var backgroundColor = Morphic.UI.ThemeColors.GetBackgroundColor();
                    var backgroundColorAsMediaColor = System.Windows.Media.Color.FromArgb(backgroundColor.A, backgroundColor.R, backgroundColor.G, backgroundColor.B);
                    themeResourceDictionary[THEME_AWARE_BACKGROUND_KEY] = new SolidColorBrush(backgroundColorAsMediaColor);
                    //
                    var foregroundColor = Morphic.UI.ThemeColors.GetForegroundColor();
                    var foregroundColorAsMediaColor = System.Windows.Media.Color.FromArgb(foregroundColor.A, foregroundColor.R, foregroundColor.G, foregroundColor.B);
                    themeResourceDictionary[THEME_AWARE_FOREGROUND_KEY] = new SolidColorBrush(foregroundColorAsMediaColor);

                    App.Current.Resources.MergedDictionaries[indexOfThemeDictionary!.Value] = themeResourceDictionary;

                    // update application icon

                    // update taskbar icon (button)
                    this.UpdateTaskbarButtonIcon(theme);
                }
                break;
            default:
                throw new Exception("unhandled case");
        }
    }

    internal void UpdateCurrentThemeColors(Windows.UI.Color foregroundColor, Windows.UI.Color backgroundColor)
    {
        // determine the index of the "theme" resource dictionary entry
        int? indexOfThemeDictionary = null;
        for (var index = 0; index < App.Current.Resources.MergedDictionaries.Count; index += 1)
        {
            if (App.Current.Resources.MergedDictionaries[index].Contains(THEME_AWARE_BACKGROUND_KEY) == true)
            {
                indexOfThemeDictionary = index;
                break;
            }
        }
        if (indexOfThemeDictionary is null)
        {
            throw new Exception("Could not detect merged resource dictionary index of theme resource dictionary");
        }

        var backgroundColorAsMediaColor = System.Windows.Media.Color.FromArgb(backgroundColor.A, backgroundColor.R, backgroundColor.G, backgroundColor.B);
        App.Current.Resources.MergedDictionaries[indexOfThemeDictionary!.Value][THEME_AWARE_BACKGROUND_KEY] = new SolidColorBrush(backgroundColorAsMediaColor);
        //
        var foregroundColorAsMediaColor = System.Windows.Media.Color.FromArgb(foregroundColor.A, foregroundColor.R, foregroundColor.G, foregroundColor.B);
        App.Current.Resources.MergedDictionaries[indexOfThemeDictionary!.Value][THEME_AWARE_FOREGROUND_KEY] = new SolidColorBrush(foregroundColorAsMediaColor);
    }

    //

    private void UpdateTheme(bool highContrastIsOn, bool isDarkColorTheme)
    {
        if (highContrastIsOn == true)
        {
            if (isDarkColorTheme == true)
            {
                this.SetTheme(Morphic.Theme.ThemeOption.HighContrastBlack);
            }
            else
            {
                this.SetTheme(Morphic.Theme.ThemeOption.HighContrastWhite);
            }
        }
        else
        {
            if (isDarkColorTheme == true)
            {
                this.SetTheme(Morphic.Theme.ThemeOption.Dark);
            }
            else
            {
                this.SetTheme(Morphic.Theme.ThemeOption.Light);
            }
        }
    }

    //

    public void UpdateTaskbarButtonIcon(Morphic.Theme.ThemeOption theme)
    {
        System.Drawing.Icon morphicIcon;
        switch (theme)
        {
            case ThemeOption.HighContrastBlack:
                {
                    var morphicIconStreamResourceInfo = Application.GetResourceStream(new Uri("pack://application:,,,/Morphic;component/Assets/Icons/morphic-highcontrastblack.ico"));
                    morphicIcon = new(morphicIconStreamResourceInfo.Stream);
                }
                break;
            case ThemeOption.HighContrastWhite:
                {
                    var morphicIconStreamResourceInfo = Application.GetResourceStream(new Uri("pack://application:,,,/Morphic;component/Assets/Icons/morphic-highcontrastwhite.ico"));
                    morphicIcon = new(morphicIconStreamResourceInfo.Stream);
                }
                break;
            case ThemeOption.Dark:
            case ThemeOption.Light:
                {
                    var morphicIconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Morphic.Assets.Icons.morphic.ico")!;
                    morphicIcon = new(morphicIconStream);
                }
                break;
            default:
                throw new Exception("invalid case");
        }
		
        // NOTE: Application.Current.HybridTrayIcon should always be non-null, but we use the nullable operator out of an abundance of caution (mostly to deal with the potential for out-of-order initialization at startup)
        if (((App)Application.Current).HybridTrayIcon is not null)
        {
            ((App)Application.Current).HybridTrayIcon.Icon = morphicIcon;
        }
    }

    //

    private void HighContrast_HighContrastIsOnChanged(object? sender, WindowsNative.Theme.HighContrast.HighContrastIsOnChangedEventArgs e)
    {
        var highContrastIsOn = e.IsOn;

        var getAppsUseLightThemeResult = Morphic.WindowsNative.Theme.LightTheme.GetAppsUseLightThemeSetting();
        if (getAppsUseLightThemeResult.IsError == true)
        {
            Debug.Assert(false, "Captured high contrast on/off change event, but cannot change app theme because light theme state capture failed");
            return;
        }

        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var isDarkColorTheme = Morphic.UI.ThemeColors.GetIsDarkColorTheme();
            this.UpdateTheme(highContrastIsOn, isDarkColorTheme);
        });
    }

    private void ThemeColors_ThemeColorsChanged(object? sender, UI.ThemeColors.ThemeColorsChangedEventArgs e)
    {
        var isDarkColorTheme = e.IsDarkColorTheme;

        var getHighContrastIsOnResult = Morphic.WindowsNative.Theme.HighContrast.GetIsOn();
        if (getHighContrastIsOnResult.IsError == true)
        {
            Debug.Assert(false, "Captured light theme change event, but cannot change app theme because high contrast on/off state capture failed");
            return;
        }
        bool highContrastIsOn = getHighContrastIsOnResult.Value!;

        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            this.UpdateTheme(highContrastIsOn, isDarkColorTheme);
            this.UpdateCurrentThemeColors(e.ForegroundColor, e.BackgroundColor);
        });
    }

    //

    protected virtual void Dispose(bool disposing)
    {
        if (!this.disposedValue)
        {
            if (disposing)
            {
                // dispose managed state (managed objects)

                // disconnect high contrast on/off change event; 
                try
                {
                    Morphic.WindowsNative.Theme.HighContrast.HighContrastIsOnChanged -= this.HighContrast_HighContrastIsOnChanged;
                }
                catch { }

                // other cleanup (not necessarily required)
                try
                {
                    Morphic.UI.ThemeColors.ThemeColorsChanged -= this.ThemeColors_ThemeColorsChanged;
                }
                catch { }
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // [none]

            // TODO: set large fields to null
            // [none]

            this.disposedValue = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~ThemeManager()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
