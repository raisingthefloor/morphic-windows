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
    using Client;

    /// <summary>
    /// Gets the login and password from the user.
    /// </summary>
    public partial class LoginWindow : Window, INotifyPropertyChanged
    {
        public UserPasswordCredentials Credentials { get; private set; }

        private TaskCompletionSource<bool?>? gotCredentials;
        
        public LoginWindow(UserPasswordCredentials credentials)
        {
            this.Credentials = credentials;
            this.DataContext = this;
            this.InitializeComponent();

            this.Credentials.Success += this.Complete;
            this.Credentials.Cancelled += this.Complete;
            this.Closed += this.OnClosed;
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            this.gotCredentials?.SetCanceled();
        }

        private void Complete(object? sender, EventArgs e)
        {
            this.Close();
        }

        public Task<bool?> GetCredentials()
        {
            this.PasswordBox.Password = this.Credentials.Password;
            this.Show();
            this.OnPropertyChanged(nameof(this.Credentials));
            this.gotCredentials = new TaskCompletionSource<bool?>();
            return this.gotCredentials.Task;
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            this.Credentials.Password = this.PasswordBox.Password;
            this.gotCredentials?.SetResult(true);
            this.gotCredentials = null;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

