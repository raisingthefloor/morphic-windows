using System.Windows;
using System.Windows.Navigation;

namespace Morphic.Client.About
{
    using System.Diagnostics;
    using System.Threading.Tasks;
    using System.Windows.Input;
    using AutoUpdaterDotNET;
    using Microsoft.Extensions.DependencyInjection;

    public partial class AboutWindow : Window
    {

        public AboutWindow(BuildInfo buildInfo)
        {
            this.buildInfo = buildInfo;
            this.DataContext = buildInfo;
            InitializeComponent();
        }

        private readonly BuildInfo buildInfo;

        private void CheckUpdate_OnClick(object sender, RequestNavigateEventArgs e)
        {
            // Make it appear that something is happening.
            Cursor oldCursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = Cursors.Wait;
            Task.Delay(3000).ContinueWith(t => this.Dispatcher.Invoke(() => Mouse.OverrideCursor = oldCursor));

            UpdateOptions? updateOptions = App.Current.ServiceProvider.GetRequiredService<UpdateOptions>();
            if (!string.IsNullOrEmpty(updateOptions?.AppCastUrl))
            {
                AutoUpdater.Start(updateOptions.AppCastUrl);
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