namespace Morphic.Client.Menu
{
    using Bar.UI;
    using CountlySDK;
    using Morphic.Client.Config;
    using System;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using Windows.Native.Input;

    public partial class MorphicMenu : ContextMenu
    {
        internal enum MenuOpenedSource
        {
            trayIcon,
            morphicBarIcon
        }
        private MenuOpenedSource? _menuOpenedSource;

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

        internal async Task ShowAsync(Control? control = null, MenuOpenedSource? menuOpenedSource = null)
        {
            _menuOpenedSource = menuOpenedSource;

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

            var segmentation = CreateMenuOpenedSourceSegmentation(_menuOpenedSource);
            await Countly.RecordEvent("showMenu", 1, segmentation);
        }

        private CountlySDK.Segmentation CreateMenuOpenedSourceSegmentation(MenuOpenedSource? menuOpenedSource)
        {
            var segmentation = new CountlySDK.Segmentation();
            if (_menuOpenedSource != null)
            {
                segmentation.Add("method", _menuOpenedSource.ToString() + "Menu");
            }
            return segmentation;
        }

        private async void ShowBarClick(object sender, RoutedEventArgs e)
        {
            this.App.BarManager.ShowBar();
            //
            var segmentation = CreateMenuOpenedSourceSegmentation(_menuOpenedSource);
            await Countly.RecordEvent("showMorphicBar", 1, segmentation);
        }

        private async void HideBarClick(object sender, RoutedEventArgs e)
        {
            this.App.BarManager.HideBar();
            //
            var segmentation = CreateMenuOpenedSourceSegmentation(_menuOpenedSource);
            await Countly.RecordEvent("hideMorphicBar", 1, segmentation);
        }

        private async void QuitClick(object sender, RoutedEventArgs e)
        {
            var segmentation = CreateMenuOpenedSourceSegmentation(_menuOpenedSource);
            await Countly.RecordEvent("quit", 1, segmentation);

            this.App.BarManager.CloseBar();
            this.App.Shutdown();
        }

        private async void AutorunAfterLoginClicked(object sender, RoutedEventArgs e)
        {
            switch (AutorunAfterLoginItem.IsChecked)
            {
                case true:
                    await Countly.RecordEvent("autorunAfterLoginEnabled");
                    break;
                case false:
                    await Countly.RecordEvent("autorunAfterLoginDisabled");
                    break;
            }
        }

        private async void ShowMorphicBarAfterLoginClicked(object sender, RoutedEventArgs e)
        {
            switch (ShowMorphicBarAfterLoginItem.IsChecked)
            {
                case true:
                    await Countly.RecordEvent("showMorphicBarAfterLoginEnabled");
                    break;
                case false:
                    await Countly.RecordEvent("showMorphicBarAfterLoginDisabled");
                    break;
            }
        }

        private void StopKeyRepeatInit(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                menuItem.IsChecked = Keyboard.KeyRepeat();
            }
        }
		//
        private async void StopKeyRepeatToggle(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                menuItem.IsChecked = Keyboard.KeyRepeat(menuItem.IsChecked);

                if (menuItem.IsChecked == true)
                {
                    await Countly.RecordEvent("stopKeyRepeatOn");
                }
                else
                {
                    await Countly.RecordEvent("stopKeyRepeatOff");
                }
            }
        }

        #region TrayIcon

        private MorphicHybridTrayIcon? _trayIcon = null;

        private void ShowTrayIcon()
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

        private async void OnTrayIconRightClicked(object? sender, EventArgs e)
        {
            await this.ShowAsync(null, MenuOpenedSource.trayIcon);
        }

        private async void OnTrayIconClicked(object? sender, EventArgs e)
        {
            if (this.App.BarManager.BarVisible)
            {
                this.App.BarManager.HideBar();
                //
                var segmentation = new CountlySDK.Segmentation();
                segmentation.Add("method", "trayIconClick");
                await Countly.RecordEvent("hideMorphicBar", 1, segmentation);
            }
            else
            {
                this.App.BarManager.ShowBar();
                //
                var segmentation = new CountlySDK.Segmentation();
                segmentation.Add("method", "trayIconClick");
                await Countly.RecordEvent("showMorphicBar", 1, segmentation);
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

        private async void LearnAboutMorphicClicked(object sender, RoutedEventArgs e)
        {
            var segmentation = CreateMenuOpenedSourceSegmentation(_menuOpenedSource);
            await Countly.RecordEvent("learnAboutMorphicClicked", 1, segmentation);
        }

        private async void QuickDemoMoviesClicked(object sender, RoutedEventArgs e)
        {
            var segmentation = CreateMenuOpenedSourceSegmentation(_menuOpenedSource);
            segmentation.Add("category", "main");
            await Countly.RecordEvent("quickDemoVideo", 1, segmentation);
        }

        private async void OtherHelpfulThingsClicked(object sender, RoutedEventArgs e)
        {
            var segmentation = CreateMenuOpenedSourceSegmentation(_menuOpenedSource);
            await Countly.RecordEvent("otherHelpfulThingsClicked", 1, segmentation);
        }

        private async void AboutMorphicClicked(object sender, RoutedEventArgs e)
        {
            var segmentation = CreateMenuOpenedSourceSegmentation(_menuOpenedSource);
            await Countly.RecordEvent("aboutMorphic", 1, segmentation);
        }
    }


}

