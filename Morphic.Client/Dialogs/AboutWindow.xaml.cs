﻿namespace Morphic.Client.Dialogs
{
    using System.Diagnostics;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Navigation;
    using AutoUpdaterDotNET;
    using Config;
    using Microsoft.Extensions.DependencyInjection;

    public partial class AboutWindow : Window
    {

        public AboutWindow()
        {
            this.DataContext = BuildInfo.Current;
            this.InitializeComponent();
        }

        private void CheckUpdate_OnClick(object sender, RequestNavigateEventArgs e)
        {
            // Make it appear that something is happening.
            Cursor oldCursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = Cursors.Wait;
            Task.Delay(3000).ContinueWith(t => this.Dispatcher.Invoke(() => Mouse.OverrideCursor = oldCursor));

            UpdateOptions? updateOptions = App.Current.ServiceProvider.GetRequiredService<UpdateOptions>();
            string appCastUrl = App.GetAppCastUrlForCurrentProcessor(updateOptions);
            if (string.IsNullOrEmpty(appCastUrl) == false)
            {
                 AutoUpdater.Start(appCastUrl);
            }
            e.Handled = true;
        }

        private void WebLink_OnClick(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri)
            {
                UseShellExecute = true
            });
            e.Handled = true;
        }
    }
}