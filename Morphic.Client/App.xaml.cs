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
using Morphic.Client.Travel;
using Morphic.Client.Login;
using Morphic.Client.QuickStrip;
using System.IO;
using System.Reflection;
using CountlySDK;
using CountlySDK.Entities;
using System.Windows.Controls;
using System.Windows.Input;
using NHotkey.Wpf;
using Morphic.Client.About;
using AutoUpdaterDotNET;
using System.Runtime.InteropServices;
using Morphic.Settings.Files;
using Morphic.Settings.Process;

namespace Morphic.Client
{

    public class AppMain
    {
        [STAThread]
        public static void Main()
        {
            // Writing our own Main function so we can use a mutex to enforce only one running instance of Morphic at a time
            using (Mutex mutex = new Mutex(false, App.ApplicationId))
            {
                if (!mutex.WaitOne(0, false))
                {
                    return;
                }
                App.Main();
            }
        }
    }

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static App Shared { get; private set; } = null!;
        public IServiceProvider ServiceProvider { get; private set; } = null!;
        public IConfiguration Configuration { get; private set; } = null!;
        public Session Session { get; private set; } = null!;
        private ILogger<App> logger = null!;

        public const string ApplicationId = "A6E8092B-51F4-4CAA-A874-A791152B5698";

        #region Configuration & Startup

