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

using Microsoft.UI.Xaml.Controls;

namespace Morphic.MorphicBar.BarControls;

// Interface implemented by every control that can sit on the MorphicBar.
// Allows the bar to hold heterogeneous items (ButtonBarControl, MultiButtonBarControl, future types) in a single list
// and to propagate bar-level concerns (such as orientation) uniformly to all items.
public interface IBarItemControl
{
    // The orientation the item should render for. Driven by the MorphicBar's own orientation:
    // when the bar docks horizontally, items use Horizontal; when the bar docks vertically, items use Vertical.
    // Items may use this to adjust their layout, text wrapping, or size behavior accordingly.
    Orientation Orientation { get; set; }
}
