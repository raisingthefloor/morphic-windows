using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Morphic.ManualTester
{
    using Settings.SettingsHandlers;

    /// <summary>
    /// Interaction logic for ManualControlDouble.xaml
    /// </summary>
    public partial class ManualControlDouble : UserControl
    {
        private readonly Setting setting;
        private MainWindow window;
        public bool changed = false;
        private Brush redfield = new SolidColorBrush(Color.FromArgb(30, 255, 0, 0));
        private Brush greenfield = new SolidColorBrush(Color.FromArgb(30, 0, 176, 0));
        private Brush whitefield = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
        public ManualControlDouble(MainWindow window, Setting setting)
        {
            InitializeComponent();
            this.window = window;
            this.setting = setting;
            ControlName.Text = setting.Name;
            CaptureSetting();
        }

        private bool Validate()
        {
            try
            {
                double value = double.Parse(InputField.Text);
                return true;
            }
            catch
            {
                InputField.Background = redfield;
                return false;
            }
        }

        public async void CaptureSetting()
        {
            LoadingIcon.Visibility = Visibility.Visible;
            InputField.Text = "";
            InputField.Text = (await this.setting.GetValue(double.NaN)).ToString();
            InputField.Background = whitefield;
            LoadingIcon.Visibility = Visibility.Hidden;
        }

        private void EnterCheck(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if(e.Key == Key.Enter)
            {
                changed = true;
                InputField.Background = greenfield;
                if (Validate() && window.AutoApply) ApplySetting();
            }
        }

        private void ValueChanged(object sender, RoutedEventArgs e)
        {
            changed = true;
            InputField.Background = greenfield;
            if (Validate() && window.AutoApply) ApplySetting();
        }

        public async void ApplySetting()
        {
            if (!changed) return;
            changed = false;
            try
            {
                var value = double.Parse(InputField.Text);
                InputField.Background = whitefield;
                bool success = await this.setting.SetValue(value);
                if (!success) CaptureSetting();
            }
            catch
            {
                CaptureSetting();
            }
        }
    }
}
