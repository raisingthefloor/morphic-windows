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
using Microsoft.UI.Xaml.Controls.Primitives;
using System;

namespace Morphic.MorphicBar.BarControls;

// Three-state model for the in-progress visual. Names match the VisualStates in the
// "ProgressStates" VisualStateGroup of every button template in this project.
//   None      -- no progress visual; ProgressBar collapsed and CommonStates is back to normal.
//   Preparing -- The action has just begun. CommonStates enters "InProgress" immediately
//                (BgBorder = pressed color) so the user sees instant feedback that the click
//                registered. ProgressBar's indeterminate animation runs invisibly
//                (Visibility=Visible, IsIndeterminate=True, Opacity=0) so that, IF it gets
//                revealed (action runs past DelayedInProgressVisual.ShowDelay), its animation appears mid-cycle
//                rather than starting fresh.
//   Visible   -- ProgressBar visible at full opacity (Opacity reverts to default 1.0).
//                CommonStates "InProgress" is still active (BgBorder still pressed-color).
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

    // Attaches the tracker to the control and wires the standard pointer + enabled events
    // PLUS dependency-property change callbacks for IsPressed and IsPointerOver. The callbacks
    // are the LOAD-BEARING piece for the in-progress visual surviving a parent-panel layout
    // change (e.g. bar rotation): when the layout pass moves a button out from under the
    // pointer, the framework writes IsPointerOver=false directly via SetValue, which fires
    // the built-in ButtonBase property-changed callback that calls UpdateVisualState ->
    // GoToState("Normal") on CommonStates. The routed PointerExited event may not fire at all
    // for synthetic transitions of this kind, so our event subscription below wouldn't catch
    // it. Registering our OWN DP change callback ensures `update` runs AFTER the built-in
    // callback (callbacks fire in registration order; built-in's is registered when the
    // ToggleButton class is initialized, ours is registered here, so ours runs second and wins).
    // Same logic for IsPressed (the framework can clear it directly during pointer capture loss
    // or layout-driven re-hit-testing).
    //
    // The caller-supplied `update` closure is invoked whenever any wired event or DP callback
    // fires; the closure is expected to recompute the CommonStates name from the tracker and
    // call VisualStateManager.GoToState. ProgressStates transitions go through
    // SetInProgressVisual, not through `update`, because they are driven by action lifecycle
    // rather than pointer events and don't need to refire on every mouse move.
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

        // DP change callback: catch framework-direct writes to IsPressed that bypass the routed
        // PointerPressed/Released/CaptureLost events (e.g. the framework clearing IsPressed during
        // pointer-capture loss). The callback runs AFTER the built-in ButtonBase callback that
        // calls UpdateVisualState (registration order: built-in registers when the class is
        // initialized, ours registers here), so our `update` re-asserts state from the tracker
        // after any built-in stomp. We re-sync the tracker from the live DP value first, in case
        // our routed-event wiring missed the transition.
        //
        // NOTE: WinUI 3 does not expose a public IsPointerOverProperty (the framework tracks it
        // internally but doesn't register a public DP), so we cannot do the same for IsPointerOver.
        button.RegisterPropertyChangedCallback(ButtonBase.IsPressedProperty, (sender, _) =>
        {
            if (sender is ButtonBase buttonBase)
            {
                tracker.IsPressed = buttonBase.IsPressed;
            }
            update();
        });

        // LayoutUpdated re-assertion guard: fires after every layout pass on the XamlRoot. While
        // an action is in flight (InProgressVisual != None -- covers both Preparing AND Visible),
        // re-assert "InProgress" on CommonStates so any built-in UpdateVisualState that ran between
        // the previous layout pass and this one gets immediately overwritten. This is the
        // load-bearing defense against parent-panel layout changes (e.g. bar rotation) silently
        // stomping our CommonStates -- the trigger can be anything from a synthetic pointer
        // transition to an internal property recompute, and there is no single event to subscribe
        // to that covers all paths.
        //
        // The early-return when InProgressVisual == None keeps the handler near-free for non-action
        // use: the body runs at most a couple of GoToState calls per layout pass, only during the
        // few seconds of an active in-progress action. GoToState is a no-op when the target state
        // is already the current state, so even repeated firings cost only the equality check.
        //
        // useTransitions: false because we are re-asserting an already-active state, not
        // performing a user-visible transition; transitions would risk visible flicker during
        // rapid layout passes (e.g. while a rotation animation is in flight).
        button.LayoutUpdated += (_, _) =>
        {
            if (tracker.InProgressVisual == InProgressVisual.None)
            {
                return;
            }
            VisualStateManager.GoToState(button, tracker.ComputeStateName(), useTransitions: false);
            VisualStateManager.GoToState(button, tracker.ComputeProgressStateName(), useTransitions: false);
        };
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

    // Re-asserts the current compound state from the tracker onto BOTH VSM groups, without
    // changing any tracker fields. Use this after a layout-altering operation that may have
    // caused WinUI's built-in ButtonBase visual-state machine to step on our CommonStates --
    // notably, a parent-panel layout change can produce synthetic PointerExited events that
    // make the built-in handler call GoToState("Normal"), wiping our "InProgress" override on
    // BgBorder.Background while leaving our custom ProgressStates group untouched (since the
    // built-in code knows nothing about it). The asymmetry is the diagnostic fingerprint:
    // ProgressBar animation survives, BgBorder shading does not.
    //
    // Callers should schedule this via DispatcherQueue.TryEnqueue so it runs AFTER the layout
    // pass and the synthetic pointer events it triggers; calling it inline would re-assert
    // before the built-in reset happened and leave the bug intact.
    public static void RefreshVisualState(Control button)
    {
        if (button.GetValue(StateTrackerProperty) is not CompoundStateTrackerBase tracker)
        {
            return;
        }

        VisualStateManager.GoToState(button, tracker.ComputeStateName(), useTransitions: false);
        VisualStateManager.GoToState(button, tracker.ComputeProgressStateName(), useTransitions: false);
    }
}
