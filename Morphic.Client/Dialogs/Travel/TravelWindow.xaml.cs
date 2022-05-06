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
    using Service;

    /// <summary>
    /// Window that walks the user the the capture and, if necessary, account creation process.
    /// Loads each panel one at time depending on what steps are required
    /// </summary>
    public partial class TravelWindow : Window, MorphicWindowWithArgs
    {

        #region Create a Window

        public TravelWindow(MorphicSession morphicSession, ILogger<TravelWindow> logger, IServiceProvider serviceProvider)
        {
            this.morphicSession = morphicSession;
            this.logger = logger;
            this.serviceProvider = serviceProvider;
            this.InitializeComponent();
        }

        /// <summary>
        /// The Morphic session to consult when making decisions
        /// </summary>
        private readonly MorphicSession morphicSession;

        /// <summary>
        /// A logger to use
        /// </summary>
        private readonly ILogger<TravelWindow> logger;

        /// <summary>
        /// A service provider to use when creating panels
        /// </summary>
        private readonly IServiceProvider serviceProvider;

        #endregion

        private string? StartPanelAction;

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            this.ShowStartPanel();
        }

        private void ShowStartPanel()
        {
            CopyStartPanel copyStartPanel = this.StepFrame.PushPanel<CopyStartPanel>();
            copyStartPanel.Completed += (sender, args) => this.Close();
        }

        void MorphicWindowWithArgs.SetArguments(Dictionary<String, object?> args)
        {
            foreach (var (key, value) in args)
            {
                switch (key.ToLower())
                {
                    case "action":
                        this.StartPanelAction = (value as string) ?? null;
                        break;
                    default:
                        Debug.Assert(false, "Unknown argument");
                        break;
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.StartPanelAction is not null)
            {
                var copyStartPanel = this.StepFrame.CurrentPanel as CopyStartPanel;

                switch (this.StartPanelAction)
                {
                    case "CopyToCloud":
                        copyStartPanel?.CopyToCloud(sender /* or null */, new RoutedEventArgs());
                        break;
                    case "CopyFromCloud":
                        copyStartPanel?.CopyFromCloud(sender /* or null */, new RoutedEventArgs());
                        break;
                    default:
                        Debug.Assert(false, "Unknown action");
                        break;
                }
            }
        }
    }
}
