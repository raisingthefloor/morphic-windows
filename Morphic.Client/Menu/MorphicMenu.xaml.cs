﻿namespace Morphic.Client.Menu
{
    using Bar.UI;
    using CountlySDK;
    using Morphic.Client.Config;
    using Morphic.Client.Dialogs;
    using Morphic.Windows.Native.OsVersion;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using System.Windows.Forms;
    using Windows.Native.Input;

    using Control = System.Windows.Controls.Control;

    public partial class MorphicMenu : ContextMenu, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

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
                this.ChangeSetupMenuItem.Visibility = Visibility.Collapsed;
                this.SaveMySetupMenuItem.Visibility = Visibility.Collapsed;
                this.RestoreSettingsFromBackupMenuItem.Visibility = Visibility.Collapsed;
                this.CloudSettingsSeparator.Visibility = Visibility.Collapsed;
            }

            this.ShowTrayIcon();
            base.OnInitialized(e);
        }

        protected override void OnOpened(RoutedEventArgs e)
        {
            // if autorun settings are configured by config.json, do not give the user the option to enable/disable
            if (ConfigurableFeatures.AutorunConfig is not null)
            {
                this.AutorunAfterLoginItem.Visibility = Visibility.Collapsed;
            }

            // if morphicBarVisibilityAfterLogin settings are configured by config.json, do not give the user the option to enable/disable
            if (ConfigurableFeatures.MorphicBarVisibilityAfterLogin is not null)
            {
                this.ShowMorphicBarAfterLoginItem.Visibility = Visibility.Collapsed;
            }

            this.ShowBar.Visibility = (!this.App.BarManager.BarVisible).ToVisibility();
            this.HideBar.Visibility = this.App.BarManager.BarVisible.ToVisibility();

            this.LoginItem.Visibility = (!this.App.MorphicSession.SignedIn).ToVisibility();
            this.LogoutItem.Visibility = this.App.MorphicSession.SignedIn.ToVisibility();

            IsInstalled = InstallerHelper.IsInstalled("Read&Write");

            base.OnOpened(e);
        }

        internal async Task ShowAsync(Control? control = null, MenuOpenedSource? menuOpenedSource = null)
        {
            _menuOpenedSource = menuOpenedSource;

            if (control is null)
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
            await App.Current.Countly_RecordEventAsync("showMenu", 1, segmentation);
        }

        private CountlySDK.Segmentation CreateMenuOpenedSourceSegmentation(MenuOpenedSource? menuOpenedSource)
        {
            var segmentation = new CountlySDK.Segmentation();
            if (_menuOpenedSource is not null)
            {
                segmentation.Add("eventSource", _menuOpenedSource.ToString() + "Menu");
            }
            return segmentation;
        }

        private async void ShowBarClick(object sender, RoutedEventArgs e)
        {
            this.App.BarManager.ShowBar();
            //
            var segmentation = CreateMenuOpenedSourceSegmentation(_menuOpenedSource);
            await App.Current.Countly_RecordEventAsync("morphicBarShow", 1, segmentation);
        }

        private async void HideBarClick(object sender, RoutedEventArgs e)
        {
            this.App.BarManager.HideBar();
            //
            var segmentation = CreateMenuOpenedSourceSegmentation(_menuOpenedSource);
            await App.Current.Countly_RecordEventAsync("morphicBarHide", 1, segmentation);
        }

        private async void QuitClick(object sender, RoutedEventArgs e)
        {
            var segmentation = CreateMenuOpenedSourceSegmentation(_menuOpenedSource);
            await App.Current.Countly_RecordEventAsync("quit", 1, segmentation);

            this.App.BarManager.CloseBar();
            this.App.Shutdown();
        }

        private async void AutorunAfterLoginClicked(object sender, RoutedEventArgs e)
        {
            switch (AutorunAfterLoginItem.IsChecked)
            {
                case true:
                    await App.Current.Countly_RecordEventAsync("autorunAfterLoginEnabled");
                    break;
                case false:
                    await App.Current.Countly_RecordEventAsync("autorunAfterLoginDisabled");
                    break;
            }
        }

        private async void ShowMorphicBarAfterLoginClicked(object sender, RoutedEventArgs e)
        {
            switch (ShowMorphicBarAfterLoginItem.IsChecked)
            {
                case true:
                    await App.Current.Countly_RecordEventAsync("showMorphicBarAfterLoginEnabled");
                    break;
                case false:
                    await App.Current.Countly_RecordEventAsync("showMorphicBarAfterLoginDisabled");
                    break;
            }
        }

        private async void WindowsSettingsAllAccessibilityOptionsClicked(object sender, RoutedEventArgs e)
        {
            string settingsUrlAsPath = null!; // required to quiet the "not initialized error"
            var windowsVersion = OsVersion.GetWindowsVersion();

            if (windowsVersion is not null)
            {
                switch (windowsVersion)
                {
                    case WindowsVersion.Win10_v1809:
                    case WindowsVersion.Win10_v1903:
                    case WindowsVersion.Win10_v1909:
                    case WindowsVersion.Win10_v2004:
                    case WindowsVersion.Win10_v20H2:
                    case WindowsVersion.Win10_v21H1:
                    case WindowsVersion.Win10_v21H2:
                        // Windows 10 1809, 1903, 1909, 2004, 20H2, 21H1, 21H2
                        // NOTE: we should re-evaluate this path in all versions of Windows (to verify that it shouldn't be simply "ms-settings:easeofaccess" instead)
                        settingsUrlAsPath = "ms-settings:easeofaccess-display";
                        break;
                    case WindowsVersion.Win10_vFuture:
                        // OBSERVATION: this may be the wrong path for future verisons of Windows (especially since Win10 and Win11 _may_ treat this differently post-21H1); re-evaluate this logic
                        settingsUrlAsPath = "ms-settings:easeofaccess-display";
                        break;
                    case WindowsVersion.Win11_v21H2:
                    case WindowsVersion.Win11_vFuture:
                        // Windows 11 21H2 (and assumed for the future)
                        settingsUrlAsPath = "ms-settings:easeofaccess";
                        break;
                    default:
                        // not supported
                        Debug.Assert(false, "This build of Windows is not supported");
                        return;
                }
            }
            else
            {
                // not supported
                Debug.Assert(false, "This build of Windows is not supported");
                return;
            }

            MorphicMenuItem.OpenMenuItemPath(settingsUrlAsPath);
            await MorphicMenuItem.RecordMenuItemTelemetryAsync(settingsUrlAsPath, ((MorphicMenuItem)sender).ParentMenuType, ((MorphicMenuItem)sender).TelemetryType, ((MorphicMenuItem)sender).TelemetryCategory);
        }

        private async void WindowsSettingsPointerSizeClicked(object sender, RoutedEventArgs e)
        {
            string settingsUrlAsPath = null!; // required to quiet the "not initialized error"
            var windowsVersion = OsVersion.GetWindowsVersion();

            if (windowsVersion is not null)
            {
                switch (windowsVersion)
                {
                    case WindowsVersion.Win10_v1809:
                    case WindowsVersion.Win10_v1903:
                    case WindowsVersion.Win10_v1909:
                        // Windows 10 1809, 1903, 1909
                        settingsUrlAsPath = "ms-settings:easeofaccess-cursorandpointersize";
                        break;
                    case WindowsVersion.Win10_v2004:
                        // Windows 10 2004
                        settingsUrlAsPath = "ms-settings:easeofaccess-MousePointer";
                        break;
                    case WindowsVersion.Win10_v20H2:
                        // Windows 10 20H2
                        // NOTE: Microsoft changed the URL for this link somwhere between 10.0.19042.986 and 10.0.19042.1052;
                        //       if we get any bug reports that this link doesn't work with v20H2, be sure to get the "winver" full version #...so we can adjust the revision # below (to something between 986 and 1051) as appropriate
                        uint? updateBuildRevision;
                        var getUpdateBuildRevisionResult = Morphic.Windows.Native.OsVersion.OsVersion.GetUpdateBuildRevision();
                        if (getUpdateBuildRevisionResult.IsSuccess == true)
                        {
                            updateBuildRevision = getUpdateBuildRevisionResult.Value!;
                        }
                        else
                        {
                            // NOTE: if we could not get the update build revision, we fail gracefully by assuming that the user's computer is updated to the OS version's most recent updates
                            updateBuildRevision = null;
                        }

                        if (updateBuildRevision.HasValue == true && updateBuildRevision.Value < 1052)
                        {
                            // NOTE: this link was verified in Windows 10 19042.985
                            settingsUrlAsPath = "ms-settings:easeofaccess-MousePointer";
                        }
                        else
                        {
                            // NOTE: this link was verified in Windows 10 19042.1052
                            settingsUrlAsPath = "ms-settings:easeofaccess-mousepointer";
                        }
                        break;
                    case WindowsVersion.Win10_v21H1:
                    case WindowsVersion.Win10_v21H2:
                    case WindowsVersion.Win10_vFuture:
                        // Windows 10 21H1, Windows 10 21H2 (and assumed for the future)
                        settingsUrlAsPath = "ms-settings:easeofaccess-mousepointer";
                        break;
                    case WindowsVersion.Win11_v21H2:
                    case WindowsVersion.Win11_vFuture:
                        // Windows 11 21H2 (and assumed for the future)
                        settingsUrlAsPath = "ms-settings:easeofaccess-mousepointer";
                        break;
                    default:
                        // not supported
                        Debug.Assert(false, "This build of Windows is not supported");
                        return;
                }
            }
            else
            {
                // not supported
                Debug.Assert(false, "This build of Windows is not supported");
                return;
            }

            MorphicMenuItem.OpenMenuItemPath(settingsUrlAsPath);
            await MorphicMenuItem.RecordMenuItemTelemetryAsync(settingsUrlAsPath, ((MorphicMenuItem)sender).ParentMenuType, ((MorphicMenuItem)sender).TelemetryType, ((MorphicMenuItem)sender).TelemetryCategory);
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
                    await App.Current.Countly_RecordEventAsync("stopKeyRepeatOn");
                }
                else
                {
                    await App.Current.Countly_RecordEventAsync("stopKeyRepeatOff");
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
            await this.Dispatcher.InvokeAsync(async () =>
            {
                await this.ShowAsync(null, MenuOpenedSource.trayIcon);
            });
        }

        private async void OnTrayIconClicked(object? sender, EventArgs e)
        {
            await this.Dispatcher.InvokeAsync(async () =>
            {
                if (this.App.BarManager.BarVisible)
                {
                    this.App.BarManager.HideBar();
                    //
                    var segmentation = new CountlySDK.Segmentation();
                    segmentation.Add("eventSource", "trayIconClick");
                    await App.Current.Countly_RecordEventAsync("morphicBarHide", 1, segmentation);
                }
                else
                {
                    this.App.BarManager.ShowBar();
                    //
                    var segmentation = new CountlySDK.Segmentation();
                    segmentation.Add("eventSource", "trayIconClick");
                    await App.Current.Countly_RecordEventAsync("morphicBarShow", 1, segmentation);
                }
            });
        }

        #endregion

        private async void Logout(object sender, RoutedEventArgs e)
        {
            AppOptions.Current.LastCommunity = null;
            AppOptions.Current.LastMorphicbarId = null;
            await App.Current.MorphicSession.SignOut();
        }

        private async void Login(object sender, RoutedEventArgs e)
        {
            // NOTE: if we want the login menu item to apply cloud-saved preferences after login, we should set this flag to true
            var applyPreferencesAfterLogin = ConfigurableFeatures.CloudSettingsTransferIsEnabled;
            var args = new Dictionary<string, object?>() { { "applyPreferencesAfterLogin", applyPreferencesAfterLogin } };
            await App.Current.Dialogs.OpenDialogAsync<LoginWindow>(args);
        }

        private async void CustomizeMorphicbarClicked(object sender, RoutedEventArgs e)
        {
            var segmentation = CreateMenuOpenedSourceSegmentation(_menuOpenedSource);
            await App.Current.Countly_RecordEventAsync("customizeMorphicbar", 1, segmentation);

            // NOTE: when we make "navigate to URL" a custom action (rather than something linked in the menu itself), then we should navigate to the appsettings value for the key"BarEditorWebAppUrlAsString"
        }

        private async void ContactUsClicked(object sender, RoutedEventArgs e)
        {
            var segmentation = CreateMenuOpenedSourceSegmentation(_menuOpenedSource);
            await App.Current.Countly_RecordEventAsync("contactUs", 1, segmentation);
        }

        private async void ExploreMorphicClicked(object sender, RoutedEventArgs e)
        {
            var segmentation = CreateMenuOpenedSourceSegmentation(_menuOpenedSource);
            await App.Current.Countly_RecordEventAsync("exploreMorphic", 1, segmentation);
        }

        private async void HowToCopySetupsClicked(object sender, RoutedEventArgs e)
        {
            var segmentation = CreateMenuOpenedSourceSegmentation(_menuOpenedSource);
            await App.Current.Countly_RecordEventAsync("howToCopySetups", 1, segmentation);
        }

        private async void QuickDemoVideosClicked(object sender, RoutedEventArgs e)
        {
            var segmentation = CreateMenuOpenedSourceSegmentation(_menuOpenedSource);
            segmentation.Add("category", "main");
            await App.Current.Countly_RecordEventAsync("quickDemoVideo", 1, segmentation);
        }

        private async void OtherHelpfulThingsClicked(object sender, RoutedEventArgs e)
        {
            var segmentation = CreateMenuOpenedSourceSegmentation(_menuOpenedSource);
            await App.Current.Countly_RecordEventAsync("otherHelpfulThings", 1, segmentation);
        }

        private async void AboutMorphicClicked(object sender, RoutedEventArgs e)
        {
            var segmentation = CreateMenuOpenedSourceSegmentation(_menuOpenedSource);
            await App.Current.Countly_RecordEventAsync("aboutMorphic", 1, segmentation);
        }

        private void SelectBasicMorphicBarClick(object sender, RoutedEventArgs e)
        {
            AppOptions.Current.LastCommunity = null;
            AppOptions.Current.LastMorphicbarId = null;
            App.Current.BarManager.LoadBasicMorphicBar();
        }

        private bool _isInstalled = false;

        public bool IsInstalled
        {
            get { return _isInstalled; }
            set
            {
                _isInstalled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ReadAndWriteInstallerDescription));
            }
        }

        public string ReadAndWriteInstallerDescription
        {
            get
            {
                return IsInstalled ? Strings.Resources.Menu_Uninstall_ReadAndWrite_Header : Strings.Resources.Menu_Install_ReadAndWrite_Header;
            }
        }

        public enum InstallerAction
        {
            Install,
            Uninstall
        }

        private async void ReadAndWriteClick(object sender, RoutedEventArgs e)
        {
            var installerAction = InstallerAction.Install;

            if (InstallerHelper.IsInstalled("Read&Write"))
                installerAction = InstallerAction.Uninstall;

            await RunInstallerAction(installerAction, "readandwrite");
        }

        private async void JawsClick(object sender, RoutedEventArgs e)
        {
            var installerAction = InstallerAction.Install;

            if (InstallerHelper.IsInstalled("Jaws"))
                installerAction = InstallerAction.Uninstall;

            await RunInstallerAction(installerAction, "jaws");
        }

        private async Task RunInstallerAction(InstallerAction installerAction, string appName)
        {
            var dialog = new FolderBrowserDialog();

            var result = dialog.ShowDialog();

            if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
            {
                var sb = new StringBuilder();

                switch (installerAction)
                {
                    case InstallerAction.Install:
                        sb.Append("install");
                        break;
                    case InstallerAction.Uninstall:
                        sb.Append("uninstall");
                        break;
                }

                sb.Append(" ");
                sb.Append(appName);
                sb.Append(" ");
                sb.Append($"\"{dialog.SelectedPath}\"");

                var arguments = sb.ToString();
                await Execute(@"InstallerServiceClient\Morphic.InstallerService.Client.exe", arguments);
            }
        }

        private static async Task Execute(string path, string arguments)
        {
            var currentPath = Assembly.GetExecutingAssembly().Location;
            var filePath = Path.Combine(Path.GetDirectoryName(currentPath) ?? string.Empty, path);

            var processInfo = new ProcessStartInfo
            {
                UseShellExecute = true,
                Verb = "runas",

                FileName = filePath,
                Arguments = arguments
            };

            var process = Process.Start(processInfo);

            process.WaitForExit();
        }
    }
}