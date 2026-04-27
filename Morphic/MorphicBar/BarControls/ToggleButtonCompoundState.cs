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
// CheckedPressed, Indeterminate, Disabled.
//
// WinUI's built-in ToggleButton emits only simple state names (Normal/PointerOver/Pressed in
// CommonStates + Checked/Unchecked/Indeterminate in CheckStates), which causes cross-group
// clashes on properties both groups touch (e.g. BgBorder.Background) — the classic symptom
// being the toggle visual getting stuck in a wrong state after a click/unclick cycle.
//
// This helper hooks a ToggleButton's pointer, checked, and enabled events and calls
// VisualStateManager.GoToState with the compound name so that transitions are deterministic.
internal static class ToggleButtonCompoundState
{
    public static void Wire(ToggleButton button)
    {
        var tracker = new StateTracker();

        void Update()
        {
            var stateName = tracker.ComputeStateName(button);
            VisualStateManager.GoToState(button, stateName, useTransitions: true);
        }

        button.PointerEntered += (_, _) =>
        {
            tracker.IsPointerOver = true;
            Update();
        };
        button.PointerExited += (_, _) =>
        {
            // also clear IsPressed -- WinUI typically "cancels" the pressed visual when the pointer
            // leaves the control, even if capture is still held
            tracker.IsPointerOver = false;
            tracker.IsPressed = false;
            Update();
        };
        button.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(button).Properties.IsLeftButtonPressed)
            {
                tracker.IsPressed = true;
                Update();
            }
        };
        button.PointerReleased += (_, _) =>
        {
            tracker.IsPressed = false;
            Update();
        };
        button.PointerCaptureLost += (_, _) =>
        {
            tracker.IsPressed = false;
            Update();
        };
        button.Checked += (_, _) => Update();
        button.Unchecked += (_, _) => Update();
        button.Indeterminate += (_, _) => Update();
        button.IsEnabledChanged += (_, _) => Update();

        // leave the initial state to the built-in ToggleButton; our handlers take over on the
        // first user interaction
    }

    private sealed class StateTracker
    {
        public bool IsPointerOver;
        public bool IsPressed;

        public string ComputeStateName(ToggleButton button)
        {
            if (!button.IsEnabled)
            {
                return "Disabled";
            }
            if (button.IsChecked is null)
            {
                return "Indeterminate";
            }

            bool isChecked = button.IsChecked == true;
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
