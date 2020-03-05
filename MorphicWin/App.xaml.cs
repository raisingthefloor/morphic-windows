using System;
using System.Windows;

namespace MorphicWin
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            CreateNotifyIcon();
            Settings.Default.PropertyChanged += OnSettingChanged;
            if (Settings.Default.UserId == "")
            {
                Morphic.Shared.OpenConfigurator();
            }
        }

        private void OnSettingChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "UserId" && Settings.Default.UserId != "")
            {
                Morphic.Shared.ShowQuickStrip();
            }
        }

        #region System Tray Icon

        private System.Windows.Forms.NotifyIcon notifyIcon = null;

        private void CreateNotifyIcon()
        {
            notifyIcon = new System.Windows.Forms.NotifyIcon();
            notifyIcon.Click += OnNotifyIconClicked;
            notifyIcon.Icon = MorphicWin.Properties.Resources.Icon;
            notifyIcon.Text = "Morphic Quick Strip";
            notifyIcon.Visible = true;
        }

        private void OnNotifyIconClicked(object sender, EventArgs e)
        {
            Morphic.Shared.ToggleQuickStrip();
        }

        #endregion

    }
}
