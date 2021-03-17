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
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using Core;
    using Elements;
    using Microsoft.Extensions.Logging;
    using Service;
    using MessageBox = System.Windows.Forms.MessageBox;

    /// <summary>
    /// A panel shown when the user needs to create an account in order to save their captured settings
    /// </summary>
    public partial class CreateAccountPanel : StackPanel, IStepPanel
    {

        #region Creating a Panel

        public CreateAccountPanel(MorphicSession morphicSession, ILogger<CreateAccountPanel> logger, IServiceProvider serviceProvider)
        {
            this.morphicSession = morphicSession;
            this.logger = logger;
            this.serviceProvider = serviceProvider;
            this.InitializeComponent();
        }

        /// <summary>
        /// A logger to use
        /// </summary>
        private readonly ILogger<CreateAccountPanel> logger;

        private readonly IServiceProvider serviceProvider;

        #endregion

        #region Completion Events

        /// <summary>
        /// Dispatched when the user's accout is successfully created
        /// </summary>
        public event EventHandler? Completed;

        #endregion

        public bool ApplyPreferencesAfterLogin { get; set; } = false;

        #region User Info

        public Preferences? Preferences = null;

        #endregion

        #region Form Submission

        /// <summary>
        /// The session to use for making requests
        /// </summary>
        private readonly MorphicSession morphicSession;

        /// <summary>
        /// Event handler for the submit button click
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public async void OnSubmit(object? sender, RoutedEventArgs e)
        {
            await this.SubmitAsync();
        }

        /// <summary>
        /// An async method for actually doing the registration submission
        /// </summary>
        /// <returns></returns>
        private async Task SubmitAsync()
        {
            // TODO: show activity indicator
            this.UpdateValidation();
            this.SetFieldsEnabled(false);
            var user = new User();
            user.Email = this.UsernameField.Text;
            var credentials = new UsernameCredentials(this.UsernameField.Text, this.PasswordField.Password);
            var success = false;
            var errorMessage = "";
            try
            {
                success = await this.morphicSession.RegisterUserAsync(user, credentials, this.Preferences ?? new Preferences());
            }
            catch (AuthService.BadPasswordException)
            {
                errorMessage = "Your password is too easily guessed.  Please use another.";
            }
            catch (AuthService.ExistingEmailException)
            {
                errorMessage = "We recognize your email.  Use the 'Already have an account?' link below.";
            }
            catch (AuthService.ExistingUsernameException)
            {
                errorMessage = "We recognize your email.  Use the 'Already have an account?' link below.";
            }
            catch (AuthService.InvalidEmailException)
            {
                errorMessage = "Please provide a valid email address";
            }
            if (success)
            {
                MessageBox.Show("Your account was created.\n\nPlease check your email for further instructions.");
                this.Completed?.Invoke(this, new EventArgs());
            }
            else
            {
                if (errorMessage == "")
                {
                    errorMessage = "We could not complete the request.  Please try again.";
                }
                this.ErrorLabel.Visibility = Visibility.Visible;
                this.ErrorLabel.Content = errorMessage;
                this.ErrorLabel.Focus(); // Causes screen reader to read label
                this.SetFieldsEnabled(true);
            }
        }

        #endregion

        #region Input Validation

        /// <summary>
        /// The various client-checked input errors the user may encounter
        /// </summary>
        private enum ValidationError
        {
            None,
            EmptyUsername,
            EmptyPassword,
            EmptyConfirmation,
            UsernameTooShort,
            PasswordTooShort,
            PasswordsDontMatch
        }

        /// <summary>
        /// Get the most revelant input error for the user
        /// </summary>
        private ValidationError inputError {
            get
            {
                var username = this.UsernameField.Text;
                var password = this.PasswordField.Password;
                var confirmation = this.ConfirmPasswordField.Password;
                if (!this.hasTypedUsername)
                {
                    return ValidationError.EmptyUsername;
                }
                if (username.Length < minimumUsernameLength)
                {
                    return ValidationError.UsernameTooShort;
                }
                if (!this.hasTypedPassword)
                {
                    return ValidationError.EmptyPassword;
                }
                if (password.Length < minimumPasswordLength)
                {
                    return ValidationError.PasswordTooShort;
                }
                if (!this.hasTypedConfirmation)
                {
                    return ValidationError.EmptyConfirmation;
                }
                if (password != confirmation)
                {
                    return ValidationError.PasswordsDontMatch;
                }
                return ValidationError.None;
            }
        }

        /// <summary>
        /// A client-enforced username minimum length
        /// </summary>
        private const int minimumUsernameLength = 2;

        /// <summary>
        /// A client-enforced password minimum length
        /// </summary>
        private const int minimumPasswordLength = 6;

        /// <summary>
        /// Indicates if the username field has been typed in, which is used to decide which errors to show
        /// </summary>
        private bool hasTypedUsername;

        /// <summary>
        /// Indicates if the password field has been typed in, which is used to decide which errors to show
        /// </summary>
        private bool hasTypedPassword;

        /// <summary>
        /// Indicates if the password confirmation has been typed in, which is used to decide which errors to show
        /// </summary>
        private bool hasTypedConfirmation;

        /// <summary>
        /// Enable or disable all the fields
        /// </summary>
        /// <param name="enabled"></param>
        private void SetFieldsEnabled(bool enabled)
        {
            this.UsernameField.IsEnabled = enabled;
            this.PasswordField.IsEnabled = enabled;
            this.SubmitButton.IsEnabled = enabled;
        }

        /// <summary>
        /// Update the UI based on the current input validation state
        /// </summary>
        private void UpdateValidation()
        {
            var error = this.inputError;
            this.SubmitButton.IsEnabled = error == ValidationError.None;
            switch (error) {
                case ValidationError.UsernameTooShort:
                    this.ErrorLabel.Content = String.Format("Your username needs to be at least {0} letters", minimumUsernameLength);
                    this.ErrorLabel.Visibility = Visibility.Visible;
                    break;
                case ValidationError.PasswordTooShort:
                    this.ErrorLabel.Content = String.Format("Your password needs to be at least {0} letters", minimumPasswordLength);
                    this.ErrorLabel.Visibility = Visibility.Visible;
                    break;
                case ValidationError.PasswordsDontMatch:
                    this.ErrorLabel.Content = "Your passwords don't match";
                    this.ErrorLabel.Visibility = Visibility.Visible;
                    break;
                default:
                    this.ErrorLabel.Visibility = Visibility.Hidden;
                    break;
            }

        }

        /// <summary>
        /// Used to remove whitespace from the username field
        /// </summary>
        private static Regex whitespaceExpression = new Regex(@"\s");

        private void UsernameField_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.UsernameField.Text = whitespaceExpression.Replace(this.UsernameField.Text, "");
            this.UsernameField.SelectionStart = this.UsernameField.Text.Length;
            this.UsernameField.SelectionLength = 0;
            this.UpdateValidation();
        }

        private void PasswordField_PasswordChanged(object sender, RoutedEventArgs e)
        {
            this.UpdateValidation();
        }

        private void UsernameField_LostFocus(object sender, RoutedEventArgs e)
        {
            this.hasTypedUsername = this.UsernameField.Text.Length > 0;
            this.UpdateValidation();
        }

        private void PasswordField_LostFocus(object sender, RoutedEventArgs e)
        {
            this.hasTypedPassword = this.PasswordField.Password.Length > 0;
            this.UpdateValidation();
        }

        private void ConfirmPasswordField_PasswordChanged(object sender, RoutedEventArgs e)
        {
            this.hasTypedConfirmation = this.ConfirmPasswordField.Password == this.PasswordField.Password;
            this.UpdateValidation();
        }

        private void ConfirmPasswordField_LostFocus(object sender, RoutedEventArgs e)
        {
            this.hasTypedConfirmation = this.ConfirmPasswordField.Password.Length > 0;
            this.UpdateValidation();
        }

        #endregion

        #region Other Actions

        /// <summary>
        /// Handler for when the user clicks on the "Already have an Account?" button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void OnAlreadyHaveAccount(object? sender, RoutedEventArgs e)
        {
            LoginPanel loginPanel = this.StepFrame.PushPanel<LoginPanel>();
            loginPanel.ApplyPreferencesAfterLogin = this.ApplyPreferencesAfterLogin;
        }

        #endregion

        public StepFrame StepFrame { get; set; }

        private void StackPanel_Loaded(object sender, RoutedEventArgs e)
        {
            // set focus to the username field once this panel is loaded
            this.UsernameField.Focus();
        }
    }
}
