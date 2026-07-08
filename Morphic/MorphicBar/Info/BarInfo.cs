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

namespace Morphic.MorphicBar.Info;

// The contextual "Info" surface for a hovered MorphicBar control -- shown as either the big "Info panel"
// (InfoPanelWindow, the default) or a future "Info tip" (a WinUI TeachingTip). It replaces 1.x's "QuickHelp"
// window. This file holds the small shared plumbing; InfoPanelWindow.xaml{,.cs} is the window itself.

// Immutable content for the Info surface. Title = the control's name; Subtitle = a short description (rendered at a
// larger size). Hint = an optional secondary FOOTER line (the "right-click to change settings" note), rendered at
// the base size below the subtitle so it can be sized independently. A null/empty Subtitle means "nothing to show".
// DotsProvider is an optional live value-indicator (the Size of Text zoom "dots"): invoked at EACH hover (not
// cached) so it tracks changes -- the user clicking +/- or switching monitors -- like 1.x QuickHelp.
internal sealed record BarInfoContent(string Title, string? Subtitle, System.Func<InfoValueDots?>? DotsProvider = null, string? Hint = null);

// A row of Count dots (the one at FilledIndex filled, the rest outlined), optionally flanked by StartLabel (the
// low/left end) and EndLabel (the high/right end) -- a compact value indicator. Count = number of display-scale
// (zoom) steps; FilledIndex = the current step. For Size of Text: StartLabel "Smaller", EndLabel "Larger".
internal sealed record InfoValueDots(int Count, int FilledIndex, string? StartLabel = null, string? EndLabel = null);

// Attached property carrying the BarInfoContent for a hover target -- a bar item control (BarButtonControl /
// BarMultiButtonControl) or the Menu button. MorphicBarWindow reads it on hover by walking UP the visual tree
// from the pointer's element, exactly like the right-click context menu walks up to find its attached flyout.
internal static class BarInfo
{
    public static readonly Microsoft.UI.Xaml.DependencyProperty ContentProperty =
        Microsoft.UI.Xaml.DependencyProperty.RegisterAttached(
            "Content", typeof(BarInfoContent), typeof(BarInfo), new Microsoft.UI.Xaml.PropertyMetadata(null));

    public static void SetContent(Microsoft.UI.Xaml.DependencyObject element, BarInfoContent? value) => element.SetValue(ContentProperty, value);
    public static BarInfoContent? GetContent(Microsoft.UI.Xaml.DependencyObject element) => (BarInfoContent?)element.GetValue(ContentProperty);

    // Returns the shared "right-click to change settings" hint (for BarInfoContent.Hint, a separate footer line so
    // it can be sized independently of the description) when the control HAS a right-click Settings menu, else null.
    // It is surfaced automatically for every settings-backed button because many users do not realize the per-button
    // Settings menu exists (it is the whole reason we offer the right-click).
    public static string? SettingsHint(bool hasSettingsMenu)
    {
        return hasSettingsMenu ? Morphic.Localization.Strings.InfoRightClickSettingsHint : null;
    }
}

// Which side of the hovered control's anchor point the Info surface sits on -- the side facing AWAY from the bar.
// MorphicBarWindow maps its GetAwayFromBarPopupDirection onto this (it is decoupled from the bar's private enum).
internal enum InfoPlacement { Above, Below, Left, Right }

// Shows/hides the contextual Info surface for a hovered bar control. Two implementations exist behind this one
// interface: the big InfoPanelWindow ("Info panel", the default) and -- later -- a TeachingTip ("Info tip"); a
// code switch (eventually a Morphic Settings toggle) chooses one. The bar just computes a screen anchor +
// placement and hands over the content, so the presenter stays independent of the bar's internals.
internal interface IBarInfoPresenter
{
    // Show the info for a control. anchorScreenX/Y is the control corner on the AWAY-from-bar side in physical
    // screen pixels; placement is which side of that anchor the surface occupies; tailCenterFromLeadingEdgeDip is the
    // hovered control's half-extent along the bar, so a tail/pointer can aim at the control's center. Calling Show
    // again (a different control) before the prior Hide completes simply retargets -- it must not flicker.
    void Show(int anchorScreenX, int anchorScreenY, InfoPlacement placement, double tailCenterFromLeadingEdgeDip, BarInfoContent content);

    // Hide the surface. The implementation applies the small fade-out delay so sliding between adjacent buttons
    // does not flash the surface off and on.
    void Hide();

    // Hide the surface RIGHT NOW, with no fade-out delay -- for when the bar is hidden for a screen capture (snip),
    // so the surface can't be caught in the shot by the snip tool's screen freeze.
    void HideImmediately();

    // Warm the surface up at startup so the FIRST real Show is instant and flash-free (a freshly created window
    // renders a brief frame and has no XamlRoot until shown once). May be a no-op for kinds that do not need it.
    void Preload();

    // The surface's current screen rectangle (physical pixels), or null when it is not shown. The bar's WCAG
    // hover-bridge uses it to keep the panel alive while the pointer is over it or in the corridor moving toward it.
    Windows.Graphics.RectInt32? GetScreenRect();
}

// Default presenter: the big "Info panel" (InfoPanelWindow). Holds one reused window instance (lazily created on
// the UI thread the first time it is shown), matching 1.x's single-instance QuickHelp window.
internal sealed class InfoPanelPresenter : IBarInfoPresenter
{
    private InfoPanelWindow? _window;

    // Create + warm the window now (at bar startup) so the first hover does not flash or fail. See
    // InfoPanelWindow.Preload; the warm-up itself is deferred to the next idle, so this returns immediately.
    public void Preload()
    {
        _window ??= new InfoPanelWindow();
        _window.Preload();
    }

    public void Show(int anchorScreenX, int anchorScreenY, InfoPlacement placement, double tailCenterFromLeadingEdgeDip, BarInfoContent content)
    {
        _window ??= new InfoPanelWindow();
        _window.ShowInfo(anchorScreenX, anchorScreenY, placement, tailCenterFromLeadingEdgeDip, content);
    }

    public void Hide() => _window?.HideInfo();

    public void HideImmediately() => _window?.HideImmediately();

    public Windows.Graphics.RectInt32? GetScreenRect() => _window?.GetScreenRect();
}
