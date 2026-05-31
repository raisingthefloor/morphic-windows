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

public sealed partial class AboutWindow : Morphic.Controls.Theme.ThemeAwareBaseWindow
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
        //
        // Design size is expressed in DIPs (effective pixels). The previous version
        // hardcoded physical-pixel constants (450 x 420) that happened to look right at
        // 150% scaling because at that DPI those physical pixels match a design footprint
        // of 300 x 280 DIPs. Multiplying the DIP-design size by the window's current monitor 
        // DPI here makes the AppWindow's physical size scale in lockstep with the XAML content, 
        // so the window fits the content at any DPI.
        var hwnd = (Windows.Win32.Foundation.HWND)WinRT.Interop.WindowNative.GetWindowHandle(this);
        var initialMonitorHandle = Windows.Win32.PInvoke.MonitorFromWindow(hwnd, Windows.Win32.Graphics.Gdi.MONITOR_FROM_FLAGS.MONITOR_DEFAULTTOPRIMARY);
        this.CenterOnMonitor(initialMonitorHandle);

        // disable resizing and the minimize and maximize buttons
        var appWindowPresenterAsOverlappedPresenter = (this.AppWindow.Presenter as OverlappedPresenter)!;
        appWindowPresenterAsOverlappedPresenter.IsResizable = false;
        appWindowPresenterAsOverlappedPresenter.IsMinimizable = false;
        appWindowPresenterAsOverlappedPresenter.IsMaximizable = false;

        // initialize our logo image (based on the current theme)
        this.UpdateLogoImage();
    }

    // Sizes the About window for the target monitor's DPI and centers it within that monitor's work
    // area. Used both at construction (initial placement) and by the App's About-menu handler to
    // (re-)center the single About box on whichever monitor the user launched it from.
    internal void CenterOnMonitor(Windows.Win32.Graphics.Gdi.HMONITOR monitorHandle)
    {
        // Design footprint in DIPs (effective pixels). Multiplying by the TARGET monitor's
        // rasterization scale yields a physical size that fits the XAML content at that monitor's
        // DPI, so the window scales in lockstep with its content on any display.
        const int DESIGN_WIDTH_DIPS = 308;
        const int DESIGN_HEIGHT_DIPS = 280;

        double scale = 1.0;
        var getScaleResult = Morphic.MorphicBar.LayoutUtils.GetRasterizationScaleForMonitor(monitorHandle);
        if (getScaleResult.IsSuccess == true)
        {
            scale = getScaleResult.Value!;
        }
        var width = (int)System.Math.Round(DESIGN_WIDTH_DIPS * scale);
        var height = (int)System.Math.Round(DESIGN_HEIGHT_DIPS * scale);

        // Center within the target monitor's WORK area (excludes the taskbar), in physical pixels.
        // Using the work area's left/top offset is what makes this correct on secondary monitors.
        bool gotMonitorInfo = false;
        var monitorInfo = new Windows.Win32.Graphics.Gdi.MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Windows.Win32.Graphics.Gdi.MONITORINFO>() };
        gotMonitorInfo = Windows.Win32.PInvoke.GetMonitorInfo(monitorHandle, ref monitorInfo);

        int x;
        int y;
        if (gotMonitorInfo == true)
        {
            var workArea = monitorInfo.rcWork;
            x = workArea.left + (((workArea.right - workArea.left) - width) / 2);
            y = workArea.top + (((workArea.bottom - workArea.top) - height) / 2);
        }
        else
        {
            // couldn't read the monitor; keep the current top-left and just apply the new size
            x = this.AppWindow.Position.X;
            y = this.AppWindow.Position.Y;
        }

        this.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, width, height));
    }

    private void AboutWindow_ThemeChanged(object? sender, ElementTheme e)
    {
        this.UpdateLogoImage();
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
