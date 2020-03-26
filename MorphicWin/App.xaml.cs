using System;
using System.Windows;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MorphicService;
using System.IO;

namespace MorphicWin
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {

#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        public IServiceProvider ServiceProvider { get; private set; }
        public IConfiguration Configuration { get; private set; }
        public Session Session { get; private set; }
        private ILogger<App> logger;
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.

        private IConfiguration GetConfiguration()
        {
            var builder = new ConfigurationBuilder();
            builder.SetBasePath(Directory.GetCurrentDirectory());
            builder.AddJsonFile("appsettings.json");
            if (Environment.GetEnvironmentVariable("MORPHIC_ENVIRONMENT") is string env)
            {
                builder.AddJsonFile($"appsettings.{env}.json", optional: true);
            }
            return builder.Build();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging();
            services.Configure<SessionOptions>(Configuration.GetSection("MorphicService"));
            services.AddSingleton<SessionOptions>(serviceProvider => serviceProvider.GetRequiredService<IOptions<SessionOptions>>().Value);
            services.AddSingleton<MorphicSettings.Settings>();
            services.AddSingleton<Session>();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            Configuration = GetConfiguration();
            var collection = new ServiceCollection();
            ConfigureServices(collection);
            ServiceProvider = collection.BuildServiceProvider();
            base.OnStartup(e);
            logger = ServiceProvider.GetRequiredService<ILogger<App>>();
            Session = ServiceProvider.GetRequiredService<Session>();
            logger.LogInformation("App Started");
            var task = Session.Open(Settings.Default.UserId);
            task.ContinueWith(SessionOpened);
        }

        private void SessionOpened(Task task)
        {
            logger.LogInformation("Creating Tray Icon");
            CreateNotifyIcon();
            Settings.Default.PropertyChanged += OnSettingChanged;
            logger.LogInformation("Ready");
            if (Session.Preferences == null)
            {
                Morphic.Shared.OpenConfigurator();
            }
        }

        private void OnSettingChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "UserId" && Settings.Default.UserId != "")
            {
                Morphic.Shared.ShowQuickStrip();
            }
        }

        #region System Tray Icon

        private System.Windows.Forms.NotifyIcon? notifyIcon = null;

        private void CreateNotifyIcon()
        {
            notifyIcon = new System.Windows.Forms.NotifyIcon();
            notifyIcon.Click += OnNotifyIconClicked;
            notifyIcon.Icon = MorphicWin.Properties.Resources.Icon;
            notifyIcon.Text = "Morphic Quick Strip";
            notifyIcon.Visible = true;
        }

        private void OnNotifyIconClicked(object? sender, EventArgs e)
        {
            Morphic.Shared.ToggleQuickStrip();
        }

        #endregion

    }
}
