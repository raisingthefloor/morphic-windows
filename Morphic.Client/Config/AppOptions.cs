namespace Morphic.Client.Config
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text.RegularExpressions;
    using CommandLine;
    using Microsoft.Win32;

    /// <summary>
    /// User options for the application.
    /// </summary>
    public class AppOptions : INotifyPropertyChanged
    {
        public static AppOptions Current { get; } = new AppOptions();

        public LaunchOptions Launch { get; } = LaunchOptions.Get();

        private Dictionary<string, object> cache = new Dictionary<string, object>();

        protected AppOptions()
        {
        }

        /// <summary>
        /// Prevents the help pop-ups from appearing.
        /// </summary>
        public bool HideQuickHelp
        {
            get => this.GetValue(false);
            set => this.SetValue(value);
        }

        /// <summary>
        /// Show the bar at startup.
        /// </summary>
        public bool AutoShow
        {
            get => this.GetValue(false);
            set => this.SetValue(value);
        }

        /// <summary>
        /// The communities the user is in.
        /// </summary>
        public string[] Communities
        {
            get => this.GetValue(string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries);
            set => this.SetValue(string.Join(',', value));
        }

        /// <summary>
        /// The community ID for which the last bar was shown.
        /// </summary>
        public string? LastCommunity
        {
            get
            {
                string value = this.GetValue(string.Empty);
                return value.Length == 0 ? null : value;
            }
            set => this.SetValue(value ?? string.Empty);
        }

        private bool? firstRun;
        private bool? firstRunUpgrade;

        /// <summary>true if this is the first run after an upgrade installation.</summary>
        public bool FirstRunUpgrade
        {
            get
            {
                if (this.firstRunUpgrade == null)
                {
                    this.CheckFirstRun();
                }

                return this.firstRunUpgrade == true;
            }
        }

        /// <summary>true if this is the first run after installation.</summary>
        public bool FirstRun
        {
            get
            {
                if (this.firstRun == null)
                {
                    this.CheckFirstRun();
                }

                return this.firstRun == true;
            }
        }

        /// <summary>Check if this instance is the first since installation.</summary>
        private void CheckFirstRun()
        {
            if (this.firstRun == null)
            {
                // ReSharper disable ExplicitCallerInfoArgument
                string lastVersion = this.GetValue(string.Empty, "version");
                if (lastVersion == BuildInfo.Current.Version)
                {
                    this.firstRun = this.firstRunUpgrade = false;
                }
                else
                {
                    this.firstRun = true;
                    this.firstRunUpgrade = !string.IsNullOrEmpty(lastVersion);

                    // Let the next instance know the version of its previous instance (this one).
                    this.SetValue(BuildInfo.Current.Version, "version");
                }
                // ReSharper restore VirtualMemberCallInConstructor
            }
        }

        public bool AutoRun
        {
            get => this.HandleAutoRun();
            set => this.HandleAutoRun(value);
        }

        /// <summary>
        /// Makes the application automatically start at login.
        /// </summary>
        private bool HandleAutoRun(bool? newValue = null)
        {
            bool enabled;
            using RegistryKey morphicKey =
                Registry.CurrentUser.CreateSubKey(@"Software\Raising the Floor\Morphic")!;
            using RegistryKey runKey =
                Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")!;

            if (newValue == null)
            {
                // Get the configured value
                object value = morphicKey.GetValue("AutoRun");
                if (value == null)
                {
                    // This might be the first time running, enable auto-run by default.
                    enabled = true;
                }
                else
                {
                    // Respect the system setting (it was probably removed on purpose).
                    enabled = runKey.GetValue("Morphic") != null;
                }
            }
            else
            {
                enabled = (bool)newValue;
            }

            morphicKey.SetValue("AutoRun", enabled ? "1" : "0", RegistryValueKind.String);
            if (enabled)
            {
                string processPath = Process.GetCurrentProcess().MainModule.FileName;
                // Only add it to the auto-run if running a release.
                if (!processPath.EndsWith("dotnet.exe"))
                {
                    runKey.SetValue("Morphic", processPath);
                }
            }
            else
            {
                runKey.DeleteValue("Morphic", false);
            }

            return enabled;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Open the registry key for this application.
        /// </summary>
        /// <returns></returns>
        public static RegistryKey OpenKey()
        {
            return Registry.CurrentUser.CreateSubKey(@"Software\Raising the Floor\Morphic")!;
        }

        /// <summary>
        /// Get a value from the registry.
        /// </summary>
        protected virtual T GetValue<T>(T defaultValue, [CallerMemberName] string name = null!)
        {
            object? value;
            if (!this.cache.TryGetValue(name, out value))
            {
                using RegistryKey key = AppOptions.OpenKey();
                value = key.GetValue(name, defaultValue);
            }

            object? result = null;
            if (value is T)
            {
                result = value;
            }
            else if (typeof(T) == typeof(int))
            {
                try
                {
                    result = ((IConvertible)value).ToInt32(null);
                }
                catch (FormatException)
                {
                    result = defaultValue;
                }
            }
            else if (typeof(T) == typeof(bool))
            {
                string? text = value.ToString();
                if (string.IsNullOrEmpty(text))
                {
                    result = defaultValue;
                }
                else
                {
                    // See if it's a false-like word, or a zero number.
                    bool isFalse = new[] { "false", "no", "off" }.Contains(text.ToLowerInvariant());
                    if (isFalse)
                    {
                        result = false;
                    }
                    else if (int.TryParse(text, NumberStyles.Any, null, out int number))
                    {
                        result = number != 0;
                    }
                    else
                    {
                        // Anything else is true.
                        result = true;
                    }
                }
                result = ((IConvertible)value).ToBoolean(null);
            }
            else if (typeof(T) == typeof(string))
            {
                result = value.ToString();
            }

            return result is T v ? v : defaultValue;
        }

        /// <summary>
        /// Set a value in the registry.
        /// </summary>
        protected virtual void SetValue(object value, [CallerMemberName] string? name = null)
        {
            using RegistryKey key = AppOptions.OpenKey();
            if (value is bool b)
            {
                key.SetValue(name, b ? 1 : 0, RegistryValueKind.DWord);
            }
            else
            {
                key.SetValue(name, value, RegistryValueKind.Unknown);
            }

            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    /// <summary>
    /// Options set during the process launch - via the command-line or environment variables.
    ///
    /// The name of the option is specified in the [Option] attribute. For environment variables, "MORPHIC_" is
    /// prefixed to the name. For duplicate definitions, the command-line takes over the environment.
    /// </summary>
    public class LaunchOptions
    {
        private const string EnvironmentPrefix = "MORPHIC_";

        protected LaunchOptions()
        {
            IDictionary envObjects = Environment.GetEnvironmentVariables();
            Dictionary<string, string> env = new Dictionary<string, string>(envObjects.Count);

            // Make the variable names upper-case, for case-insensitive matching.
            foreach (object? key in envObjects.Keys)
            {
                if (key is string keyName)
                {
                    env[keyName.ToUpperInvariant()] = envObjects[key] as string ?? string.Empty;
                }
            }
            Regex isFalse = new Regex("^(|0+|false|no|off)$", RegexOptions.IgnoreCase);

            // Set the value of each property to the relevant environment variable.
            foreach (PropertyInfo property in this.GetType().GetProperties())
            {
                OptionAttribute? option = property.GetCustomAttribute<OptionAttribute>(true);
                if (option != null)
                {
                    string varName = $"{EnvironmentPrefix}{option.LongName}".ToUpperInvariant();

                    if (env.TryGetValue(varName, out string? value))
                    {
                        if (property.PropertyType == typeof(bool))
                        {
                            bool boolValue = !isFalse.IsMatch(value);
                            property.SetValue(this, boolValue);
                        }
                        else
                        {
                            object valueObject = ((IConvertible)value).ToType(property.PropertyType, null);
                            property.SetValue(this, valueObject);
                        }
                    }
                }
            }
        }

        public static LaunchOptions Get()
        {
            LaunchOptions? result = null;
            Parser.Default
                .ParseArguments(() => new LaunchOptions(), Environment.GetCommandLineArgs())
                .WithParsed(o => result = o);

            return result ?? new LaunchOptions();
        }

        /// <summary>
        /// The bar json to load.
        /// </summary>
        [Option("bar")]
        public string? BarFile { get; set; }

        /// <summary>
        /// Running in "debug mode".
        /// </summary>
        [Option("debug")]
        public bool Debug { get; set; }

        [Option("features")]
        public string? FeaturesFile { get; set; }
    }
}
