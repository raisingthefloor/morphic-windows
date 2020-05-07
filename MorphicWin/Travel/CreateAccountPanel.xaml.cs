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
            UsernameTooShort,
            PasswordTooShort
        }

        private const int minimumUsernameLength = 2;

        private ValidationError inputError {
            get
            {
                var username = UsernameField.Text;
                var password = PasswordField.Password;
                if (username.Length == 0)
                {
                    return ValidationError.EmptyUsername;
                }
                if (username.Length < minimumUsernameLength)
                {
                    return ValidationError.UsernameTooShort;
                }
                if (password.Length == 0)
                {
                    return ValidationError.EmptyPassword;
                }
                if (password.Length < minimumPasswordLength)
                {
                    return ValidationError.PasswordTooShort;
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

        private void UpdateValidation()
        {
            var error = inputError;
            SubmitButton.IsEnabled = error == ValidationError.None;
        }

        private static Regex whitespaceExpression = new Regex(@"\w");

        private void UsernameField_TextChanged(object sender, TextChangedEventArgs e)
        {
            UsernameField.Text = whitespaceExpression.Replace(UsernameField.Text, "");
            UpdateValidation();
        }

        private void PasswordField_PasswordChanged(object sender, RoutedEventArgs e)
        {
            UpdateValidation();
        }
    }
}
