// Copyright 2020-2026 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-controls-lib-cs/blob/main/LICENSE.txt
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

namespace Morphic.Controls.Tooltip;

// A small borderless always-on-top window that renders a single string in a Windows 11
// styled tooltip surface (rounded corners, theme-aware fill/border, multi-line wrap).
// Inherits TransparentBaseWindow so the corners outside the rounded XAML Border are truly
// transparent (no Window backdrop showing through behind the rounded shape).
//
// Reusable by any control that needs a modern tooltip; callers decide where to place the
// window (the tooltip itself only sizes to its text and shows / hides on demand).
//
// Lifecycle: create once and keep alive. Call SetText to update content and learn the
// resulting physical size, then ShowAt to position. Call Hide to dismiss. The window
// stays "shown" (parked offscreen) between visible appearances so a fresh XAML layout
// pass is not required on every Show.
public sealed partial class TooltipWindow : Morphic.Controls.Windowing.TransparentBaseWindow, IDisposable
{
    // Parking position for the hidden state. Far enough off any plausible virtual screen
    // that the window is not even partially visible to the user.
    private const int OFFSCREEN_X = -32000;
    private const int OFFSCREEN_Y = -32000;

    private bool _disposed = false;
    private Windows.Win32.Foundation.HWND _hwnd;

    public TooltipWindow()
    {
        this.InitializeComponent();

        // TransparentBaseWindow already stripped chrome and set up the transparent backdrop;
        // we additionally pin to the topmost z-band and hide from ALT-TAB / taskbar switchers.
        var presenter = this.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
        if (presenter is not null)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsResizable = false;
        }
        this.AppWindow.IsShownInSwitchers = false;

        // Allow the window to shrink to the tooltip's natural size. Without this override
        // the OS enforces the OverlappedPresenter default minimum (~196x156 DIPs) on every
        // AppWindow.MoveAndResize call, which inflates the rendered tooltip well beyond its
        // text. The override is opt-in on the base class so other TransparentBaseWindow
        // users keep the standard minimum.
        this.SetMinimumTrackSize(1, 1);

        _hwnd = (Windows.Win32.Foundation.HWND)WinRT.Interop.WindowNative.GetWindowHandle(this);

