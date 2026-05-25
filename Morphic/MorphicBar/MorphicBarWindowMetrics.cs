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

using Microsoft.UI.Xaml;

namespace Morphic.MorphicBar;

// Centralized sizing constants for MorphicBarWindow's own chrome and layout
// (bar border, items panel margins, menu button placement, header chrome).
// Constants used INSIDE the bar's items live in BarControls.BarControlMetrics.
//
// Both XAML and code reference these, so a change here propagates to both sides 
// without duplicate maintenance.
public static class MorphicBarWindowMetrics
{
    // Outer bar border thickness (per side). The bar's chrome border is 1
    // logical px on each side; layout math (innerThicknessCap, content-area
    // calculations) subtracts this twice for the inside available area.
    public const double BarBorderThicknessPerSide = 1.0;
    //
    // XAML-facing form of BarBorderThicknessPerSide (so the OuterBorder's
    // BorderThickness can {x:Bind} to it -- BorderThickness is a Thickness,
    // not a double, and {x:Bind} doesn't apply type converters). Derived from
    // the scalar above so the two stay in sync.
    public static readonly Thickness BarBorderThickness = new(BarBorderThicknessPerSide);

    // Bar outer-corner radius (logical px). The MorphicBar's OuterBorder rounds
    // all four corners to this radius. The close button's outer corner (top-right
    // in LTR, top-left in RTL) also uses this radius so it aligns visually with
    // the bar's outer corner (as the close button sits flush against that corner).
    public const double BarCornerRadius = 5.0;

    // Defensive cap on the number of bar items that will be considered and
    // laid out. Items beyond this index are dropped silently. Not a perceptible
    // visual limit; protects against pathological data (e.g. 1000-item configs).
    public const int MaxBarItemCount = 128;

    // Default bar length and thickness in logical pixels.
    public const uint DefaultLogicalLength = 67;
    public const uint DefaultLogicalThickness = 67;

    // BarItemsPanel outer margin (different per bar orientation). The
    // fractional vertical components in the horizontal case (default 3.65) center
    // the items vertically within the bar's natural height, compensating for the
    // header's reserved-ascender top space.
    public static readonly Thickness ItemsPanelMarginHorizontal = new(10, 3.65, 13, 3.65);
    public static readonly Thickness ItemsPanelMarginVertical = new(7, 25, 7, 5);

    // BarItemsPanel inter-item spacing (along the layout axis). The gap that
    // appears between adjacent bar items in both orientations.
    public const double ItemsPanelSpacing = 15;

    // MorphicMenuButton (logo button) outer margin (different per bar
    // orientation).
    public static readonly Thickness MenuButtonMarginHorizontal = new(0, 0, 5, 0);
    public static readonly Thickness MenuButtonMarginVertical = new(0, 10, 0, 10);

    // MorphicMenuButton logo viewbox dimensions
    public const double MenuLogoWidth = 35;
    public const double MenuLogoHeight = 46;

    // Close button (top-right corner of the bar in horizontal orientation, top-left
    // in RTL flow direction; closes the MorphicBar window). Sized to match the
    // Windows standard window close-button convention.
    public const double CloseButtonWidth = 25;
    public const double CloseButtonHeight = 20;
    //
    // GridLength form of CloseButtonWidth for the close-button column in
    // MorphicBarWindow.xaml. The column width derives from the button width so
    // the two stay in sync (the column is sized to exactly hold the button).
    // Stored as GridLength (not double).
    public static readonly GridLength CloseButtonColumnGridLength = new(CloseButtonWidth);
}
