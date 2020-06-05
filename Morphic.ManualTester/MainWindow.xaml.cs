using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Morphic.Settings;
using Morphic.Settings.Ini;
using Morphic.Settings.Registry;
using Morphic.Settings.Spi;
using Morphic.Settings.SystemSettings;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Morphic.ManualTester
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public IServiceProvider ServiceProvider { get; private set; } = null!;
        public IConfiguration Configuration { get; private set; } = null!;
        private ILogger<MainWindow> logger = null!;
        public string fileContent = "";
        public string filePath = "";
        public bool AutoApply { get { return (AutoApplyToggle.IsChecked != null) ? (bool)AutoApplyToggle.IsChecked : false; } }

        public MainWindow()
        {
            InitializeComponent();
            OnStartup();
        }

        /// <summary>
        /// Configure the dependency injection system with services
        /// </summary>
        /// <param name="services"></param>
        private void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(ConfigureLogging);
            services.AddSingleton<IServiceCollection>(services);
            services.AddSingleton<IServiceProvider>(provider => provider);
            services.AddSingleton<IRegistry, WindowsRegistry>();
            services.AddSingleton<IIniFileFactory, IniFileFactory>();
            services.AddSingleton<ISystemSettingFactory, SystemSettingFactory>();
            services.AddSingleton<ISystemParametersInfo, SystemParametersInfo>();
            services.AddTransient<SettingsManager>();
            services.AddMorphicSettingsHandlers(ConfigureSettingsHandlers);
        }

        void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Exception ex = e.Exception;
            logger.LogError("handled uncaught exception: {msg}", ex.Message);
            logger.LogError(ex.StackTrace);

            Dictionary<String, String> extraData = new Dictionary<string, string>();

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
            logging.SetMinimumLevel(LogLevel.Debug);
        }

        private void ConfigureSettingsHandlers(SettingsHandlerBuilder settings)
        {
        }

        protected void OnStartup()
        {
            var collection = new ServiceCollection();
            ConfigureServices(collection);
            ServiceProvider = collection.BuildServiceProvider();
            logger = ServiceProvider.GetRequiredService<ILogger<MainWindow>>();
        }

        private async void LoadNewRegistry(object sender, RoutedEventArgs e)
        {
            var filedialog = new OpenFileDialog();
            filedialog.InitialDirectory = "Documents";
            filedialog.Filter = "json files (*.json)|*.json|All files (*.*)|*.*";
            if(filedialog.ShowDialog() == true)
            {
                this.LoadedFileName.Text = "...";
                this.SettingsList.Items.Clear();
                var loadtext = new TextBlock();
                loadtext.Text = "LOADING...";
                this.SettingsList.Items.Add(loadtext);
                var manager = ServiceProvider.GetRequiredService<SettingsManager>();
                try
                {
                    await manager.Populate(filedialog.FileName);
                    this.LoadedFileName.Text = "Loaded file " + filedialog.FileName;
                    this.SettingsList.Items.Clear();
                    foreach(var solution in manager.SolutionsById)
                    {
                        SolutionHeader header = new SolutionHeader(this, manager, solution.Value);
                        SettingsList.Items.Add(header);
                    }
                }
                catch
                {
                    this.LoadedFileName.Text = "ERROR";
                    this.SettingsList.Items.Clear();
                    var feature = new TextBlock();
                    feature.Text = "AN ERROR HAS OCCURRED. TRY A DIFFERENT FILE";
                    this.SettingsList.Items.Add(feature);
                }
            }
        }

        private void ApplyAllSettings(object sender, RoutedEventArgs e)
        {
            foreach(var element in this.SettingsList.Items)
            {
                try
                {
                    SolutionHeader? header = (SolutionHeader?)element;
                    if (header != null)
                    {
                        header.ApplyAllSettings();
                    }
                }
                catch { }
            }
        }
    }
}
