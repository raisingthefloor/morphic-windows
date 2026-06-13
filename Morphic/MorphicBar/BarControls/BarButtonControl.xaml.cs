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

    // Re-asserts this button's compound visual state. See IBarItemControl.RefreshButtonCompoundStates
    // for why this is needed after the bar's AppWindow.Show. The caller has already deferred past the
    // post-show layout pass, so we re-assert synchronously. No-op before the button is built (Data not
    // yet set) or after it's been cleared on Data reassignment.
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
    // Orientation does NOT rebuild the button -- rebuilding would tear down the live ToggleButton instance
    // and lose its IsChecked state and any in-progress action visual. The one orientation-dependent visual is
    // the button's HorizontalAlignment (see ApplyButtonHorizontalAlignmentForOrientation), updated in place
    // here on a flip without a rebuild.
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
            this.ApplyButtonHorizontalAlignmentForOrientation();
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
        this.ApplyButtonHorizontalAlignmentForOrientation();
    }

    // In a VERTICAL bar every item's Width is pinned to the bar's full inner thickness so headers wrap
    // deterministically (see MorphicBarWindow.AnimateMoveTo). A button left at the style's
    // HorizontalAlignment=Stretch would then balloon to that full width even for a 3-character label, so in
    // vertical mode center it at its natural content width instead -- matching how BarMultiButtonControl
    // centers its natural-width sub-buttons. Horizontal keeps Stretch: there the item is content-sized along
    // the bar's length axis, so the button fills its group. No-op before the button is built.
    private void ApplyButtonHorizontalAlignmentForOrientation()
    {
        if (_button is not null)
        {
            _button.HorizontalAlignment = _orientation == Orientation.Vertical
                ? HorizontalAlignment.Center
                : HorizontalAlignment.Stretch;
        }
    }
}
