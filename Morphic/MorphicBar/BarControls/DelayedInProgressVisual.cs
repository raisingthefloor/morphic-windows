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

using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Threading.Tasks;

namespace Morphic.MorphicBar.BarControls;

// Runs a button's async action with delayed in-progress visual feedback. The CommonStates
// "InProgress" visual (BgBorder = pressed color) fires IMMEDIATELY at click time, regardless
// of action length, so the user always sees instant feedback that their click registered.
// The indeterminate ProgressBar is delayed: quick actions finish before
// DelayedInProgressVisual.ShowDelay elapses and never paint a bar; long actions reveal the bar
// after DelayedInProgressVisual.ShowDelay and clear it on completion. Used by both
// BarButtonControl and BarMultiButtonControl click handlers.
//
// Why the "Preparing" Opacity=0 trick:
//   WinUI's indeterminate ProgressBar runs a continuous animation Storyboard whose phase
//   resets each time IsIndeterminate flips False -> True (or each time the bar transitions
//   from collapsed -> visible). If we simply waited DelayedInProgressVisual.ShowDelay and 
//   THEN set the bar visible, the user would see the animation start fresh -- a small but 
//   noticeable "burst of activity" at the moment of reveal. Instead, we make the bar 
//   Visible+IsIndeterminate immediately at click time but with Opacity=0 so it's invisible. The 
//   animation runs for the full DelayedInProgressVisual.ShowDelay; when we then drop Opacity 
//   (Preparing -> Visible) the animation is already DelayedInProgressVisual.ShowDelay into its
//   cycle and looks like it's been running quietly all along.
//
// See ProgressStates VisualStateGroup in BarButtonControl.xaml / BarMultiButtonControl.xaml
// for the matching XAML setters, and ComputeStateName in {Button,ToggleButton}CompoundState
// for the rule that maps InProgressVisual != None to the CommonStates "InProgress" state.
internal static class DelayedInProgressVisual
{
    // How long an action must run before we reveal the indeterminate progress bar.
    // Tunable. 200ms is in the typical band for "delay-before-progress-indicator"
    // (GTK's spinner-show-delay defaults to 300ms; CSS transition libs hover around
    // 200-300ms). Below ~100ms feels jumpy (bar flashes for trivially fast actions);
    // above ~500ms users start wondering if the click registered.
    public static readonly TimeSpan ShowDelay = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Runs <paramref name="action"/> with delayed in-progress feedback on
    /// <paramref name="button"/>, returning the action's result. The in-progress visual is
    /// always cleared on completion (success, returned error, or thrown exception). Safe to
    /// call only on the UI thread.
    /// </summary>
    public static async Task<TResult> RunAsync<TResult>(ButtonBase button, Func<Task<TResult>> action)
    {
        // start the indeterminate animation invisibly so that, once revealed, it appears
        // mid-cycle rather than freshly starting (see file-level comment for the why)
        CompoundStatePointerWiring.SetInProgressVisual(button, InProgressVisual.Preparing);
        try
        {
            // Race the action against DelayedInProgressVisual.ShowDelay. Whichever completes first decides
            // whether we reveal the bar.
            //   * delayTask wins -> action is still running; reveal the bar.
            //   * actionTask wins -> action completed before the threshold; never reveal.
            //
            // Edge case (Task.WhenAny resumed because delayTask completed, but actionTask
            // also just completed in the gap): re-check actionTask.IsCompleted before
            // calling SetInProgressVisual(Visible), so we don't briefly flash the bar
            // for an action that's already done. The two checks happen consecutively on
            // the UI thread with no await between them, so action completion that races
            // past Task.WhenAny but before the IsCompleted check is the only remaining
            // window -- vanishingly small in practice.
            var actionTask = action();
            var delayTask = Task.Delay(DelayedInProgressVisual.ShowDelay);
            var firstCompleted = await Task.WhenAny(actionTask, delayTask);

            if (firstCompleted == delayTask && !actionTask.IsCompleted)
            {
                CompoundStatePointerWiring.SetInProgressVisual(button, InProgressVisual.Visible);
            }

            // observe and return the action's result (propagates any thrown exception)
            return await actionTask;
        }
        finally
        {
            // always clear the in-progress visual, regardless of which path was taken
            // (Preparing-only, Preparing -> Visible, or thrown exception)
            CompoundStatePointerWiring.SetInProgressVisual(button, InProgressVisual.None);
        }
    }
}
