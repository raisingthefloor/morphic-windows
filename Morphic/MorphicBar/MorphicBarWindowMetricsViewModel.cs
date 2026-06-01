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

namespace Morphic.MorphicBar;

// Instance-property wrapper for MorphicBarWindowMetrics so XAML {Binding} can
// reach the values via Source={StaticResource ...}. Needed because:
//   {x:Bind} works for direct attribute values and Style.Setter values, but
//   crashes at runtime when used in VisualState.Setter values for compound
//   types (NullReferenceException in generated XamlBindingSetters code).
// Properties proxy to the static MorphicBarWindowMetrics constants; the class
// itself holds no state. Add an instance to a Window.Resources block and use
// {Binding Path=PropertyName, Source={StaticResource KeyName}}.
public class MorphicBarWindowMetricsViewModel
{
    public Thickness BarBorderThickness => MorphicBarWindowMetrics.BarBorderThickness;
    public CornerRadius BarCornerRadiusUniform => MorphicBarWindowMetrics.BarCornerRadiusUniform;
    public CornerRadius BarCornerRadiusTopLeftOnly => MorphicBarWindowMetrics.BarCornerRadiusTopLeftOnly;
    public CornerRadius BarCornerRadiusTopRightOnly => MorphicBarWindowMetrics.BarCornerRadiusTopRightOnly;
    public GridLength CloseButtonColumnGridLength => MorphicBarWindowMetrics.CloseButtonColumnGridLength;
    public double CloseButtonWidth => MorphicBarWindowMetrics.CloseButtonWidth;
    public double CloseButtonHeight => MorphicBarWindowMetrics.CloseButtonHeight;
    public double ItemsPanelSpacing => MorphicBarWindowMetrics.ItemsPanelSpacing;
    public double MenuLogoWidth => MorphicBarWindowMetrics.MenuLogoWidth;
    public double MenuLogoHeight => MorphicBarWindowMetrics.MenuLogoHeight;
}
