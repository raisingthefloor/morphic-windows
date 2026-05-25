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

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Morphic.Core;
using System;
using System.ComponentModel;
using System.Diagnostics;

namespace Morphic.MorphicBar.BarControls;

public sealed partial class BarButtonControl : UserControl, IBarItemControl
{
    private BarButtonData? _data;
    private ButtonBase? _button;
    private bool _isActionInProgress;
    private Orientation _orientation = Orientation.Horizontal;

    public BarButtonControl()
    {
        this.InitializeComponent();
    }

    public BarButtonData? Data
    {
        get => _data;
        set
        {
            _data = value;
            this.ApplyData();
        }
    }

    // Layout orientation propagated by the MorphicBar.
    // The control stores the value; Horizontal and Vertical render identically because a single
    // TextOnly button fills whatever width/height its parent allocates. So orientation change does
    // NOT rebuild the button -- rebuilding would tear down the live ToggleButton instance and lose
    // its IsChecked state and any in-progress action visual. When width-/height-dependent behaviors
    // are added (e.g. different max-width handling for a vertical bar), they should branch on this
    // property via the validating switch in ApplyData AND, if rebuild is unavoidable, the orientation
    // setter should preserve the live ToggleButton's IsChecked + tracker state across the rebuild.
    public Orientation Orientation
    {
        get => _orientation;
        set
        {
            if (_orientation == value)
            {
                return;
            }
            _orientation = value;
        }
    }

    // BarButtonControl's layout does not vary by orientation; a single TextOnly button fills
    // whatever width/height its parent allocates regardless of the bar's orientation. Just measure
    // self with the supplied available size and return DesiredSize.
    public Windows.Foundation.Size MeasureForOrientation(Windows.Foundation.Size availableSize, Orientation orientation)
    {
        this.Measure(availableSize);
        return this.DesiredSize;
    }

