using System;
using System.Windows;

namespace MorphicWin
{
    /// <summary>
    /// Interaction logic for MorphicConfigurator.xaml
    /// </summary>
    public partial class MorphicConfigurator : Window
    {
        public MorphicConfigurator()
        {
            InitializeComponent();
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (Settings.Default.UserId == "")
            {
                createUserButton.Visibility = Visibility.Visible;
                clearUserButton.Visibility = Visibility.Hidden;
            }
            else
            {
                createUserButton.Visibility = Visibility.Hidden;
                clearUserButton.Visibility = Visibility.Visible;
            }
        }

        private void CreateTestUser(object sender, RoutedEventArgs e)
        {
            Settings.Default.UserId = Guid.NewGuid().ToString();
            Settings.Default.Save();
            Close();
        }

        private void ClearTestUser(object sender, RoutedEventArgs e)
        {
            Settings.Default.UserId = "";
            Settings.Default.Save();
            Close();
        }
    }
}
