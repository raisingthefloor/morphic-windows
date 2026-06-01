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

namespace Morphic.MorphicBar;

internal class FlowDirectionTrigger : StateTriggerBase
{
    private long _callbackToken;

    public static readonly DependencyProperty TargetProperty =
        DependencyProperty.Register(nameof(Target), typeof(FrameworkElement), typeof(FlowDirectionTrigger),
            new PropertyMetadata(null, OnTargetChanged));

    public FlowDirection Direction { get; set; }

    public FrameworkElement? Target
    {
        get => (FrameworkElement?)this.GetValue(FlowDirectionTrigger.TargetProperty);
        set => this.SetValue(FlowDirectionTrigger.TargetProperty, value);
    }

    private static void OnTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var trigger = (FlowDirectionTrigger)d;

        if (e.OldValue is FrameworkElement oldTarget)
        {
            oldTarget.UnregisterPropertyChangedCallback(
                FrameworkElement.FlowDirectionProperty, trigger._callbackToken);
        }

        if (e.NewValue is FrameworkElement newTarget)
        {
            trigger._callbackToken = newTarget.RegisterPropertyChangedCallback(
                FrameworkElement.FlowDirectionProperty, trigger.OnFlowDirectionChanged);
            trigger.Evaluate();
        }
    }

    private void OnFlowDirectionChanged(DependencyObject sender, DependencyProperty dp)
    {
        this.Evaluate();
    }

    private void Evaluate()
    {
        this.SetActive(Target?.FlowDirection == Direction);
    }
}
