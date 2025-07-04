﻿using Morphic.Client.Bar.UI;
using Morphic.Client.Config;
using Morphic.Client.Dialogs;
using Morphic.WindowsNative.Input;
using Morphic.WindowsNative.OsVersion;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Morphic.Client.MainMenu;

public partial class MorphicMainMenu : ContextMenu
{
    internal enum MenuOpenedSource
    {
        trayIcon,
        morphicBarIcon
    }
    private MenuOpenedSource? _menuOpenedSource;

    public App App => App.Current;

    private Morphic.Client.AboutWindow.AboutWindow? _aboutWindow = null;

    public MorphicMainMenu()
    {
        this.DataContext = this;
        this.InitializeComponent();
    }

    protected override void OnInitialized(EventArgs e)
    {
        // if ConfigurableFeatures.SignInIsEnabled is false, then hide the settings which allow the user to sign in
        if (ConfigurableFeatures.SignInIsEnabled == false)
        {
            this.LoginItem.Visibility = Visibility.Collapsed;
            //this.LogoutItem.Visibility = Visibility.Collapsed; // NOTE: this is not necessary because it is invisible by default; in a future update, we should consider using one menu item to sign in/out and relabel it (i.e. treat it as a toggle) for simplicity
            //
            this.LoginSeparator.Visibility = Visibility.Collapsed;
        }

        // if ConfigurableFeatures.CloudSettingsTransferIsEnabled is false, then hide the settings which can transfer/restore settings
        if (ConfigurableFeatures.CloudSettingsTransferIsEnabled == false)
        {
            this.ChangeSetupMenuItem.Visibility = Visibility.Collapsed;
            this.SaveMySetupMenuItem.Visibility = Visibility.Collapsed;
            this.RestoreSettingsFromBackupMenuItem.Visibility = Visibility.Collapsed;
            //
            this.CloudSettingsSeparator.Visibility = Visibility.Collapsed;
        }

        // if ConfigurableFeatures.CustomMorphicBarsIsEnabled is false, then hide the settings which can change the current MorphicBar
        if (ConfigurableFeatures.CustomMorphicBarsIsEnabled == false)
        {
            this.ChangeMorphicBar.Visibility = Visibility.Collapsed;
            this.CustomizeMorphicBar.Visibility = Visibility.Collapsed;
            //
            this.ChangeMorphicBarSeparator.Visibility = Visibility.Collapsed;
        }

        base.OnInitialized(e);
    }

    protected override void OnOpened(RoutedEventArgs e)
    {
        // if Morphic was installed for all users, disable the ability to enable/disable autorun
        // NOTE: in Morphic v2.0, we should let each user specify whether the app should start or not--even in this circumstance--possibly by just immediately shutting down if Morphic is started
        if (App.WasInstalledUsingEnterpriseInstaller() == true)
        {
            this.AutorunAfterLoginItem.Visibility = Visibility.Collapsed;
        }
        //
        // if autorun settings are configured by config.json, do not give the user the option to enable/disable
        if (ConfigurableFeatures.AutorunConfig is not null)
        {
            this.AutorunAfterLoginItem.Visibility = Visibility.Collapsed;
        }

        // if morphicBarVisibilityAfterLogin settings are configured by config.json, do not give the user the option to enable/disable
        if (ConfigurableFeatures.MorphicBarVisibilityAfterLogin is not null)
        {
            this.ShowMorphicBarAfterLoginItem.Visibility = Visibility.Collapsed;
        }

        this.ShowBar.Visibility = (!this.App.BarManager.BarVisible).ToVisibility();
        this.HideBar.Visibility = this.App.BarManager.BarVisible.ToVisibility();

        if (ConfigurableFeatures.SignInIsEnabled == true)
        {
            this.LoginItem.Visibility = (!this.App.MorphicSession.SignedIn).ToVisibility();
            this.LogoutItem.Visibility = this.App.MorphicSession.SignedIn.ToVisibility();
        }
        else
        {
            this.LoginItem.Visibility = Visibility.Collapsed;
            this.LogoutItem.Visibility = Visibility.Collapsed;
        }

        base.OnOpened(e);
    }

    internal async Task ShowAsync(Control? control = null, MenuOpenedSource? menuOpenedSource = null)
    {
        _menuOpenedSource = menuOpenedSource;

        if (control is null)
        {
            this.Placement = PlacementMode.Mouse;
            this.PlacementTarget = null;
        }
        else
        {
            this.Placement = PlacementMode.Top;
            this.PlacementTarget = control;
        }

        // to prevent the bottom of our menu from being covered, defer any "resurface task button" checks until our menu is closed; we do this because resurfacing the tray button--whose owner is the taskbar--can cause our menu to be covered by the taskbar; this is especially problematic when a timer does this frequently
        App.Current.SuppressTaskbarButtonResurfaceChecks(true);
        //
        // [when the menu is closed, we will cancel the suppression so that our task button can continue resurfacing itself
        this.Closed += (sender, eventArgs) => {
             App.Current.SuppressTaskbarButtonResurfaceChecks(false);
        };
        //
        // open our menu
        this.IsOpen = true;

        await App.Current.Telemetry_RecordEventAsync("showMenu");
    }

