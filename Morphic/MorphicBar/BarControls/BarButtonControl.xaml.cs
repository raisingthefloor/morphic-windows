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
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Morphic.MorphicBar.BarControls;

public sealed partial class BarButtonControl : UserControl, IBarItemControl
{
    private BarButtonData? _data;
    private ButtonBase? _button;
    private Orientation _orientation = Orientation.Horizontal;

    public BarButtonControl()
    {
        this.InitializeComponent();
    }

    public void RefreshButtonCompoundStates()
    {
        if (_button is not null)
        {
            CompoundStatePointerWiring.RefreshVisualState(_button);
        }
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
                throw new System.ComponentModel.InvalidEnumArgumentException(
                    nameof(_data.LayoutStyle), (int)_data.LayoutStyle, _data.LayoutStyle.GetType());
        }

        // validate the orientation up front (defends against unknown values added in the future)
        switch (_orientation)
        {
            case Orientation.Horizontal:
            case Orientation.Vertical:
                break;
            default:
                throw new System.ComponentModel.InvalidEnumArgumentException(
                    nameof(_orientation), (int)_orientation, _orientation.GetType());
        }

        var plainStyle = (Style)this.Resources["BarButtonStyle"];
        var toggleStyle = (Style)this.Resources["BarToggleButtonStyle"];

        var button = BarButtonBuilder.CreateButton(_data, plainStyle, toggleStyle);

        // a standalone bar button is the lone "sub-button" of its group, so all 4 corners are
        // rounded -- matches BarMultiButtonControl.ApplyCornerRadii's single-button case
        button.CornerRadius = new CornerRadius(BarControlMetrics.ButtonCornerRadius);

        this.RootContainer.Children.Add(button);
        _button = button;
    }
}
