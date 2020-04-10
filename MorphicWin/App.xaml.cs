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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MorphicService;
using MorphicCore;
using System.IO;

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
            services.AddSingleton<SessionOptions>(serviceProvider => serviceProvider.GetRequiredService<IOptions<SessionOptions>>().Value);
            services.AddSingleton<MorphicSettings.Settings>();
            services.AddSingleton<IKeychain, Keychain>();
            services.AddSingleton<Session>();
            services.AddTransient<MorphicConfigurator>();
            services.AddTransient<QuickStrip>();
        }

        /// <summary>
        /// Configure the logging for the application
        /// </summary>
        /// <param name="logging"></param>
        private void ConfigureLogging(ILoggingBuilder logging)
        {
            logging.AddDebug();
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
            var task = Session.Open(Settings.Default.UserId);
            task.ContinueWith(SessionOpened, TaskScheduler.FromCurrentSynchronizationContext());
        }

        /// <summary>
        /// Called when the session open task completes
        /// </summary>
        /// <param name="task"></param>
        private void SessionOpened(Task task)
        {
            logger.LogInformation("Creating Tray Icon");
            CreateNotifyIcon();
            Settings.Default.PropertyChanged += OnSettingChanged;
            logger.LogInformation("Ready");
            if (Session.Preferences == null)
            {
                OpenConfigurator();
            }
        }

        /// <summary>
        /// Called when the UserId saved setting changes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnSettingChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "UserId" && Settings.Default.UserId != "")
            {
                ShowQuickStrip();
            }
        }

        #region System Tray Icon

        /// <summary>
        /// Create an icon in the system tray
        /// </summary>
        private void CreateNotifyIcon()
        {
            notifyIcon = new System.Windows.Forms.NotifyIcon();
            notifyIcon.Click += OnNotifyIconClicked;
            notifyIcon.Icon = MorphicWin.Properties.Resources.Icon;
            notifyIcon.Text = "Morphic Quick Strip";
            notifyIcon.Visible = true;
        }

        /// <summary>
        /// The icon in the system tray
        /// </summary>
        private System.Windows.Forms.NotifyIcon? notifyIcon = null;

        /// <summary>
        /// Called when the system tray icon is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnNotifyIconClicked(object? sender, EventArgs e)
        {
            ToggleQuickStrip();
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
        public void ShowQuickStrip()
        {
            if (QuickStrip == null)
            {
                QuickStrip = ServiceProvider.GetRequiredService<QuickStrip>();
                QuickStrip.Closed += QuickStripClosed;
                var screenSize = SystemParameters.WorkArea;
                QuickStrip.Top = screenSize.Height - QuickStrip.Height;
                QuickStrip.Left = screenSize.Width - QuickStrip.Width;
                QuickStrip.Show();
            }
            QuickStrip.Activate();
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

        #region Configurator Window

        /// <summary>
        /// The Configurator window, if visible
        /// </summary>
        private MorphicConfigurator? Configurator;

        /// <summary>
        /// Show the Morphic Configurator window
        /// </summary>
        internal void OpenConfigurator()
        {
            if (Configurator == null)
            {
                Configurator = ServiceProvider.GetRequiredService<MorphicConfigurator>();
                Configurator.Show();
                Configurator.Closed += OnConfiguratorClosed;
            }
            Configurator.Activate();
        }

        /// <summary>
        /// Called when the configurator window closes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnConfiguratorClosed(object? sender, EventArgs e)
        {
            Configurator = null;
        }

        #endregion
    }
}
