using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Morphic.ManualTester
{
    using Settings.SettingsHandlers;

    /// <summary>
    /// Interaction logic for ManualControlBoolean.xaml
    /// </summary>
    public partial class ManualControlBoolean : UserControl
    {
        public Setting setting;
        private MainWindow window;
        public bool changed;
        private Brush greenfield = new SolidColorBrush(Color.FromArgb(30, 0, 176, 0));
        private Brush whitefield = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
        public ManualControlBoolean(MainWindow window, Setting setting)
        {
            InitializeComponent();
            this.window = window;
            this.setting = setting;
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
            bool? check = await this.setting.GetValue<bool>();
            ControlCheckBox.IsChecked = check;
            LoadingIcon.Visibility = Visibility.Hidden;
        }

        public async void ApplySetting()
        {
            if (!changed) return;
            changed = false;
            ControlCheckBox.Background = whitefield;
            bool success = await this.setting.SetValue(ControlCheckBox.IsChecked);
            if (!success) CaptureSetting();
        }
    }
}
