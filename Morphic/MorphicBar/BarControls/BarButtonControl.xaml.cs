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

namespace Morphic.MorphicBar.BarControls;

public sealed partial class BarButtonControl : UserControl, IBarItemControl
{
    private const double ControlButtonCornerRadius = 5.0;
    private BarButtonData? _data;
    private ButtonBase? _button;
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
    // The control stores the value; Horizontal and Vertical are both valid but currently render identically,
    // because a single TextOnly button fills whatever width/height its parent allocates.
    // When width-/height-dependent behaviors are added (e.g. different max-width handling for a vertical bar),
    // they should branch on this property via the validating switch in ApplyData.
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
                Content = _data.Text,
            };
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
            button = plainButton;
        }

        // set the accessible (screen reader) name for the button; fall back to the text if no accessible name was specified
        AutomationProperties.SetName(button, _data.AccessibleName ?? _data.Text);
        ToolTipService.SetToolTip(button, _data.Tooltip);

        this.RootContainer.Children.Add(button);
        _button = button;
    }

    private async void Button_Click(object sender, RoutedEventArgs e)
    {
        if (_data?.Action is null)
        {
            return;
        }

        bool? isChecked = (sender as ToggleButton)?.IsChecked;
        await _data.Action.Invoke(_data.ActionTag, isChecked);
    }
}
