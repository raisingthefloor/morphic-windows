namespace Morphic.ManualTester
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using Settings.SettingsHandlers;

    /// <summary>
    /// Interaction logic for ManualControlBoolean.xaml
    /// </summary>
    public partial class ManualControlBoolean : UserControl, IManualControlEntry
    {
        private bool changed;
        private bool pending;
        private readonly Brush greenfield = new SolidColorBrush(Color.FromArgb(30, 0, 176, 0));
        public Setting setting;
        private readonly Brush bluefield = new SolidColorBrush(Color.FromArgb(30, 0, 0, 176));
        private readonly Brush cyanfield = new SolidColorBrush(Color.FromArgb(30, 0, 176, 176));
        private readonly Brush whitefield = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
        private readonly MainWindow window;

        public ManualControlBoolean(MainWindow window, Setting setting)
        {
            this.InitializeComponent();
            this.window = window;
            this.setting = setting;
            this.ControlName.Text = (setting.Title != string.Empty) ? setting.Title : setting.Name;
            if(setting.Description != string.Empty)
            {
                this.ControlName.ToolTip = setting.Description;
            }
            this.SetLoading();
            //this.CaptureSetting();
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
            if (setting.Default != "")
            {
                this.DataType.ToolTip = "DEFAULT: " + setting.Default;
            }
            bool? check = await this.setting.GetValue<bool>();
            this.ControlCheckBox.IsChecked = check;
            this.LoadingIcon.Visibility = Visibility.Hidden;
        }

        public void SetLoading()
        {
            this.LoadingIcon.Visibility = Visibility.Visible;
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
                this.ControlCheckBox.Background = this.bluefield;
                this.ControlCheckBox.IsChecked = this.setting.Default.ToLower().Contains("true");
                return;
            }
            this.pending = false;
            this.ControlCheckBox.Background = this.whitefield;
            if (val.Get(setting) as bool? != null)
            {
                this.ControlCheckBox.IsChecked = val.Get(setting) as bool?;
                if (val.GetType(setting) == Values.ValueType.Hardcoded)
                {
                    this.ControlCheckBox.Background = this.cyanfield;
                }
            }
        }

        public bool isChanged()
        {
            return this.changed;
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

        public void SaveCSV(MainWindow main)
        {
            string[] line = { setting.Title, setting.Name, "Boolean", "N/A", setting.Default };
            main.AddCSVLine(line);
        }
    }
}
