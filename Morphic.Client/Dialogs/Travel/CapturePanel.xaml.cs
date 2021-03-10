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
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using Core;
    using Elements;
    using Microsoft.Extensions.Logging;
    using Service;

    /// <summary>
    /// The Capture panel is one of the steps shown when the user is walked through the process of taking their settings with them.
    /// It shows a progress indicator while a <code>CaptureSession</code> is run behind the scenes.  No user interaction is required.
    /// </summary>
    public partial class CapturePanel : StackPanel, IStepPanel
    {

        #region Creating a Panel

        public CapturePanel(MorphicSession morphicSession, ILogger<CapturePanel> logger, IServiceProvider serviceProvider)
        {
            this.morphicSession = morphicSession;
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
            // OBSERVATION: we do not check to see if the save to the server was successful
            _ = await this.morphicSession.Service.SaveAsync(this.Preferences);
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
        private readonly MorphicSession morphicSession;

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
            this.Preferences = this.morphicSession.Preferences!;

            var capturePreferencesResult = await this.morphicSession.Solutions.CapturePreferencesAsync(this.morphicSession.Preferences!);
            if (capturePreferencesResult.IsError == true) 
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
