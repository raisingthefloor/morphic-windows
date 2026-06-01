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
using Morphic.Core;
using System;
using System.Diagnostics;

namespace Morphic.Controls.Theme;

// Base Window class that tracks the current light/dark theme AND high-contrast state, and
// (optionally) swaps the window's title-bar icon to a contrast-appropriate variant when those
// states change. Subclasses set zero, one, or all three icon paths via the XAML attributes
// StandardContrastIconPath / HighContrastBlackIconPath / HighContrastWhiteIconPath; the base
// picks the right one for the current system state and applies it via AppWindow.SetIcon. If a
// needed variant is missing the icon is left untouched (graceful degrade).
//
// NOTE: specifies icons as string paths + AppWindow.SetIcon (since WinUI 3 Window has no Icon 
// dependency property).
public class ThemeAwareBaseWindow : Window
{
    public ElementTheme CurrentTheme { get; private set; }
    public event EventHandler<ElementTheme>? ThemeChanged;

    private string? _standardContrastIconPath;
    private string? _highContrastBlackIconPath;
    private string? _highContrastWhiteIconPath;

    public ThemeAwareBaseWindow() : base()
    {
        // set the initial theme based on the legacy Win32 theme detection mechanism
        // NOTE: this won't account for WinUI-specific theme overrides, so the subclass should call SwitchToWinUIThemeTracking() after InitializeComponent() in its constructor
        this.CurrentTheme = Win32AppTheme.GetAppTheme();
        Win32AppTheme.ThemeChanged += AppTheme_ThemeChanged;

        // if dark mode is enabled for the app, color it appropriately
        _ = this.SetNonClientUIDarkModeAttribute(this.CurrentTheme == ElementTheme.Dark);

        // Wire up to the broad HC-setting signal so we catch every transition:
        //   * HC toggling on / off
        //   * HC swapping between its Black/White/etc. variants (HC stays on)
        // HighContrast.IsOnChanged only fires on the on/off case; SystemSettingsListener's
        // HighContrastChanged is driven by the SPI_SETHIGHCONTRAST broadcast which Windows
        // sends for ALL HC setting changes, so it covers the variant-swap case too.
        Morphic.WindowsNative.SystemSettings.SystemSettingsListener.Shared.HighContrastChanged += HighContrastSetting_Changed;

        // Unsubscribe both system-level handlers when the window closes; otherwise an HC
        // or theme change fired after Close would queue UpdateWindowIcon onto the closed
        // window's DispatcherQueue, and AppWindow.SetIcon would then throw a COMException
        // ("WinUI Desktop Window object has already been closed").
        this.Closed += this.ThemeAwareBaseWindow_Closed;
    }

    private void ThemeAwareBaseWindow_Closed(object sender, WindowEventArgs args)
    {
        Win32AppTheme.ThemeChanged -= AppTheme_ThemeChanged;
        Morphic.WindowsNative.SystemSettings.SystemSettingsListener.Shared.HighContrastChanged -= HighContrastSetting_Changed;
        this.Closed -= this.ThemeAwareBaseWindow_Closed;
    }

    private MorphicResult<MorphicUnit, MorphicUnit> SetNonClientUIDarkModeAttribute(bool value)
    {
        var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        return Morphic.WindowsNative.Theme.WindowUtils.SetNonClientUIDarkModeAttribute(windowHandle, value);
    }

    private void AppTheme_ThemeChanged(object? sender, ElementTheme e)
    {
        // NOTE: Win32AppTheme.ThemeChanged gets called even when it's just theme colors changing (i.e. not the overall theme itself), so only process this event when the actual theme has changed
        if (this.CurrentTheme != e)
        {
            this.CurrentTheme = e;
            DispatcherQueue.TryEnqueue(() =>
            {
                _ = this.SetNonClientUIDarkModeAttribute(e == ElementTheme.Dark);
                this.UpdateWindowIcon();
                this.ThemeChanged?.Invoke(this, CurrentTheme);
            });
        }
    }

