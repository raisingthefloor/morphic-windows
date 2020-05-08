using Microsoft.Extensions.Logging;
using MorphicCore;
using MorphicService;
using System;
using System.Windows;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace MorphicWin
{
    /// <summary>
    /// Interaction logic for CreateAccountPanel.xaml
    /// </summary>
    public partial class CreateAccountPanel : StackPanel
    {
        public CreateAccountPanel(Session session, ILogger<CreateAccountPanel> logger)
        {
            this.session = session;
            this.logger = logger;
            InitializeComponent();
        }

        private readonly Session session;

        private readonly ILogger<CreateAccountPanel> logger;

        public EventHandler? Completed;

        public void OnSubmit(object? sender, RoutedEventArgs e)
        {
            _ = Submit();
        }

        private async Task Submit()
        {
            // TODO: show activity indicator
            SetFieldsEnabled(false);
            var user = new User();
            var credentials = new UsernameCredentials(UsernameField.Text, PasswordField.Password);
            var success = await session.RegisterUser(user, credentials);
            if (success)
            {
                Completed?.Invoke(this, new EventArgs());
            }
            else
            {
                // TODO: get and show error
                SetFieldsEnabled(true);
            }
        }

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

        private const int minimumUsernameLength = 2;

        private ValidationError inputError {
            get
            {
                var username = UsernameField.Text;
                var password = PasswordField.Password;
                var confirmation = ConfirmPasswordField.Password;
                if (!hasTypedUsername)
                {
                    return ValidationError.EmptyUsername;
                }
                if (username.Length < minimumUsernameLength)
                {
                    return ValidationError.UsernameTooShort;
                }
                if (!hasTypedPassword)
                {
                    return ValidationError.EmptyPassword;
                }
                if (password.Length < minimumPasswordLength)
                {
                    return ValidationError.PasswordTooShort;
                }
                if (!hasTypedConfirmation)
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

        private const int minimumPasswordLength = 6;

        private void SetFieldsEnabled(bool enabled)
        {
            UsernameField.IsEnabled = enabled;
            PasswordField.IsEnabled = enabled;
            SubmitButton.IsEnabled = enabled;
        }

        private bool hasTypedUsername;
        private bool hasTypedPassword;
        private bool hasTypedConfirmation;

        private void UpdateValidation()
        {
            var error = inputError;
            SubmitButton.IsEnabled = error == ValidationError.None;
            switch (error) {
                case ValidationError.UsernameTooShort:
                    ErrorLabel.Content = String.Format("Your username needs to be at least {0} letters", minimumUsernameLength);
                    ErrorLabel.Visibility = Visibility.Visible;
                    break;
                case ValidationError.PasswordTooShort:
                    ErrorLabel.Content = String.Format("Your password needs to be at least {0} letters", minimumPasswordLength);
                    ErrorLabel.Visibility = Visibility.Visible;
                    break;
                case ValidationError.PasswordsDontMatch:
                    ErrorLabel.Content = "Your passwords don't match";
                    ErrorLabel.Visibility = Visibility.Visible;
                    break;
                default:
                    ErrorLabel.Visibility = Visibility.Hidden;
                    break;
            }

        }

        private static Regex whitespaceExpression = new Regex(@"\s");

        private void UsernameField_TextChanged(object sender, TextChangedEventArgs e)
        {
            UsernameField.Text = whitespaceExpression.Replace(UsernameField.Text, "");
            UsernameField.SelectionStart = UsernameField.Text.Length;
            UsernameField.SelectionLength = 0;
            UpdateValidation();
        }

        private void PasswordField_PasswordChanged(object sender, RoutedEventArgs e)
        {
            UpdateValidation();
        }

        public void OnAlreadyHaveAccount(object? sender, RoutedEventArgs e)
        {
            // TODO: show login
        }

        private void UsernameField_LostFocus(object sender, RoutedEventArgs e)
        {
            hasTypedUsername = UsernameField.Text.Length > 0;
            UpdateValidation();
        }

        private void PasswordField_LostFocus(object sender, RoutedEventArgs e)
        {
            hasTypedPassword = PasswordField.Password.Length > 0;
            UpdateValidation();
        }

        private void ConfirmPasswordField_PasswordChanged(object sender, RoutedEventArgs e)
        {
            UpdateValidation();
        }

        private void ConfirmPasswordField_LostFocus(object sender, RoutedEventArgs e)
        {
            hasTypedConfirmation = ConfirmPasswordField.Password.Length > 0;
            UpdateValidation();
        }
    }
}
