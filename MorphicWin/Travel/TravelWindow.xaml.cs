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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MorphicCore;
using MorphicService;
using System;
using System.Windows;

namespace MorphicWin
{
    /// <summary>
    /// Window that walks the user the the capture and, if necessary, account creation process.
    /// Loads each panel one at time depending on what steps are required
    /// </summary>
    public partial class TravelWindow : Window
    {

        #region Create a Window

        public TravelWindow(Session session, ILogger<TravelWindow> logger, IServiceProvider serviceProvider)
        {
            this.session = session;
            this.logger = logger;
            this.serviceProvider = serviceProvider;
            InitializeComponent();
        }

        /// <summary>
        /// The Morphic session to consult when making decisions
        /// </summary>
        private readonly Session session;

        /// <summary>
        /// A logger to use
        /// </summary>
        private readonly ILogger<TravelWindow> logger;

        /// <summary>
        /// A service provider to use when creating panels
        /// </summary>
        private readonly IServiceProvider serviceProvider;

        #endregion

        #region Lifecycle

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            if (session.User != null)
            {
                // If the user is logged in, we'll just capture to their preferences directly
                preferences = session.Preferences!;
            }
            else
            {
                // If we'll be creating a new user, start with a copy of the default preferences
                // so we don't actually change the defaults
                preferences = new Preferences(session.Preferences!);
            }
            ShowCapturePanel(animated: false);
        }

        #endregion

        #region Capture

        /// <summary>
        /// Show the capture panel and listen for its completion event
        /// </summary>
        /// <param name="animated"></param>
        private void ShowCapturePanel(bool animated = true)
        {
            var capturePage = serviceProvider.GetRequiredService<CapturePanel>();
            capturePage.Preferences = preferences;
            capturePage.Completed += CaptureCompleted;
            StepFrame.PushPanel(capturePage, animated: animated);
        }

        /// <summary>
        /// Called when the capture panel has completed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CaptureCompleted(object? sender, EventArgs e)
        {
            if (session.User == null)
            {
                ShowCreateAccountPanel();
            }
            else
            {
                ShowCompletedPanel();
            }
        }

        #endregion

        #region Create Account

        private Preferences preferences = null!;

        /// <summary>
        /// Show the account creation panel
        /// </summary>
        /// <param name="animated"></param>
        private void ShowCreateAccountPanel(bool animated = true)
        {
            var accountPanel = serviceProvider.GetRequiredService<CreateAccountPanel>();
            accountPanel.Preferences = preferences;
            accountPanel.Completed += AccountCreationCompleted;
            StepFrame.PushPanel(accountPanel, animated: animated);
        }

        /// <summary>
        /// Called when the account creation panel is complete
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AccountCreationCompleted(object? sender, EventArgs e)
        {
            ShowCompletedPanel();
        }

        #endregion

        #region Completed

        /// <summary>
        /// Show the completed panel
        /// </summary>
        /// <param name="animated"></param>
        private void ShowCompletedPanel(bool animated = true)
        {
            var completedPage = serviceProvider.GetRequiredService<TravelCompletedPanel>();
            completedPage.Completed += CompletedCompleted;
            StepFrame.PushPanel(completedPage, animated: animated);
        }

        /// <summary>
        /// Called when the completed panel is complete
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CompletedCompleted(object? sender, EventArgs e)
        {
            Close();
        }

        #endregion
    }
}
