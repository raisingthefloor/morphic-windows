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

using System.Threading.Tasks;

namespace Morphic.MorphicBar.BarControls;

// Invoked when a BarButtonData-backed button is clicked.
// - param `actionTag` is the button's optional ActionTag (opaque caller-supplied value).
// - param `isChecked` is the new checked state for toggle buttons (null for non-toggle buttons).
public delegate Task BarButtonAction(string? actionTag, bool? isChecked);

public class BarButtonData
{
    // Optional label displayed above the button. When null or empty, no header is rendered.
    public string? Header { get; set; }

	// Contents for text component of button
    public string Text { get; set; } = "";

    // Name exposed to screen readers (via AutomationProperties.Name); falls back to `Text` if null.
    public string? AccessibleName { get; set; }

    public string? Tooltip { get; set; }

    public BarButtonLayoutStyle LayoutStyle { get; set; } = BarButtonLayoutStyle.TextOnly;

    // When true, the button renders as a ToggleButton and maintains checked state.
    public bool IsToggle { get; set; }

    // Initial checked state; only meaningful when IsToggle is true.
    public bool IsChecked { get; set; }

    // Opaque caller-supplied value passed back to the Action callback.
    public string? ActionTag { get; set; }

    public BarButtonAction? Action { get; set; }
}
