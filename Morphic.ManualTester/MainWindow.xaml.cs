namespace Morphic.ManualTester
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using System.Windows.Threading;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Win32;
    using Settings.SolutionsRegistry;
    using Settings.Resolvers;

    //Resolver for testing
    public class TestResolver : Resolver
    {
        public Dictionary<string, string> Values { get; set; } = new Dictionary<string, string>();

        public override string? ResolveValue(string valueName)
        {
            return this.Values.TryGetValue(valueName, out string? value)
                ? value
                : null;
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public bool AutoApply = true;
        private string? currentRegistryFile;
        private ILogger<MainWindow> logger = null!;
        private const string RegistryPath = @"HKEY_CURRENT_USER\Software\Raising the Floor\Morphic\ManualTester";

        public MainWindow()
        {
            this.InitializeComponent();
            this.OnStartup();
        }

        public IServiceProvider ServiceProvider { get; private set; } = null!;
        public IConfiguration Configuration { get; } = null!;

        /// <summary>AutoReload_OnCheckedre the dependency injection system with services
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

        private static TestResolver resolver = new TestResolver()
        {
            Values = new Dictionary<string, string>()
            {
                { "value1", "first" },
                { "value2", "second" }
            }
        };

        protected void OnStartup()
        {
            ServiceCollection? collection = new ServiceCollection();
            this.ConfigureServices(collection);
            this.ServiceProvider = collection.BuildServiceProvider();
            this.logger = this.ServiceProvider.GetRequiredService<ILogger<MainWindow>>();

            this.AutoReload = Registry.GetValue(RegistryPath, "AutoReload", null) as string == "1";
            string? lastFile = Registry.GetValue(RegistryPath, "LastFile", null) as string;
            if (string.IsNullOrEmpty(lastFile))
            {
                this.LoadNewRegistry(new object(), new RoutedEventArgs());
            }
            else
            {
                this.LoadRegistryFile(lastFile);
            }

        }

        private void LoadNewRegistry(object sender, RoutedEventArgs e)
        {
            OpenFileDialog? filedialog = new OpenFileDialog();
            filedialog.InitialDirectory = "Documents";
            filedialog.Filter = "json files (*.json, *.json5)|*.json;*.json5|All files (*.*)|*.*";
            if (filedialog.ShowDialog() == true)
            {
                this.LoadRegistryFile(filedialog.FileName);
            }
        }

        private void LoadRegistryFile(string path)
        {
            this.LoadedFileName.Text = "...";
            this.SettingsList.Items.Clear();
            TextBlock? loadtext = new TextBlock();
            loadtext.Text = "LOADING...";
            this.SettingsList.Items.Add(loadtext);

            try
            {
                this.currentRegistryFile = path;
                if (this.AutoReload)
                {
                    this.WatchFile(this.currentRegistryFile);
                }

                Solutions solutions = Solutions.FromFile(this.ServiceProvider, this.currentRegistryFile);
                this.LoadedFileName.Text = "Loaded file " + this.currentRegistryFile;
                this.SettingsList.Items.Clear();
                foreach (var solution in solutions.All.Values)
                {
                    SolutionHeader header = new SolutionHeader(this, solution);
                    this.SettingsList.Items.Add(header);
                }

                Registry.SetValue(RegistryPath, "LastFile", this.currentRegistryFile, RegistryValueKind.String);
            }
            catch (Exception e)
            {
                this.LoadedFileName.Text = "ERROR";
                this.SettingsList.Items.Clear();
                TextBlock? feature = new TextBlock();
                feature.Text = "AN ERROR HAS OCCURRED. TRY A DIFFERENT FILE\n\n";
                feature.Text += e.ToString();
                this.SettingsList.Items.Add(feature);
            }
        }

        private void Reload(string path)
        {
            if (path != this.currentRegistryFile)
            {
                this.LoadRegistryFile(path);
                return;
            }

            HashSet<string> expanded = new HashSet<string>();

            // See which nodes are expanded.
            ItemContainerGenerator containerGenerator = this.SettingsList.ItemContainerGenerator;
            foreach (SolutionHeader item in this.SettingsList.Items.OfType<SolutionHeader>())
            {
                if (item.IsExpanded)
                {
                    expanded.Add(item.Solution.SolutionId);
                }
            }

            // Reload the file.
            this.LoadRegistryFile(this.currentRegistryFile);

            if (expanded.Count > 0)
            {
                // Expand the nodes that were expanded before the reload.
                containerGenerator.StatusChanged += ItemsLoaded;
            }

            void ItemsLoaded(object? sender, EventArgs e)
            {
                if (containerGenerator.Status == GeneratorStatus.ContainersGenerated)
                {
                    containerGenerator.StatusChanged -= ItemsLoaded;

                    foreach (SolutionHeader item in this.SettingsList.Items.OfType<SolutionHeader>())
                    {
                        if (expanded.Contains(item.Solution.SolutionId))
                        {
                            item.IsExpanded = true;
                        }
                    }
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

        private void ReloadAll(object sender, RoutedEventArgs e)
        {
            foreach (object? element in this.SettingsList.Items)
            {
                try
                {
                    SolutionHeader? header = (SolutionHeader?)element;
                    if (header != null)
                    {
                        header.RefreshSettings();
                    }
                }
                catch
                {
                }
            }
        }

        private string CSVText;

        private void SaveCSV(object sender, RoutedEventArgs e)
        {
            CSVText = "";
            SaveFileDialog? filedialog = new SaveFileDialog();
            filedialog.Title = "Save CSV File As";
            filedialog.InitialDirectory = "Documents";
            filedialog.Filter = "CSV files|*.csv|All files (*.*)|*.*";
            filedialog.AddExtension = true;
            if (filedialog.ShowDialog() == true)
            {
                CSVText = filedialog.FileName;
                File.WriteAllText(CSVText, "Title, Machine Name, DataType, Range, Default, Comments");

                foreach (object? element in this.SettingsList.Items)
                {
                    try
                    {
                        SolutionHeader? header = (SolutionHeader?)element;
                        if (header != null)
                        {
                            header.SaveCSV(this);
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }

        public void AddCSVLine(String[] values)
        {
            bool first = true;
            string line = "\n";
            foreach (String val in values)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    line += ",";
                }
                line += val;
            }
            File.AppendAllText(CSVText, line);
        }

        private FileSystemWatcher? fileWatcher = null;

        private void AutoReload_OnChecked(object sender, RoutedEventArgs e)
        {
            this.AutoReload = this.AutoReloadCheckBox.IsChecked == true;
        }

        private bool _autoReload;
        private bool AutoReload
        {
            get => this._autoReload;
            set
            {
                if (this._autoReload != value)
                {
                    this._autoReload = value;
                    Registry.SetValue(RegistryPath, "AutoReload", this._autoReload ? "1" : "0");
                    if (this.currentRegistryFile != null && this._autoReload)
                    {
                        this.WatchFile(this.currentRegistryFile);
                    }
                }

                this.AutoReloadCheckBox.IsChecked = this._autoReload;
            }
        }

        private void StopWatching()
        {
            this.fileWatcher?.Dispose();
            this.fileWatcher = null;
        }

        private void WatchFile(string file)
        {
            string fullPath = Path.GetFullPath(file);
            string dir = Path.GetDirectoryName(fullPath)!;
            string filename = Path.GetFileName(fullPath);

            if (this.fileWatcher != null)
            {
                if (this.fileWatcher.Filter == filename)
                {
                    // The file is already being watched.
                    return;
                }

                this.StopWatching();
            }

            this.fileWatcher = new FileSystemWatcher(dir)
            {
                Filter = filename,
                NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.LastWrite | NotifyFilters.Size
                    | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            this.fileWatcher.Changed += this.WatcherOnChanged;
            this.fileWatcher.Created += this.WatcherOnChanged;
            this.fileWatcher.Renamed += this.WatcherOnChanged;
        }

        private CancellationTokenSource? changed;

        private async void WatcherOnChanged(object sender, FileSystemEventArgs e)
        {
            this.changed?.Cancel();
            this.changed = new CancellationTokenSource();

            if (!this.AutoReload)
            {
                this.StopWatching();
                return;
            }

            try
            {
                // Wait for the change events to finish.
                await Task.Delay(1000, this.changed.Token);
                this.changed = null;

                Application.Current.Dispatcher.Invoke(() => this.Reload(e.FullPath));
            }
            catch (TaskCanceledException)
            {
                // Do nothing.
            }
        }

    }
}
