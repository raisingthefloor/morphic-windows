using Morphic.Core;
using Morphic.Settings;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Morphic.ManualTester
{
    /// <summary>
    /// Interaction logic for ManualControlString.xaml
    /// </summary>
    public partial class ManualControlString : UserControl
    {
        public SettingsManager manager;
        public string solutionId;
        public Setting setting;
        public Preferences.Key key;
        private MainWindow window;
        public Boolean changed = false;
        private Brush greenfield = new SolidColorBrush(Color.FromArgb(30, 0, 176, 0));
        private Brush whitefield = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
        public ManualControlString(MainWindow window, SettingsManager manager, string solutionId, Setting setting)
        {
            InitializeComponent();
            this.window = window;
            this.manager = manager;
            this.solutionId = solutionId;
            this.setting = setting;
            key = new Preferences.Key(solutionId, setting.Name);
            ControlName.Text = setting.Name;
            CaptureSetting();
        }

        public async void CaptureSetting()
        {
            LoadingIcon.Visibility = Visibility.Visible;
            InputField.Text = "";
            if (await manager.Capture(key) is string value)
            {
                InputField.Text = value;
            }
            InputField.Background = whitefield;
            LoadingIcon.Visibility = Visibility.Hidden;
        }

        private void EnterCheck(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                changed = true;
                InputField.Background = greenfield;
                if (window.AutoApply) ApplySetting();
            }
        }

        private void ValueChanged(object sender, RoutedEventArgs e)
        {
            changed = true;
            InputField.Background = greenfield;
            if (window.AutoApply) ApplySetting();
        }

        public async void ApplySetting()
        {
            if (!changed) return;
            changed = false;
            InputField.Background = whitefield;
            bool success = await manager.Apply(key, InputField.Text);
            if (!success) CaptureSetting();
        }
    }
}