    private void HighContrastSetting_Changed(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            // Refresh CurrentTheme since HC transitions can flip the effective theme
            // (HC-Black vs HC-White) without firing ActualThemeChanged: Windows leaves
            // the registry dark-mode bit alone when toggling HC or swapping HC variants,
            // so WinUI's ActualTheme signal doesn't notice. Win32AppTheme.GetAppTheme
            // reads the live foreground color, which DOES flip across HC variants, so
            // it's the reliable source.
            var freshTheme = Win32AppTheme.GetAppTheme();
            if (this.CurrentTheme != freshTheme)
            {
                this.CurrentTheme = freshTheme;
                this.ThemeChanged?.Invoke(this, this.CurrentTheme);
            }
            this.UpdateWindowIcon();
        });
    }

    // NOTE: this code should be called in the constructor for the subclass, after InitializeComponent();
    protected MorphicResult<MorphicUnit, MorphicUnit> SwitchToWinUIThemeTracking()
    {
        if (Content is FrameworkElement fe)
        {
            // remove fallback theme change event capture (from Win32 app theme-aware code)
            Win32AppTheme.ThemeChanged -= AppTheme_ThemeChanged;

            // Upgrade to the actual element theme (accounts for per-element overrides)
            var actualTheme = fe.ActualTheme;
            if (actualTheme != this.CurrentTheme)
            {
                this.CurrentTheme = actualTheme;
                ThemeChanged?.Invoke(this, this.CurrentTheme);
            }

            // capture theme changes via WinUI instead
            fe.ActualThemeChanged += (sender, _) => { AppTheme_ThemeChanged(sender, sender.ActualTheme); };

            return MorphicResult.OkResult();
        }
        else
        {
            return MorphicResult.ErrorResult();
        }
    }

    // Picks the appropriate icon for the current HC + light/dark state and applies it. Called
    // automatically when any icon-path property changes, when the system theme changes, and
    // when high-contrast is toggled on/off. If the variant for the current state was never
    // assigned, the existing icon is left alone (i.e. gracefully degrade).
    private void UpdateWindowIcon()
    {
        bool highContrastIsOn = false;
        var getHighContrastIsOnResult = Morphic.WindowsNative.Theme.HighContrast.GetIsOn();
        if (getHighContrastIsOnResult.IsSuccess == true)
        {
            highContrastIsOn = getHighContrastIsOnResult.Value!;
        }
        else
        {
            Debug.WriteLine("[ThemeAwareBaseWindow.UpdateWindowIcon] Cannot update window icon because high contrast on/off state capture failed");
            highContrastIsOn = false; // gracefully degrade
        }

        string? iconPath;
        if (highContrastIsOn == true)
        {
            // Read the live theme directly here (and don't trust the CurrentTheme cache) so
            // that HC-Black vs HC-White picks the right icon even if ActualThemeChanged missed
            // the variant swap (as Windows doesn't toggle the registry dark-mode bit on HC
            // swaps; only the foreground color flips, which Win32AppTheme.GetAppTheme reads).
            var liveTheme = Win32AppTheme.GetAppTheme();
            iconPath = (liveTheme == ElementTheme.Dark)
                ? _highContrastBlackIconPath
                : _highContrastWhiteIconPath;
        }
        else
        {
            iconPath = _standardContrastIconPath;
        }

        if (iconPath is not null)
        {
            try
            {
                // see: https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.windowing.appwindow.seticon?view=windows-app-sdk-1.0#microsoft-ui-windowing-appwindow-seticon(microsoft-ui-iconid)

		        // implementation option 1 (for packaged app):
		        //var uri = new Uri("ms-appx:///Assets/application.ico"); // specify iconPath as ms-appx:/// path
		        //StorageFile? storageFile = null;
		        //try
		        //{
		        //    storageFile = StorageFile.GetFileFromApplicationUriAsync(uri).GetAwaiter().GetResult();
		        //}
		        //catch/* (Exception ex)*/
		        //{
		        //    // Use default icon.
		        //}

		        //if (storageFile is not null)
		        //{
		        //    this.AppWindow.SetIcon(storageFile.Path);
		        //}

		        // implementation option 2 (for unpackaged app):
                this.AppWindow?.SetIcon(iconPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ThemeAwareBaseWindow.UpdateWindowIcon] AppWindow.SetIcon failed for path '{iconPath}': {ex.Message}");
            }
        }
    }

    // Three icon-path properties; setting any of them triggers an immediate UpdateWindowIcon. 
	// The XAML attribute form is:
    //   <theme:ThemeAwareBaseWindow
    //       StandardContrastIconPath="Assets/Icons/morphic-standardcontrast.ico"
    //       HighContrastBlackIconPath="Assets/Icons/morphic-highcontrastblack.ico"
    //       HighContrastWhiteIconPath="Assets/Icons/morphic-highcontrastwhite.ico" ...>
    public string? StandardContrastIconPath
    {
        get => _standardContrastIconPath;
        set
        {
            _standardContrastIconPath = value;
            this.UpdateWindowIcon();
        }
    }

    public string? HighContrastBlackIconPath
    {
        get => _highContrastBlackIconPath;
        set
        {
            _highContrastBlackIconPath = value;
            this.UpdateWindowIcon();
        }
    }

    public string? HighContrastWhiteIconPath
    {
        get => _highContrastWhiteIconPath;
        set
        {
            _highContrastWhiteIconPath = value;
            this.UpdateWindowIcon();
        }
    }
}
