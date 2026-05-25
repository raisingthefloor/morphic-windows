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

namespace Morphic.MorphicBar.BarControls;

// Centralized sizing constants for the BarControl family (BarButtonControl,
// BarMultiButtonControl). Both XAML and code reference these, so a change 
// here propagates to both sides without duplicate maintenance.
public static class BarControlMetrics
{
    // Button typography (BarButtonControl + BarMultiButtonControl button templates,
    // header TextBlocks)
    public const double ButtonFontSize = 14;

    // Button geometry
    public const double ButtonMinHeight = 27;
    public const double ButtonCornerRadius = 5.0;
    //
    public static readonly Thickness ButtonBorderThickness = new(2);

    // Multi-button-specific: per-side gap between adjacent sub-buttons (so the
    // group's outer rounded corners still read as a single capsule). 0.5 logical
    // px per side gives a 1-px visible gap.
    public const double ButtonInnerMargin = 0.5;

    // Multi-button-specific: per-column width cap when SizingMode is
    // StretchToLargest. Prevents one long label from widening the entire group.
    public const double MaxSubButtonWidth = 150.0;

    // Button text padding (logical px). Same value in plain + toggle templates,
    // and same in single-button + multi-button controls.
    public static readonly Thickness ButtonTextPadding = new(9, 3, 9, 3);

    // ProgressBar dimensions; ProgressBars are shown inside buttons during in-progress action visual
    public const double ProgressBarHeight = 2;

    // Gap below the group header text and above the button container. Applied
    // to HeaderTextBlock in both BarButtonControl and BarMultiButtonControl.
    public static readonly Thickness HeaderBottomMargin = new(0, 0, 0, 3);

    // Bottom margin applied to the button container in HORIZONTAL bar mode. The
    // font's reserved ascender area at the top of the header (empty space above
    // the caps for accent marks like é, ü, ô) shifts the header text visually
    // downward by ~1.5 logical px; this matching bottom margin counterbalances
    // it, keeping the item's content visually vertically centered within the
    // bar. In vertical bar mode the value is 0 instead (per-item bottom margins
    // would multiply across stacked items and push later items off the bar).
    // BarButtonControl.RootContainer mirrors this value unconditionally so that
    // single-button items align with multi-button items across the bar.
    public static readonly Thickness ButtonContainerBottomMargin_Horizontal = new(0, 0, 0, 3);
}
