namespace Morphic.Client.Menu
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using Bar.UI;

    public partial class MorphicMenu : ContextMenu
    {
        public App App => App.Current;

        public MorphicMenu()
        {
            this.DataContext = this;
            this.InitializeComponent();
        }

        protected override void OnInitialized(EventArgs e)
        {
            this.ShowTrayButton();
            base.OnInitialized(e);
        }

        protected override void OnOpened(RoutedEventArgs e)
        {
            if (this.App.BarManager.BarVisible)
            {
                this.ShowBar.Visibility = Visibility.Collapsed;
                this.HideBar.Visibility = Visibility.Visible;
            }
            else
            {
                this.ShowBar.Visibility = Visibility.Visible;
                this.HideBar.Visibility = Visibility.Collapsed;
            }

            base.OnOpened(e);
        }

        public void Show(Control? control = null)
        {
            if (control == null)
            {
                this.Placement = PlacementMode.Mouse;
                this.PlacementTarget = null;
            }
            else
            {
                this.Placement = PlacementMode.Top;
                this.PlacementTarget = control;
            }

            this.IsOpen = true;
        }

        private void ShowBarClick(object sender, RoutedEventArgs e)
        {
            this.App.BarManager.ShowBar();
        }

        private void HideBarClick(object sender, RoutedEventArgs e)
        {
            this.App.BarManager.HideBar();
        }

        private void QuitClick(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result =
                MessageBox.Show("Do you really want to stop using Morphic?", "Quit Morphic", MessageBoxButton.YesNo);

            if (result == MessageBoxResult.Yes)
            {
                this.App.BarManager.CloseBar();
                this.App.Shutdown();
            }
        }

        private void StopKeyRepeatInit(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                menuItem.IsChecked = Morphic.Windows.Native.Keyboard.KeyRepeat();
            }
        }
        private void StopKeyRepeatToggle(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                menuItem.IsChecked = Morphic.Windows.Native.Keyboard.KeyRepeat(menuItem.IsChecked);
            }
        }



        #region TrayIcon

        private TrayButton trayButton = null!;

        private async void ShowTrayButton()
        {
            // TODO: re-implement using solutions registry.
            // SystemSetting filterType = new SystemSetting("SystemSettings_Notifications_ShowIconsOnTaskbar",
            //     new LoggerFactory().CreateLogger<SystemSetting>());
            // bool allNotificationIconsShown = await filterType.GetValue() as bool? == true;

            Window window = new Window();
            WindowMessageHook windowMessageHook = new WindowMessageHook(window, true);
            this.trayButton = new TrayButton(windowMessageHook);
            this.trayButton.Click += this.OnTrayButtonClicked;
            this.trayButton.SecondaryClick += this.OnTrayButtonRightClicked;
            this.trayButton.DoubleClick += this.OnTrayButtonDoubleClicked;
            this.trayButton.Icon = Client.Properties.Resources.Icon;
            this.trayButton.Text = "Morphic";
            //this.trayButton.UseNotificationIcon = allNotificationIconsShown;
            this.trayButton.Visible = true;
            this.App.Exit += (sender, args) =>
            {
                this.trayButton.Visible = false;
                this.trayButton.Dispose();
                this.trayButton = null!;
            };
        }

        private void OnTrayButtonDoubleClicked(object? sender, EventArgs e)
        {
            this.App.BarManager.ShowBar();
        }

        private void OnTrayButtonRightClicked(object? sender, EventArgs e)
        {
            this.Show();
        }

        private void OnTrayButtonClicked(object? sender, EventArgs e)
        {
            if (this.App.BarManager.BarVisible)
            {
                this.App.BarManager.HideBar();
            }
            else
            {
                this.App.BarManager.ShowBar();
            }
        }

        #endregion

        private void StopKeyRepeatInit2(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }
    }


}

