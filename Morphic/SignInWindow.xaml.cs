// Copyright 2020-2021 Raising the Floor - US, Inc.
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
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Morphic
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SignInWindow : Window
    {
        public SignInWindow()
        {
            this.InitializeComponent();

            // get this WinUI window's native hWnd
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

            // app windows like this one get tray entries by default; make sure we hide that tray entry
            // NOTE: we need to do this NOW, in this constructor, before the window is activated
            Morphic.Windows.Native.Windowing.Utils.WindowUtils.SetShowInTaskbar(hWnd, false);

            // set window properties
            // NOTE: at the time of writing, WinUI did not support setting these properties via XAML
            this.Title = "Sign into Morphic";
            //
            // set width and height (scaled by per-display DPI)
            Morphic.Windows.Native.Windowing.Utils.WindowUtils.SetWindowSize(hWnd, 600 /* width */, 500 /* height */);
            //FontSize = "17"
            //ResizeMode = "NoResize"
            //Icon = "/Icon.png"
            //WindowStartupLocation = "CenterScreen"
        }
    }
}