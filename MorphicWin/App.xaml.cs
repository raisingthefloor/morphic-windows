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
using System.Text.Json;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MorphicService;
using MorphicCore;
using MorphicSettings;
using System.IO;
using CountlySDK;
using CountlySDK.Entities;

namespace MorphicWin
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {

#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        public static App Shared { get; private set; }

        public IServiceProvider ServiceProvider { get; private set; }
        public IConfiguration Configuration { get; private set; }
        public Session Session { get; private set; }
        private ILogger<App> logger;
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.

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
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") is string env)
            {
                builder.AddJsonFile($"appsettings.{env}.json", optional: true);
            }
            builder.AddJsonFile($"appsettings.Local.json", optional: true);
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
            services.AddSingleton<IServiceCollection>(services);
            services.AddSingleton<IServiceProvider>(provider => provider);
            services.AddSingleton<SessionOptions>(serviceProvider => serviceProvider.GetRequiredService<IOptions<SessionOptions>>().Value);
            services.AddSingleton(new StorageOptions { RootPath = Path.Combine(ApplicationDataFolderPath, "Data") });
            services.AddSingleton(new KeychainOptions { Path = Path.Combine(ApplicationDataFolderPath, "keychain") });
            services.AddSingleton<IDataProtection, DataProtector>();
            services.AddSingleton<IUserSettings, UserSettings>();
            services.AddSingleton<MorphicSettings.Settings>();
            services.AddSingleton<Keychain>();
            services.AddSingleton<Storage>();
            services.AddSingleton<Session>();
            services.AddTransient<TravelWindow>();
            services.AddTransient<CreateAccountPanel>();
            services.AddTransient<CapturePanel>();
            services.AddTransient<TravelCompletedPanel>();
            services.AddTransient<QuickStrip>();
            services.AddMorphicSettingsHandlers(ConfigureSettingsHandlers);
        }

        private void ConfigureCountly()
        {
            var section = Configuration.GetSection("Countly");
            CountlyConfig cc = new CountlyConfig();
            cc.appKey = section["AppKey"];
            cc.serverUrl = section["ServerUrl"];
            // @TODO is there some type of compile time we could stick in here? Or something real?
            cc.appVersion = "1.2.3";
            Countly.Instance.Init(cc);
            Countly.Instance.SessionBegin();
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
            logger.LogInformation("App Started");
            logger.LogInformation("Creating Tray Icon");
            CreateMainMenu();
            CreateNotifyIcon();
            var task = OpenSession();
            task.ContinueWith(SessionOpened, TaskScheduler.FromCurrentSynchronizationContext());
            ConfigureCountly();
        }

        private async Task OpenSession()
        {
            await CopyDefaultPreferences();
            await Session.Settings.Populate("Solutions.json");
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
            if (Session.GetBool(MorphicWin.QuickStrip.PreferenceKeys.Visible) ?? true)
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
            notifyIcon.Icon = MorphicWin.Properties.Resources.Icon;
            notifyIcon.Text = "Morphic";
            notifyIcon.Visible = true;
            notifyIcon.ContextMenuStrip = mainMenu;
        }

        /// <summary>
        /// Create the main menu that is displayed from the system tray icon
        /// </summary>
        private void CreateMainMenu()
        {
            mainMenu.AutoClose = true;
            System.Windows.Forms.ToolStripItem item;
            showQuickStripItem = mainMenu.Items.Add("Show Quick Strip");
            showQuickStripItem.Click += (sender, e) => { ShowQuickStrip(); };
            hideQuickStripItem = mainMenu.Items.Add("Hide Quick Strip");
            hideQuickStripItem.Click += (sender, e) => { HideQuickStrip(); };
            hideQuickStripItem.Visible = false;
            item = mainMenu.Items.Add("Customize Quick Strip...");
            item.Enabled = false;
            mainMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            item = mainMenu.Items.Add("Take My Settings with Me...");
            item.Click += (object? sender, EventArgs e) => { OpenTravelWindow(); };
            item = mainMenu.Items.Add("Apply My Settings...");
            item.Enabled = false;
            mainMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            item = mainMenu.Items.Add("Quit Morphic");
            item.Click += (sender, e) => { Shutdown(); };
        }

        /// <summary>
        /// The icon in the system tray
        /// </summary>
        private System.Windows.Forms.NotifyIcon? notifyIcon = null;

        /// <summary>
        /// The main menu shown from the system tray icon
        /// </summary>
        private System.Windows.Forms.ContextMenuStrip mainMenu = new System.Windows.Forms.ContextMenuStrip();

        /// <summary>
        /// The main menu item for showing the quick strip
        /// </summary>
        private System.Windows.Forms.ToolStripItem? showQuickStripItem;

        /// <summary>
        /// The main menu item for hiding the quick strip
        /// </summary>
        private System.Windows.Forms.ToolStripItem? hideQuickStripItem;

        /// <summary>
        /// Called when the system tray icon is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnNotifyIconClicked(object? sender, EventArgs e)
        {
            if (e is System.Windows.Forms.MouseEventArgs mouseEvent)
            {
                if (mouseEvent.Button == System.Windows.Forms.MouseButtons.Left)
                {
                    // Ideally we'd show the mainMenu here, but I can't figure out how to
                    // get the position of the click or even just the notifyIcon.  The
                    // MouseEventArgs always say location is 0,0.  For now, we'll just
                    // show the quick strip as the click action.  Right click shows the
                    // menu automatically
                    ShowQuickStrip();
                }
            }
        }

        #endregion

        #region Quick Strip Window

        /// <summary>
        ///  The Quick Strip Window, if visible
        /// </summary>
        private Window? QuickStrip = null;

        /// <summary>
        /// Toggle the Quick Strip window based on its current visibility
        /// </summary>
        public void ToggleQuickStrip()
        {
            if (QuickStrip != null)
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
            if (QuickStrip == null)
            {
                QuickStrip = ServiceProvider.GetRequiredService<QuickStrip>();
                QuickStrip.Closed += QuickStripClosed;
                QuickStrip.Show();
            }
            QuickStrip.Activate();
            if (showQuickStripItem != null)
            {
                showQuickStripItem.Visible = false;
            }
            if (hideQuickStripItem != null)
            {
                hideQuickStripItem.Visible = true;
            }
            if (!skippingSave)
            {
                Session.SetPreference(MorphicWin.QuickStrip.PreferenceKeys.Visible, true);
            }
        }

        /// <summary>
        /// Ensure the Quick Strip window is hidden
        /// </summary>
        public void HideQuickStrip()
        {
            if (QuickStrip != null)
            {
                QuickStrip.Close();
            }
            if (showQuickStripItem != null)
            {
                showQuickStripItem.Visible = true;
            }
            if (hideQuickStripItem != null)
            {
                hideQuickStripItem.Visible = false;
            }
            Session.SetPreference(MorphicWin.QuickStrip.PreferenceKeys.Visible, false);
        }

        /// <summary>
        /// Called when the Quick Strip window closes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void QuickStripClosed(object? sender, EventArgs e)
        {
            QuickStrip = null;
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