    private void ApplyData()
    {
        // tear down any previously-built button
        this.RootContainer.Children.Clear();
        _button = null;

        if (_data is null)
        {
            this.HeaderTextBlock.Text = string.Empty;
            this.HeaderTextBlock.Visibility = Visibility.Collapsed;
            return;
        }

        // header (only shown when Header is non-empty)
        if (string.IsNullOrEmpty(_data.Header))
        {
            this.HeaderTextBlock.Text = string.Empty;
            this.HeaderTextBlock.Visibility = Visibility.Collapsed;
        }
        else
        {
            this.HeaderTextBlock.Text = _data.Header;
            this.HeaderTextBlock.Visibility = Visibility.Visible;
        }

        // validate the layout style; defends against unknown values added in the future
        switch (_data.LayoutStyle)
        {
            case BarButtonLayoutStyle.TextOnly:
                break;
            default:
                throw new MorphicUnhandledCaseException(_data.LayoutStyle);
        }

        // validate the orientation up front (defends against unknown values added in the future)
        switch (_orientation)
        {
            case Orientation.Horizontal:
            case Orientation.Vertical:
                break;
            default:
                throw new MorphicUnhandledCaseException(_orientation);
        }

        var plainStyle = (Style)this.Resources["BarButtonStyle"];
        var toggleStyle = (Style)this.Resources["BarToggleButtonStyle"];

        ButtonBase button;
        if (_data.IsToggle)
        {
            var toggleButton = new ToggleButton
            {
                Style = toggleStyle,
                IsChecked = _data.IsChecked,
                IsEnabled = _data.IsEnabled,
                Content = _data.Text,
            };
            // Intentionally NOT mirroring Checked/Unchecked back into _data.IsChecked. Data updates
            // happen only in Button_Click on action SUCCESS so that an in-flight real-time event
            // listener can update _data.IsChecked during the action without our immediate-toggle
            // handler clobbering the listener's value.
            //
            // The data is the source of truth: when _data.IsChecked or IsEnabled changes (via the
            // action's post-completion write, OR via an external listener writing directly to the
            // data), the PropertyChanged subscription below pulls the new value into the live
            // ToggleButton. The subscriber is unsubscribed on Unloaded so the data doesn't hold a
            // reference to a discarded UI (matters when the BarButtonControl's Data is reassigned,
            // since ApplyData clears RootContainer.Children and the old ToggleButton unloads).
            //
            // Marshal through DispatcherQueue because the writer may be off the UI thread (system
            // event listeners typically fire on background threads). The setter's equality short-
            // circuit prevents a feedback loop if the data write originated from the UI.
            var capturedData = _data;
            var capturedToggleButton = toggleButton;
            PropertyChangedEventHandler propertyChangedHandler = (_, args) =>
            {
                if (args.PropertyName == nameof(BarButtonData.IsChecked))
                {
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        capturedToggleButton.IsChecked = capturedData.IsChecked;
                    });
                }
                else if (args.PropertyName == nameof(BarButtonData.IsEnabled))
                {
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        capturedToggleButton.IsEnabled = capturedData.IsEnabled;
                    });
                }
            };
            capturedData.PropertyChanged += propertyChangedHandler;
            toggleButton.Unloaded += (_, _) => capturedData.PropertyChanged -= propertyChangedHandler;
            toggleButton.Click += Button_Click;
            ToggleButtonCompoundState.Wire(toggleButton);
            button = toggleButton;
        }
        else
        {
            var plainButton = new Button
            {
                Style = plainStyle,
                Content = _data.Text,
            };
            plainButton.Click += Button_Click;
            ButtonCompoundState.Wire(plainButton);
            button = plainButton;
        }

        // set the accessible (screen reader) name for the button; fall back to the text if no accessible name was specified
        AutomationProperties.SetName(button, _data.AccessibleName ?? _data.Text);
        ToolTipService.SetToolTip(button, _data.Tooltip);

        // a standalone bar button is the lone "sub-button" of its group, so all 4 corners are
        // rounded -- matches BarMultiButtonControl.ApplyCornerRadii's single-button case
        button.CornerRadius = new CornerRadius(BarControlMetrics.ButtonCornerRadius);

        this.RootContainer.Children.Add(button);
        _button = button;
    }

    private async void Button_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ButtonBase button)
        {
            return;
        }
		//
        var action = _data?.Action;
        if (action is null)
        {
            return;
        }
		//
        if (_isActionInProgress)
        {
            return;
        }

        var toggleButton = sender as ToggleButton;
        bool? postClickIsChecked = toggleButton?.IsChecked;
        var actionTag = _data!.ActionTag;
        //
        // re-entry during the action is prevented by _isActionInProgress (checked above), so we
        // don't gate clicks via IsHitTestVisible. Doing so would suppress PointerEntered/Exited on
        // the button for the duration of the action; if the user moved the pointer off the button
        // while it was running, tracker.IsPointerOver would stay stale (true) and
        // SetInProgressVisual(None) would compute "PointerOver" instead of "Normal" -- leaving the
        // button stuck in the hover background, visually indistinguishable from the InProgress
        // pressed background. While InProgressVisual == Visible, ComputeStateName always returns
        // "InProgress" regardless of pointer state, so letting pointer events flow during the
        // action causes no visual flicker.
        _isActionInProgress = true;
		//
        bool actionSucceeded;
        try
        {
            var result = await DelayedInProgressVisual.RunAsync(button, () => action.Invoke(actionTag, postClickIsChecked));
            actionSucceeded = result.IsSuccess;
        }
        catch (Exception ex)
        {
            // an action throwing an exception is treated as failure -- the system state didn't change
            // in any well-defined way, so the toggle (if any) should revert
            Debug.WriteLine($"[BarItem] {actionTag} threw: {ex}");
            actionSucceeded = false;
        }
        finally
        {
            _isActionInProgress = false;
        }

        // mirror the outcome onto the toggle and the backing data. Data updates happen only on
        // action completion (not on the immediate Checked/Unchecked event from the framework's
        // toggle) so that an in-flight real-time event listener -- e.g. one observing an external
        // dark-mode change -- can update BarButtonData.IsChecked during the action without us
        // blindly overwriting it. On success we write postClickIsChecked (our action committed
        // that state); on failure we revert the visual toggle and leave data alone (data was never
        // changed by this click, so it still reflects the unchanged system state). No-op for
        // non-toggle buttons (toggleButton == null).
        if (toggleButton is not null && postClickIsChecked.HasValue)
        {
            if (actionSucceeded)
            {
                _data.IsChecked = postClickIsChecked.Value;
            }
            else
            {
                toggleButton.IsChecked = !postClickIsChecked.Value;
            }
        }
    }
}
