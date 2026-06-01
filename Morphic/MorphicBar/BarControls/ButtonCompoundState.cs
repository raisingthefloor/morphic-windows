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

// Plain (non-toggle) sub-button templates in this project use a single VisualStateGroup
// named "CommonStates" that contains: Normal, PointerOver, Pressed, InProgress, Disabled.
//
// WinUI's built-in Button drives the standard pointer states (Normal/PointerOver/Pressed)
// itself, which clobbers any caller-driven transition into "InProgress" the next time the
// pointer moves -- in particular, setting IsHitTestVisible=false at the start of an action
// can produce a synthetic PointerExited that races our GoToState("InProgress") call and
// yanks the button back to "Normal".
//
// This helper takes over the state machine for plain Buttons via CompoundStatePointerWiring,
// routing every transition through ComputeStateName, which returns "InProgress" first when
// set, so the in-progress visual is sticky for the duration of an async action regardless
// of pointer movement.
internal static class ButtonCompoundState
{
    public static void Wire(Button button)
    {
        var tracker = new StateTracker(button);

        void Update()
        {
            VisualStateManager.GoToState(button, tracker.ComputeStateName(), useTransitions: true);
        }

        CompoundStatePointerWiring.Wire(button, tracker, Update);

        // leave the initial state to the built-in Button; our handlers take over on the
        // first user interaction
    }

    private sealed class StateTracker : CompoundStateTrackerBase
    {
        private readonly Button _button;

        public StateTracker(Button button)
        {
            _button = button;
        }

        public override string ComputeStateName()
        {
            // NOTE: CommonStates enters "InProgress" the moment the action begins (Preparing)
            // AND while the progress bar is fully visible. Showing the BgBorder pressed-color
            // immediately at click time gives the user instant feedback that their click
            // registered, even for actions short enough that the ProgressBar reveal never
            // happens. The ProgressBar itself still has the anti-flicker show-delay (see
            // DelayedInProgressVisual.ShowDelay): actions that complete before that delay
            // animate the bar invisibly during Preparing and never reveal it.
            if (this.InProgressVisual != InProgressVisual.None)
            {
                return "InProgress";
            }
            if (!_button.IsEnabled)
            {
                return "Disabled";
            }
            if (this.IsPressed)
            {
                return "Pressed";
            }
            if (this.IsPointerOver)
            {
                return "PointerOver";
            }
            return "Normal";
        }
    }
}
