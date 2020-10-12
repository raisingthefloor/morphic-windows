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
using System.Threading;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using Morphic.Settings;
using System.Text.RegularExpressions;
using Morphic.Core;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace Morphic.Client.Travel
{
    using Elements;

    /// <summary>
    /// The Capture panel is one of the steps shown when the user is walked through the process of taking their settings with them.
    /// It shows a progress indicator while a <code>CaptureSession</code> is run behind the scenes.  No user interaction is required.
    /// </summary>
    public partial class CapturePanel : StackPanel, IStepPanel
    {

        #region Creating a Panel

        public CapturePanel(Session session, ILogger<CapturePanel> logger, IServiceProvider serviceProvider)
        {
            this.session = session;
            this.logger = logger;
            this.InitializeComponent();
            this.Loaded += this.OnLoaded;
        }

        /// <summary>
        /// A logger to use
        /// </summary>
        private readonly ILogger<CapturePanel> logger;

        #endregion

        public async void OnSave(object sender, EventArgs args)
        {
            await this.session.Service.Save(this.Preferences);
            this.CompletePanel.Visibility = Visibility.Collapsed;
            this.SavedPanel.Visibility = Visibility.Visible;
        }

        public void OnCancel(object sender, EventArgs args)
        {
            MessageBoxResult result = MessageBox.Show(Window.GetWindow(this)!, "Do you really want to cancel?",
                "Saving Morphic Cloud Vault", MessageBoxButton.YesNo);

            if (result == MessageBoxResult.Yes)
            {
                this.StepFrame.CloseWindow();
            }
        }

        public void OnOk(object sender, EventArgs args)
        {
            this.Completed?.Invoke(this, EventArgs.Empty);
        }


        #region Completion Events

        public StepFrame StepFrame { get; set; }

        /// <summary>
        /// Dispatched when the capture process is complete and the minimum time has elapsed
        /// </summary>
        public event EventHandler? Completed;

        #endregion

        #region Lifecycle

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _ = this.RunCapture();
        }

        #endregion

        #region Capture

        /// <summary>
        /// The Morphic Session to use for making requests
        /// </summary>
        private readonly Session session;

        /// <summary>
        /// The preferences where captured values will be stored
        /// </summary>
        public Preferences Preferences { get; set; } = null!;

        /// <summary>
        /// Create and run a capture session
        /// </summary>
        /// <returns></returns>
        private async Task RunCapture()
        {
            int startTime = Environment.TickCount;
            int minTime = 5000;
            this.Preferences = this.session.Preferences!;
            CaptureSession captureSession = new CaptureSession(this.session.SettingsManager, this.Preferences);
            captureSession.AddAllSolutions();
            try
            {
                await captureSession.Run();
            }
            catch (Exception e)
            {
                MessageBox.Show(Window.GetWindow(this), "Morphic ran into a problem while capturing the settings");
            }

            await Task.Delay(Math.Max(minTime - (Environment.TickCount - startTime), 1));

            this.CollectingPanel.Visibility = Visibility.Collapsed;
            this.CompletePanel.Visibility = Visibility.Visible;
        }

        #endregion
    }
}