    private async void ShowBarClick(object sender, RoutedEventArgs e)
    {
        this.App.BarManager.ShowBar();
        //
        await App.Current.Telemetry_RecordEventAsync("morphicBarShow");
    }

    private async void HideBarClick(object sender, RoutedEventArgs e)
    {
        this.App.BarManager.HideBar();
        //
        await App.Current.Telemetry_RecordEventAsync("morphicBarHide");
    }

    private async void AutorunAfterLoginClicked(object sender, RoutedEventArgs e)
    {
        switch (AutorunAfterLoginItem.IsChecked)
        {
            case true:
                await App.Current.Telemetry_RecordEventAsync("autorunAfterLoginEnabled");
                break;
            case false:
                await App.Current.Telemetry_RecordEventAsync("autorunAfterLoginDisabled");
                break;
        }
    }

    private async void ShowMorphicBarAfterLoginClicked(object sender, RoutedEventArgs e)
    {
        switch (ShowMorphicBarAfterLoginItem.IsChecked)
        {
            case true:
                await App.Current.Telemetry_RecordEventAsync("showMorphicBarAfterLoginEnabled");
                break;
            case false:
                await App.Current.Telemetry_RecordEventAsync("showMorphicBarAfterLoginDisabled");
                break;
        }
    }

    private async void WindowsSettingsAllAccessibilityOptionsClicked(object sender, RoutedEventArgs e)
    {
        string settingsUrlAsPath = null!; // required to quiet the "not initialized error"
        var windowsVersion = OsVersion.GetWindowsVersion();

        if (windowsVersion is not null)
        {
            switch (windowsVersion)
            {
                case WindowsVersion.Win10_v21H2:
                case WindowsVersion.Win10_v22H2:
                    // Windows 10 21H2, 22H2
                    // NOTE: we should re-evaluate this path in all versions of Windows (to verify that it shouldn't be simply "ms-settings:easeofaccess" instead)
                    settingsUrlAsPath = "ms-settings:easeofaccess-display";
                    break;
                case WindowsVersion.Win10_vFuture:
                    // OBSERVATION: this may be the wrong path for future verisons of Windows (especially since Win10 and Win11 _may_ treat this differently post-21H1); re-evaluate this logic
                    settingsUrlAsPath = "ms-settings:easeofaccess-display";
                    break;
                case WindowsVersion.Win11_v21H2:
                case WindowsVersion.Win11_v22H2:
                case WindowsVersion.Win11_v23H2:
                case WindowsVersion.Win11_v24H2:
                case WindowsVersion.Win11_vFuture:
                    // Windows 11 21H2 (and assumed for the future)
                    settingsUrlAsPath = "ms-settings:easeofaccess";
                    break;
                default:
                    // not supported
                    Debug.Assert(false, "This build of Windows is not supported");
                    return;
            }
        }
        else
        {
            // not supported
            Debug.Assert(false, "This build of Windows is not supported");
            return;
        }

        MorphicMenuItem.OpenMenuItemPath(settingsUrlAsPath);
        await MorphicMenuItem.RecordMenuItemTelemetryAsync(settingsUrlAsPath, ((MorphicMenuItem)sender).ParentMenuType, ((MorphicMenuItem)sender).TelemetryType, ((MorphicMenuItem)sender).TelemetryCategory);
    }

    private async void WindowsSettingsPointerSizeClicked(object sender, RoutedEventArgs e)
    {
        string settingsUrlAsPath = null!; // required to quiet the "not initialized error"
        var windowsVersion = OsVersion.GetWindowsVersion();

        if (windowsVersion is not null)
        {
            switch (windowsVersion)
            {
                case WindowsVersion.Win10_v21H2:
                case WindowsVersion.Win10_v22H2:
                case WindowsVersion.Win10_vFuture:
                    // Windows 10 21H1, Windows 10 21H2 (and assumed for the future)
                    settingsUrlAsPath = "ms-settings:easeofaccess-mousepointer";
                    break;
                case WindowsVersion.Win11_v21H2:
                case WindowsVersion.Win11_v22H2:
                case WindowsVersion.Win11_v23H2:
                case WindowsVersion.Win11_v24H2:
                case WindowsVersion.Win11_vFuture:
                    // Windows 11 21H2 (and assumed for the future)
                    settingsUrlAsPath = "ms-settings:easeofaccess-mousepointer";
                    break;
                default:
                    // not supported
                    Debug.Assert(false, "This build of Windows is not supported");
                    return;
            }
        }
        else
        {
            // not supported
            Debug.Assert(false, "This build of Windows is not supported");
            return;
        }

        MorphicMenuItem.OpenMenuItemPath(settingsUrlAsPath);
        await MorphicMenuItem.RecordMenuItemTelemetryAsync(settingsUrlAsPath, ((MorphicMenuItem)sender).ParentMenuType, ((MorphicMenuItem)sender).TelemetryType, ((MorphicMenuItem)sender).TelemetryCategory);
    }

