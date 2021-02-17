// LoginWindow.xaml.cs: Gets login details from the user.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

namespace Morphic.Client.Dialogs
{
    using System;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using System.Windows;
    using Core;
    using Microsoft.Extensions.Logging;
    using Service;

    /// <summary>
    /// Gets the login and password from the user.
    /// </summary>
    public partial class CommunityLoginWindow : Window, INotifyPropertyChanged
    {
        public CommunityLoginWindow(MorphicSession session, ILogger<CommunityLoginWindow> logger)
        {
            this.DataContext = this;
            this.InitializeComponent();
            this.session = session;
            this.Closed += this.OnClosed;
            this.logger = logger;
        }

        private readonly MorphicSession session;
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
            _ = this.Login();
        }
        /// <summary>
        /// Perform the method
        /// </summary>
        /// <returns></returns>
        private async Task Login()
        {
            this.ErrorLabel.Visibility = Visibility.Hidden;
            this.SetFieldsEnabled(false);
            var credentials = new UsernameCredentials(this.UsernameBox.Text, this.PasswordBox.Password);
            var success = false;
            try
            {
                success = await this.session.Authenticate(credentials);
            }
            catch (HttpService.BadRequestException e)
            {
                this.logger.LogWarning(e, "Bad login request");
            }
            if (!success)
            {
                this.ErrorLabel.Visibility = Visibility.Visible;
                this.ErrorLabel.Focus(); // Makes narrator read the error label
                this.SetFieldsEnabled(true);
            }
            else
            {
                this.Close();
            }
        }

        private void SetFieldsEnabled(bool enabled)
        {
            this.UsernameBox.IsEnabled = enabled;
            this.PasswordBox.IsEnabled = enabled;
            this.LoginButton.IsEnabled = enabled;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

