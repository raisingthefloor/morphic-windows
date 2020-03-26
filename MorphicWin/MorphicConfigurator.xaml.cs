using System;
using System.Windows;
using System.Threading.Tasks;
using MorphicCore;
using MorphicService;

namespace MorphicWin
{
    /// <summary>
    /// Interaction logic for MorphicConfigurator.xaml
    /// </summary>
    public partial class MorphicConfigurator : Window
    {
        public MorphicConfigurator(Session session)
        {
            this.session = session;
            InitializeComponent();
        }

        private readonly Session session;

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

        private void CreateTestUser(object? sender, RoutedEventArgs e)
        {
            var task = session.RegisterUser();
            createUserButton.IsEnabled = false;
            task.ContinueWith(task =>
            {
                if (task.Result)
                {
                    if (session.User != null)
                    {
                        Settings.Default.UserId = session.User.Id;
                        Settings.Default.Save();
                    }
                    Close();
                }
                else
                {
                    createUserButton.IsEnabled = true;
                }

            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void ClearTestUser(object? sender, RoutedEventArgs e)
        {
            session.Signout();
            Settings.Default.UserId = "";
            Settings.Default.Save();
            Close();
        }
    }
}
