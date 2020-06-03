using Morphic.Core;
using Morphic.Settings;
using System.Windows;
using System.Windows.Controls;

namespace Morphic.ManualTester
{
    /// <summary>
    /// Interaction logic for ManualControlDouble.xaml
    /// </summary>
    public partial class ManualControlDouble : UserControl
    {
        public SettingsManager manager;
        public string solutionId;
        public Setting setting;
        public Preferences.Key key;
        public ManualControlDouble(SettingsManager manager, string solutionId, Setting setting)
        {
            InitializeComponent();
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
            if (await manager.Capture(key) is double value)
            {
                InputField.Text = value.ToString();
            }
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var value = double.Parse(InputField.Text);
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
