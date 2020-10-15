// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt
//
// The R&D leading to these results received funding from the:
// * Rehabilitation Services Administration, US Dept. of Education under 
//   grant H421A150006 (APCP)
// * National Institute on Disability, Independent Living, and 
//   Rehabilitation Research (NIDILRR)
// * Administration for Independent Living & Dept. of Education under grants 
//   H133E080022 (RERC-IT) and H133E130028/90RE5003-01-00 (UIITA-RERC)
// * European Union's Seventh Framework Programme (FP7/2007-2013) grant 
//   agreement nos. 289016 (Cloud4all) and 610510 (Prosperity4All)
// * William and Flora Hewlett Foundation
// * Ontario Ministry of Research and Innovation
// * Canadian Foundation for Innovation
// * Adobe Foundation
// * Consumer Electronics Association Foundation

using System;
using System.Windows;
using System.Threading.Tasks;
using System.Threading;
using System.Text.Json;
using System.Collections.Generic;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Morphic.Service;
using Morphic.Core;
using Morphic.Settings;
using Morphic.Settings.Ini;
using Morphic.Settings.Registry;
using Morphic.Settings.Spi;
using Morphic.Settings.SystemSettings;
using Morphic.Client.QuickStrip;
using System.IO;
using System.Reflection;
using CountlySDK;
using CountlySDK.Entities;
using System.Windows.Controls;
using System.Windows.Input;
using NHotkey.Wpf;
using AutoUpdaterDotNET;
using Morphic.Settings.Files;

namespace Morphic.Client
{
    using System.Diagnostics;
    using System.Windows.Controls.Primitives;
    using Bar;
    using Config;
    using CountlySDK.CountlyCommon;
    using Dialogs;
    using Dialogs.Travel;
    using Microsoft.Win32;

    public class AppMain
    {
        [STAThread]
        public static void Main()
        {
            // Writing our own Main function so we can use a mutex to enforce only one running instance of Morphic at a time
            using Mutex mutex = new Mutex(false, App.ApplicationId);
            if (!mutex.WaitOne(0, false))
            {
                TrayButton.SendActivate();
                return;
            }

            // Ensure the current directory is the same as the executable, so relative paths work.
            Directory.SetCurrentDirectory(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

            App.Main();
        }
    }

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>Current application instance.</summary>
        public new static App Current { get; private set; } = null!;

        public IServiceProvider ServiceProvider { get; private set; } = null!;
        public IConfiguration Configuration { get; private set; } = null!;
        public ILogger<App> Logger { get; private set; } = null!;

        public MorphicSession MorphicSession { get; private set; } = null!;
        public AppOptions AppOptions => AppOptions.Current;

        public DialogManager Dialogs { get; } = new DialogManager();
        public BarManager BarManager { get; } = new BarManager();

        public const string ApplicationId = "A6E8092B-51F4-4CAA-A874-A791152B5698";

        #region Configuration & Startup

        public App()
        {
            App.Current = this;
        }

        /// <summary>
        /// Create a Configuration from appsettings.json
        /// </summary>
        /// <returns></returns>
        private IConfiguration GetConfiguration()
        {
            ConfigurationBuilder builder = new ConfigurationBuilder();
            builder.SetBasePath(Directory.GetCurrentDirectory());
            builder.AddJsonFile("appsettings.json", optional: false);
            if (this.AppOptions.Launch.Debug)
            {
                builder.AddJsonFile("appsettings.Debug.json", optional: true);
                builder.AddJsonFile("appsettings.Local.json", optional: true);
            }
            builder.AddEnvironmentVariables();
            return builder.Build();
        }

