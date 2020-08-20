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
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Windows.Threading;
    using Bar;
    using Microsoft.Extensions.Logging;
    using Morphic.Core.Community;
    using UI;
    using UI.AppBarWindow;
    using Application = System.Windows.Application;

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public new static App Current => (App)Application.Current;

        private PrimaryBarWindow? barWindow;

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

        private readonly string ApplicationDataFolderPath = Path.Combine(new string[] { Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MorphicCommunity" });

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
            services.AddLogging(ConfigureLogging);
            services.Configure<SessionOptions>(Configuration.GetSection("MorphicService"));
            // TODO: autoupdate
            //services.Configure<UpdateOptions>(Configuration.GetSection("Update"));
            //services.AddSingleton<UpdateOptions>(serviceProvider => serviceProvider.GetRequiredService<IOptions<UpdateOptions>>().Value);
            services.AddSingleton<IServiceCollection>(services);
            services.AddSingleton<IServiceProvider>(provider => provider);
            services.AddSingleton<SessionOptions>(serviceProvider => serviceProvider.GetRequiredService<IOptions<SessionOptions>>().Value);
            services.AddSingleton(new StorageOptions { RootPath = Path.Combine(ApplicationDataFolderPath, "Data") });
            services.AddSingleton(new KeychainOptions { Path = Path.Combine(ApplicationDataFolderPath, "keychain") });
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

            services.AddMorphicSettingsHandlers(ConfigureSettingsHandlers);
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
                Logger.LogError("exception thrown while countly recording exception: {msg}", e.Message);
                throw e;
            }
            Logger.LogDebug("successfully recorded countly exception");
        }

        void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Exception ex = e.Exception;
            Logger.LogError("handled uncaught exception: {msg}", ex.Message);
            Logger.LogError(ex.StackTrace);

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
            logging.AddConfiguration(Configuration);
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddDebug();
        }

        private void ConfigureSettingsHandlers(SettingsHandlerBuilder settings)
        {
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            Configuration = GetConfiguration();
            var collection = new ServiceCollection();
            ConfigureServices(collection);
            ServiceProvider = collection.BuildServiceProvider();
            Logger = ServiceProvider.GetRequiredService<ILogger<App>>();
            base.OnStartup(e);
            AppPaths.Log(Logger);
            Session = ServiceProvider.GetRequiredService<CommunitySession>();
            Session.UserChanged += Session_UserChanged;
            Logger.LogInformation("App Started");
            ConfigureCountly();
            // TODO: autoupdate
            //StartCheckingForUpdates();
            var task = OpenSession();
            task.ContinueWith(SessionOpened, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void Session_UserChanged(object? sender, EventArgs e)
        {
            if (Session.User != null)
            {
                if (Session.Bar != null)
                {
                    ShowBar(Session.Bar);
                }
                else
                {
                    if (Session.Communities.Length == 0)
                    {
                        // TODO: show "No comminities" error
                    }
                    else if (Session.Communities.Length == 1)
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
            await Session.Open();
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
            Logger.LogInformation("Session Open");
            if (Session.User == null)
            {
                var loginWindow = ServiceProvider.GetRequiredService<LoginWindow>();
                loginWindow.Show();
            }
        }

        #endregion

        public void ShowBar(UserBar userBar)
        {
            BarData bar = new BarData(userBar);

            if (this.barWindow != null)
            {
                this.barWindow.Close();
                this.barWindow = null;
            }

            if (bar != null)
            {
                this.barWindow = new PrimaryBarWindow(bar);
                this.barWindow.Show();
            }
        }

        // The mouse is over any window in mouseOverWindows
        private bool mouseOver = false;

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

    }
}