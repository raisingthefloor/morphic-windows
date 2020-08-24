using System.Windows;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Morphic.Core;
using Morphic.Service;
using Morphic.Settings;
using Morphic.Settings.Registry;
using Morphic.Settings.Ini;
using Morphic.Settings.SystemSettings;
using Morphic.Settings.Spi;
using Morphic.Settings.Files;

namespace Morphic.Bar
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Windows.Threading;
    using Microsoft.Extensions.Logging;
    using UI;
    using UI.AppBarWindow;
    using Application = System.Windows.Application;
    using MessageBox = System.Windows.MessageBox;

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public new static App Current => (App)Application.Current;

        public BarManager BarManager { get; private set; } = null!;

        /// <summary>
        /// true if the current application is active.
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>The mouse has entered any window belonging to the application.</summary>
        public event EventHandler? MouseEnter;
        /// <summary>The mouse has left any window belonging to the application.</summary>
        public event EventHandler? MouseLeave;

        public App()
        {
            AppPaths.CreateAll();

            this.Activated += (sender, args) => this.IsActive = true;
            this.Deactivated += (sender, args) => this.IsActive = false;
        }

        public IServiceProvider ServiceProvider { get; private set; } = null!;
        public IConfiguration Configuration { get; private set; } = null!;
        public CommunitySession Session { get; private set; } = null!;
        public ILogger Logger { get; private set; } = null!;

        #region Configuration & Startup

        /// <summary>
        /// Create a Configuration from appsettings.json
        /// </summary>
        /// <returns></returns>
        private IConfiguration GetConfiguration()
        {
            ConfigurationBuilder builder = new ConfigurationBuilder();
            builder.SetBasePath(Directory.GetCurrentDirectory());
            builder.AddJsonFile("appsettings.json", optional: false);
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MORPHIC_DEBUG")))
            {
                builder.AddUserSecrets(Assembly.GetExecutingAssembly());
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
            // TODO: autoupdate
            //services.Configure<UpdateOptions>(Configuration.GetSection("Update"));
            //services.AddSingleton<UpdateOptions>(serviceProvider => serviceProvider.GetRequiredService<IOptions<UpdateOptions>>().Value);
            services.AddSingleton<IServiceCollection>(services);
            services.AddSingleton<IServiceProvider>(provider => provider);
            services.AddSingleton<SessionOptions>(serviceProvider => serviceProvider.GetRequiredService<IOptions<SessionOptions>>().Value);
            services.AddSingleton(new StorageOptions { RootPath = Path.Combine(AppPaths.ConfigDir, "Data") });
            services.AddSingleton(new KeychainOptions { Path = Path.Combine(AppPaths.ConfigDir, "keychain") });
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
            services.AddSingleton<CommunitySession>();
            services.AddTransient<LoginWindow>();
            // TODO: build info for about window
            //services.AddSingleton<BuildInfo>(BuildInfo.FromJsonFile("build-info.json"));

            services.AddMorphicSettingsHandlers(this.ConfigureSettingsHandlers);
        }

        private void ConfigureCountly()
        {
            // TODO: configure countly
            //var section = Configuration.GetSection("Countly");
            //CountlyConfig cc = new CountlyConfig();
            //cc.appKey = section["AppKey"];
            //cc.serverUrl = section["ServerUrl"];
            //var buildInfo = ServiceProvider.GetRequiredService<BuildInfo>();

            //cc.appVersion = buildInfo.InformationalVersion;

            //Countly.Instance.Init(cc);
            //Countly.Instance.SessionBegin();
            //Countly.IsLoggingEnabled = true;
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
            Exception ex = e.Exception;
            this.Logger.LogError("handled uncaught exception: {msg}", ex.Message);
            this.Logger.LogError(ex.StackTrace);

            Dictionary<String, String> extraData = new Dictionary<string, string>();
            // TODO: countly
            //Countly.RecordException(ex.Message, ex.StackTrace, extraData, true).ContinueWith(RecordedException, TaskScheduler.FromCurrentSynchronizationContext());

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
            logging.AddConfiguration(this.Configuration);
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddDebug();
            logging.AddFile(AppPaths.GetConfigFile("morphic-bar.log"));
            logging.AddConsole();
        }

        private void ConfigureSettingsHandlers(SettingsHandlerBuilder settings)
        {
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            this.Configuration = this.GetConfiguration();
            ServiceCollection collection = new ServiceCollection();
            this.ConfigureServices(collection);
            this.ServiceProvider = collection.BuildServiceProvider();
            this.Logger = this.ServiceProvider.GetRequiredService<ILogger<App>>();
            this.Logger.LogInformation("Started {Version}",
                FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion);

            System.AppDomain.CurrentDomain.UnhandledException += (sender, args) => this.Logger.LogCritical(args.ExceptionObject as Exception, "Unhandled exception");

            base.OnStartup(e);
            AppPaths.Log(this.Logger);
            this.Session = this.ServiceProvider.GetRequiredService<CommunitySession>();
            this.Session.UserChanged += this.Session_UserChanged;
            this.Logger.LogInformation("App Started");
            this.ConfigureCountly();
            // TODO: autoupdate
            //StartCheckingForUpdates();

            this.BarManager = new BarManager(this.Logger);

            if (Options.Current.BarFile != null)
            {
                this.BarManager.ShowBar(Options.Current.BarFile);
            }
            else
            {
                Task task = this.OpenSession();
                task.ContinueWith(this.SessionOpened, TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        private void Session_UserChanged(object? sender, EventArgs e)
        {
            if (this.Session.User != null)
            {
                if (this.Session.Bar != null)
                {
                    this.BarManager.ShowBar(this.Session.Bar);
                }
                else
                {
                    if (this.Session.Communities.Length == 0)
                    {
                        // TODO: show "No comminities" error
                    }
                    else if (this.Session.Communities.Length == 1)
                    {
                        // TODO: show "Could not load bar" error
                    }
                    else
                    {
                        // TODO: show community picker
                    }
                }
            }
        }

        private async Task OpenSession()
        {
            //await CopyDefaultPreferences();
            //await Session.SettingsManager.Populate(Path.Combine("Solutions", "windows.solutions.json"));
            //await Session.SettingsManager.Populate(Path.Combine("Solutions", "jaws2020.solutions.json"));
            await this.Session.Open();
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
            if (this.Session.User == null)
            {
                LoginWindow? loginWindow = this.ServiceProvider.GetRequiredService<LoginWindow>();
                loginWindow.Show();
            }
        }

        #endregion

         // The mouse is over any window in mouseOverWindows
        private bool mouseOver;

        // The windows where the mouse-over status is needed.
        private readonly List<Window> mouseOverWindows = new List<Window>();
        private DispatcherTimer? mouseTimer;

        /// <summary>
        /// Register interest in observing the mouse-over state of a window.
        /// </summary>
        /// <param name="window"></param>
        public void AddMouseOverWindow(Window window)
        {
            this.mouseOverWindows.Add(window);
            window.MouseEnter += this.CheckMouseOver;
            window.MouseLeave += this.CheckMouseOver;
        }

        private void CheckMouseOver(object? sender, EventArgs e)
        {
            if (this.mouseOverWindows.Count == 0)
            {
                return;
            }

            bool isOver = false;
            IEnumerable<Window> windows = this.mouseOverWindows.Where(w => w.IsVisible && w.Opacity > 0);

            Point? cursor = null;

            // Window.IsMouseOver is false if the mouse is over the window border, check if that's the case.
            foreach (Window window in windows)
            {
                if (window.IsMouseOver)
                {
                    isOver = true;
                    break;
                }

                cursor ??= PresentationSource.FromVisual(window)?.CompositionTarget.TransformFromDevice
                                             .Transform(WindowMovement.GetCursorPos());

                if (cursor != null)
                {
                    System.Windows.Rect rc = window.GetRect();
                    rc.Inflate(10, 10);
                    if (rc.Contains(cursor.Value))
                    {
                        isOver = true;
                        if (this.mouseTimer == null)
                        {
                            // Keep an eye on the current position.
                            this.mouseTimer = new DispatcherTimer(DispatcherPriority.Input)
                            {
                                Interval = TimeSpan.FromMilliseconds(100),
                            };
                            this.mouseTimer.Tick += this.CheckMouseOver;
                            this.mouseTimer.Start();
                        }

                        break;
                    }
                }
            }

            if (!isOver)
            {
                this.mouseTimer?.Stop();
                this.mouseTimer = null;
            }

            if (this.mouseOver != isOver)
            {
                this.mouseOver = isOver;
                if (isOver)
                {
                    this.MouseEnter?.Invoke(sender, new EventArgs());
                }
                else
                {
                    this.MouseLeave?.Invoke(sender, new EventArgs());
                }
            }
        }

        private void MenuItem_Close(object sender, RoutedEventArgs e)
        {
            this.BarManager.CloseBar();
            this.Shutdown();
        }

        private void MenuItem_About(object sender, RoutedEventArgs e)
        {
            string ver = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion
                ?? "unknown";
            MessageBox.Show($"Morphic Community Bar\n\nVersion: {ver}");
        }
    }
}