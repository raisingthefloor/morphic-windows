using System;
using System.Windows;

namespace MorphicWin
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {            
            InitializeComponent();
        }
        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            CreateNotifyIcon();
            Settings.Default.PropertyChanged += OnSettingChanged;
            if (Settings.Default.UserId == "")
            {
                var configurator = new MorphicConfigurator();
                configurator.Show();
                configurator.Activate();
            }
        }

        private void OnSettingChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "UserId" && Settings.Default.UserId != "" && quickStrip == null)
            {
                ToggleQuickStrip();
            }
        }

        private System.Windows.Forms.NotifyIcon notifyIcon = null;

        private void CreateNotifyIcon()
        {
            notifyIcon = new System.Windows.Forms.NotifyIcon();
            notifyIcon.Click += OnNotifyIconClicked;
            notifyIcon.Icon = Properties.Resources.Icon;
            notifyIcon.Text = "Morphic Quick Strip";
            notifyIcon.Visible = true;
        }

        private void OnNotifyIconClicked(object sender, EventArgs e)
        {
            ToggleQuickStrip();
        }

        private Window quickStrip = null;

        private void ToggleQuickStrip()
        {
            if (quickStrip != null)
            {
                quickStrip.Close();
            }
            else
            {
                quickStrip = new QuickStrip();
                quickStrip.Closed += QuickStripClosed;
                var screenSize = SystemParameters.WorkArea;
                quickStrip.Top = screenSize.Height - quickStrip.Height;
                quickStrip.Left = screenSize.Width - quickStrip.Width;
                quickStrip.Show();
                quickStrip.Activate();
            }
        }

        private void QuickStripClosed(object sender, EventArgs e)
        {
            quickStrip = null;
        }
    }
}
