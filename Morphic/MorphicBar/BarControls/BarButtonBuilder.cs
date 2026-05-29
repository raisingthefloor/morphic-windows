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
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Morphic.MorphicBar.BarControls;

internal static class BarButtonBuilder
{
    private static readonly DependencyProperty IsActionInProgressProperty =
        DependencyProperty.RegisterAttached(
            "IsActionInProgress",
            typeof(bool),
            typeof(BarButtonBuilder),
            new PropertyMetadata(false));

    private static bool GetIsActionInProgress(DependencyObject target)
    {
        return (bool)target.GetValue(IsActionInProgressProperty);
    }

    private static void SetIsActionInProgress(DependencyObject target, bool value)
    {
        target.SetValue(IsActionInProgressProperty, value);
    }

    public static ButtonBase CreateButton(BarButtonData data, Style plainStyle, Style toggleStyle)
    {
        ButtonBase button;
        if (data.IsToggle)
        {
            var toggleButton = new GuardedToggleButton
            {
                Style = toggleStyle,
                IsChecked = data.IsChecked,
                IsEnabled = data.IsEnabled,
                Content = data.Text,
            };
            PropertyChangedEventHandler propertyChangedHandler = (_, args) =>
            {
                if (args.PropertyName == nameof(BarButtonData.IsChecked))
                {
                    _ = toggleButton.DispatcherQueue.TryEnqueue(() =>
                    {
                        toggleButton.IsChecked = data.IsChecked;
                    });
                }
                else if (args.PropertyName == nameof(BarButtonData.IsEnabled))
                {
                    _ = toggleButton.DispatcherQueue.TryEnqueue(() =>
                    {
                        toggleButton.IsEnabled = data.IsEnabled;
                    });
                }
            };
            data.PropertyChanged += propertyChangedHandler;
            toggleButton.Unloaded += (_, _) => data.PropertyChanged -= propertyChangedHandler;
            ToggleButtonCompoundState.Wire(toggleButton);
            button = toggleButton;
        }
        else
        {
            var plainButton = new Button
            {
                Style = plainStyle,
                IsEnabled = data.IsEnabled,
                Content = data.Text,
            };
            PropertyChangedEventHandler plainPropertyChangedHandler = (_, args) =>
            {
                if (args.PropertyName == nameof(BarButtonData.IsEnabled))
                {
                    _ = plainButton.DispatcherQueue.TryEnqueue(() =>
                    {
                        plainButton.IsEnabled = data.IsEnabled;
                    });
                }
            };
            data.PropertyChanged += plainPropertyChangedHandler;
            plainButton.Unloaded += (_, _) => data.PropertyChanged -= plainPropertyChangedHandler;
            ButtonCompoundState.Wire(plainButton);
            button = plainButton;
        }

        button.Click += async (_, _) => await BarButtonBuilder.RunClickAsync(button, data);

        AutomationProperties.SetName(button, data.AccessibleName ?? data.Text);
        ToolTipService.SetToolTip(button, data.Tooltip);

        return button;
    }

    private static async Task RunClickAsync(ButtonBase button, BarButtonData data)
    {
        var action = data.Action;
        if (action is null)
        {
            return;
        }
        if (BarButtonBuilder.GetIsActionInProgress(button))
        {
            return;
        }

        var toggleButton = button as ToggleButton;
        bool? intendedIsChecked = toggleButton is null ? null : !(toggleButton.IsChecked == true);
        var actionTag = data.ActionTag;

        BarButtonBuilder.SetIsActionInProgress(button, true);

        bool actionSucceeded;
        try
        {
            var result = await DelayedInProgressVisual.RunAsync(button, () => action.Invoke(actionTag, intendedIsChecked));
            actionSucceeded = result.IsSuccess;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BarItem] {actionTag} threw: {ex}");
            actionSucceeded = false;
        }
        finally
        {
            BarButtonBuilder.SetIsActionInProgress(button, false);
        }

        if (toggleButton is not null && intendedIsChecked.HasValue && actionSucceeded)
        {
            data.IsChecked = intendedIsChecked.Value;
        }
    }
}
