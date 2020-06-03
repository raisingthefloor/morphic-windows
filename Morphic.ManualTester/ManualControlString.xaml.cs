using Microsoft.Extensions.DependencyInjection;
using Morphic.Core;
using Morphic.Settings;
using System;
using System.Windows;
using System.Windows.Controls;

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
        public ManualControlString(SettingsManager manager, string solutionId, Setting setting)
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
            if (await manager.Capture(key) is string value)
            {
                InputField.Text = value;
            }
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            bool success = await manager.Apply(key, InputField.Text);
            if (!success) CheckValue();
        }
    }
}
