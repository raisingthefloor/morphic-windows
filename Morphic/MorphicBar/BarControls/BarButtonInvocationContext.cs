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

namespace Morphic.MorphicBar.BarControls;

// Carries the input modality (keyboard / assistive-technology vs mouse / touch / pen) of the
// in-flight bar-button click from the shared click dispatcher (BarButtonBuilder.RunClickAsync) to
// the action delegate. The action delegate's signature -- (string? actionTag, bool? isChecked) ->
// Task<...> -- is fixed and shared by every bar button, so there is no parameter to carry the
// modality; this single ambient UI-thread slot bridges that gap.
//
// Race-free by construction: bar-button clicks are serialized on the UI thread, RunClickAsync sets
// this immediately before invoking the action (which DelayedInProgressVisual.RunAsync does
// synchronously, before any await), and modality-aware actions read it at their very top before
// their first await. No second click can interleave between the set and the read.
internal static class BarButtonInvocationContext
{
    // True when the in-flight click was raised by a keyboard or assistive-technology (programmatic)
    // invocation; false when raised by a mouse / touch / pen pointer. Defaults to true so that,
    // before any click has run, the value reflects the accessible default (keep focus on the bar).
    public static bool InvokedViaKeyboard { get; private set; } = true;

    public static void SetInvokedViaKeyboard(bool value)
    {
        BarButtonInvocationContext.InvokedViaKeyboard = value;
    }
}
