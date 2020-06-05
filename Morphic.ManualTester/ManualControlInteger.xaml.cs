using Morphic.Core;
using Morphic.Settings;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Morphic.ManualTester
{
    /// <summary>
    /// Interaction logic for ManualControlInteger.xaml
    /// </summary>
    public partial class ManualControlInteger : UserControl
    {
        public SettingsManager manager;
        public string solutionId;
        public Setting setting;
        public Preferences.Key key;
        private MainWindow window;
        private Boolean changed = false;
        public ManualControlInteger(MainWindow window, SettingsManager manager, string solutionId, Setting setting)
        {
            InitializeComponent();
            this.window = window;
            this.manager = manager;
            this.solutionId = solutionId;
            this.setting = setting;
            key = new Preferences.Key(solutionId, setting.Name);
            ControlName.Text = setting.Name;
            CheckValue();
        }

        private async void CheckValue()
        {
            InputField.Text = "";
            if (await manager.Capture(key) is long value)
            {
                InputField.Text = value.ToString();
            }
        }

        private void EnterCheck(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                changed = true;
                if (window.AutoApply) ApplySetting();
            }
        }

        private void ValueChanged(object sender, RoutedEventArgs e)
        {
            changed = true;
            if (window.AutoApply) ApplySetting();
        }

        public async void ApplySetting()
        {
            if (!changed) return;
            changed = false;
            try
            {
                var value = long.Parse(InputField.Text);
                bool success = await manager.Apply(key, value);
                if (!success) CheckValue();
            }
            catch
            {
                CheckValue();
            }
        }
    }
}