        /// <summary>
        /// Configure the dependency injection system with services
        /// </summary>
        /// <param name="services"></param>
        private void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(this.ConfigureLogging);
            services.Configure<SessionOptions>(this.Configuration.GetSection("MorphicService"));
            services.Configure<UpdateOptions>(this.Configuration.GetSection("Update"));
            services.AddSingleton<IServiceCollection>(services);
            services.AddSingleton<IServiceProvider>(provider => provider);
            services.AddSingleton<SessionOptions>(serviceProvider => serviceProvider.GetRequiredService<IOptions<SessionOptions>>().Value);
            services.AddSingleton(new StorageOptions { RootPath = AppPaths.GetConfigDir("Data") });
            services.AddSingleton(new KeychainOptions { Path = AppPaths.GetConfigDir("keychain") });
            services.AddSingleton<UpdateOptions>(serviceProvider => serviceProvider.GetRequiredService<IOptions<UpdateOptions>>().Value);
            services.AddSingleton<IDataProtection, DataProtector>();
            services.AddSingleton<IUserSettings, WindowsUserSettings>();
            services.AddSingleton<IRegistry, WindowsRegistry>();
            services.AddSingleton<IIniFileFactory, IniFileFactory>();
            services.AddSingleton<ISystemSettingFactory, SystemSettingFactory>();
            services.AddSingleton<ISystemParametersInfo, SystemParametersInfo>();
            services.AddSingleton<IFileManager, FileManager>();
            services.AddSingleton<SettingsManager>();
            services.AddSingleton<Keychain>();
            services.AddSingleton<Storage>();
            services.AddSingleton<MorphicSession>();
            services.AddTransient<TravelWindow>();
            services.AddTransient<CreateAccountPanel>();
            services.AddTransient<CapturePanel>();
            services.AddTransient<TravelCompletedPanel>();
            services.AddTransient<QuickStripWindow>();
            services.AddTransient<LoginWindow>();
            services.AddTransient<LoginPanel>();
            services.AddTransient<CreateAccountPanel>();
            services.AddTransient<AboutWindow>();
            services.AddTransient<CopyStartPanel>();
            services.AddTransient<ApplyPanel>();
            services.AddTransient<RestoreWindow>();
            services.AddSingleton<Backups>();
            services.AddMorphicSettingsHandlers(this.ConfigureSettingsHandlers);
        }

        private void ConfigureCountly()
        {
            // TODO: Move metrics related things to own class.
            IConfigurationSection? section = this.Configuration.GetSection("Countly");
            CountlyConfig cc = new CountlyConfig
            {
                appKey = section["AppKey"],
                serverUrl = section["ServerUrl"],
                appVersion = BuildInfo.Current.InformationalVersion
            };

            Countly.Instance.Init(cc);
            Countly.Instance.SessionBegin();
            CountlyBase.IsLoggingEnabled = true;
        }

        private void RecordedException(Task task)
        {
            if (task.Exception is Exception e)
            {
                this.Logger.LogError("exception thrown while countly recording exception: {msg}", e.Message);
                throw e;
            }
            this.Logger.LogDebug("successfully recorded countly exception");
        }

        void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // TODO: Improve error logging/reporting.
            Exception ex = e.Exception;
            MessageBox.Show($"Morphic ran into a problem:\n\n{e.Exception.Message}\n\nFurther information:\n{e.Exception}", "Morphic", MessageBoxButton.OK, MessageBoxImage.Warning);
            Console.WriteLine(ex);

            try
            {
                this.Logger.LogError("handled uncaught exception: {msg}", ex.Message);
                this.Logger.LogError(ex.StackTrace);

                Dictionary<String, String> extraData = new Dictionary<string, string>();
                CountlyBase.RecordException(ex.Message, ex.StackTrace, extraData, true)
                    .ContinueWith(this.RecordedException, TaskScheduler.FromCurrentSynchronizationContext());
            }
            catch (Exception)
            {
                // ignore
            }