        private readonly string ApplicationDataFolderPath = Path.Combine(new string[] { Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MorphicLite" });

        /// <summary>
        /// Create a Configuration from appsettings.json
        /// </summary>
        /// <returns></returns>
        private IConfiguration GetConfiguration()
        {
            var builder = new ConfigurationBuilder();
            builder.SetBasePath(Directory.GetCurrentDirectory());
            builder.AddJsonFile("appsettings.json", optional: false);
            if (Environment.GetEnvironmentVariable("MORPHIC_DEBUG") is string debug)
            {
                if (debug == "True")
                {
                    builder.AddJsonFile($"appsettings.Debug.json", optional: true);
                    builder.AddJsonFile($"appsettings.Local.json", optional: true);
                }
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
            services.AddLogging(ConfigureLogging);
            services.Configure<SessionOptions>(Configuration.GetSection("MorphicService"));
            services.Configure<UpdateOptions>(Configuration.GetSection("Update"));
            services.AddSingleton<IServiceCollection>(services);
            services.AddSingleton<IServiceProvider>(provider => provider);
            services.AddSingleton<SessionOptions>(serviceProvider => serviceProvider.GetRequiredService<IOptions<SessionOptions>>().Value);
            services.AddSingleton(new StorageOptions { RootPath = Path.Combine(ApplicationDataFolderPath, "Data") });
            services.AddSingleton(new KeychainOptions { Path = Path.Combine(ApplicationDataFolderPath, "keychain") });
            services.AddSingleton<UpdateOptions>(serviceProvider => serviceProvider.GetRequiredService<IOptions<UpdateOptions>>().Value);
            services.AddSingleton<IDataProtection, DataProtector>();
            services.AddSingleton<IUserSettings, WindowsUserSettings>();
            services.AddSingleton<IRegistry, WindowsRegistry>();
            services.AddSingleton<IIniFileFactory, IniFileFactory>();
            services.AddSingleton<ISystemSettingFactory, SystemSettingFactory>();
            services.AddSingleton<ISystemParametersInfo, SystemParametersInfo>();
            services.AddSingleton<IFileManager, FileManager>();
            services.AddSingleton<IProcessManager, ProcessManager>();
            services.AddSingleton<SettingsManager>();
            services.AddSingleton<Keychain>();
            services.AddSingleton<Storage>();
            services.AddSingleton<Session>();
            services.AddSingleton<BuildInfo>(BuildInfo.FromJsonFile("build-info.json"));
            services.AddTransient<TravelWindow>();
            services.AddTransient<CreateAccountPanel>();
            services.AddTransient<CapturePanel>();
            services.AddTransient<TravelCompletedPanel>();
            services.AddTransient<QuickStripWindow>();
            services.AddTransient<LoginWindow>();
            services.AddTransient<AboutWindow>();
            services.AddMorphicSettingsHandlers(ConfigureSettingsHandlers);
        }

        private void ConfigureCountly()
        {
            var section = Configuration.GetSection("Countly");
            CountlyConfig cc = new CountlyConfig();
            cc.appKey = section["AppKey"];
            cc.serverUrl = section["ServerUrl"];
            var buildInfo = ServiceProvider.GetRequiredService<BuildInfo>();

            cc.appVersion = buildInfo.InformationalVersion;

            Countly.Instance.Init(cc);
            Countly.Instance.SessionBegin();
            Countly.IsLoggingEnabled = true;
        }
        
        private void RecordedException(Task task)
        {
            if (task.Exception is Exception e)
            {
                logger.LogError("exception thrown while countly recording exception: {msg}", e.Message);
                throw e;
            }
            logger.LogDebug("successfully recorded countly exception");
        }

        void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Exception ex = e.Exception;
            logger.LogError("handled uncaught exception: {msg}", ex.Message);
            logger.LogError(ex.StackTrace);

            Dictionary<String, String> extraData = new Dictionary<string, string>();
            Countly.RecordException(ex.Message, ex.StackTrace, extraData, true)
                .ContinueWith(RecordedException, TaskScheduler.FromCurrentSynchronizationContext());

            MessageBox.Show("An unhandled exception just occurred: " + e.Exception.Message, "Exception Sample", MessageBoxButton.OK, MessageBoxImage.Warning);
            // This prevents the exception from crashing the application
            e.Handled = true;
        }

        /// <summary>
        /// Configure the logging for the application
        /// </summary>
        /// <param name="logging"></param>
        private void ConfigureLogging(ILoggingBuilder logging)
        {
            logging.AddConfiguration(Configuration);
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddDebug();
        }

        private void ConfigureSettingsHandlers(SettingsHandlerBuilder settings)
        {
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            Shared = this;
            Configuration = GetConfiguration();
            var collection = new ServiceCollection();
            ConfigureServices(collection);
            ServiceProvider = collection.BuildServiceProvider();
            base.OnStartup(e);
            logger = ServiceProvider.GetRequiredService<ILogger<App>>();
            Session = ServiceProvider.GetRequiredService<Session>();
            Session.UserChanged += Session_UserChanged;
            logger.LogInformation("App Started");
            logger.LogInformation("Creating Tray Icon");
            CreateMainMenu();
            CreateNotifyIcon();
            RegisterGlobalHotKeys();
            ConfigureCountly();
            StartCheckingForUpdates();
            var task = OpenSession();
            task.ContinueWith(SessionOpened, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void Session_UserChanged(object? sender, EventArgs e)
        {
            if (logoutItem != null)
            {
                if (Session.User == null)
                {
                    logoutItem.Visibility = Visibility.Collapsed;
                }
                else
                {
                    logoutItem.Visibility = Visibility.Visible;
                }
            }
        }

        private void RegisterGlobalHotKeys()
        {
            HotkeyManager.Current.AddOrReplace("Login with Morphic", Key.M, ModifierKeys.Control | ModifierKeys.Shift, (sender, e) =>
            {
                OpenLoginWindow();
                loginWindow?.Announce();
            });
        }

        private async Task OpenSession()
        {
            await CopyDefaultPreferences();
            await Session.SettingsManager.Populate(Path.Combine("Solutions", "windows.solutions.json"));
            await Session.SettingsManager.Populate(Path.Combine("Solutions", "jaws2020.solutions.json"));
            await Session.Open();
        }

        private async Task CopyDefaultPreferences()
        {
            if (!Session.Storage.Exists<Preferences>("__default__"))
            {
                logger.LogInformation("Saving default preferences");
                var prefs = new Preferences();
                prefs.Id = "__default__";
                try
                {
                    using (var stream = File.OpenRead("DefaultPreferences.json"))
                    {
                        var options = new JsonSerializerOptions();
                        options.Converters.Add(new JsonElementInferredTypeConverter());
                        prefs.Default = await JsonSerializer.DeserializeAsync<Dictionary<string, SolutionPreferences>>(stream, options);
                    }
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Failed to read default preferences");
                    return;
                }
                if (!await Session.Storage.Save(prefs))
                {
                    logger.LogError("Failed to save default preferences");
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
            logger.LogInformation("Session Open");
            if (Session.GetBool(QuickStrip.QuickStripWindow.PreferenceKeys.Visible) ?? true)
            {
                ShowQuickStrip(skippingSave: true);
            }
        }

        #endregion

        #region System Tray Icon

        /// <summary>
        /// Create an icon in the system tray
        /// </summary>
        private void CreateNotifyIcon()
        {
            notifyIcon = new System.Windows.Forms.NotifyIcon();
            notifyIcon.Click += OnNotifyIconClicked;
            notifyIcon.Icon = Client.Properties.Resources.Icon;
            notifyIcon.Text = "Morphic";
            notifyIcon.Visible = true;
        }

        /// <summary>
        /// Create the main menu that is displayed from the system tray icon
        /// </summary>
        private void CreateMainMenu()
        {
            mainMenu = (Resources["ContextMenu"] as ContextMenu)!;
            foreach (var item in mainMenu.Items)
            {
                if (item is MenuItem menuItem)
                {
                    if (menuItem.Name == "showQuickStripItem")
                    {
                        showQuickStripItem = menuItem;
                    }
                    else if (menuItem.Name == "hideQuickStripItem")
                    {
                        hideQuickStripItem = menuItem;
                    }
                    else if (menuItem.Name == "logoutItem")
                    {
                        logoutItem = menuItem;
                    }
                }
            }
        }

        /// <summary>
        /// The icon in the system tray
        /// </summary>
        private System.Windows.Forms.NotifyIcon? notifyIcon = null;

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
            Countly.RecordEvent("Tray Menu");
            mainMenu.IsOpen = true;
        }

        /// <summary>
        /// Event handler for when the user selects Show Quick Strip from the main menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ShowQuickStrip(object sender, RoutedEventArgs e)
        {
            Countly.RecordEvent("Show MorphicBar");
            ShowQuickStrip();
        }

        /// <summary>
        /// Event handler for when the user selects Hide Quick Strip from the main menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HideQuickStrip(object sender, RoutedEventArgs e)
        {
            Countly.RecordEvent("Hide MorphicBar");
            HideQuickStrip();
        }

        /// <summary>
        /// Event handler for when the user selects Customize Quick Strip from the main menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CustomizeQuickStrip(object sender, RoutedEventArgs e)
        {
            Countly.RecordEvent("Customize MorphicBar");
        }

        private void TravelWithSettings(object sender, RoutedEventArgs e)
        {
            Countly.RecordEvent("Travel");
            OpenTravelWindow();
        }

        private void ApplyMySettings(object sender, RoutedEventArgs e)
        {
            Countly.RecordEvent("Login");
            OpenLoginWindow();
        }

        private void Logout(object sender, RoutedEventArgs e)
        {
            Countly.RecordEvent("Logout");
            _ = Session.Signout();
        }

        private void About(object sender, RoutedEventArgs e)
        {
            Countly.RecordEvent("About");
            OpenAboutWindow();
        }

        /// <summary>
        /// Event handler for when the user selects Quit from the logo button's menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Quit(object sender, RoutedEventArgs e)
        {
            Countly.RecordEvent("Quit");
            App.Shared.Shutdown();
        }

        #endregion

        #region Quick Strip Window

        /// <summary>
        ///  The Quick Strip Window, if visible
        /// </summary>
        public QuickStripWindow? QuickStripWindow { get; private set; } = null;

        /// <summary>
        /// Toggle the Quick Strip window based on its current visibility
        /// </summary>
        public void ToggleQuickStrip()
        {
            if (QuickStripWindow != null)
            {
                HideQuickStrip();
            }
            else
            {
                ShowQuickStrip();
            }
        }

        /// <summary>
        /// Ensure the Quick Strip window is shown
        /// </summary>
        public void ShowQuickStrip(bool skippingSave = false)
        {
            if (QuickStripWindow == null)
            {
                QuickStripWindow = ServiceProvider.GetRequiredService<QuickStripWindow>();
                QuickStripWindow.Closed += QuickStripClosed;
                QuickStripWindow.Show();
            }
            QuickStripWindow.Activate();
            if (showQuickStripItem != null)
            {
                showQuickStripItem.Visibility = Visibility.Collapsed;
            }
            if (hideQuickStripItem != null)
            {
                hideQuickStripItem.Visibility = Visibility.Visible;
            }
            if (!skippingSave)
            {
                Session.SetPreference(QuickStrip.QuickStripWindow.PreferenceKeys.Visible, true);
            }
        }

        /// <summary>
        /// Ensure the Quick Strip window is hidden
        /// </summary>
        public void HideQuickStrip()
        {
            if (QuickStripWindow != null)
            {
                QuickStripWindow.Close();
            }
            if (showQuickStripItem != null)
            {
                showQuickStripItem.Visibility = Visibility.Visible;
            }
            if (hideQuickStripItem != null)
            {
                hideQuickStripItem.Visibility = Visibility.Collapsed;
            }
            Session.SetPreference(QuickStrip.QuickStripWindow.PreferenceKeys.Visible, false);
        }

        /// <summary>
        /// Called when the Quick Strip window closes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void QuickStripClosed(object? sender, EventArgs e)
        {
            QuickStripWindow = null;
        }

        #endregion

        #region Travel Window

        /// <summary>
        /// The Configurator window, if visible
        /// </summary>
        private TravelWindow? TravelWindow;

        /// <summary>
        /// Show the Morphic Configurator window
        /// </summary>
        internal void OpenTravelWindow()
        {
            if (TravelWindow == null)
            {
                TravelWindow = ServiceProvider.GetRequiredService<TravelWindow>();
                TravelWindow.Show();
                TravelWindow.Closed += OnTravelWindowClosed;
            }
            TravelWindow.Activate();
        }
        

        /// <summary>
        /// Called when the configurator window closes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnTravelWindowClosed(object? sender, EventArgs e)
        {
            TravelWindow = null;
        }

        #endregion
        
        #region About Window

        private AboutWindow? AboutWindow = null;
        
        /// <summary>
        /// Show the Morphic Configurator window
        /// </summary>
        internal void OpenAboutWindow()
        {
            if (AboutWindow == null)
            {
                AboutWindow = ServiceProvider.GetRequiredService<AboutWindow>();
                AboutWindow.Show();
                AboutWindow.Closed += OnAboutWindowClosed;
            }
            AboutWindow.Activate();
        }
        
        private void OnAboutWindowClosed(object? sender, EventArgs e)
        {
            AboutWindow = null;
        }
        
        #endregion

        #region Login Window

        private LoginWindow? loginWindow;

        public void OpenLoginWindow()
        {
            if (loginWindow == null)
            {
                loginWindow = ServiceProvider.GetRequiredService<LoginWindow>();
                loginWindow.Show();
                loginWindow.Closed += OnLoginWindowClosed;
            }
            loginWindow.Activate();
        }

        private void OnLoginWindowClosed(object? sender, EventArgs e)
        {
            loginWindow = null;
        }

        #endregion

        #region Updates

        void StartCheckingForUpdates()
        {
            var options = ServiceProvider.GetRequiredService<UpdateOptions>();
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
            if (notifyIcon is System.Windows.Forms.NotifyIcon icon)
            {
                icon.Visible = false;
            }
            base.OnExit(e);
        }

        #endregion
    }
}
