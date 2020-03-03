using System;
using System.Windows;

namespace MorphicWin
{
    /// <summary>
    /// Interaction logic for QuickStrip.xaml
    /// </summary>
    public partial class QuickStrip : Window
    {
        public QuickStrip()
        {
            InitializeComponent();
            Deactivated += OnDeactivated;
        }

        private void OnDeactivated(object sender, EventArgs e)
        {
            Close();
        }

        private void OpenConfigurator(object sender, RoutedEventArgs e)
        {
            var configurator = new MorphicConfigurator();
            configurator.Show();
            configurator.Activate();
            Close();
        }
    }
}
