// Copyright 2026 Raising the Floor - US, Inc.
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

namespace Morphic.MorphicBar.Info;

// The big "Info panel": a transparent, topmost, click-through, non-activating overlay that pops up next to the
// MorphicBar showing the hovered control's name + a short description. Replaces 1.x's QuickHelp window.
//
// THE TEXT FIT (the deliberate rethink vs 1.x): 1.x squeezed everything into a FIXED 750x160 window via a Viewbox,
// so long localized strings scaled down to mush. Here the width is FIXED but the height GROWS to fit: the subtitle
// uses a fixed, readable font and WRAPS, and the window measures its content and resizes taller (clamped to the
// work area). The title scales down ONLY to a floor (TITLE_SCALE_FLOOR); past that it wraps instead of shrinking
// further. So the subtitle is never illegible, no matter the locale (we now ship 100+).
public sealed partial class InfoPanelWindow : Morphic.Controls.Windowing.TransparentBaseWindow
{
    // Fixed outer width; the window only ever grows in HEIGHT. Inner width = outer minus the ContentHost padding.
    private const double OUTER_WIDTH_DIP = 340.0;
    private const double PADDING_HORIZONTAL_DIP = 20.0;   // must match ContentHost Padding's horizontal value (XAML)
    private const double INNER_WIDTH_DIP = OUTER_WIDTH_DIP - (PADDING_HORIZONTAL_DIP * 2);

    // The body's rounded-corner radius. Single source of truth: the body+tail shape is built from it in code
    // (BuildPanelGeometry), so there is no XAML CornerRadius to duplicate.
    private const double CORNER_RADIUS_DIP = 10.0;

    private const double BASE_TITLE_FONT_SIZE = 22.0;
    // Subtitle is ~25% larger than the base (14 -> 18, the nearest natural size). The HINT footer stays at the base
    // size, so the secondary "right-click" note reads as smaller than the description.
    private const double SUBTITLE_FONT_SIZE = 18.0;
    private const double HINT_FONT_SIZE = 14.0;
    // The title shrinks to fit one line, but never below this fraction of the base size; past the floor it wraps.
    private const double TITLE_SCALE_FLOOR = 0.70;

    // Sliding between adjacent buttons hides one and shows the next; without a small delay that flashes the panel
    // off and on. HideInfo arms this timer; a ShowInfo before it fires cancels the hide (retarget, no flash).
    private const int HIDE_DELAY_MILLISECONDS = 200;

    // Value-indicator dots (the Size of Text zoom ladder): a white ring per step, the current step a filled white
    // disc. The disc-vs-ring shape is the primary cue; both use the same white as the text.
    private const double DOT_DIAMETER_DIP = 10.0;
    private const double DOT_STROKE_DIP = 1.5;
    private const double DOT_LABEL_FONT_SIZE = 14.0;   // the "Smaller"/"Larger" end labels flanking the dots

    // Speech-bubble tail on the panel's bar-facing edge, pointing at the MorphicBar. HEIGHT is how far it extends
    // toward the bar; the bar positions the panel this far PLUS the apex clearance from itself (see
    // GetBarGapInPixels), so the tip ends up a hairline off the bar. HALF_BASE is half its base width where it meets
    // the body. Its center along the bar is passed in per-show as the hovered button's half-extent from the panel's
    // leading corner, so the tail points at the BUTTON'S CENTER. Drawn as part of the single body+tail geometry
    // (BuildPanelGeometry), so it shares the body's fill with no join seam.
    private const double TAIL_HEIGHT_DIP = 7.0;
    private const double TAIL_HALF_BASE_DIP = 8.0;

