using System;
using System.Windows;

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
            VersionLabel.Content = buildInfo.Version;
            BuildLabel.Content = String.Format("(build {0})", buildInfo.Commit);
        }
    }
}