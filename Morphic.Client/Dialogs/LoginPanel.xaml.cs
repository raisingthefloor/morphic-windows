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
    using System.Media;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using Core;
    using Elements;
    using Microsoft.Extensions.Logging;
    using Service;
    using Settings.SolutionsRegistry;

    /// <summary>
    /// Login window for authenticating users and applying their settings
    /// </summary>
    public partial class LoginPanel : StackPanel, IStepPanel
    {

        #region Create a Window

        public LoginPanel(MorphicSession morphicSession, ILogger<LoginPanel> logger, SessionOptions options, IServiceProvider serviceProvider)
        {
            this.morphicSession = morphicSession;
            this.logger = logger;
            this.serviceProvider = serviceProvider;
            UriBuilder? builder = new UriBuilder(options.FrontEndUri);
            builder.Path = "/password/reset";
            this.ForgotPasswordUriString = builder.Uri.AbsoluteUri;
            this.InitializeComponent();
        }

        /// <summary>
        /// The session to use
        /// </summary>
        private readonly MorphicSession morphicSession;

        /// <summary>
        /// A logger to use
        /// </summary>
        private readonly ILogger<LoginPanel> logger;

        private readonly IServiceProvider serviceProvider;

        #endregion

        #region Logging In

        public bool ApplyPreferencesAfterLogin { get; set; } = false;

        /// <summary>
        /// Event handler for the login button click
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnLogin(object sender, RoutedEventArgs e)
        {
            _ = this.Login();
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
            this.ErrorLabel.Visibility = Visibility.Hidden;
            this.SetFieldsEnabled(false);
            UsernameCredentials? credentials = new UsernameCredentials(this.UsernameField.Text, this.PasswordField.Password);
            bool success = false;
            try
            {
                success = await this.morphicSession.Authenticate(credentials, true);
            }
            catch (HttpService.BadRequestException e)
            {
                this.logger.LogWarning(e, "Bad login request");
            }
            if (!success)
            {
                this.ErrorLabel.Visibility = Visibility.Visible;
                // OBSERVATION: this may not be the best option, as the user then needs to figure out how to get back to the password field; setting focus to the Password field would be ideal if we can also make narrator read this error label
                this.ErrorLabel.Focus(); // Makes narrator read the error label
                this.SetFieldsEnabled(true);
            }
            else if (this.ApplyPreferencesAfterLogin)
            {
                // login successful
                await App.Current.Countly_RecordEventAsync("signIn");

                _ = this.morphicSession.ApplyAllPreferences();
                this.Close();
            }
            else
            {
                this.OnComplete();
            }
        }

        private void SetFieldsEnabled(bool enabled)
        {
            this.UsernameField.IsEnabled = enabled;
            this.PasswordField.IsEnabled = enabled;
            this.LoginButton.IsEnabled = enabled;
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
            accountPanel.ApplyPreferencesAfterLogin = this.ApplyPreferencesAfterLogin;
            accountPanel.Completed += (o, args) => this.OnComplete();
            e.Handled = true;
        }

        #endregion

        #region Checking Input

        private void UsernameField_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (this.UsernameField.Text == " ")
            {
                _ = this.EnableNarrator();
                this.UsernameField.Text = "";
            }
            this.UpdateValidation();
        }

        private void PasswordField_PasswordChanged(object sender, RoutedEventArgs e)
        {
            this.UpdateValidation();
        }

        private void UpdateValidation()
        {
            this.LoginButton.IsEnabled = this.UsernameField.Text.Length > 0 && this.PasswordField.Password.Length > 0;
        }

        #endregion

        #region Announcement

        public async Task Announce()
        {
            bool isNarratorEnabled = await this.morphicSession.GetSetting<bool>(SettingId.NarratorEnabled);
            if (!isNarratorEnabled)
            {
                var player = new SoundPlayer(Properties.Resources.LoginAnnounce);
                player.Load();
                player.Play();
            }
        }

        public async Task EnableNarrator()
        {
            await this.morphicSession.SetSetting(SettingId.NarratorEnabled, true);
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

        private void Panel_Loaded(object sender, RoutedEventArgs e)
        {
            // set focus to the username field once this panel is loaded
            this.UsernameField.Focus();
        }
    }
}
