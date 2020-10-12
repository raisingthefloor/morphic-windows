using System.Windows;

namespace Morphic.Client.Login
{
    using System;
    using Microsoft.Extensions.DependencyInjection;

    public partial class LoginWindow : Window
    {
        private readonly IServiceProvider serviceProvider;

        public LoginWindow(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
            InitializeComponent();
            this.ShowLoginPanel();
        }

        private void ShowLoginPanel()
        {
            LoginPanel loginPanel = this.StepFrame.PushPanel<LoginPanel>();
        }
    }
}

