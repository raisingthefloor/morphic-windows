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

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Morphic.MorphicBar.BarControls;

// Header-text measurement helper. Keeps the vertical MorphicBar wide enough that the longest single WORD
// in any section header fits without wrapping mid-character -- a problem surfaced by longer translated
// headers (e.g. Spanish "Magnificador", German compounds), where the header's TextWrapping="Wrap" otherwise
// breaks a too-wide single word between letters (WinUI Wrap does emergency character breaks).
internal static class BarHeaderText
{
    // A couple of logical px of headroom added on top of the measured word width. The bar reserves the word's
    // (fractional) width as its thickness, but WinUI layout-rounds the header's ARRANGED width down a hair --
    // enough to shave the last glyph onto a second line. This slack absorbs that rounding; it is the
    // "make the bar a little wider" cushion.
    private const double WIDEST_WORD_SLACK_IN_DIPS = 2.0;

    // Width (in DIPs) of the widest single space-delimited word in the header (rounded up and cushioned), or 0
    // when the header is hidden or empty.
    //
    // Measures on a DETACHED probe TextBlock that mirrors the live header's text-affecting properties, rather
    // than mutating the live header. A detached TextBlock still computes valid text metrics, so this stays
    // correct even when called mid-measure before the live header has a finished layout.
    public static double WidestWordWidth(TextBlock? header)
    {
        if (header is null || header.Visibility != Visibility.Visible || string.IsNullOrWhiteSpace(header.Text))
        {
            return 0.0;
        }

        // Mirror only the properties that affect glyph advance widths; NoWrap so each single word measures at
        // its full one-line width.
        var probe = new TextBlock
        {
            FontFamily = header.FontFamily,
            FontSize = header.FontSize,
            FontWeight = header.FontWeight,
            FontStyle = header.FontStyle,
            FontStretch = header.FontStretch,
            CharacterSpacing = header.CharacterSpacing,
            TextWrapping = TextWrapping.NoWrap,
        };

        var infinite = new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity);
        double widest = 0.0;
        foreach (var word in header.Text.Split((char[]?)null, System.StringSplitOptions.RemoveEmptyEntries))
        {
            probe.Text = word;
            probe.Measure(infinite);
            if (probe.DesiredSize.Width > widest) { widest = probe.DesiredSize.Width; }
        }

        return (widest > 0.0) ? System.Math.Ceiling(widest) + BarHeaderText.WIDEST_WORD_SLACK_IN_DIPS : 0.0;
    }
}
