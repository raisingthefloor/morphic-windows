// Copyright 2020-2024 Raising the Floor - US, Inc.
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
using System.Reflection;
using System.Windows;

namespace Morphic.AboutWindow
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Morphic.UI.ThemeAwareWindow
    {
        private readonly Version ApplicationVersion;
        //
        public string MajorMinorVersionString
        {
            get
            {
                return this.ApplicationVersion.Major.ToString() + "." + this.ApplicationVersion.Minor.ToString();
            }
        }
        //
        public string BuildVersionString
        {
            get
            {
                var build = this.ApplicationVersion.Build;
                if (build != 0)
                {
                    return this.ApplicationVersion.Build.ToString();
                }
                else
                {
                    return "[build version is unknown]";
                }
            }
        }

        public AboutWindow()
        {
            this.ApplicationVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0,0,0,0);
            InitializeComponent();
        }

        private void LearnMoreHyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Morphic.Utils.WebBrowserUtils.OpenBrowserToUri(e.Uri);
            e.Handled = true;
        }
    }
}
