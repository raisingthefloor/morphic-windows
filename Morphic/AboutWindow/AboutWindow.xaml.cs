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

    // Design footprint in DIPs (effective pixels). Multiplied by the rasterization scale the content
    // actually renders at to get a physical size that fits the XAML content on any display.
    private const int DESIGN_WIDTH_DIPS = 308;
    private const int DESIGN_HEIGHT_DIPS = 280;

    // The monitor the box should be sized + centered on (the launch monitor; see CenterOnMonitor).
    private Windows.Win32.Graphics.Gdi.HMONITOR _targetMonitorHandle;
    // The rasterization scale we last sized to. Used to ignore XamlRoot.Changed notifications that are
    // only our own MoveAndResize (a size change, not a scale change), which would otherwise loop.
    private double _lastAppliedRasterizationScale = 0.0;
    // The XamlRoot we subscribed Changed on, kept so we can unsubscribe on Close.
    private XamlRoot? _subscribedXamlRoot;

    public string VersionDisplayString
    {
        get
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, Morphic.Localization.Strings.AboutVersionFormat, _applicationVersion.Value.Major, _applicationVersion.Value.Minor);
        }
    }
    //
    public string BuildDisplayString
    {
        get
        {
            var applicationVersion = Assembly.GetExecutingAssembly().GetName().Version!;
            return applicationVersion.Build != 0 ? string.Format(System.Globalization.CultureInfo.InvariantCulture, Morphic.Localization.Strings.AboutBuildFormat, applicationVersion.Build) : Morphic.Localization.Strings.AboutBuildUnknown;
        }
    }
    //
    // Copyright notice with the year range injected from code (so the years are not part of the
    // translatable string). CopyrightNoticeFormat = "Copyright {0} Raising the Floor - US, Inc.".
    public string CopyrightDisplayString
    {
        get
        {
            var yearRange = Morphic.App.COPYRIGHT_START_YEAR.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "-" + Morphic.App.COPYRIGHT_END_YEAR.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return string.Format(Morphic.Localization.Strings.CopyrightNoticeFormat, yearRange);
        }
    }

    public AboutWindow()
    {
        InitializeComponent();
        // NOTE: we should call base.SwitchToWinUIThemeTracking() after InitializeComponent (to switch from Win32 theme tracking to WinUI theme tracking)
        base.SwitchToWinUIThemeTracking();

        // Window.Title cannot be set via x:Uid, so apply the localized title from code.
        this.Title = Morphic.Localization.Strings.AboutWindowTitle;

        // capture theme changes so we can update our iconography
        base.ThemeChanged += AboutWindow_ThemeChanged;

        // Re-fit the window once its content is connected and we can read the scale it ACTUALLY
        // renders at, and unhook that tracking on close. The pre-show monitor-DPI estimate (in
        // ApplySizeAndCenterOnTargetMonitor) is not enough on a non-primary display: a WinUI window
        // is born on the primary monitor and keeps that rasterization scale when moved to another
        // monitor before being shown, so the content can render at the primary's scale while the
        // frame was sized for the launch display. RootContent_Loaded + XamlRoot.Changed correct that.
        if (this.Content is FrameworkElement rootContentForScaleTracking)
        {
            rootContentForScaleTracking.Loaded += this.RootContent_Loaded;
        }
        this.Closed += this.AboutWindow_Closed;

        // initial placement on the window's creation monitor (a best estimate before the content is
        // shown; re-applied with the true scale once Loaded fires)
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

    // Records the monitor the About box should appear on and (re-)fits the window there. Used at
    // construction (initial placement) and by the App's About-menu handler to (re-)center the single
    // About box on whichever monitor the user launched it from.
    internal void CenterOnMonitor(Windows.Win32.Graphics.Gdi.HMONITOR monitorHandle)
    {
        _targetMonitorHandle = monitorHandle;
        this.ApplySizeAndCenterOnTargetMonitor();
    }

    private void RootContent_Loaded(object sender, RoutedEventArgs e)
    {
        // The content is connected now, so XamlRoot (and its real RasterizationScale) is available.
        // Subscribe to scale changes once, so a later DPI settle / cross-monitor move re-fits, then
        // re-apply immediately with the now-known scale (this is the step that corrects a box shown
        // on a non-primary display, where the pre-show monitor-DPI estimate did not match the
        // content's actual scale).
        var xamlRoot = (this.Content as FrameworkElement)?.XamlRoot;
        if (xamlRoot is not null && _subscribedXamlRoot is null)
        {
            _subscribedXamlRoot = xamlRoot;
            _subscribedXamlRoot.Changed += this.XamlRoot_Changed;
        }
        this.ApplySizeAndCenterOnTargetMonitor();
    }

    private void XamlRoot_Changed(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        // XamlRoot.Changed fires on rasterization-scale AND size changes (including the size changes
        // our own MoveAndResize causes). Only re-fit when the SCALE actually changed: that both does
        // the real work (a DPI settle, or a cross-monitor move while shown) and avoids looping on our
        // own resize.
        if (sender.RasterizationScale != _lastAppliedRasterizationScale)
        {
            this.ApplySizeAndCenterOnTargetMonitor();
        }
    }

    private void AboutWindow_Closed(object sender, WindowEventArgs args)
    {
        if (_subscribedXamlRoot is not null)
        {
            _subscribedXamlRoot.Changed -= this.XamlRoot_Changed;
            _subscribedXamlRoot = null;
        }
    }

    // Sizes the About window to fit its content and centers it within the target monitor's work area.
    private void ApplySizeAndCenterOnTargetMonitor()
    {
        // Size from the scale the content ACTUALLY renders at whenever we can read it (XamlRoot), so
        // the frame always matches the content and never clips or over-sizes. Before the content is
        // shown XamlRoot is null, so fall back to the target monitor's DPI as a first estimate;
        // RootContent_Loaded re-applies with the real scale once it is available. (A WinUI window is
        // born on the primary monitor and keeps that rasterization scale when moved to another monitor
        // before being shown, so the monitor-DPI estimate alone is wrong on a non-primary display:
        // the regression this fixes.)
        double rasterizationScale = 1.0;
        var xamlRoot = (this.Content as FrameworkElement)?.XamlRoot;
        if (xamlRoot is not null && xamlRoot.RasterizationScale > 0.0)
        {
            rasterizationScale = xamlRoot.RasterizationScale;
        }
        else
        {
            var getScaleResult = Morphic.MorphicBar.LayoutUtils.GetRasterizationScaleForMonitor(_targetMonitorHandle);
            if (getScaleResult.IsSuccess == true)
            {
                rasterizationScale = getScaleResult.Value!;
            }
        }
        _lastAppliedRasterizationScale = rasterizationScale;

        var width = (int)System.Math.Round(AboutWindow.DESIGN_WIDTH_DIPS * rasterizationScale);
        var height = (int)System.Math.Round(AboutWindow.DESIGN_HEIGHT_DIPS * rasterizationScale);

        // Center within the target monitor's WORK area (excludes the taskbar), in physical pixels.
        // Using the work area's left/top offset is what makes this correct on secondary monitors.
        bool gotMonitorInfo;
        var monitorInfo = new Windows.Win32.Graphics.Gdi.MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Windows.Win32.Graphics.Gdi.MONITORINFO>() };
        gotMonitorInfo = Windows.Win32.PInvoke.GetMonitorInfo(_targetMonitorHandle, ref monitorInfo);

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
