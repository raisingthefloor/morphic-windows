﻿// Copyright 2020-2024 Raising the Floor - US, Inc.
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Morphic.MorphicBar;

/// <summary>
/// Interaction logic for MorphicBarWindow.xaml
/// </summary>
public partial class MorphicBarWindow : Window
{
    public static readonly DependencyProperty OrientationProperty;

    static MorphicBarWindow()
    {
        // wire up dependency properties
        MorphicBarWindow.OrientationProperty = DependencyProperty.Register("Orientation", typeof(System.Windows.Controls.Orientation), typeof(MorphicBarWindow), new FrameworkPropertyMetadata(System.Windows.Controls.Orientation.Horizontal, new PropertyChangedCallback(OnOrientationChanged)));
    }

    // .NET property wrapper for dependency property (NOTE: this is the standard .NET pattern; it must not be changed)
    public System.Windows.Controls.Orientation Orientation
    {
        get { return (Orientation)GetValue(MorphicBarWindow.OrientationProperty); }
        set { SetValue(MorphicBarWindow.OrientationProperty, value); }
    }

    private static void OnOrientationChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        var morphicBar = (MorphicBarWindow)obj;

        // TODO: tell the morphicBar that its orientation has been changed so that it can modify its layout
    }

    //

    public MorphicBarWindow()
    {
        InitializeComponent();
    }
}