        // Park offscreen and Show once so XAML layout passes run when SetText is called.
        // A WinUI 3 Window that has never been shown will not measure its content. By keeping
        // the window "shown" but parked, every SetText measures correctly without us having
        // to toggle visibility.
        this.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(OFFSCREEN_X, OFFSCREEN_Y, 1, 1));
        this.AppWindow.Show();

        this.ApplyThemeColors();
        Morphic.WindowsNative.Theme.DarkMode.SystemUsesDarkModeChanged += this.OnSystemThemeChanged;
        Morphic.WindowsNative.Theme.HighContrast.IsOnChanged += this.OnHighContrastChanged;
    }

    // Updates the tooltip's text and returns the resulting outer size in physical pixels
    // for a display at the given DPI. Callers use this to compute exact placement (e.g.
    // "above the tray button, centered") before calling ShowAt.
    public Windows.Graphics.SizeInt32 SetText(string text, uint targetDpi)
    {
        this.TooltipTextBlock.Text = text;

        // Measure with TextWrapping temporarily off so we get the unconstrained natural
        // single-line width, regardless of MaxWidth or any cached wrap state from a prior
        // pass. If that natural width exceeds the desired wrap point (MaxWidth), we then
        // re-measure with wrap enabled and a known-large cap. This sidesteps reading the
        // TextBlock.MaxWidth property (which appears to return an unexpected value before
        // the visual tree has fully settled).
        var savedWrapping = this.TooltipTextBlock.TextWrapping;
        this.TooltipTextBlock.TextWrapping = Microsoft.UI.Xaml.TextWrapping.NoWrap;
        this.TooltipTextBlock.InvalidateMeasure();
        this.TooltipTextBlock.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        var textDesired = this.TooltipTextBlock.DesiredSize;

        const double wrapCapDip = 320.0;
        if (textDesired.Width > wrapCapDip)
        {
            this.TooltipTextBlock.TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap;
            this.TooltipTextBlock.InvalidateMeasure();
            this.TooltipTextBlock.Measure(new Windows.Foundation.Size(wrapCapDip, double.PositiveInfinity));
            textDesired = this.TooltipTextBlock.DesiredSize;
        }
        else
        {
            // Restore the original wrapping mode (so the XAML default stays in effect for
            // arrange, even though the natural width fit without wrap).
            this.TooltipTextBlock.TextWrapping = savedWrapping;
        }

        // The TextBlock's natural width comes back as a fractional DIP value (e.g.
        // 44.666... for "Morphic"). When the Window is later sized to exactly that width,
        // the arrange pass computes the TextBlock's available width back to the same
        // fractional value -- but rasterization/rounding inside TextBlock can shave off
        // a sub-DIP and trigger TextWrapping=Wrap to wrap the line. Rounding up and
        // adding a tiny slack avoids the edge-case wrap without making the tooltip
        // visibly larger.
        const double TEXT_SLACK_DIP = 2.0;
        double textWidthDip = System.Math.Ceiling(textDesired.Width) + TEXT_SLACK_DIP;
        double textHeightDip = System.Math.Ceiling(textDesired.Height);

        var padding = this.RootBorder.Padding;
        var borderThickness = this.RootBorder.BorderThickness;
        double widthDip = textWidthDip + padding.Left + padding.Right + borderThickness.Left + borderThickness.Right;
        double heightDip = textHeightDip + padding.Top + padding.Bottom + borderThickness.Top + borderThickness.Bottom;

        // +1 physical pixel on each axis prevents sub-pixel clipping of the Border's
        // outline at fractional DPI scales (e.g. 150%). At 1.5x, a 31-DIP Border maps to
        // exactly 46.5 physical px and is centered in a 47-px window, leaving 0.25 px
        // slack each side -- enough for the bottom AA pixel of the rounded border to
        // fall outside the rendered area, which reads as a "cut-off" corner in HC light
        // mode (where the border is full-contrast). The extra pixel guarantees the
        // border + AA always renders inside the window bounds.
        double scale = targetDpi / 96.0;
        return new Windows.Graphics.SizeInt32(
            (int)System.Math.Ceiling(widthDip * scale) + 1,
            (int)System.Math.Ceiling(heightDip * scale) + 1);
    }

    // Moves the tooltip to the given screen position (top-left, physical pixels), resizes
    // to the given size, and explicitly re-asserts HWND_TOPMOST so the tooltip rises above
    // peer topmost windows (the OverlappedPresenter.IsAlwaysOnTop set in the constructor
    // places it in the topmost z-band, but doesn't *raise* it within that band on each
    // show, so a peer topmost window like the MorphicBar can sit above it otherwise).
    public void ShowAt(Windows.Graphics.PointInt32 screenTopLeft, Windows.Graphics.SizeInt32 sizePhysical)
    {
        // Re-read system colors on every show. Switching HC themes (Black <-> White) keeps HC
        // on, so HighContrast.IsOnChanged does not fire and our event-driven palette stays
        // stale. Re-applying here picks up whatever sys colors are live right now.
        this.ApplyThemeColors();

        // AppWindow can be null during a teardown race (window closed mid-call), same reason
        // Hide guards with ?. -- skip the move/raise in that case rather than throw.
        var appWindow = this.AppWindow;
        if (appWindow is null) { return; }

        appWindow.MoveAndResize(new Windows.Graphics.RectInt32(
            screenTopLeft.X, screenTopLeft.Y, sizePhysical.Width, sizePhysical.Height));

        _ = Windows.Win32.PInvoke.SetWindowPos(
            _hwnd,
            Windows.Win32.Foundation.HWND.HWND_TOPMOST,
            0, 0, 0, 0,
            Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOMOVE |
            Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
            Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
    }

    // Parks the window offscreen. Stays "shown" so subsequent SetText calls measure
    // immediately (no fresh layout pass needed).
    public void Hide()
    {
        this.AppWindow?.MoveAndResize(new Windows.Graphics.RectInt32(OFFSCREEN_X, OFFSCREEN_Y, 1, 1));
    }

    private void OnSystemThemeChanged(object? sender, Morphic.WindowsNative.Theme.DarkModeChangedEventArgs e)
    {
        this.DispatcherQueue.TryEnqueue(this.ApplyThemeColors);
    }

    private void OnHighContrastChanged(object? sender, Morphic.WindowsNative.Theme.HighContrastIsOnChangedEventArgs e)
    {
        this.DispatcherQueue.TryEnqueue(this.ApplyThemeColors);
    }

    // Approximates the Windows 11 system-tooltip palette:
    //   * non-HC light: near-white background, subtle gray border, near-black text
    //   * non-HC dark : dark gray background, subtle dark border, near-white text
    //   * HC (either) : COLOR_WINDOW / COLOR_WINDOWTEXT live from GetSysColor so the tooltip
    //                   follows the active HC palette (HC-Black: black bg + white text;
    //                   HC-White: white bg + black text).
    //                   NOTE: COLOR_INFOBK / COLOR_INFOTEXT (the classic-Windows "tooltip"
    //                   sys-color slots) don't follow HC themes correctly in modern Windows --
    //                   they retain a near-white background in HC-Black, which is the wrong
    //                   contrast direction. COLOR_WINDOW is the reliable HC anchor.
    private void ApplyThemeColors()
    {
        // Guard against a queued DispatcherQueue callback running after Dispose has closed
        // the window: touching this.RootBorder / TooltipTextBlock at that point throws a
        // COMException ("WinUI Desktop Window object has already been closed").
        if (_disposed) { return; }

        bool isHighContrast = false;
        var hcResult = Morphic.WindowsNative.Theme.HighContrast.GetIsOn();
        if (hcResult.IsSuccess)
        {
            isHighContrast = hcResult.Value!;
        }

        if (isHighContrast)
        {
            var backgroundColor = GetSysColorAsWinUiColor(Windows.Win32.Graphics.Gdi.SYS_COLOR_INDEX.COLOR_WINDOW);
            var textColor = GetSysColorAsWinUiColor(Windows.Win32.Graphics.Gdi.SYS_COLOR_INDEX.COLOR_WINDOWTEXT);
            var borderColor = GetSysColorAsWinUiColor(Windows.Win32.Graphics.Gdi.SYS_COLOR_INDEX.COLOR_WINDOWTEXT);

            this.RootBorder.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(backgroundColor);
            this.RootBorder.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(borderColor);
            this.TooltipTextBlock.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(textColor);
        }
        else
        {
            bool isDark = false;
            var darkResult = Morphic.WindowsNative.Theme.DarkMode.GetSystemUsesDarkMode();
            if (darkResult.IsSuccess && darkResult.Value is bool darkValue)
            {
                isDark = darkValue;
            }

            if (isDark)
            {
                this.RootBorder.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x2C, 0x2C, 0x2C));
                this.RootBorder.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x3F, 0x3F, 0x3F));
                this.TooltipTextBlock.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xF0, 0xF0, 0xF0));
            }
            else
            {
                // Light mode bg uses a very pale lime tint, matching the system tooltip look
                // that Win11 applies in light non-HC mode regardless of the user's accent
                // color. (Win11 likely achieves this via Mica/Acrylic + a fixed tooltip tint;
                // we approximate with an opaque equivalent so it works on our transparent
                // backdrop without acrylic.)
                this.RootBorder.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xF1, 0xF5, 0xEC));
                this.RootBorder.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xE0, 0xE0, 0xE0));
                this.TooltipTextBlock.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x18, 0x18, 0x18));
            }
        }
    }

    // GetSysColor returns COLORREF (0x00BBGGRR). Convert to a WinUI ARGB color with full alpha.
    private static Windows.UI.Color GetSysColorAsWinUiColor(Windows.Win32.Graphics.Gdi.SYS_COLOR_INDEX index)
    {
        uint colorRef = Windows.Win32.PInvoke.GetSysColor(index);
        byte r = (byte)(colorRef & 0xFF);
        byte g = (byte)((colorRef >> 8) & 0xFF);
        byte b = (byte)((colorRef >> 16) & 0xFF);
        return Windows.UI.Color.FromArgb(0xFF, r, g, b);
    }

    public void Dispose()
    {
        if (_disposed) { return; }
        _disposed = true;

        Morphic.WindowsNative.Theme.DarkMode.SystemUsesDarkModeChanged -= this.OnSystemThemeChanged;
        Morphic.WindowsNative.Theme.HighContrast.IsOnChanged -= this.OnHighContrastChanged;

        this.Close();
    }
}
