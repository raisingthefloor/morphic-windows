using Morphic.Core;
using Morphic.Settings;
using System.Windows;
using System.Windows.Controls;

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
        public ManualControlBoolean(MainWindow window, SettingsManager manager, string solutionId, Setting setting)
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

        private void ValueChanged(object sender, RoutedEventArgs e)
        {
            changed = true;
            if (window.AutoApply) ApplySetting();
        }

        private async void CheckValue()
        {
            bool? check = await manager.CaptureBool(key);
            ControlCheckBox.IsChecked = check;
        }

        public async void ApplySetting()
        {
            if (!changed) return;
            changed = false;
            bool success = await manager.Apply(key, ControlCheckBox.IsChecked);
            if (!success) CheckValue();
        }
    }
}
