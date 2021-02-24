namespace Morphic.ManualTester
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using Settings.SettingsHandlers;

    /// <summary>
    /// Interaction logic for ManualControlBoolean.xaml
    /// </summary>
    public partial class ManualControlBoolean : UserControl
    {
        public bool changed;
        private readonly Brush greenfield = new SolidColorBrush(Color.FromArgb(30, 0, 176, 0));
        public Setting setting;
        private readonly Brush whitefield = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
        private readonly MainWindow window;

        public ManualControlBoolean(MainWindow window, Setting setting)
        {
            this.InitializeComponent();
            this.window = window;
            this.setting = setting;
            this.ControlName.Text = setting.Name;
            this.CaptureSetting();
        }

        private void ValueChanged(object sender, RoutedEventArgs e)
        {
            this.changed = true;
            this.ControlCheckBox.Background = this.greenfield;
            if (this.window.AutoApply)
            {
                this.ApplySetting();
            }
        }

        public async void CaptureSetting()
        {
            this.LoadingIcon.Visibility = Visibility.Visible;
            this.ControlCheckBox.Background = this.whitefield;
            bool? check = await this.setting.GetValue<bool>();
            this.ControlCheckBox.IsChecked = check;
            this.LoadingIcon.Visibility = Visibility.Hidden;
        }

        public async void ApplySetting()
        {
            if (!this.changed)
            {
                return;
            }

            this.changed = false;
            this.ControlCheckBox.Background = this.whitefield;
            var result = await this.setting.SetValueAsync(this.ControlCheckBox.IsChecked);
            if (result.IsError)
            {
                this.CaptureSetting();
            }
        }
    }
}
