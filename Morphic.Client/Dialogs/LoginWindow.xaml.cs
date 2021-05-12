// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt
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

namespace Morphic.Client.Dialogs
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Windows;
    using Microsoft.Extensions.Logging;

    public partial class LoginWindow : Window, MorphicWindowWithArgs
    {

        public LoginWindow(ILogger<TravelWindow> logger, IServiceProvider serviceProvider)
        {
            this.logger = logger;
            this.serviceProvider = serviceProvider;
            this.InitializeComponent();
        }

        /// <summary>
        /// A logger to use
        /// </summary>
        private readonly ILogger<TravelWindow> logger;

        private readonly IServiceProvider serviceProvider;

        private LoginPanel? LoginPanel = null;
        private bool ApplyPreferencesAfterLogin = false;

        void MorphicWindowWithArgs.SetArguments(Dictionary<String, object?> args)
        {
            foreach (var (key, value) in args)
            {
                switch (key.ToLower()) {
                    case "applypreferencesafterlogin":
                        this.ApplyPreferencesAfterLogin = (value as bool?) ?? false;
                        if (this.LoginPanel != null)
                        {
                            this.LoginPanel.ApplyPreferencesAfterLogin = this.ApplyPreferencesAfterLogin;
                        }
                        break;
                    default:
                        Debug.Assert(false, "Unknown argument");
                        break;
                }
            }
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            this.ShowLoginPanel(this.ApplyPreferencesAfterLogin);
        }

        private void ShowLoginPanel(bool applyPreferencesAfterLogin)
        {
            LoginPanel loginPanel = this.StepFrame.PushPanel<LoginPanel>();
            loginPanel.ApplyPreferencesAfterLogin = applyPreferencesAfterLogin;
            loginPanel.Completed += (sender, args) => this.Close();
            this.LoginPanel = loginPanel;
        }
    }
}

