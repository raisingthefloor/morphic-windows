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

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Reflection;

namespace Morphic.AboutWindow;

public sealed partial class AboutWindow : Morphic.Theme.ThemeAwareBaseWindow
{
    private readonly Lazy<Version> _applicationVersion = new(() => Assembly.GetExecutingAssembly().GetName().Version! );

    public string VersionDisplayString
    {
        get
        {
            return $"version {_applicationVersion.Value.Major}.{_applicationVersion.Value.Minor}";
        }
    }
    //
    public string BuildDisplayString
    {
        get
        {
            var applicationVersion = Assembly.GetExecutingAssembly().GetName().Version!;
            return applicationVersion.Build != 0 ? $"(build {applicationVersion.Build})" : "(build unknown)";
        }
    }

    public AboutWindow()
    {
        InitializeComponent();
        // NOTE: we should call base.SwitchToWinUIThemeTracking() after InitializeComponent (to switch from Win32 theme tracking to WinUI theme tracking)
        base.SwitchToWinUIThemeTracking();

        // capture theme changes so we can update our iconography
        base.ThemeChanged += AboutWindow_ThemeChanged;

        // resize and recenter window
        const int newWidth = 450;
        const int newHeight = 420;
        //
        var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(this.AppWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
        var newX = (displayArea.WorkArea.Width - newWidth) / 2;
        var newY = (displayArea.WorkArea.Height - newHeight) / 2;
        //
        this.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(newX, newY, newWidth, newHeight));

        // disable resizing and the minimize and maximize buttons
        var appWindowPresenterAsOverlappedPresenter = (this.AppWindow.Presenter as OverlappedPresenter)!;
        appWindowPresenterAsOverlappedPresenter.IsResizable = false;
        appWindowPresenterAsOverlappedPresenter.IsMinimizable = false;
        appWindowPresenterAsOverlappedPresenter.IsMaximizable = false;

        // set our window icon
        this.SetIcon();

        // initialize our logo image (based on the current theme)
        this.UpdateLogoImage();
    }

    private void AboutWindow_ThemeChanged(object? sender, ElementTheme e)
    {
        this.UpdateLogoImage();
    }

    // see: https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.windowing.appwindow.seticon?view=windows-app-sdk-1.0#microsoft-ui-windowing-appwindow-seticon(microsoft-ui-iconid)
    private void SetIcon()
    {
        // implementation option 1 (for packaged app):
        //var uri = new Uri("ms-appx:///Assets/application.ico");
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
        this.AppWindow.SetIcon("Assets/Icons/morphic.ico");
    }

    private void UpdateLogoImage()
    {
        var themeFolder = this.CurrentTheme == ElementTheme.Dark ? "theme_dark" : "theme_light";
        var imagePath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Images", themeFolder, "Square50x50Logo.scale-400.png");
        this.LogoImage.Source = new BitmapImage(new Uri(imagePath));
    }

    private async void LearnMoreHyperlink_Click(object sender, RoutedEventArgs e)
    {
        await Windows.System.Launcher.LaunchUriAsync(new Uri("https://morphic.org"));
    }
}
