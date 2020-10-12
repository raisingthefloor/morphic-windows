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

using Microsoft.Extensions.Logging;
using Morphic.Service;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;

namespace Morphic.Client.Travel
{
    using Elements;

    public partial class ApplyPanel : StackPanel, IStepPanel
    {
        #region Creating a Panel

        public ApplyPanel(Session session, ILogger<ApplyPanel> logger, Backups backups)
        {
            this.session = session;
            this.logger = logger;
            this.backups = backups;
            this.InitializeComponent();
        }

        /// <summary>
        /// A logger to use
        /// </summary>
        private readonly ILogger<ApplyPanel> logger;

        private readonly Backups backups;

        #endregion

        public async void OnApply(object sender, EventArgs args)
        {
            this.ReadyPanel.Visibility = Visibility.Collapsed;
            this.ApplyingPanel.Visibility = Visibility.Visible;

            int startTime = Environment.TickCount;
            int minTime = 5000;

            await this.backups.Store();

            await this.session.ApplyAllPreferences();

            await Task.Delay(Math.Max(minTime - (Environment.TickCount - startTime), 1));

            this.ApplyingPanel.Visibility = Visibility.Collapsed;
            this.CompletePanel.Visibility = Visibility.Visible;
        }

        public void OnCancel(object sender, EventArgs args)
        {
            MessageBoxResult result = MessageBox.Show(Window.GetWindow(this)!, "Do you really want to cancel?",
                "Apply from Morphic Cloud Vault", MessageBoxButton.YesNo);

            if (result == MessageBoxResult.Yes)
            {
                this.StepFrame.CloseWindow();
            }
        }

        public void OnOk(object sender, EventArgs args)
        {
            this.Completed?.Invoke(this, EventArgs.Empty);
        }

        public StepFrame StepFrame { get; set; } = null!;

        /// <summary>
        /// Dispatched when the capture process is complete and the minimum time has elapsed
        /// </summary>
        public event EventHandler? Completed;

        /// <summary>
        /// The Morphic Session to use for making requests
        /// </summary>
        private readonly Session session;

    }
}