    // How far the tail apex clears the MorphicBar (see GetBarGapInPixels). It is computed as its OWN scaled-pixel
    // value, floored at 1px, and ADDED to the tail's pixels to form the panel-to-bar gap -- so the clearance is a
    // deliberate, zoom-consistent hairline instead of an accident of rounding the gap and the tail separately (which
    // left the tip touching the bar at some scales, e.g. 100% / 125% / 200%). At ~0.5 DIP the floor makes it a crisp
    // 1px hairline through ~250% zoom and only grows past that; raise this to make the gap widen with zoom sooner.
    private const double APEX_CLEARANCE_DIP = 0.5;

    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _hideTimer;
    private readonly Microsoft.UI.Xaml.Media.Brush _dotBrush =
        new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White);

    private bool _isShown;

    // Inputs for the deferred (post-layout) measure-and-place pass.
    private int _pendingAnchorX;
    private int _pendingAnchorY;
    private InfoPlacement _pendingPlacement;
    // The tail's center along the bar, as the hovered button's half-extent from the panel's leading corner (DIP), so
    // the tail points at the button's center.
    private double _pendingTailCenterDip;

    public InfoPanelWindow()
    {
        // TransparentBaseWindow's constructor strips chrome and installs the transparent backdrop; this ctor only
        // adds the overlay styles (tool window + no-activate + click-through) and makes it topmost, mirroring
        // LayoutPreviewWindow's setup.
        this.InitializeComponent();

        _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        this.ContentHost.Width = InfoPanelWindow.OUTER_WIDTH_DIP;
        this.SubtitleText.FontSize = InfoPanelWindow.SUBTITLE_FONT_SIZE;
        this.HintText.FontSize = InfoPanelWindow.HINT_FONT_SIZE;

        // Mirror the app language's reading direction onto the BODY content (RTL when the app language is RTL). The
        // Canvas stays LeftToRight (pinned in XAML) so the tail's positional math in LayoutAndPlace is not mirrored.
        this.ContentHost.FlowDirection = Morphic.Localization.ReadingDirection.SessionFlowDirection;

        var hwnd = (Windows.Win32.Foundation.HWND)WinRT.Interop.WindowNative.GetWindowHandle(this);

        var presenter = this.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
        if (presenter is not null)
        {
            presenter.IsResizable = false;
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
            // Topmost so the help is visible above other windows; it pops AWAY from the bar so it does not overlap
            // the (also-topmost) bar in practice.
            presenter.IsAlwaysOnTop = true;
        }

        // WS_EX_TOOLWINDOW = no Alt+Tab entry; WS_EX_NOACTIVATE = cannot steal focus; WS_EX_TRANSPARENT = clicks
        // pass through (the panel is informational, like 1.x's IsHitTestVisible=False).
        var exStyle = (Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE)Windows.Win32.PInvoke.GetWindowLongPtr(hwnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        exStyle |= Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_TOOLWINDOW;
        exStyle |= Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_NOACTIVATE;
        exStyle |= Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_TRANSPARENT;
        _ = Windows.Win32.PInvoke.SetWindowLongPtr(hwnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (nint)exStyle);

        // Configuring the OverlappedPresenter above makes the Windows App SDK re-apply its window configuration,
        // which clobbers the base ctor's DWMWCP_DONOTROUND back to ROUND -- and on Win11, DWM corner rounding
        // brings its own compositor-drawn window-rect shadow with it. Through the tail band's transparent pixels
        // that shadow rendered as a light semi-transparent rounded band touching the bar. Re-assert no-rounding
        // (and no border) now that the presenter writes are done; the setting survives hide/re-show cycles.
        this.DisableDwmCornerRoundingAndBorder();
    }

    // Show (or retarget) the panel for a control. anchorScreenX/Y is the control corner on the away-from-bar side
    // in physical screen pixels; placement is which side of that anchor the panel sits; tailCenterFromLeadingEdgeDip
    // is the hovered button's half-extent along the bar, so the tail points at the button's center. Safe to call
    // repeatedly as the pointer slides between controls -- it cancels any pending hide and re-places without a flash.
    internal void ShowInfo(int anchorScreenX, int anchorScreenY, InfoPlacement placement, double tailCenterFromLeadingEdgeDip, BarInfoContent content)
    {
        _hideTimer?.Stop();

        this.TitleText.Text = content.Title;
        var hasSubtitle = string.IsNullOrWhiteSpace(content.Subtitle) == false;
        this.SubtitleText.Text = content.Subtitle ?? string.Empty;
        this.SubtitleText.Visibility = hasSubtitle ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        // Secondary footer hint (e.g. "Right-click to change settings"), on its own line at the base font size.
        var hasHint = string.IsNullOrWhiteSpace(content.Hint) == false;
        this.HintText.Text = content.Hint ?? string.Empty;
        this.HintText.Visibility = hasHint ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        // Value indicator (e.g. Size of Text zoom dots): invoke the provider fresh here so it reflects the CURRENT
        // level (the user may have clicked +/- since the last hover). No provider -> the row stays collapsed.
        this.RenderDots(content.DotsProvider?.Invoke());

        // Reset the title to base size / no-wrap; the deferred pass applies the scale-to-floor-then-wrap fit.
        this.TitleText.FontSize = InfoPanelWindow.BASE_TITLE_FONT_SIZE;
        this.TitleText.TextWrapping = Microsoft.UI.Xaml.TextWrapping.NoWrap;
        this.TitleText.MaxWidth = double.PositiveInfinity;

        _pendingAnchorX = anchorScreenX;
        _pendingAnchorY = anchorScreenY;
        _pendingTailCenterDip = tailCenterFromLeadingEdgeDip;
        _pendingPlacement = placement;

        // Hide the content while we re-measure + re-place it, then reveal in the deferred pass once it is correctly
        // sized and placed. On a FIRST show this hides the initial off-screen layout; on a RETARGET (sliding button
        // to button, panel already visible) it stops the PREVIOUS content from flashing at the new position for a
        // frame -- the window move otherwise outpaces the content re-render, compositing the stale content there.
        this.RootGrid.Opacity = 0;
        if (_isShown == false)
        {
            this.AppWindow.Show(false);   // non-activated
            _isShown = true;
        }

        // Defer the measure/place to the next dispatcher turn so the content has laid out and XamlRoot is set.
        _ = _dispatcherQueue.TryEnqueue(this.LayoutAndPlace);
    }

    // The window's current screen rectangle (physical pixels) while shown, else null. Used by the bar's WCAG
    // hover-bridge to test whether the cursor is over the panel.
    internal Windows.Graphics.RectInt32? GetScreenRect()
    {
        if (_isShown == false)
        {
            return null;
        }
        var position = this.AppWindow.Position;
        var size = this.AppWindow.Size;
        return new Windows.Graphics.RectInt32(position.X, position.Y, size.Width, size.Height);
    }

    // Hide after a short delay (so sliding between adjacent buttons does not flash the panel off/on). A ShowInfo
    // before the delay elapses cancels this.
    internal void HideInfo()
    {
        if (_isShown == false)
        {
            return;
        }

        _hideTimer ??= _dispatcherQueue.CreateTimer();
        _hideTimer.Interval = TimeSpan.FromMilliseconds(InfoPanelWindow.HIDE_DELAY_MILLISECONDS);
        _hideTimer.IsRepeating = false;
        _hideTimer.Tick -= this.OnHideTimerTick;
        _hideTimer.Tick += this.OnHideTimerTick;
        _hideTimer.Start();
    }

    private void OnHideTimerTick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        sender.Tick -= this.OnHideTimerTick;
        this.RootGrid.Opacity = 0;
        this.AppWindow.Hide();
        _isShown = false;
    }

    // Hide the panel RIGHT NOW, cancelling any pending delayed hide. Used when the bar is hidden for a screen
    // capture (snip): the normal HideInfo waits HIDE_DELAY_MILLISECONDS, and the snip tool freezes the screen the
    // instant its overlay appears, so a delayed hide can leave the panel in the captured image.
    internal void HideImmediately()
    {
        _hideTimer?.Stop();
        if (_isShown == false)
        {
            return;
        }
        this.RootGrid.Opacity = 0;
        this.AppWindow.Hide();
        _isShown = false;
    }

    // Warm the window up OFF-SCREEN once, deferred to the next idle (so the bar finishes initializing first): the
    // first time a WinUI window is shown it renders a brief frame before its transparent backdrop applies, and its
    // XamlRoot is not ready until it has been shown once. Doing that quietly at startup makes the first REAL hover
    // instant, correctly placed, and flash-free (the center-screen flash on first hover was that first render).
    internal void Preload()
    {
        _ = _dispatcherQueue.TryEnqueue(() =>
        {
            if (_isShown)
            {
                return;   // a real hover already beat the warm-up to it
            }
            this.TitleText.Text = " ";
            this.SubtitleText.Text = string.Empty;
            this.DotsPanel.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            this.ShapePath.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            this.RootGrid.Opacity = 0;
            this.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(-30000, -30000, 200, 120));
            this.AppWindow.Show(false);       // non-activated; renders the first frame off-screen
            this.ContentHost.UpdateLayout();  // force a layout pass so XamlRoot initializes
            this.AppWindow.Hide();
        });
    }

    // (Re)build the value-indicator dots for this show. Null/empty -> hide the row. Each step is a white ring; the
    // one at FilledIndex is a filled white disc, optionally flanked by StartLabel (low end) and EndLabel (high end).
    // FlowDirection (RTL) flips the whole row, keeping the low end at the reading start. LayoutAndPlace's height
    // measure then grows the window to fit it.
    private void RenderDots(InfoValueDots? dots)
    {
        this.DotsPanel.Children.Clear();
        if (dots is null || dots.Count <= 0)
        {
            this.DotsPanel.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            return;
        }

        if (string.IsNullOrWhiteSpace(dots.StartLabel) == false)
        {
            this.DotsPanel.Children.Add(this.CreateDotLabel(dots.StartLabel!));
        }

        for (int index = 0; index < dots.Count; index += 1)
        {
            var isCurrent = index == dots.FilledIndex;
            this.DotsPanel.Children.Add(new Microsoft.UI.Xaml.Shapes.Ellipse
            {
                Width = InfoPanelWindow.DOT_DIAMETER_DIP,
                Height = InfoPanelWindow.DOT_DIAMETER_DIP,
                Stroke = _dotBrush,
                StrokeThickness = InfoPanelWindow.DOT_STROKE_DIP,
                Fill = isCurrent ? _dotBrush : null,   // null Fill = an open ring; the current step is a filled disc
                VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center,
            });
        }

        if (string.IsNullOrWhiteSpace(dots.EndLabel) == false)
        {
            this.DotsPanel.Children.Add(this.CreateDotLabel(dots.EndLabel!));
        }

        this.DotsPanel.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
    }

    // A "Smaller"/"Larger" end label flanking the dots: white, base-sized, vertically centered on the dots.
    // TextLineBounds=Tight trims the line box to the glyphs so VerticalAlignment=Center lines the text up with the
    // dots (the default Full line box has leading above/below the glyphs, which floats the text off the dot center).
    private Microsoft.UI.Xaml.Controls.TextBlock CreateDotLabel(string text)
    {
        return new Microsoft.UI.Xaml.Controls.TextBlock
        {
            Text = text,
            Foreground = _dotBrush,
            FontSize = InfoPanelWindow.DOT_LABEL_FONT_SIZE,
            TextLineBounds = Microsoft.UI.Xaml.TextLineBounds.Tight,
            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center,
        };
    }

    // Deferred: apply the title fit, measure the content height, resize the window (fixed width, grown height), and
    // place it next to the bar; then reveal it.
    private void LayoutAndPlace()
    {
        // A hide (HideImmediately) can run between ShowInfo enqueuing this and it firing (e.g. the bar was hidden for
        // a screen capture the instant after a hover). Don't place/reveal a window that is no longer meant to show.
        if (_isShown == false)
        {
            return;
        }
        if (this.Content is not Microsoft.UI.Xaml.FrameworkElement root || root.XamlRoot is null)
        {
            return;
        }

        this.ApplyTitleFit();

        // Measure the body at the fixed outer width to learn how tall it needs to be.
        this.ContentHost.Measure(new Windows.Foundation.Size(InfoPanelWindow.OUTER_WIDTH_DIP, double.PositiveInfinity));
        var bodyWidthDip = InfoPanelWindow.OUTER_WIDTH_DIP;
        var bodyHeightDip = this.ContentHost.DesiredSize.Height;

        // Window = the body grown by the tail on the bar-facing side; the body is pushed off that edge by the tail.
        var tail = InfoPanelWindow.TAIL_HEIGHT_DIP;
        var horizontal = _pendingPlacement == InfoPlacement.Above || _pendingPlacement == InfoPlacement.Below;
        var windowWidthDip = horizontal ? bodyWidthDip : bodyWidthDip + tail;
        var windowHeightDip = horizontal ? bodyHeightDip + tail : bodyHeightDip;
        var bodyLeftDip = _pendingPlacement == InfoPlacement.Right ? tail : 0.0;
        var bodyTopDip = _pendingPlacement == InfoPlacement.Below ? tail : 0.0;
        Microsoft.UI.Xaml.Controls.Canvas.SetLeft(this.ContentHost, bodyLeftDip);
        Microsoft.UI.Xaml.Controls.Canvas.SetTop(this.ContentHost, bodyTopDip);

        var scale = root.XamlRoot.RasterizationScale;
        var widthPx = (int)Math.Ceiling(windowWidthDip * scale);
        var heightPx = (int)Math.Ceiling(windowHeightDip * scale);
        var tailPx = (int)Math.Round(tail * scale);

        var (unclampedX, unclampedY) = InfoPanelWindow.ComputeTopLeft(_pendingAnchorX, _pendingAnchorY, _pendingPlacement, widthPx, heightPx, tailPx);
        var (x, y) = InfoPanelWindow.ClampToWorkArea(unclampedX, unclampedY, widthPx, heightPx, _pendingAnchorX, _pendingAnchorY, scale);

        // A work-area clamp shifts the whole window (e.g. bar docked bottom-RIGHT, panel nudged left to stay on-
        // screen); the tail must keep pointing at the button's FIXED screen spot, so it moves the opposite way inside
        // the Canvas by that shift. Build the shape AFTER the clamp using it.
        var clampShiftAlongBarDip = (horizontal ? (x - unclampedX) : (y - unclampedY)) / scale;
        this.UpdatePanelShape(bodyLeftDip, bodyTopDip, bodyWidthDip, bodyHeightDip, clampShiftAlongBarDip);

        this.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, widthPx, heightPx));

        this.RootGrid.Opacity = 1;
    }

    // Title fit: shrink the title to fit one line on the fixed inner width, but never below TITLE_SCALE_FLOOR;
    // past the floor, wrap instead of shrinking further (so it stays legible).
    private void ApplyTitleFit()
    {
        this.TitleText.FontSize = InfoPanelWindow.BASE_TITLE_FONT_SIZE;
        this.TitleText.TextWrapping = Microsoft.UI.Xaml.TextWrapping.NoWrap;
        this.TitleText.MaxWidth = double.PositiveInfinity;
        this.TitleText.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        var naturalWidth = this.TitleText.DesiredSize.Width;

        if (naturalWidth <= InfoPanelWindow.INNER_WIDTH_DIP || naturalWidth <= 0)
        {
            return;   // fits at full size
        }

        var scale = Math.Max(InfoPanelWindow.TITLE_SCALE_FLOOR, InfoPanelWindow.INNER_WIDTH_DIP / naturalWidth);
        this.TitleText.FontSize = InfoPanelWindow.BASE_TITLE_FONT_SIZE * scale;
        if (naturalWidth * scale > InfoPanelWindow.INNER_WIDTH_DIP + 0.5)
        {
            // Even at the floor it overflows one line -> wrap within the inner width.
            this.TitleText.TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap;
            this.TitleText.MaxWidth = InfoPanelWindow.INNER_WIDTH_DIP;
        }
    }

    // Top-left (physical px) for the window given the anchor + placement + measured size. The anchor is where the
    // BODY's bar-facing edge sits (gap from the bar); the window is bigger than the body by tailPx on that side, so
    // the window origin is shifted toward the bar by the tail. ClampToWorkArea then nudges it fully on-screen.
    private static (int X, int Y) ComputeTopLeft(int anchorX, int anchorY, InfoPlacement placement, int widthPx, int heightPx, int tailPx)
    {
        int x = placement switch
        {
            InfoPlacement.Right => anchorX - tailPx,               // tail extends left toward the bar
            InfoPlacement.Left => anchorX - (widthPx - tailPx),    // body's right edge at the anchor; tail on the right
            _ => anchorX,
        };
        int y = placement switch
        {
            InfoPlacement.Below => anchorY - tailPx,               // tail extends up toward the bar
            InfoPlacement.Above => anchorY - (heightPx - tailPx),  // body's bottom edge at the anchor; tail below
            _ => anchorY,
        };
        return (x, y);
    }

    // Compute the tail's center along the bar-facing edge (clamped off the rounded corners), build the single
    // body+tail geometry, and assign it to ShapePath. The center aims the tail apex at the hovered button's center:
    // the passed half-extent from the panel's leading corner (RTL flips that for a horizontal bar), MINUS any
    // work-area clamp shift so the apex stays over the button when the panel was nudged on-screen. (Body size +
    // offset are computed by LayoutAndPlace, which owns the clamp.)
    private void UpdatePanelShape(double bodyLeftDip, double bodyTopDip, double bodyWidthDip, double bodyHeightDip, double clampShiftAlongBarDip)
    {
        var half = InfoPanelWindow.TAIL_HALF_BASE_DIP;
        var horizontal = _pendingPlacement == InfoPlacement.Above || _pendingPlacement == InfoPlacement.Below;

        var alongExtent = horizontal ? bodyWidthDip : bodyHeightDip;
        var minCenter = InfoPanelWindow.CORNER_RADIUS_DIP + half;
        var maxCenter = alongExtent - InfoPanelWindow.CORNER_RADIUS_DIP - half;
        var leadingCenter = _pendingTailCenterDip;
        if (horizontal && Morphic.Localization.ReadingDirection.AppIsRightToLeft)
        {
            // RTL: the panel's leading corner is on the right, so measure the button center back from that edge.
            leadingCenter = alongExtent - _pendingTailCenterDip;
        }
        leadingCenter -= clampShiftAlongBarDip;   // keep the apex over the button after a work-area clamp
        var center = Math.Max(minCenter, Math.Min(leadingCenter, maxCenter));

        // `center` is measured along the body from its near corner. The body's along-axis offset in the Canvas is 0
        // (Right offsets X only, Below offsets Y only), so adding that offset (a no-op today, but explicit) yields
        // the Canvas along-coordinate the geometry builder expects.
        var centerAlongCanvas = center + (horizontal ? bodyLeftDip : bodyTopDip);

        this.ShapePath.Data = this.BuildPanelGeometry(bodyLeftDip, bodyTopDip, bodyWidthDip, bodyHeightDip, centerAlongCanvas);
        this.ShapePath.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
    }

    // Build the panel outline (rounded-rectangle body + speech-bubble tail) as ONE closed geometry, so the tail joins
    // the body with no seam. Two separate semi-transparent shapes that merely abut leave an antialiasing hairline
    // along the join (a light line over light backgrounds, most visible on a left-docked bar). All coordinates are
    // Canvas DIPs; `center` is the tail's center along the bar-facing edge (a Canvas X for Above/Below, a Canvas Y
    // for Left/Right), already clamped off the rounded corners.
    private Microsoft.UI.Xaml.Media.Geometry BuildPanelGeometry(double bodyLeft, double bodyTop, double bodyWidth, double bodyHeight, double center)
    {
        var r = InfoPanelWindow.CORNER_RADIUS_DIP;
        var tail = InfoPanelWindow.TAIL_HEIGHT_DIP;
        var half = InfoPanelWindow.TAIL_HALF_BASE_DIP;
        var left = bodyLeft;
        var top = bodyTop;
        var right = bodyLeft + bodyWidth;
        var bottom = bodyTop + bodyHeight;

        var segments = new Microsoft.UI.Xaml.Media.PathSegmentCollection();
        void Line(double x, double y)
        {
            segments.Add(new Microsoft.UI.Xaml.Media.LineSegment { Point = new Windows.Foundation.Point(x, y) });
        }
        void Corner(double x, double y)
        {
            segments.Add(new Microsoft.UI.Xaml.Media.ArcSegment
            {
                Point = new Windows.Foundation.Point(x, y),
                Size = new Windows.Foundation.Size(r, r),
                SweepDirection = Microsoft.UI.Xaml.Media.SweepDirection.Clockwise,
            });
        }

        // Trace the outline clockwise from just after the top-left corner. On whichever edge faces the bar, the
        // straight run detours out to the tail apex and back; the two base points are ordered along that edge's
        // direction of travel.
        if (_pendingPlacement == InfoPlacement.Below)   // tail on the TOP edge, apex up (traveling +x)
        {
            Line(center - half, top);
            Line(center, top - tail);
            Line(center + half, top);
        }
        Line(right - r, top);
        Corner(right, top + r);

        if (_pendingPlacement == InfoPlacement.Left)    // tail on the RIGHT edge, apex right (traveling +y)
        {
            Line(right, center - half);
            Line(right + tail, center);
            Line(right, center + half);
        }
        Line(right, bottom - r);
        Corner(right - r, bottom);

        if (_pendingPlacement == InfoPlacement.Above)   // tail on the BOTTOM edge, apex down (traveling -x)
        {
            Line(center + half, bottom);
            Line(center, bottom + tail);
            Line(center - half, bottom);
        }
        Line(left + r, bottom);
        Corner(left, bottom - r);

        if (_pendingPlacement == InfoPlacement.Right)   // tail on the LEFT edge, apex left (traveling -y)
        {
            Line(left, center + half);
            Line(left - tail, center);
            Line(left, center - half);
        }
        Line(left, top + r);
        Corner(left + r, top);

        var figure = new Microsoft.UI.Xaml.Media.PathFigure
        {
            StartPoint = new Windows.Foundation.Point(left + r, top),
            IsClosed = true,
            IsFilled = true,
            Segments = segments,
        };
        var geometry = new Microsoft.UI.Xaml.Media.PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    // Nudge the window fully onto the anchor monitor's work area so it never spills off-screen -- e.g. bar docked
    // top/bottom-RIGHT, where the panel lines up with the rightmost button's leading edge and would otherwise run
    // off the right side. Keeps the SAME distance from the screen edge the MorphicBar uses when it floats (its
    // corner keepaway), and prefers keeping the panel's TOP-LEFT corner in view so content stays readable even if
    // the panel is bigger than the available room. Uses DisplayArea (WinUI) rather than raw MonitorFromPoint interop.
    private static (int X, int Y) ClampToWorkArea(int x, int y, int widthPx, int heightPx, int anchorX, int anchorY, double scale)
    {
        var display = Microsoft.UI.Windowing.DisplayArea.GetFromPoint(
            new Windows.Graphics.PointInt32(anchorX, anchorY),
            Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
        if (display is null)
        {
            return (x, y);
        }

        var work = display.WorkArea;
        var marginPx = (int)Math.Round(Morphic.MorphicBar.LayoutUtils.WINDOW_CORNER_DOCKING_DISTANCE_FROM_SCREEN_EDGE_IN_DEVICE_UNITS * scale);
        // Clamp so the whole panel fits inside the work area minus the margin; Math.Max wins if the panel is larger
        // than the room, pinning the leading (top-left) corner in view rather than the trailing edge.
        x = Math.Max(work.X + marginPx, Math.Min(x, work.X + work.Width - marginPx - widthPx));
        y = Math.Max(work.Y + marginPx, Math.Min(y, work.Y + work.Height - marginPx - heightPx));
        return (x, y);
    }

    // The panel-to-bar gap (the body's bar-facing edge to the bar) in PHYSICAL PIXELS for a given rasterization
    // scale. The MorphicBar calls this to position the panel. It is the tail's pixel length PLUS a floored, scaled
    // apex clearance -- and because the panel then draws the tail exactly TAIL_HEIGHT_DIP long (same rounding), the
    // leftover (this gap minus the tail) is precisely the clearance at EVERY zoom. Deriving the gap FROM the tail
    // like this is the fix: previously the gap and the tail were rounded independently (ceil vs round of two
    // separate DIP constants), so their difference -- the visible tip-to-bar clearance -- drifted between 0px
    // (touching) and 1px depending on the scale.
    internal static int GetBarGapInPixels(double scale)
    {
        var tailPx = (int)Math.Round(InfoPanelWindow.TAIL_HEIGHT_DIP * scale);
        var clearancePx = Math.Max(1, (int)Math.Round(InfoPanelWindow.APEX_CLEARANCE_DIP * scale));
        return tailPx + clearancePx;
    }
}