    private void StopKeyRepeatInit(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            menuItem.IsChecked = Keyboard.KeyRepeat();
        }
    }
		//
    private async void StopKeyRepeatToggle(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            menuItem.IsChecked = Keyboard.KeyRepeat(menuItem.IsChecked);

            if (menuItem.IsChecked == true)
            {
                await App.Current.Telemetry_RecordEventAsync("stopKeyRepeatOn");
            }
            else
            {
                await App.Current.Telemetry_RecordEventAsync("stopKeyRepeatOff");
            }
        }
    }

    private async void Logout(object sender, RoutedEventArgs e)
    {
        AppOptions.Current.LastCommunity = null;
        AppOptions.Current.LastMorphicbarId = null;
        await App.Current.MorphicSession.SignOut();
    }

    private async void Login(object sender, RoutedEventArgs e)
    {
        // NOTE: if we want the login menu item to apply cloud-saved preferences after login, we should set this flag to true
        var applyPreferencesAfterLogin = ConfigurableFeatures.CloudSettingsTransferIsEnabled;
        var args = new Dictionary<string, object?>() { { "applyPreferencesAfterLogin", applyPreferencesAfterLogin } };
        await App.Current.Dialogs.OpenDialogAsync<LoginWindow>(args);
    }

    private async void CustomizeMorphicbarClicked(object sender, RoutedEventArgs e)
    {
        await App.Current.Telemetry_RecordEventAsync("customizeMorphicbar");

        // NOTE: when we make "navigate to URL" a custom action (rather than something linked in the menu itself), then we should navigate to the appsettings value for the key"BarEditorWebAppUrlAsString"
    }

    private async void ContactUsClicked(object sender, RoutedEventArgs e)
    {
        await App.Current.Telemetry_RecordEventAsync("contactUs");
    }

    private async void ExploreMorphicClicked(object sender, RoutedEventArgs e)
    {
        await App.Current.Telemetry_RecordEventAsync("exploreMorphic");
    }

    private async void HowToCopySetupsClicked(object sender, RoutedEventArgs e)
    {
        await App.Current.Telemetry_RecordEventAsync("howToCopySetups");
    }

    private async void QuickDemoVideosClicked(object sender, RoutedEventArgs e)
    {
        await App.Current.Telemetry_RecordEventAsync("quickDemoVideo");
    }

    private async void OtherHelpfulThingsClicked(object sender, RoutedEventArgs e)
    {
        await App.Current.Telemetry_RecordEventAsync("otherHelpfulThings");
    }

    private async void AboutMorphicMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await App.Current.Telemetry_RecordEventAsync("aboutMorphic");

        // NOTE: we only create one instance of the about window (so that the user doesn't pop up multiple instances)
        if (_aboutWindow is null)
        {
            _aboutWindow = new AboutWindow.AboutWindow();
        }
        // NOTE: we clean up the window's reference when it is closed
        _aboutWindow.Closed += delegate (object? sender, EventArgs e)
        {
            _aboutWindow = null;
        };

        // show the window (and activate it, in case it was already visible--but obscured by another UI element)
        try
        {
            // NOTE: there is a potential edge case where our window was previously closed but not set to null; that should not happen--but if it does then these functions could result in an exception (since a window cannot be shown once it's closed)
            _aboutWindow?.Show();
            _aboutWindow?.Activate();
        }
        catch
        {
            Debug.Assert(false, "Could not show About window.");
        }
    }

    private async void QuitMorphicMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await App.Current.Telemetry_RecordEventAsync("quit");

        this.App.BarManager.CloseBar();
        this.App.Shutdown();
    }


    private void SelectBasicMorphicBarClick(object sender, RoutedEventArgs e)
    {
        AppOptions.Current.LastCommunity = null;
        AppOptions.Current.LastMorphicbarId = null;
        App.Current.BarManager.LoadBasicMorphicBar();
    }
}

