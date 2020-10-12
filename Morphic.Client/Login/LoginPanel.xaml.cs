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
using Morphic.Core;
using Morphic.Settings;
using Morphic.Service;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System;

namespace Morphic.Client.Login
{
    using Elements;
    using Microsoft.Extensions.DependencyInjection;
    using Travel;

    /// <summary>
    /// Login window for authenticating users and applying their settings
    /// </summary>
    public partial class LoginPanel : StackPanel, IStepPanel
    {

        #region Create a Window

        public LoginPanel(Session session, ILogger<LoginPanel> logger, SessionOptions options, IServiceProvider serviceProvider)
        {
            this.session = session;
            this.logger = logger;
            this.serviceProvider = serviceProvider;
            var builder = new UriBuilder(options.FontEndUri);
            builder.Path = "/password/reset";
            ForgotPasswordUriString = builder.Uri.AbsoluteUri;
            InitializeComponent();
        }

        /// <summary>
        /// The session to use
        /// </summary>
        private readonly Session session;

        /// <summary>
        /// A logger to use
        /// </summary>
        private readonly ILogger<LoginPanel> logger;

        private readonly IServiceProvider serviceProvider;

        #endregion

        #region Logging In

        public bool ApplyPreferences { get; set; }

        /// <summary>
        /// Event handler for the login button click
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnLogin(object sender, RoutedEventArgs e)
        {
            _ = Login();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// Perform the method
        /// </summary>
        /// <returns></returns>
        private async Task Login()
        {
            ErrorLabel.Visibility = Visibility.Hidden;
            SetFieldsEnabled(false);
            var credentials = new UsernameCredentials(UsernameField.Text, PasswordField.Password);
            var success = false;
            try
            {
                success = await session.Authenticate(credentials);
            }
            catch (HttpService.BadRequestException e)
            {
                logger.LogWarning(e, "Bad login request");
            }
            if (!success)
            {
                ErrorLabel.Visibility = Visibility.Visible;
                ErrorLabel.Focus(); // Makes narrator read the error label
                SetFieldsEnabled(true);
            }
            else if (this.ApplyPreferences)
            {
                _ = session.ApplyAllPreferences();
                Close();
            }
            else
            {
                this.OnComplete();
            }
        }

        private void SetFieldsEnabled(bool enabled)
        {
            UsernameField.IsEnabled = enabled;
            PasswordField.IsEnabled = enabled;
            LoginButton.IsEnabled = enabled;
        }

        public string ForgotPasswordUriString { get; set; } = "";

        private void ForgotPassword(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start("explorer.exe", e.Uri.AbsoluteUri);
            e.Handled = true;
        }

        private void CreateAccount(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            CreateAccountPanel accountPanel = this.StepFrame.PushPanel<CreateAccountPanel>();
            accountPanel.Completed += (o, args) => this.OnComplete();
            e.Handled = true;
        }

        #endregion

        #region Checking Input

        private void UsernameField_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (UsernameField.Text == " ")
            {
                _ = EnableNarrator();
                UsernameField.Text = "";
            }
            UpdateValidation();
        }

        private void PasswordField_PasswordChanged(object sender, RoutedEventArgs e)
        {
            UpdateValidation();
        }

        private void UpdateValidation()
        {
            LoginButton.IsEnabled = UsernameField.Text.Length > 0 && PasswordField.Password.Length > 0;
        }

        #endregion

        #region Announcement

        public async Task Announce()
        {
            var isNarratorEnabled = await session.SettingsManager.CaptureBool(SettingsManager.Keys.WindowsNarratorEnabled) ?? false;
            if (!isNarratorEnabled)
            {
                var player = new SoundPlayer(Properties.Resources.LoginAnnounce);
                player.Load();
                player.Play();
            }
        }

        public async Task EnableNarrator()
        {
            _ = await session.SettingsManager.Apply(SettingsManager.Keys.WindowsNarratorEnabled, true);
        }

        #endregion

        private void Close()
        {
            this.StepFrame.CloseWindow();
        }

        #region IStepPanel

        public StepFrame StepFrame { get; set; }
        public event EventHandler? Completed;

        protected virtual void OnComplete()
        {
            this.Completed?.Invoke(this, EventArgs.Empty);
        }

        #endregion
    }
}
