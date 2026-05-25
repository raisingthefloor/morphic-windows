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

// ToggleButton subclass that suppresses the framework's automatic toggle of IsChecked
// on click, unconditionally. The Click event still fires; the click handler is responsible
// for computing the new IsChecked value and writing it to the backing data, which then
// propagates back to the button's visual via the PropertyChanged subscription in ApplyData.
//
// Why: WinUI's stock ToggleButton auto-toggles IsChecked on every pointer click BEFORE the
// Click event fires. That auto-toggle is "optimistic UI": the visual flips immediately,
// and the click handler is expected to reconcile with backing state afterward. For our bar
// buttons -- where the click triggers a real OS state change (Dark mode, HC, etc.) that
// can take noticeable time AND can be observed via an external listener firing during the
// action -- the optimistic flip causes two problems:
//
//   1. If the user clicks again while the first action is in flight, the framework's
//      second auto-toggle flips the visual to the wrong state. Our handler's re-entry
//      guard rejects the action logically but cannot un-fire the visual flip.
//   2. If an external state listener (e.g. CachedDarkModeState) writes the new value to
//      data during the action, the post-action data write becomes a no-op (INPC equality
//      short-circuit). No PropertyChanged fires; the visual stays at whatever stale value
//      a spurious click left it at.
//
// By suppressing OnToggle entirely, the visual is ONLY changed by data-driven writes
// (via the PropertyChanged subscription). Data is the authoritative source; spurious
// clicks cannot disturb the visual at all.
//
// The brief "no-feedback" window between click and visual change is bounded by
// DelayedInProgressVisual.ShowDelay; within that window the InProgress CommonStates
// setter (BgBorder = pressed color) provides immediate feedback that the click was
// registered, so the user does see the button respond to their click.
internal sealed class GuardedToggleButton : Microsoft.UI.Xaml.Controls.Primitives.ToggleButton
{
    protected override void OnToggle()
    {
        // intentionally empty -- framework auto-toggle is fully suppressed.
        // IsChecked is managed by the click handler writing to backing data, which
        // propagates back via the BarButtonData.PropertyChanged subscription set up
        // in ApplyData on BarButtonControl / BarMultiButtonControl.
    }
}
