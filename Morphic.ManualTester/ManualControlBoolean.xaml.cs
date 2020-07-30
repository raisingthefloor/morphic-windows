using Morphic.Core;
using Morphic.Settings;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Morphic.ManualTester
{
    /// <summary>
    /// Interaction logic for ManualControlBoolean.xaml
    /// </summary>
    public partial class ManualControlBoolean : UserControl
    {
        public SettingsManager manager;
        public string solutionId;
        public Setting setting;
        public Preferences.Key key;
        private MainWindow window;
        public bool changed;
        private Brush greenfield = new SolidColorBrush(Color.FromArgb(30, 0, 176, 0));
        private Brush whitefield = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
        public ManualControlBoolean(MainWindow window, SettingsManager manager, string solutionId, Setting setting)
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

        private void ValueChanged(object sender, RoutedEventArgs e)
        {
            changed = true;
            ControlCheckBox.Background = greenfield;
            if (window.AutoApply) ApplySetting();
        }

        public async void CaptureSetting()
        {
            LoadingIcon.Visibility = Visibility.Visible;
            ControlCheckBox.Background = whitefield;
            bool? check = await manager.CaptureBool(key);
            ControlCheckBox.IsChecked = check;
            LoadingIcon.Visibility = Visibility.Hidden;
        }

        public async void ApplySetting()
        {
            if (!changed) return;
            changed = false;
            ControlCheckBox.Background = whitefield;
            bool success = await manager.Apply(key, ControlCheckBox.IsChecked);
            if (!success) CaptureSetting();
        }
    }
}