            // This prevents the exception from crashing the application
            e.Handled = true;
        }

        /// <summary>
        /// Configure the logging for the application
        /// </summary>
        /// <param name="logging"></param>
        private void ConfigureLogging(ILoggingBuilder logging)
        {
            logging.AddConfiguration(this.Configuration);
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddDebug();
        }

        private void ConfigureSettingsHandlers(SettingsHandlerBuilder settings)
        {
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            this.Dispatcher.UnhandledException += this.App_DispatcherUnhandledException;

            this.Configuration = this.GetConfiguration();
            ServiceCollection collection = new ServiceCollection();
            this.ConfigureServices(collection);
            this.ServiceProvider = collection.BuildServiceProvider();

            base.OnStartup(e);
            this.Logger = this.ServiceProvider.GetRequiredService<ILogger<App>>();
            this.MorphicSession = this.ServiceProvider.GetRequiredService<MorphicSession>();
            this.MorphicSession.UserChanged += this.Session_UserChanged;
            this.Logger.LogInformation("App Started");

            this.CreateMainMenu();
            this.RegisterGlobalHotKeys();
            this.ConfigureCountly();
            this.StartCheckingForUpdates();

            this.AddSettingsListener();

            Task task = this.OpenSession();
            task.ContinueWith(this.SessionOpened, TaskScheduler.FromCurrentSynchronizationContext());
        }

        /// <summary>
        /// Makes the application automatically start at login.
        /// </summary>
        private bool ConfigureAutoRun(bool? newValue = null)
        {
            bool enabled;
            using RegistryKey morphicKey =
                Registry.CurrentUser.CreateSubKey(@"Software\Raising the Floor\Morphic")!;
            using RegistryKey runKey =
                Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")!;

            if (newValue == null)
            {
                // Get the configured value
                object value = morphicKey.GetValue("AutoRun");
                if (value == null)
                {
                    // This might be the first time running, enable auto-run by default.
                    enabled = true;
                }
                else
                {
                    // Respect the system setting (it was probably removed on purpose).
                    enabled = runKey.GetValue("Morphic") != null;
                }
            }
            else
            {
                enabled = (bool)newValue;
            }

            morphicKey.SetValue("AutoRun", enabled ? "1" : "0", RegistryValueKind.String);
            if (enabled)
            {
                string processPath = Process.GetCurrentProcess().MainModule.FileName;
                // Only add it to the auto-run if running a release.
                if (!processPath.EndsWith("dotnet.exe"))
                {
                    runKey.SetValue("Morphic", processPath);
                }
            }
            else
            {
                runKey.DeleteValue("Morphic", false);
            }

            return enabled;
        }

        /// <summary>
        /// Actions to perform when this instance is the first since installation.
        /// </summary>
        private void OnFirstRun()
        {
            this.Logger.LogInformation("Performing first-run tasks");

            // Set the magnifier to lens mode at 200%
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\ScreenMagnifier", "Magnification", 200);
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\ScreenMagnifier", "MagnificationMode", 3);

            // Set the colour filter type - if it's not currently enabled.
            bool filterOn = this.MorphicSession.GetBool(SettingsManager.Keys.WindowsDisplayColorFilterEnabled) == true;
            if (!filterOn)
            {
                SystemSetting filterType = new SystemSetting("SystemSettings_Accessibility_ColorFiltering_FilterType",
                    new LoggerFactory().CreateLogger<SystemSetting>());
                filterType.SetValue(5);
            }

            // Set the high-contrast theme, if high-contrast is off.
            bool highcontrastOn = this.MorphicSession.GetBool(SettingsManager.Keys.WindowsDisplayContrastEnabled) == true;
            if (!highcontrastOn)
            {
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes",
                    "LastHighContrastTheme", @"%SystemRoot\resources\Ease of Access Themes\hcwhite.theme",
                    RegistryValueKind.ExpandString);

                // For windows 10 1809+
                Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Accessibility\HighContrast",
                    "High Contrast Scheme", "High Contrast White");
            }
        }

        private void Session_UserChanged(object? sender, EventArgs e)
        {
            if (this.logoutItem != null)
            {
                this.logoutItem.Visibility = this.MorphicSession.User == null
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
        }

        private void RegisterGlobalHotKeys()
        {
            HotkeyManager.Current.AddOrReplace("Login with Morphic", Key.M, ModifierKeys.Control | ModifierKeys.Shift, (sender, e) =>
            {
                this.Dialogs.OpenDialog<LoginWindow>();
            });
            HotkeyManager.Current.AddOrReplace("Show Morphic", Key.M, ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt, (sender, e) =>
            {
                this.ShowQuickStrip(true);
            });
        }

        private async Task OpenSession()
        {
            await this.CopyDefaultPreferences();
            await this.MorphicSession.SettingsManager.Populate(Path.Combine("Solutions", "windows.solutions.json"));
            await this.MorphicSession.SettingsManager.Populate(Path.Combine("Solutions", "jaws2020.solutions.json"));
            await this.MorphicSession.Open();
        }

        private async Task CopyDefaultPreferences()
        {
            if ((this.AppOptions.FirstRun && !this.AppOptions.FirstRunUpgrade)
                || !this.MorphicSession.Storage.Exists<Preferences>("__default__"))
            {
                this.Logger.LogInformation("Saving default preferences");
                Preferences prefs = new Preferences { Id = "__default__" };
                try
                {
                    await using FileStream? stream = File.OpenRead("DefaultPreferences.json");
                    JsonSerializerOptions options = new JsonSerializerOptions();
                    options.Converters.Add(new JsonElementInferredTypeConverter());
                    prefs.Default =
                        await JsonSerializer.DeserializeAsync<Dictionary<string, SolutionPreferences>>(stream, options);
                }
                catch (Exception e)
                {
                    this.Logger.LogError(e, "Failed to read default preferences");
                    return;
                }

                if (!await this.MorphicSession.Storage.Save(prefs))
                {
                    this.Logger.LogError("Failed to save default preferences");
                }
            }
        }

        /// <summary>
        /// Called when the session open task completes
        /// </summary>
        /// <param name="task"></param>
        private void SessionOpened(Task task)
        {
            if (task.Exception is Exception e)
            {
                throw e;
            }
            this.Logger.LogInformation("Session Open");

            this.LoadQuickStrip();

            if (this.AppOptions.AutoShow)
            {
                this.ShowQuickStrip();
            }

            if (this.AppOptions.FirstRun)
            {
                this.OnFirstRun();
            }
        }

        #endregion

        #region System Tray Icon

        /// <summary>
        /// Create an icon in the system tray
        /// </summary>
        private async void CreateNotifyIcon()
        {
            if (this.QuickStripWindow == null)
            {
                throw new InvalidOperationException("Attempted to create the tray button before the quickstrip was loaded");
            }

            bool allNotificationIconsShown = await this.GetShowIconsOnTaskbar();

            this.notifyIcon = new TrayButton(this.QuickStripWindow);
            this.notifyIcon.Click += this.OnNotifyIconClicked;
            this.notifyIcon.SecondaryClick += this.OnNotifyIconRightClicked;
            this.notifyIcon.DoubleClick += this.OnNotifyIconDoubleClicked;
            this.notifyIcon.Icon = Client.Properties.Resources.Icon;
            this.notifyIcon.Text = "Morphic";
            this.notifyIcon.UseNotificationIcon = allNotificationIconsShown;
            this.notifyIcon.Visible = true;
        }

        /// <summary>
        /// Determines if the tray icons are always visible on the task tray.
        /// </summary>
        /// <returns></returns>
        private async Task<bool> GetShowIconsOnTaskbar()
        {
            SystemSetting filterType = new SystemSetting("SystemSettings_Notifications_ShowIconsOnTaskbar",
                new LoggerFactory().CreateLogger<SystemSetting>());
            return await filterType.GetValue() as bool? == true;
        }

        /// <summary>
        /// Create the main menu that is displayed from the system tray icon
        /// </summary>
        private void CreateMainMenu()
        {
            this.mainMenu = (this.Resources["ContextMenu"] as ContextMenu)!;
            this.mainMenu.DataContext = this;
            foreach (object? item in this.mainMenu.Items)
            {
                if (item is MenuItem menuItem)
                {
                    if (menuItem.Name == "showQuickStripItem")
                    {
                        this.showQuickStripItem = menuItem;
                    }
                    else if (menuItem.Name == "hideQuickStripItem")
                    {
                        this.hideQuickStripItem = menuItem;
                    }
                    else if (menuItem.Name == "logoutItem")
                    {
                        this.logoutItem = menuItem;
                    }
                }
            }
        }

        /// <summary>
        /// The icon in the system tray
        /// </summary>
        private TrayButton? notifyIcon;

        /// <summary>
        /// The main menu shown from the system tray icon
        /// </summary>
        private ContextMenu mainMenu = new ContextMenu();

        /// <summary>
        /// The main menu item for showing the quick strip
        /// </summary>
        private MenuItem? showQuickStripItem;

        /// <summary>
        /// The main menu item for hiding the quick strip
        /// </summary>
        private MenuItem? hideQuickStripItem;

        /// <summary>
        /// The main menu item for logging out
        /// </summary>
        private MenuItem? logoutItem;

        /// <summary>
        /// Called when the system tray icon is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnNotifyIconClicked(object? sender, EventArgs e)
        {
            CountlyBase.RecordEvent("Tray Click");
            if (this.QuickStripWindow?.Visibility != Visibility.Visible || this.QuickStripWindow?.WindowState == WindowState.Minimized)
            {
                this.ShowQuickStrip();
            }
            else
            {
                this.HideQuickStrip();
            }
        }

        /// <summary>
        /// Called when the system tray icon is right-clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnNotifyIconRightClicked(object? sender, EventArgs e)
        {
            CountlyBase.RecordEvent("Tray double-click");
            this.ShowMenu();
        }

        public void ShowMenu(Control? control = null)
        {
            if (control == null)
            {
                this.mainMenu.Placement = PlacementMode.Mouse;
                this.mainMenu.PlacementTarget = null;
            }
            else
            {
                this.mainMenu.Placement = PlacementMode.Top;
                this.mainMenu.PlacementTarget = control;
            }

            this.mainMenu.IsOpen = true;
        }

        /// <summary>
        /// Called when the system tray icon is double-clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnNotifyIconDoubleClicked(object? sender, EventArgs e)
        {
            CountlyBase.RecordEvent("Tray Menu");
            this.ShowQuickStrip();
        }

        /// <summary>
        /// Event handler for when the user selects Show Quick Strip from the main menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ShowQuickStrip(object sender, RoutedEventArgs e)
        {
            CountlyBase.RecordEvent("Show MorphicBar");
            this.ShowQuickStrip();
        }

        /// <summary>
        /// Event handler for when the user selects Hide Quick Strip from the main menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HideQuickStrip(object sender, RoutedEventArgs e)
        {
            CountlyBase.RecordEvent("Hide MorphicBar");
            this.HideQuickStrip();
        }

        private void TravelWithSettings(object sender, RoutedEventArgs e)
        {
            CountlyBase.RecordEvent("Travel");
            this.Dialogs.OpenDialog<TravelWindow>();
        }

        private void RestoreSettings(object sender, RoutedEventArgs e)
        {
            CountlyBase.RecordEvent("Restore");
            this.Dialogs.OpenDialog<RestoreWindow>();
        }

        private void Logout(object sender, RoutedEventArgs e)
        {
            CountlyBase.RecordEvent("Logout");
            _ = this.MorphicSession.SignOut();
        }

        private void About(object sender, RoutedEventArgs e)
        {
            CountlyBase.RecordEvent("About");
            this.Dialogs.OpenDialog<AboutWindow>();
        }

        private void MenuLink(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item)
            {
                CountlyBase.RecordEvent("Menu:" + item.Header);
                Process.Start(new ProcessStartInfo(item.Tag as string)
                {
                    UseShellExecute = true
                });
            }
        }

        private void StopKeyRepeatInit(object sender, EventArgs e)
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


        /// <summary>
        /// Event handler for when the user selects Quit from the logo button's menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Quit(object sender, RoutedEventArgs e)
        {
            CountlyBase.RecordEvent("Quit");
            App.Current.Shutdown();
        }

        private void AutoRunInit(object? sender, EventArgs e)
        {
            if (sender is MenuItem item)
            {
                item.IsChecked = this.ConfigureAutoRun();
            }
        }

        private void AutoRunToggle(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item)
            {
                this.ConfigureAutoRun(item.IsChecked);
            }
        }

        #endregion

        #region Quick Strip Window

        /// <summary>
        ///  The Quick Strip Window
        /// </summary>
        public QuickStripWindow? QuickStripWindow { get; private set; }

        /// <summary>
        /// true to always show in the alt+tab list, even when hidden.
        /// </summary>
        public bool AlwaysAltTab { get; set; } = true;

        /// <summary>
        /// Toggle the Quick Strip window based on its current visibility
        /// </summary>
        public void ToggleQuickStrip()
        {
            if (this.QuickStripWindow != null)
            {
                this.HideQuickStrip();
            }
            else
            {
                this.ShowQuickStrip();
            }
        }

        /// <summary>
        /// Loads the quick strip.
        /// </summary>
        public void LoadQuickStrip()
        {
            if (this.QuickStripWindow == null)
            {
                this.QuickStripWindow = this.ServiceProvider.GetRequiredService<QuickStripWindow>();
                this.QuickStripWindow.Closed += this.QuickStripClosed;
                this.QuickStripWindow.SourceInitialized += (sender, args) => this.CreateNotifyIcon();
                // So the tray button works, the window needs to be shown (to create the window), but not displayed.
                this.QuickStripWindow.AllowsTransparency = true;
                this.QuickStripWindow.Opacity = 0;
                if (this.AlwaysAltTab)
                {
                    this.ShowQuickStrip();
                    this.QuickStripWindow.WindowState = WindowState.Minimized;
                }
                else
                {
                    this.QuickStripWindow.Show();
                    this.QuickStripWindow.Hide();
                }
            }
        }

        /// <summary>
        /// true if the quick-strip has been shown before.
        /// </summary>
        private bool quickStripShown;

        /// <summary>
        /// Ensure the Quick Strip window is shown
        /// </summary>
        public void ShowQuickStrip(bool skippingSave = false, bool keyboardFocus = false)
        {
            this.LoadQuickStrip();
            if (this.QuickStripWindow == null)
            {
                throw new ApplicationException("The quickstrip was not loaded");
            }

            this.QuickStripWindow.WindowState = WindowState.Normal;
            this.QuickStripWindow.Show();
            this.QuickStripWindow.Activate();

            if (!this.quickStripShown)
            {
                this.QuickStripWindow.FocusFirstItem(keyboardFocus);
                this.quickStripShown = true;
            }
            else
            {
                if (this.focusedElement != null)
                {
                    FocusManager.SetFocusedElement(this.QuickStripWindow, this.focusedElement);
                }
            }

            if (keyboardFocus)
            {
                this.QuickStripWindow.SetKeyboardFocus();
            }

            if (this.showQuickStripItem != null)
            {
                this.showQuickStripItem.Visibility = Visibility.Collapsed;
            }
            if (this.hideQuickStripItem != null)
            {
                this.hideQuickStripItem.Visibility = Visibility.Visible;
            }
            if (!skippingSave)
            {
                this.MorphicSession.SetPreference(QuickStrip.QuickStripWindow.PreferenceKeys.Visible, true);
            }
        }

        private IInputElement? focusedElement;

        /// <summary>
        /// Ensure the Quick Strip window is hidden
        /// </summary>
        public void HideQuickStrip()
        {
            QuickHelpWindow.Dismiss(true);
            if (this.QuickStripWindow != null)
            {
                if (this.AlwaysAltTab)
                {
                    this.QuickStripWindow.WindowState = WindowState.Minimized;
                }
                else
                {
                    this.QuickStripWindow.Hide();
                }

                // Re-focus the same control when the qs is re-shown.
                this.focusedElement = FocusManager.GetFocusedElement(this.QuickStripWindow);
            }

            if (this.showQuickStripItem != null)
            {
                this.showQuickStripItem.Visibility = Visibility.Visible;
            }
            if (this.hideQuickStripItem != null)
            {
                this.hideQuickStripItem.Visibility = Visibility.Collapsed;
            }

            this.MorphicSession.SetPreference(QuickStrip.QuickStripWindow.PreferenceKeys.Visible, false);
        }

        /// <summary>
        /// Called when the Quick Strip window closes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void QuickStripClosed(object? sender, EventArgs e)
        {
            this.QuickStripWindow = null;
        }

        #endregion


        #region Login Window

        private LoginWindow? loginWindow;

        public Task<bool> OpenLoginWindow()
        {
            TaskCompletionSource<bool> taskCompletionSource = new TaskCompletionSource<bool>();

            if (this.loginWindow == null)
            {
                this.loginWindow = this.ServiceProvider.GetRequiredService<LoginWindow>();
                this.loginWindow.Show();
                this.loginWindow.Closed += (sender, args) =>
                {
                    this.OnLoginWindowClosed(sender, args);
                    taskCompletionSource.SetResult(true);
                };
            }

            this.loginWindow.Activate();
            return taskCompletionSource.Task;
        }

        private void OnLoginWindowClosed(object? sender, EventArgs e)
        {
            this.loginWindow = null;
        }

        #endregion

        #region Updates

        void StartCheckingForUpdates()
        {
            UpdateOptions? options = this.ServiceProvider.GetRequiredService<UpdateOptions>();
            if (options.AppCastUrl != "")
            {
                AutoUpdater.Start(options.AppCastUrl);
            }
        }

        #endregion

        #region Shutdown

        protected override void OnExit(ExitEventArgs e)
        {
            Countly.Instance.SessionEnd();
            // Windows doesn't seem to clean up the system tray icon until the user
            // hovers over it after the application closes.  So, we need to make it
            // invisible on app exit ourselves.
            if (this.notifyIcon != null)
            {
                this.notifyIcon.Visible = false;
            }

            base.OnExit(e);
        }

        #endregion

        #region SystemEvents

        public event EventHandler? SystemSettingChanged;

        private bool addedSystemEvents;
        private DispatcherTimer? systemSettingTimer;

        /// <summary>
        /// Start listening to some changes to system settings.
        /// </summary>
        private void AddSettingsListener()
        {
            if (this.addedSystemEvents)
            {
                return;
            }

            this.addedSystemEvents = true;
            this.systemSettingTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };

            this.systemSettingTimer.Tick += (sender, args) =>
            {
                this.systemSettingTimer.Stop();
                this.SystemSettingChanged?.Invoke(this, EventArgs.Empty);
            };

            SystemEvents.DisplaySettingsChanged += this.SystemEventsOnDisplaySettingsChanged;
            SystemEvents.UserPreferenceChanged += this.SystemEventsOnDisplaySettingsChanged;

            this.Exit += (sender, args) =>
            {
                SystemEvents.DisplaySettingsChanged -= this.SystemEventsOnDisplaySettingsChanged;
                SystemEvents.UserPreferenceChanged -= this.SystemEventsOnDisplaySettingsChanged;
            };
        }

        private void SystemEventsOnDisplaySettingsChanged(object? sender, EventArgs e)
        {
            // Wait a bit, to see if any other events have been raised.
            this.systemSettingTimer?.Start();
        }

        #endregion
    }
}
