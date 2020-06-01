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
        public IServiceProvider ServiceProvider;
        public string solutionId;
        public Setting setting;
        public Preferences.Key key;
        public ManualControlString(IServiceProvider sp, string solutionId, Setting setting)
        {
            InitializeComponent();
            this.ServiceProvider = sp;
            this.solutionId = solutionId;
            this.setting = setting;
            key = new Preferences.Key(solutionId, setting.Name);
            ControlName.Text = setting.Name;
            CheckValue();
        }

        private async void CheckValue()
        {
            var manager = ServiceProvider.GetRequiredService<SettingsManager>();
            InputField.Text = "";
            if (await manager.Capture(key) is string value)
            {
                InputField.Text = value;
            }
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            var manager = ServiceProvider.GetRequiredService<SettingsManager>();
            bool success = await manager.Apply(key, InputField.Text);
            if (!success) CheckValue();
        }
    }
}
