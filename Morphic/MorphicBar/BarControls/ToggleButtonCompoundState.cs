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
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Morphic.MorphicBar.BarControls;

// Toggle-button templates in this project use a single VisualStateGroup named "CommonStates"
// that contains COMPOUND state names: Normal, PointerOver, Pressed, Checked, CheckedPointerOver,
// CheckedPressed, Indeterminate, Disabled, CheckedDisabled.
//
// WinUI's built-in ToggleButton emits only simple state names (Normal/PointerOver/Pressed in
// CommonStates + Checked/Unchecked/Indeterminate in CheckStates), which causes cross-group
// clashes on properties both groups touch (e.g. BgBorder.Background) -- the classic symptom
// being the toggle visual getting stuck in a wrong state after a click/unclick cycle.
//
// This helper takes over the state machine for ToggleButtons via CompoundStatePointerWiring
// (plus the toggle-specific Checked/Unchecked/Indeterminate events) and routes every
// transition through ComputeStateName so transitions are deterministic.
internal static class ToggleButtonCompoundState
{
    public static void Wire(ToggleButton button)
    {
        var tracker = new StateTracker(button);

        void Update()
        {
            VisualStateManager.GoToState(button, tracker.ComputeStateName(), useTransitions: true);
        }

        CompoundStatePointerWiring.Wire(button, tracker, Update);

        // toggle-specific events -- not on plain Button
        button.Checked += (_, _) => Update();
        button.Unchecked += (_, _) => Update();
        button.Indeterminate += (_, _) => Update();

        // leave the initial state to the built-in ToggleButton; our handlers take over on the
        // first user interaction
    }

    private sealed class StateTracker : CompoundStateTrackerBase
    {
        private readonly ToggleButton _button;

        public StateTracker(ToggleButton button)
        {
            _button = button;
        }

        public override string ComputeStateName()
        {
            // CommonStates enters "InProgress" the moment the action begins (Preparing) AND
            // while the progress bar is fully visible. Showing the BgBorder pressed-color
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
                // CheckedDisabled is its own state so the toggle's checked appearance remains
                // visible (faded) when disabled. Without this split, "Disabled" with empty
                // setters would fall back to the base (unchecked) appearance regardless of
                // IsChecked.
                if (_button.IsChecked == true)
                {
                    return "CheckedDisabled";
                }
                return "Disabled";
            }
            if (_button.IsChecked is null)
            {
                return "Indeterminate";
            }

            bool isChecked = _button.IsChecked == true;
            if (isChecked)
            {
                if (this.IsPressed)
                {
                    return "CheckedPressed";
                }
                if (this.IsPointerOver)
                {
                    return "CheckedPointerOver";
                }
                return "Checked";
            }
            else
            {
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
}
