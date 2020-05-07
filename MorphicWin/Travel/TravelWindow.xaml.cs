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
using MorphicService;
using System;
using System.Windows;

namespace MorphicWin
{
    /// <summary>
    /// Interaction logic for TravelWindow.xaml
    /// </summary>
    public partial class TravelWindow : Window
    {

        private readonly Session session;

        private readonly ILogger<TravelWindow> logger;

        private readonly IServiceProvider serviceProvider;

        public TravelWindow(Session session, ILogger<TravelWindow> logger, IServiceProvider serviceProvider)
        {
            this.session = session;
            this.logger = logger;
            this.serviceProvider = serviceProvider;
            InitializeComponent();
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            if (session.User == null)
            {
                logger.LogInformation("No user logged in, showing account creation panel");
                ShowCreateAccountPanel(animated: false);
            }
            else
            {
                logger.LogInformation("User logged in, skipping directly to capture panel");
                ShowCapturePanel(animated: false);
            }
        }

        private void ShowCreateAccountPanel(bool animated = true)
        {
            var accountPanel = serviceProvider.GetRequiredService<CreateAccountPanel>();
            accountPanel.Completed += AccountCreationCompleted;
            StepFrame.PushPanel(accountPanel, animated: animated);
        }

        private void ShowCapturePanel(bool animated = true)
        {
            var capturePage = serviceProvider.GetRequiredService<CapturePanel>();
            capturePage.Completed += CaptureCompleted;
            StepFrame.PushPanel(capturePage, animated: animated);
        }

        private void ShowCompletedPanel(bool animated = true)
        {
            var completedPage = serviceProvider.GetRequiredService<TravelCompletedPanel>();
            StepFrame.PushPanel(completedPage, animated: animated);
        }

        private void AccountCreationCompleted(object? sender, EventArgs e)
        {
            ShowCapturePanel();
        }

        private void CaptureCompleted(object? sender, EventArgs e)
        {
            ShowCompletedPanel();
        }
    }
}
