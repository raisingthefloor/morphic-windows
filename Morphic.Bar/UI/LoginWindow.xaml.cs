// LoginWindow.xaml.cs: Gets login details from the user.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

using System.Windows;

namespace Morphic.Bar.UI
{
    using System;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using Morphic.Service;
    using Morphic.Core;
    using Microsoft.Extensions.Logging;
    using CommandLine;

    /// <summary>
    /// Gets the login and password from the user.
    /// </summary>
    public partial class LoginWindow : Window, INotifyPropertyChanged
    {
        
        public LoginWindow(CommunitySession session, ILogger<LoginWindow> logger)
        {
            this.DataContext = this;
            this.InitializeComponent();
            this.session = session;
            this.Closed += this.OnClosed;
            this.logger = logger;
        }

        private readonly CommunitySession session;
        private readonly ILogger logger;

        private void OnClosed(object? sender, EventArgs e)
        {
        }

        private void Complete(object? sender, EventArgs e)
        {
            this.Close();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            _ = Login();
        }
        /// <summary>
        /// Perform the method
        /// </summary>
        /// <returns></returns>
        private async Task Login()
        {
            ErrorLabel.Visibility = Visibility.Hidden;
            SetFieldsEnabled(false);
            var credentials = new UsernameCredentials(UsernameBox.Text, PasswordBox.Password);
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
            else
            {
                Close();
            }
        }

        private void SetFieldsEnabled(bool enabled)
        {
            UsernameBox.IsEnabled = enabled;
            PasswordBox.IsEnabled = enabled;
            LoginButton.IsEnabled = enabled;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

