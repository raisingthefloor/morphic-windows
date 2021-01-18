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

using AutoUpdaterDotNET;
using CountlySDK;
using CountlySDK.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Morphic.Core;
using Morphic.Service;
using NHotkey.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Morphic.Client
{
    using Bar;
    using Bar.Data;
    using Config;
    using CountlySDK.CountlyCommon;
    using Dialogs;
    using Menu;
    using Microsoft.Win32;
    using Settings.SettingsHandlers;
    using Settings.SolutionsRegistry;
    using System.Text.Json;

    public class AppMain
    {
        private static Mutex _singleInstanceMutex;
        private static uint _singleInstanceMessageId;

        // NOTE: we created our own Main function so that we can use a mutex to enforce running only one instance of Morphic at a time
        [STAThread]
        public static void Main()
        {
            // create a message which we can send/receive to indicate that a secondary instance has been started; use the application ID as its backing unique string
            _singleInstanceMessageId = WinApi.RegisterWindowMessage(App.ApplicationId);

            // create a mutex which we will use to make sure only one copy of Morphic runs at a time
            bool mutexCreatedNew;
            _singleInstanceMutex = new Mutex(true, App.ApplicationId, out mutexCreatedNew);

            // if the mutex already existed (i.e. the application is already running), send a message to it now asking it to show its MorphicBar
            if (mutexCreatedNew == false)
            {
                // send the "single instance" message to the main instance; leave both parameters as zero
                MessageWatcherNativeWindow.PostMessage(_singleInstanceMessageId, IntPtr.Zero, IntPtr.Zero);

                // shut down our application (gracefully by returning from Main)
                return;
            }

            // Ensure the current directory is the same as the executable, so relative paths work.
            Directory.SetCurrentDirectory(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

            App.Main();
        }

        internal static void ReleaseSingleInstanceMutex()
        {
            _singleInstanceMutex.ReleaseMutex();
        }

        internal static uint SingleInstanceMessageId
        {
            get
            {
                return _singleInstanceMessageId;
            }
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
        public CommunitySession CommunitySession { get; private set; } = null!;

        public AppOptions AppOptions => AppOptions.Current;

        public DialogManager Dialogs { get; } = new DialogManager();
        public BarManager BarManager { get; } = new BarManager();

        public const string ApplicationId = "A6E8092B-51F4-4CAA-A874-A791152B5698";

        #region Configuration & Startup

        public App()
        {
            App.Current = this;
        }

        public class MorphicBarExtraItem
        {
            public string? type { get; set; }
            public string? label { get; set; }
            public string? tooltipHeader { get; set; }
            public string? tooltipText { get; set; }
            // for type: link
            public string? url { get; set; }
            // for type: action
            public string? function { get; set; }
        }

        public class ConfigFileContents
        {
            public class FeaturesConfigSection
            {
                public class EnabledFeature
                {
                    public bool? enabled { get; set; }
                    public string? scope { get; set; }
                }
                //
                public EnabledFeature? autorunAfterLogin { get; set; }
                public EnabledFeature? checkForUpdates { get; set; }
                public EnabledFeature? cloudSettingsTransfer { get; set; }
                public EnabledFeature? resetSettings { get; set; }
            }
            public class MorphicBarConfigSection
            {
                public string? visibilityAfterLogin { get; set; }
                public List<MorphicBarExtraItem>? extraItems { get; set; }
            }
            //
            public int? version { get; set; }
            public FeaturesConfigSection? features { get; set; }
            public MorphicBarConfigSection? morphicBar { get; set; }
        }

        private struct CommonConfigurationContents
        {
            public ConfigurableFeatures.AutorunConfigOption? AutorunConfig;
            public bool CheckForUpdatesIsEnabled;
            public bool CloudSettingsTransferIsEnabled;
            public bool ResetSettingsIsEnabled;
            public ConfigurableFeatures.MorphicBarVisibilityAfterLoginOption? MorphicBarVisibilityAfterLogin;
            public List<MorphicBarExtraItem> ExtraMorphicBarItems;
        }
        private async Task<CommonConfigurationContents> GetCommonConfigurationAsync()
        {
            // set up default configuration
            var result = new CommonConfigurationContents();
            //
            // autorun
            result.AutorunConfig = null;
            //
            // check for updates
            result.CheckForUpdatesIsEnabled = true;
            //
            // copy settings to/from cloud
            result.CloudSettingsTransferIsEnabled = true;
            //
            // reset settings (to standard)
            result.ResetSettingsIsEnabled = false;
            //
            // morphic bar (visibility and extra items)
            result.MorphicBarVisibilityAfterLogin = null;
            result.ExtraMorphicBarItems = new List<MorphicBarExtraItem>();

            // NOTE: we have intentionally chosen not to create the CommonConfigDir (e.g. "C:\ProgramData\Morphic") since Morphic does not currently create files in this folder.
            var morphicCommonConfigPath = AppPaths.GetCommonConfigDir("", false);
            if (Directory.Exists(morphicCommonConfigPath) == false)
            {
                // no config file; return defaults
                return result;
            }

            var morphicConfigFilePath = Path.Combine(morphicCommonConfigPath, "config.json");
            if (File.Exists(morphicConfigFilePath) == false)
            {
                // no config file; return defaults
                return result;
            }

            string json;
            try
            {
                json = await File.ReadAllTextAsync(morphicConfigFilePath);
            }
            catch (Exception ex)
            {
                // error reading config file; return defaults
                // NOTE: consider refusing to start up (for security reasons) if the configuration file cannot be read
                Logger?.LogError("Could not read configuration file: " + morphicConfigFilePath + "; error: " + ex.Message);
                return result;
            }

            ConfigFileContents deserializedJson;
            try
            {
                deserializedJson = JsonSerializer.Deserialize<ConfigFileContents>(json);
            }
            catch (Exception ex)
            {
                // NOTE: consider refusing to start up (for security reasons) if the configuration file cannot be read
                Logger?.LogError("Could not deserialize json configuration file: " + morphicConfigFilePath + "; error: " + ex.Message);
                return result;
            }

            if ((deserializedJson.version == null) || (deserializedJson.version.Value < 0) || (deserializedJson.version.Value > 0))
            {
                // sorry, we don't understand this version of the file
                // NOTE: consider refusing to start up (for security reasons) if the configuration file cannot be read
                Logger?.LogError("Unknown config file version: " + deserializedJson.version.ToString());
                return result;
            }

            // capture the autorun setting
            if (deserializedJson.features?.autorunAfterLogin?.enabled != null)
            {
                if (deserializedJson.features!.autorunAfterLogin!.enabled == false)
                {
                    result.AutorunConfig = ConfigurableFeatures.AutorunConfigOption.Disabled;
                } 
                else
                {
                    switch (deserializedJson.features!.autorunAfterLogin!.scope)
                    {
                        case "allLocalUsers":
                            result.AutorunConfig = ConfigurableFeatures.AutorunConfigOption.AllLocalUsers;
                            break;
                        case "currentUser":
                            result.AutorunConfig = ConfigurableFeatures.AutorunConfigOption.CurrentUser;
                            break;
                        case null:
                            // no scope present; use the default scope
                            break;
                        default:
                            // sorry, we don't understand this scope setting
                            // NOTE: consider refusing to start up (for security reasons) if the configuration file cannot be read
                            Logger?.LogError("Unknown autorunAfterLogin scope: " + deserializedJson.features!.autorunAfterLogin!.scope);
                            return result;
                    }
                }
            }

            // capture the check for updates "is enabled" setting
            if (deserializedJson.features?.checkForUpdates?.enabled != null)
            {
                result.CheckForUpdatesIsEnabled = deserializedJson.features.checkForUpdates.enabled.Value;
            }

            // capture the cloud settings transfer "is enabled" setting
            if (deserializedJson.features?.cloudSettingsTransfer?.enabled != null)
            {
                result.CloudSettingsTransferIsEnabled = deserializedJson.features.cloudSettingsTransfer.enabled.Value;
            }

            // capture the reset settings (to standard) "is enabled" setting
            if (deserializedJson.features?.resetSettings?.enabled != null)
            {
                result.ResetSettingsIsEnabled = deserializedJson.features.resetSettings.enabled.Value;
            }

            // capture the desired after-login (autorun) visibility of the MorphicBar
            switch (deserializedJson.morphicBar?.visibilityAfterLogin)
            {
                case "restore":
                    result.MorphicBarVisibilityAfterLogin = ConfigurableFeatures.MorphicBarVisibilityAfterLoginOption.Restore;
                    break;
                case "show":
                    result.MorphicBarVisibilityAfterLogin = ConfigurableFeatures.MorphicBarVisibilityAfterLoginOption.Show;
                    break;
                case "hide":
                    result.MorphicBarVisibilityAfterLogin = ConfigurableFeatures.MorphicBarVisibilityAfterLoginOption.Hide;
                    break;
                case null:
                    // no setting present; use the default setting
                    break;
                default:
                    // sorry, we don't understand this visibility setting
                    // NOTE: consider refusing to start up (for security reasons) if the configuration file cannot be read
                    Logger?.LogError("Unknown morphicBar.visibilityAfterLogin setting: " + deserializedJson.morphicBar?.visibilityAfterLogin);
                    return result;
            }


            // capture any extra items (up to 3)
            if (deserializedJson.morphicBar?.extraItems != null)
            {
                foreach (var extraItem in deserializedJson.morphicBar!.extraItems)
                {
                    // if we already captured 3 extra items, skip this one
                    if (result.ExtraMorphicBarItems.Count >= 3)
                    {
                        continue;
                    }

                    var extraItemType = extraItem.type;
                    var extraItemLabel = extraItem.label;
                    var extraItemTooltipHeader = extraItem.tooltipHeader;
                    var extraItemTooltipText = extraItem.tooltipText;
                    // for type: link
                    var extraItemUrl = extraItem.url;
                    // for type: action
                    var extraItemFunction = extraItem.function;

                    // if the item is invalid, log the error and skip this item
                    if ((extraItemType == null) || (extraItemLabel == null) || (extraItemTooltipHeader == null))
                    {
                        // NOTE: consider refusing to start up (for security reasons) if the configuration file cannot be read
                        Logger?.LogError("Invalid MorphicBar item: " + extraItem.ToString());
                        continue;
                    }

                    // if the "link" is missing its url, log the error and skip this item
                    if ((extraItemType == "link") && (extraItemUrl == null))
                    {
                        // NOTE: consider refusing to start up (for security reasons) if the configuration file cannot be read
                        Logger?.LogError("Invalid MorphicBar item: " + extraItem.ToString());
                        continue;
                    }

                    // if the "action" is missing its function, log the error and skip this item
                    if ((extraItemType == "action") && (extraItemFunction == null || extraItemFunction == ""))
                    {
                        // NOTE: consider refusing to start up (for security reasons) if the configuration file cannot be read
                        Logger?.LogError("Invalid MorphicBar item: " + extraItem.ToString());
                        continue;
                    }

                    var extraMorphicBarItem = new MorphicBarExtraItem();
                    extraMorphicBarItem.type = extraItemType;
                    extraMorphicBarItem.label = extraItemLabel;
                    extraMorphicBarItem.tooltipHeader = extraItemTooltipHeader;
                    extraMorphicBarItem.tooltipText = extraItemTooltipText;
                    extraMorphicBarItem.url = extraItemUrl;
                    extraMorphicBarItem.function = extraItemFunction;
                    result.ExtraMorphicBarItems.Add(extraMorphicBarItem);
                }
            }

            return result;
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
            services.AddSingleton(new StorageOptions { RootPath = AppPaths.GetUserLocalConfigDir("Data") });
            services.AddSingleton(new KeychainOptions { Path = AppPaths.GetUserLocalConfigDir("keychain") });
            services.AddSingleton<UpdateOptions>(serviceProvider => serviceProvider.GetRequiredService<IOptions<UpdateOptions>>().Value);
            services.AddSingleton<IDataProtection, DataProtector>();
            services.AddSingleton<IUserSettings, WindowsUserSettings>();
            services.AddSingleton<Solutions>();
            services.AddSingleton<Keychain>();
            services.AddSingleton<Storage>();
            services.AddSingleton<MorphicSession>();
            services.AddSingleton<CommunitySession>();
            services.AddTransient<TravelWindow>();
            services.AddTransient<CreateAccountPanel>();
            services.AddTransient<CapturePanel>();
            services.AddTransient<TravelCompletedPanel>();
            services.AddTransient<LoginWindow>();
            services.AddTransient<CommunityLoginWindow>();
            services.AddTransient<LoginPanel>();
            services.AddTransient<CreateAccountPanel>();
            services.AddTransient<AboutWindow>();
            services.AddTransient<CopyStartPanel>();
            services.AddTransient<ApplyPanel>();
            services.AddTransient<RestoreWindow>();
            services.AddSingleton<Backups>();
            services.AddTransient<BarData>();
            services.AddSingleton<BarPresets>(s => BarPresets.Default);
            services.AddSolutionsRegistryServices();
            services.AddSingleton<Solutions>(s => Solutions.FromFile(s, AppPaths.GetAppFile("solutions.json5")));
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

            Console.WriteLine(ex);

            MessageBox.Show($"Morphic ran into a problem:\n\n{e.Exception.Message}\n\nFurther information:\n{e.Exception}", "Morphic", MessageBoxButton.OK, MessageBoxImage.Warning);

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
            logging.AddConsole();
            logging.AddFile(this.AppOptions.Launch.Logfile, options =>
            {
                options.Append = true;
                options.FileSizeLimitBytes = 0x100000;
                options.MaxRollingFiles = 3;
            });
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddDebug();
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

            // load (optional) common configuration file
            // NOTE: we currently load this AFTER setting up the logger because the GetCommonConfigurationAsync function logs config file errors to the logger
            var commonConfiguration = this.GetCommonConfigurationAsync().GetAwaiter().GetResult();
            ConfigurableFeatures.SetFeatures(
                autorunConfig: commonConfiguration.AutorunConfig,
                checkForUpdatesIsEnabled: commonConfiguration.CheckForUpdatesIsEnabled,
                cloudSettingsTransferIsEnabled: commonConfiguration.CloudSettingsTransferIsEnabled,
                resetSettingsIsEnabled: commonConfiguration.ResetSettingsIsEnabled,
                morphicBarvisibilityAfterLogin: commonConfiguration.MorphicBarVisibilityAfterLogin,
                morphicBarExtraItems: commonConfiguration.ExtraMorphicBarItems
                );

            this.MorphicSession = this.ServiceProvider.GetRequiredService<MorphicSession>();
            this.MorphicSession.UserChanged += this.Session_UserChanged;

            this.CommunitySession = this.ServiceProvider.GetRequiredService<CommunitySession>();
            this.CommunitySession.UserChanged += this.Session_UserChanged;

            this.Logger.LogInformation("App Started");

            this.morphicMenu = new MorphicMenu();

            this.RegisterGlobalHotKeys();
            this.ConfigureCountly();

            if (ConfigurableFeatures.CheckForUpdatesIsEnabled == true)
            {
                this.StartCheckingForUpdates();
            }

            this.AddSettingsListener();

            this.OpenSession();

            // Make settings displayed on the UI update when a system setting has changed, or when the app is focused.
            this.SystemSettingChanged += (sender, args) => SettingsHandler.SystemSettingChanged();
            AppFocus.Current.MouseEnter += (sender, args) => SettingsHandler.SystemSettingChanged();
            AppFocus.Current.Activated += (sender, args) => SettingsHandler.SystemSettingChanged();
        }

        /// <summary>
        /// Actions to perform when this instance is the first since installation.
        /// </summary>
        private async Task OnFirstRun()
        {
            this.Logger.LogInformation("Performing first-run tasks");

            // Set the magnifier to lens mode at 200%
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\ScreenMagnifier", "Magnification", 200);
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\ScreenMagnifier", "MagnificationMode", 3);

            if (Features.Basic.IsEnabled())
            {
                // Set the colour filter type - if it's not currently enabled.
                //bool filterOn = this.MorphicSession.GetBool(SettingsManager.Keys.WindowsDisplayColorFilterEnabled) == true;
                bool filterOn =
                    await this.MorphicSession.GetSetting<bool>(SettingId.ColorFiltersEnabled);
                if (!filterOn)
                {
                    await this.MorphicSession.SetSetting(SettingId.ColorFiltersFilterType, 5);
                }

                // Set the high-contrast theme, if high-contrast is off.
                bool highcontrastOn = await this.MorphicSession.GetSetting<bool>(SettingId.HighContrastEnabled);
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
        }

        private async Task ResetSettingsAsync()
        {
            // NOTE: we want to move these defaults to config.json, and we want to modify the solutions registry to allow _all_ settings to be specified,
            //       with defaults, in config.json.

            // default values
            var colorFiltersEnabledDefault = false;
            var darkModeEnabledDefault = false;
            var highContrastEnabledDefault = false;
            //
            // NOTE: displayDpiOffsetDefault realistically needs to be a fixed value ("recommended value") until we have logic to adjust by a relative % 
            int displayDpiOffsetDefault = 0;
            //
            var nightModeIsEnabled = false;

            // verify that settings are reset to their default values; if they are not, then set them now
            // NOTE: we do these in an order that makes sense during logout (i.e. we try to do as much as we can before Windows wants to close us, so we push
            //       settings like screen scaling, dark mode and high contrast to the end since they take much longer to change)
            //
            // color filters
            if (await this.MorphicSession.GetSetting<bool>(SettingId.ColorFiltersEnabled) != colorFiltersEnabledDefault)
            {
                await this.MorphicSession.SetSetting(SettingId.ColorFiltersEnabled, colorFiltersEnabledDefault);
            }
            //
            // night mode
            if (await this.MorphicSession.GetSetting<bool>(SettingId.NightModeEnabled) != nightModeIsEnabled)
            {
                await this.MorphicSession.SetSetting(SettingId.NightModeEnabled, nightModeIsEnabled);
            }
            //
            // screen scaling
            var monitorName = Morphic.Windows.Native.Display.Display.GetMonitorName(null);
            if (monitorName != null)
            {
                // get the adapterId and sourceId for this monitor
                var adapterIdAndSourceId = Morphic.Windows.Native.Display.Display.GetAdapterIdAndSourceId(monitorName);
                if (adapterIdAndSourceId != null)
                {
                    // get the current DPI offset
                    var currentDisplayDpiOffset = Morphic.Windows.Native.Display.Display.GetCurrentDpiOffsetAndRange(adapterIdAndSourceId.Value.adapterId, adapterIdAndSourceId.Value.sourceId);
                    if (currentDisplayDpiOffset != null)
                    {
                        if (currentDisplayDpiOffset.Value.currentDpiOffset != displayDpiOffsetDefault)
                        {
                            _ = Morphic.Windows.Native.Display.Display.SetDpiOffset(displayDpiOffsetDefault, adapterIdAndSourceId.Value);
                        }
                    }
                }
            }
            //
            //
            // high contrast
            if (await this.MorphicSession.GetSetting<bool>(SettingId.HighContrastEnabled) != highContrastEnabledDefault)
            {
                await this.MorphicSession.SetSetting(SettingId.HighContrastEnabled, highContrastEnabledDefault);
            }
            //
            // dark mode
            // NOTE: due to the interrelation between high contrast and dark mode, we reset dark mode AFTER resetting high contrast mode
            if (await this.MorphicSession.GetSetting<bool>(SettingId.LightThemeSystem) != !darkModeEnabledDefault)
            {
                await this.MorphicSession.SetSetting(SettingId.LightThemeSystem, !darkModeEnabledDefault);
                await this.MorphicSession.SetSetting(SettingId.LightThemeApps, !darkModeEnabledDefault);
            }
        }

        private void Session_UserChanged(object? sender, EventArgs e)
        {
            if (sender is CommunitySession communitySession)
            {
                if (communitySession.SignedIn)
                {
                    this.BarManager.LoadSessionBar(communitySession);
                }
                else
                {
                    this.BarManager.CloseBar();
                }
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
                this.BarManager.ShowBar();
            });
        }

        public async Task OpenSession()
        {
            if (Features.Basic.IsEnabled())
            {
                await this.MorphicSession.Open();
            }

            if (Features.Community.IsEnabled())
            {
                if (this.CommunitySession.CurrentCredentials == null)
                {
                    await this.Dialogs.OpenDialog<CommunityLoginWindow>();
                }

                await this.CommunitySession.Open();
            }

            this.OnSessionOpened();
        }

        /// <summary>
        /// Called when the session open task completes
        /// </summary>
        /// <param name="task"></param>
        private async void OnSessionOpened()
        {
            this.Logger.LogInformation("Session Open");

            if (ConfigurableFeatures.ResetSettingsIsEnabled == true)
            {
                await this.ResetSettingsAsync();
            }

            if (this.AppOptions.FirstRun)
            {
                await this.OnFirstRun();
            }

            if (Features.Basic.IsEnabled())
            {
                this.BarManager.LoadFromBarJson(AppPaths.GetConfigFile("basic-bar.json5", true));
            }
        }

        #endregion

        /// <summary>
        /// The main menu shown from the system tray icon
        /// </summary>
        private MorphicMenu? morphicMenu;

        public void ShowMenu(Control? control = null)
        {
            this.morphicMenu?.Show(control);
        }

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

        private MessageWatcherNativeWindow? _messageWatcherNativeWindow;

        protected override void OnActivated(EventArgs e)
        {
            if (_messageWatcherNativeWindow == null)
            {
                // create a list of the messages we want to watch for
                List<uint> messagesToWatch = new List<uint>();
                messagesToWatch.Add(AppMain.SingleInstanceMessageId); // this is the message that lets us know that another instance of Morphic was started up

                _messageWatcherNativeWindow = new MessageWatcherNativeWindow(messagesToWatch);
                _messageWatcherNativeWindow.WatchedMessageEvent += _messageWatcherNativeWindow_WatchedMessageEvent;
                try
                {
                    _messageWatcherNativeWindow.Initialize();
                }
                catch (Exception ex)
                {
                    this.Logger.LogError("could not create messages watcher window: {msg}", ex.Message);
                }
            }

            base.OnActivated(e);
        }

        private void _messageWatcherNativeWindow_WatchedMessageEvent(object sender, MessageWatcherNativeWindow.WatchedMessageEventArgs args)
        {
            this.BarManager.ShowBar();
        }

        #region Shutdown

        protected override void OnExit(ExitEventArgs e)
        {
            _messageWatcherNativeWindow?.Dispose();
            Countly.Instance.SessionEnd();

            if (ConfigurableFeatures.ResetSettingsIsEnabled == true)
            {
                this.ResetSettingsAsync().GetAwaiter().GetResult();
            }

            AppMain.ReleaseSingleInstanceMutex();

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

            SystemEvents.SessionEnding += SystemEvents_SessionEnding;

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

        private void SystemEvents_SessionEnding(object sender, SessionEndingEventArgs e)
        {
            // NOTE: in our preliminary testing, we do not have enough time during shutdown
            // to call/complete this function; we should look for a way to keep Windows from
            // forcibly logging out until we have completed our settings reset (or at least a few
            // critical 'reset settings' items)
            if (ConfigurableFeatures.ResetSettingsIsEnabled == true)
            {
                this.ResetSettingsAsync().GetAwaiter().GetResult();
            }
        }

        #endregion
    }
}
