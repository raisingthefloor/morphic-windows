namespace Morphic.Client.Dialogs
{
    using System;
    using System.Windows;

    public partial class LoginWindow : Window
    {
        private readonly IServiceProvider serviceProvider;

        public LoginWindow(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
            this.InitializeComponent();
            this.ShowLoginPanel();
        }

        private void ShowLoginPanel()
        {
            LoginPanel loginPanel = this.StepFrame.PushPanel<LoginPanel>();
        }
    }
}

