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
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Morphic.Core;
using System;
using System.Collections.Generic;

namespace Morphic.MorphicBar.BarControls;

public sealed partial class BarMultiButtonControl : UserControl, IBarItemControl
{
    private const double ControlButtonCornerRadius = 5.0;
    private const double ControlButtonInnerMargin = 0.5;

    private BarMultiButtonData? _data;
    private readonly List<ButtonBase> _subButtons = new();
    private bool _incDecShortcutsEnabled = false;
    private int _decrementButtonIndex = -1;
    private int _incrementButtonIndex = -1;
    private Orientation _orientation = Orientation.Horizontal;

    public BarMultiButtonControl()
    {
        this.InitializeComponent();
    }

    public BarMultiButtonData? Data
    {
        get => _data;
        set
        {
            _data = value;
            this.ApplyData();
        }
    }

    // Layout direction for the sub-button group.
    //   Horizontal: sub-buttons run left-to-right; first/last round their left/right corners.
    //   Vertical:   sub-buttons stack top-to-bottom; first/last round their top/bottom corners.
    // The header always sits above the sub-button group regardless of orientation.
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
            this.ApplyData();
        }
    }

    private void ApplyData()
    {
        // tear down any previously-built sub-buttons
        this.ButtonsContainer.Children.Clear();
        this.ButtonsContainer.ColumnDefinitions.Clear();
        this.ButtonsContainer.RowDefinitions.Clear();
        _subButtons.Clear();

        // if the inc/dec shortcuts were enabled for the previous data, strip them; the caller must re-enable them
        // after assigning new Data (the sub-button count could have changed)
        if (_incDecShortcutsEnabled)
        {
            this.KeyDown -= BarMultiButtonControl_KeyDown_IncDec;
            _incDecShortcutsEnabled = false;
            _decrementButtonIndex = -1;
            _incrementButtonIndex = -1;
        }

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

        // pick the primary-axis length used for every sub-button (column width when horizontal, row height when vertical)
        GridLength primaryAxisLength;
        switch (_data.SizingMode)
        {
            case MultiButtonSizingMode.AutoSize:
                primaryAxisLength = GridLength.Auto;
                break;
            case MultiButtonSizingMode.StretchToLargest:
                primaryAxisLength = new GridLength(1, GridUnitType.Star);
                break;
            default:
                throw new MorphicUnhandledCaseException(_data.SizingMode);
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

        var plainStyle = (Style)this.Resources["SubButtonStyle"];
        var toggleStyle = (Style)this.Resources["SubToggleButtonStyle"];

        for (int i = 0; i < _data.Buttons.Count; i++)
        {
            var buttonData = _data.Buttons[i];

            // sub-buttons must be TextOnly; reject any other layout style
            switch (buttonData.LayoutStyle)
            {
                case BarButtonLayoutStyle.TextOnly:
                    break;
                default:
                    throw new ArgumentException("LayoutStyle must be 'TextOnly' for horizontal multi-button controls");
            }

            ButtonBase button;
            if (buttonData.IsToggle)
            {
                var toggleButton = new ToggleButton
                {
                    Style = toggleStyle,
                    IsChecked = buttonData.IsChecked,
                    Content = buttonData.Text,
                };
                toggleButton.Click += SubButton_Click;
                ToggleButtonCompoundState.Wire(toggleButton);
                button = toggleButton;
            }
            else
            {
                var plainButton = new Button
                {
                    Style = plainStyle,
                    Content = buttonData.Text,
                };
                plainButton.Click += SubButton_Click;
                button = plainButton;
            }

            // set the accessible (screen reader) name for the button; fall back to the text if no accessible name was specified
            AutomationProperties.SetName(button, buttonData.AccessibleName ?? buttonData.Text);
            if (this.HeaderTextBlock.Visibility == Visibility.Visible)
            {
                AutomationProperties.SetLabeledBy(button, this.HeaderTextBlock);
            }
			
            ToolTipService.SetToolTip(button, buttonData.Tooltip);

            // leave a tiny gap between adjacent sub-buttons along the layout axis while preserving rounded outer corners
            bool isFirst = (i == 0);
            bool isLast = (i == _data.Buttons.Count - 1);
            button.Margin = _orientation == Orientation.Horizontal
                ? new Thickness(
                    isFirst ? 0 : ControlButtonInnerMargin,
                    0,
                    isLast ? 0 : ControlButtonInnerMargin,
                    0)
                : new Thickness(
                    0,
                    isFirst ? 0 : ControlButtonInnerMargin,
                    0,
                    isLast ? 0 : ControlButtonInnerMargin);

            // stash the sub-button data in the button's `Tag` property so the Click handler can retrieve it without a separate dictionary
            button.Tag = buttonData;

            if (_orientation == Orientation.Horizontal)
            {
                this.ButtonsContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = primaryAxisLength });
                Grid.SetColumn(button, i);
            }
            else
            {
                this.ButtonsContainer.RowDefinitions.Add(new RowDefinition { Height = primaryAxisLength });
                Grid.SetRow(button, i);
            }
            this.ButtonsContainer.Children.Add(button);

            _subButtons.Add(button);
        }

        this.ApplyCornerRadii();
    }

    private void ApplyCornerRadii()
    {
        if (_subButtons.Count == 0)
        {
            return;
        }

        if (_subButtons.Count == 1)
        {
            // sole sub-button: round all 4 corners
            _subButtons[0].CornerRadius = new CornerRadius(ControlButtonCornerRadius);
            return;
        }
		
		// NOTE: at this point, we know we have at least 2 sub-buttons

        // CornerRadius is (topLeft, topRight, bottomRight, bottomLeft) -- clockwise from top-left
        if (_orientation == Orientation.Horizontal)
        {
            // first sub-button rounds its left (leading) corners
            _subButtons[0].CornerRadius = new CornerRadius(
                ControlButtonCornerRadius, 0, 0, ControlButtonCornerRadius);

            // middle sub-buttons: no rounding
            for (int i = 1; i < _subButtons.Count - 1; i++)
            {
                _subButtons[i].CornerRadius = new CornerRadius(0);
            }

            // last sub-button rounds its right (trailing) corners
            _subButtons[^1].CornerRadius = new CornerRadius(
                0, ControlButtonCornerRadius, ControlButtonCornerRadius, 0);
        }
        else
        {
            // first sub-button rounds its top corners
            _subButtons[0].CornerRadius = new CornerRadius(
                ControlButtonCornerRadius, ControlButtonCornerRadius, 0, 0);

            // middle sub-buttons: no rounding
            for (int i = 1; i < _subButtons.Count - 1; i++)
            {
                _subButtons[i].CornerRadius = new CornerRadius(0);
            }

            // last sub-button rounds its bottom corners
            _subButtons[^1].CornerRadius = new CornerRadius(
                0, 0, ControlButtonCornerRadius, ControlButtonCornerRadius);
        }
    }

    private async void SubButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ButtonBase button)
        {
            return;
        }
        if (button.Tag is not BarButtonData subData)
        {
            return;
        }

        bool? isChecked = (sender as ToggleButton)?.IsChecked;

        if (subData.Action is not null)
        {
            await subData.Action.Invoke(subData.ActionTag, isChecked);
        }
    }

    /// <summary>
    /// Wires the minus/plus (and OemMinus/OemPlus) keys to invoke the sub-buttons at the given indices.
    /// Throws <see cref="InvalidOperationException"/> if the group does not contain exactly two sub-buttons,
    /// <see cref="ArgumentOutOfRangeException"/> if either index is outside the sub-button range, and
    /// <see cref="ArgumentException"/> if the two indices are equal.
    /// </summary>
    /// <param name="decrementButtonIndex">Index of the sub-button the minus key should invoke.</param>
    /// <param name="incrementButtonIndex">Index of the sub-button the plus key should invoke.</param>
    public void EnableIncDecKeyboardShortcuts(int decrementButtonIndex, int incrementButtonIndex)
    {
        if (_subButtons.Count != 2)
        {
            throw new InvalidOperationException(
                $"EnableIncDecKeyboardShortcuts requires exactly 2 sub-buttons; current count is {_subButtons.Count}.");
        }
        if (decrementButtonIndex < 0 || decrementButtonIndex >= _subButtons.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(decrementButtonIndex));
        }
        if (incrementButtonIndex < 0 || incrementButtonIndex >= _subButtons.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(incrementButtonIndex));
        }
        if (decrementButtonIndex == incrementButtonIndex)
        {
            throw new ArgumentException("decrementButtonIndex and incrementButtonIndex must differ.");
        }

        _decrementButtonIndex = decrementButtonIndex;
        _incrementButtonIndex = incrementButtonIndex;

        if (_incDecShortcutsEnabled)
        {
            return;
        }

        this.KeyDown += BarMultiButtonControl_KeyDown_IncDec;
        _incDecShortcutsEnabled = true;
    }

	// NOTE: This function is called for keydown events; it only handles IncDev operations
    private void BarMultiButtonControl_KeyDown_IncDec(object sender, KeyRoutedEventArgs e)
    {
        // as OemMinus and OemPlus are not named in Windows.System.VirtualKey, we import the raw values here
        const int VkOemMinus = 189;
        const int VkOemPlus = 187;

        int clickIndex = -1;
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Subtract:
                clickIndex = _decrementButtonIndex;
                break;
            case Windows.System.VirtualKey.Add:
                clickIndex = _incrementButtonIndex;
                break;
            default:
                if ((int)e.Key == VkOemMinus)
                {
                    clickIndex = _decrementButtonIndex;
                }
                else if ((int)e.Key == VkOemPlus)
                {
                    clickIndex = _incrementButtonIndex;
                }
                break;
        }

        if (clickIndex < 0 || clickIndex >= _subButtons.Count)
        {
            return;
        }

        var target = _subButtons[clickIndex];

        // invoke the sub-button via its automation peer (trigging the Click event)
        AutomationPeer peer = target is ToggleButton toggleButton
            ? new ToggleButtonAutomationPeer(toggleButton)
            : new ButtonAutomationPeer((Button)target);

        (peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider)?.Invoke();

        e.Handled = true;
    }
}
