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
using System;

namespace Morphic.MorphicBar.BarControls;

// Three-state model for the in-progress visual. Names match the VisualStates in the
// "ProgressStates" VisualStateGroup of every button template in this project.
//   None      -- no progress visual; ProgressBar collapsed.
//   Preparing -- ProgressBar's indeterminate animation runs invisibly (Visibility=Visible,
//                IsIndeterminate=True, Opacity=0). Used during the show-delay so that, when
//                the bar is revealed, its animation appears mid-cycle rather than starting fresh.
//   Visible   -- ProgressBar visible at full opacity (Opacity reverts to default 1.0). The
//                CommonStates "InProgress" state is also entered while in this mode, which
//                flips BgBorder.Background to the pressed color as additional feedback.
internal enum InProgressVisual
{
    None,
    Preparing,
    Visible,
}

// Base class for the per-button-type state trackers used by ButtonCompoundState and
// ToggleButtonCompoundState. Holds the bookkeeping fields the two trackers share.
//
// Each subclass provides:
//   * ComputeStateName()         -- name of the active CommonStates VisualState (handles
//                                   pointer/checked + the "InProgress" override when the
//                                   in-progress visual is Visible).
//   * ComputeProgressStateName() -- name of the active ProgressStates VisualState (always
//                                   one of "None"/"Preparing"/"Visible"; identical for both
//                                   subclasses, so it lives on the base).
internal abstract class CompoundStateTrackerBase
{
    public bool IsPointerOver;
    public bool IsPressed;
    public InProgressVisual InProgressVisual;

    public abstract string ComputeStateName();

    public string ComputeProgressStateName()
    {
        return this.InProgressVisual switch
        {
            InProgressVisual.Preparing => "Preparing",
            InProgressVisual.Visible => "Visible",
            _ => "None",
        };
    }
}

// Shared infrastructure for compound-state helpers in this project: the attached property
// that pins a tracker onto a control, the pointer/enabled event subscriptions every helper
// needs, and the SetInProgressVisual entry point. The per-button-type helpers
// (ButtonCompoundState, ToggleButtonCompoundState) own only the parts that genuinely differ
// -- the StateTracker subclass and any extra event subscriptions specific to that button type.
internal static class CompoundStatePointerWiring
{
    private static readonly DependencyProperty StateTrackerProperty =
        DependencyProperty.RegisterAttached(
            "StateTracker",
            typeof(CompoundStateTrackerBase),
            typeof(CompoundStatePointerWiring),
            new PropertyMetadata(null));

    // Attaches the tracker to the control and wires the standard pointer + enabled events.
    // The caller-supplied `update` closure is invoked whenever a wired event fires; the
    // closure is expected to recompute the CommonStates name from the tracker and call
    // VisualStateManager.GoToState. ProgressStates transitions go through SetInProgressVisual,
    // not through `update`, because they are driven by action lifecycle rather than pointer
    // events and don't need to refire on every mouse move.
    public static void Wire(Control button, CompoundStateTrackerBase tracker, Action update)
    {
        button.SetValue(StateTrackerProperty, tracker);

        button.PointerEntered += (_, _) =>
        {
            tracker.IsPointerOver = true;
            update();
        };
        button.PointerExited += (_, _) =>
        {
            // also clear IsPressed -- WinUI typically "cancels" the pressed visual when the pointer
            // leaves the control, even if capture is still held
            tracker.IsPointerOver = false;
            tracker.IsPressed = false;
            update();
        };
        button.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(button).Properties.IsLeftButtonPressed)
            {
                tracker.IsPressed = true;
                update();
            }
        };
        button.PointerReleased += (_, _) =>
        {
            tracker.IsPressed = false;
            update();
        };
        button.PointerCaptureLost += (_, _) =>
        {
            tracker.IsPressed = false;
            update();
        };
        button.IsEnabledChanged += (_, _) => update();
    }

    // Updates the in-progress visual. Drives BOTH state groups:
    //   * CommonStates may need to enter or leave "InProgress" (which flips BgBorder.Background)
    //     when transitioning into or out of Visible.
    //   * ProgressStates always reflects the current InProgressVisual value.
    public static void SetInProgressVisual(Control button, InProgressVisual state)
    {
        if (button.GetValue(StateTrackerProperty) is not CompoundStateTrackerBase tracker)
        {
            return;
        }

        tracker.InProgressVisual = state;
        VisualStateManager.GoToState(button, tracker.ComputeStateName(), useTransitions: true);
        VisualStateManager.GoToState(button, tracker.ComputeProgressStateName(), useTransitions: true);
    }
}
