namespace Morphic.ManualTester
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using Settings.SettingsHandlers;

    /// <summary>
    /// Interaction logic for ManualControlDouble.xaml
    /// </summary>
    public partial class ManualControlDouble : UserControl, IManualControlEntry
    {
        private readonly Setting setting;
        private bool changed;
        private bool pending;
        private readonly Brush greenfield = new SolidColorBrush(Color.FromArgb(30, 0, 176, 0));
        private readonly Brush redfield = new SolidColorBrush(Color.FromArgb(30, 255, 0, 0));
        private readonly Brush bluefield = new SolidColorBrush(Color.FromArgb(30, 0, 0, 176));
        private readonly Brush cyanfield = new SolidColorBrush(Color.FromArgb(30, 0, 176, 176));
        private readonly Brush whitefield = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
        private readonly MainWindow window;

        public ManualControlDouble(MainWindow window, Setting setting)
        {
            this.InitializeComponent();
            this.window = window;
            this.setting = setting;
            this.ControlName.Text = (setting.Title != string.Empty) ? setting.Title : setting.Name;
            if (setting.Description != string.Empty)
            {
                this.ControlName.ToolTip = setting.Description;
            }
            this.SetLoading();
            //this.CaptureSetting();
        }

        private bool Validate()
        {
            try
            {
                double value = double.Parse(this.InputField.Text);
                return true;
            }
            catch
            {
                this.InputField.Background = this.redfield;
                return false;
            }
        }

        public async void CaptureSetting()
        {
            this.LoadingIcon.Visibility = Visibility.Visible;
            this.InputField.Text = "";
            if (setting.Default != "")
            {
                this.DataType.ToolTip = "DEFAULT: " + setting.Default;
            }
            this.InputField.Text = (await this.setting.GetValue(double.NaN)).ToString();
            this.InputField.Background = this.whitefield;
            this.LoadingIcon.Visibility = Visibility.Hidden;
        }

        public void SetLoading()
        {
            this.LoadingIcon.Visibility = Visibility.Visible;
            this.InputField.Text = "";
            this.pending = true;
            if (setting.Default != "")
            {
                this.DataType.ToolTip = "DEFAULT: " + setting.Default;
            }
        }

        public void ReadCapture(Values val)
        {
            if (!this.pending)
            {
                return;
            }
            this.LoadingIcon.Visibility = Visibility.Hidden;
            if (!val.Contains(this.setting) || val.GetType(setting) == Values.ValueType.NotFound)
            {
                this.InputField.Background = this.bluefield;
                this.InputField.Text = this.setting.Default;
                return;
            }
            this.pending = false;
            this.InputField.Text = val.Get(setting)!.ToString();
            this.InputField.Background = this.whitefield;
            if (val.GetType(setting) == Values.ValueType.Hardcoded)
            {
                this.InputField.Background = this.cyanfield;
            }
        }

        public bool isChanged()
        {
            return this.changed;
        }

        private void EnterCheck(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                this.changed = true;
                this.InputField.Background = this.greenfield;
                if (this.Validate() && this.window.AutoApply)
                {
                    this.ApplySetting();
                }
            }
        }

        private void ValueChanged(object sender, RoutedEventArgs e)
        {
            this.changed = true;
            this.InputField.Background = this.greenfield;
            if (this.Validate() && this.window.AutoApply)
            {
                this.ApplySetting();
            }
        }

        public async void ApplySetting()
        {
            if (!this.changed)
            {
                return;
            }

            this.changed = false;
            try
            {
                double value = double.Parse(this.InputField.Text);
                this.InputField.Background = this.whitefield;
                var result = await this.setting.SetValueAsync(value);
                if (result.IsError)
                {
                    this.CaptureSetting();
                }
            }
            catch
            {
                this.CaptureSetting();
            }
        }

        public void SaveCSV(MainWindow main)
        {
            string[] line = { setting.Title, setting.Name, "Double", "", setting.Default };
            if (setting.Range != null)
            {
                line[3] = this.Range.Text;
            }
            main.AddCSVLine(line);
        }
    }
}
