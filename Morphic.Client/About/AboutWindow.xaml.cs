using System;
using System.Windows;
using System.Windows.Navigation;

namespace Morphic.Client.About
{
    public partial class AboutWindow : Window
    {

        public AboutWindow(BuildInfo buildInfo)
        {
            this.buildInfo = buildInfo;
            InitializeComponent();
        }

        private readonly BuildInfo buildInfo;

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            VersionLabel.Content = buildInfo.InformationalVersion;
            BuildLabel.Content = String.Format("(build {0})", buildInfo.Commit);
        }

        public void ContactUs(object? sender, RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start("explorer.exe", e.Uri.AbsoluteUri);
            e.Handled = true;
        }
    }
}