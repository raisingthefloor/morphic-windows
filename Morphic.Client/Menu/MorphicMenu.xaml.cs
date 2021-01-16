namespace Morphic.Client.Menu
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using Windows.Native.Input;
    using Bar.UI;
    using Morphic.Client.Config;

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
            // if ConfigurableFeatures.CloudSettingsTransferIsEnabled is false, then hide the settings which can transfer/restore settings
            if (ConfigurableFeatures.CloudSettingsTransferIsEnabled == false)
            {
                this.CopySettingsBetweenComputersMenuItem.Visibility = Visibility.Collapsed;
                this.RestoreSettingsFromBackupMenuItem.Visibility = Visibility.Collapsed;
                this.CloudSettingsSeparator.Visibility = Visibility.Collapsed;
            }

            this.ShowTrayIcon();
            base.OnInitialized(e);
        }

        protected override void OnOpened(RoutedEventArgs e)
        {
            // if autorun settings are configured by config.json, do not give the user the option to enable/disable
            if (ConfigurableFeatures.AutorunConfig != null)
            {
                this.AutorunAfterLoginItem.Visibility = Visibility.Collapsed;
            }

            // if morphicBarVisibilityAfterLogin settings are configured by config.json, do not give the user the option to enable/disable
            if (ConfigurableFeatures.MorphicBarVisibilityAfterLogin != null)
            {
                this.ShowMorphicBarAfterLoginItem.Visibility = Visibility.Collapsed;
            }

            this.ShowBar.Visibility = (!this.App.BarManager.BarVisible).ToVisibility();
            this.HideBar.Visibility = this.App.BarManager.BarVisible.ToVisibility();

            if (Features.Community.IsEnabled())
            {
                this.LoginItem.Visibility = (!this.App.CommunitySession.SignedIn).ToVisibility();
                this.LogoutItem.Visibility = this.App.CommunitySession.SignedIn.ToVisibility();
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
            this.App.BarManager.CloseBar();
            this.App.Shutdown();
        }

        private void StopKeyRepeatInit(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                menuItem.IsChecked = Keyboard.KeyRepeat();
            }
        }
        private void StopKeyRepeatToggle(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                menuItem.IsChecked = Keyboard.KeyRepeat(menuItem.IsChecked);
            }
        }



        #region TrayIcon

        private MorphicHybridTrayIcon? _trayIcon = null;

        private async void ShowTrayIcon()
        {
            // TODO: re-implement using solutions registry.
            // SystemSetting filterType = new SystemSetting("SystemSettings_Notifications_ShowIconsOnTaskbar",
            //     new LoggerFactory().CreateLogger<SystemSetting>());
            // var allNotificationIconsShown = (await filterType.GetValue() as bool? == true) ? TrayIcon.TrayIconLocationOption.NotificationTray : TrayIcon.TrayIconLocationOption.NextToNotificationTry;

            WindowMessageHook windowMessageHook = WindowMessageHook.GetGlobalMessageHook();
            MorphicHybridTrayIcon trayIcon = new MorphicHybridTrayIcon();
            trayIcon = new MorphicHybridTrayIcon();
            trayIcon.Click += this.OnTrayIconClicked;
            trayIcon.SecondaryClick += this.OnTrayIconRightClicked;
            trayIcon.Icon = Client.Properties.Resources.Icon;
            trayIcon.Text = "Morphic";
            //trayIcon.TrayIconLocation = allNotificationIconsShown;
            trayIcon.TrayIconLocation = MorphicHybridTrayIcon.TrayIconLocationOption.NextToNotificationTray;
            trayIcon.Visible = true;
            _trayIcon = trayIcon;

            this.App.Exit += (sender, args) =>
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            };
        }

        private void OnTrayIconRightClicked(object? sender, EventArgs e)
        {
                this.Show();
        }

        private void OnTrayIconClicked(object? sender, EventArgs e)
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

        private void Logout(object sender, RoutedEventArgs e)
        {
            App.Current.CommunitySession.SignOut();
        }

        private void Login(object sender, RoutedEventArgs e)
        {
            _ = App.Current.OpenSession();
        }
    }


}

