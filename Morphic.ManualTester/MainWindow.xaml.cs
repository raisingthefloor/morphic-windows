namespace Morphic.ManualTester
{
    using System;
    using System.Collections.Generic;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Threading;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Win32;
    using Settings.SolutionsRegistry;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public bool AutoApply = true;
        public string fileContent = "";
        public string filePath = "";
        private ILogger<MainWindow> logger = null!;

        public MainWindow()
        {
            this.InitializeComponent();
            this.OnStartup();
        }

        public IServiceProvider ServiceProvider { get; private set; } = null!;
        public IConfiguration Configuration { get; } = null!;

        /// <summary>
        /// Configure the dependency injection system with services
        /// </summary>
        /// <param name="services"></param>
        private void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(this.ConfigureLogging);
            services.AddSingleton(services);
            services.AddSingleton<IServiceProvider>(provider => provider);
            services.AddSolutionsRegistryServices();
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Exception ex = e.Exception;
            this.logger.LogError("handled uncaught exception: {msg}", ex.Message);
            this.logger.LogError(ex.StackTrace);

            Dictionary<string, string> extraData = new Dictionary<string, string>();

            MessageBox.Show("An unhandled exception just occurred: " + e.Exception.Message, "Exception Sample",
                MessageBoxButton.OK, MessageBoxImage.Warning);
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

        protected void OnStartup()
        {
            ServiceCollection? collection = new ServiceCollection();
            this.ConfigureServices(collection);
            this.ServiceProvider = collection.BuildServiceProvider();
            this.logger = this.ServiceProvider.GetRequiredService<ILogger<MainWindow>>();
            this.LoadNewRegistry(new object(), new RoutedEventArgs());
        }

        private async void LoadNewRegistry(object sender, RoutedEventArgs e)
        {
            OpenFileDialog? filedialog = new OpenFileDialog();
            filedialog.InitialDirectory = "Documents";
            filedialog.Filter = "json files (*.json, *.json5)|*.json;*.json5|All files (*.*)|*.*";
            if (filedialog.ShowDialog() == true)
            {
                this.LoadedFileName.Text = "...";
                this.SettingsList.Items.Clear();
                TextBlock? loadtext = new TextBlock();
                loadtext.Text = "LOADING...";
                this.SettingsList.Items.Add(loadtext);

                try
                {
                    Solutions solutions = Solutions.FromFile(this.ServiceProvider, filedialog.FileName);
                    this.LoadedFileName.Text = "Loaded file " + filedialog.FileName;
                    this.SettingsList.Items.Clear();
                    foreach (var solution in solutions.All.Values)
                    {
                        SolutionHeader header = new SolutionHeader(this, solution);
                        this.SettingsList.Items.Add(header);
                    }
                }
                catch
                {
                    this.LoadedFileName.Text = "ERROR";
                    this.SettingsList.Items.Clear();
                    TextBlock? feature = new TextBlock();
                    feature.Text = "AN ERROR HAS OCCURRED. TRY A DIFFERENT FILE";
                    this.SettingsList.Items.Add(feature);
                }
            }
        }

        private void ToggleAutoApply(object sender, RoutedEventArgs e)
        {
            if (this.AutoApplyToggle.IsChecked != null && this.ApplySettings != null)
            {
                if ((bool)this.AutoApplyToggle.IsChecked)
                {
                    this.AutoApply = true;
                    this.ApplySettings.Visibility = Visibility.Hidden;
                }
                else
                {
                    this.AutoApply = false;
                    this.ApplySettings.Visibility = Visibility.Visible;
                }
            }
        }

        private void ApplyAllSettings(object sender, RoutedEventArgs e)
        {
            foreach (object? element in this.SettingsList.Items)
            {
                try
                {
                    SolutionHeader? header = (SolutionHeader?)element;
                    if (header != null)
                    {
                        header.ApplyAllSettings();
                    }
                }
                catch
                {
                }
            }
        }
    }
}
