namespace Morphic.Bar
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Win32;

    /// <summary>
    /// Application settings, configurable by the user and stored in the registry.
    /// </summary>
    /// <remarks>
    /// The properties access the registry directly without caching; keep them out of loops.
    /// </remarks>
    public class RegistrySettings
    {
        private ILogger<RegistrySettings> logger;
        public static RegistrySettings Current { get; } = App.Current.ServiceProvider.GetRequiredService<RegistrySettings>();

        public RegistrySettings(ILogger<RegistrySettings> logger)
        {
            this.logger = logger;
        }

        /// <summary>
        /// The communities the user is in.
        /// </summary>
        public IEnumerable<string> Communities
        {
            get => this.GetSetting("")?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Enumerable.Empty<string>();
            set => this.SetSetting(string.Join(',', value));
        }

        /// <summary>
        /// The community ID for which the last bar was shown.
        /// </summary>
        public string? LastCommunity
        {
            get => this.GetSetting();
            set => this.SetSetting(value);
        }

        /// <summary>
        /// Gets a setting
        /// </summary>
        /// <param name="defaultValue">The value to return if the setting is not found.</param>
        /// <param name="name">The setting name.</param>
        /// <returns>The value of the setting.</returns>
        private string? GetSetting(string? defaultValue = null, [CallerMemberName] string? name = null)
        {
            string? result = null;
            const string notFound = "<not found>";
            bool useDefault = true;
            try
            {
                using RegistryKey key = this.OpenRegistryKey();
                result = key.GetValue(name, notFound).ToString();
                useDefault = result == notFound;
            }
            catch (ApplicationException e)
            {
                this.logger.LogError(e, $"Getting setting for {name}");
            }
            finally
            {
                if (useDefault)
                {
                    result = defaultValue;
                }

                string suffix = useDefault ? " (default)" : string.Empty;
                this.logger.LogInformation($"Setting for {name}: '{result}'{suffix}");
            }

            return result;
        }

        /// <summary>
        /// Stores an application setting to the registry.
        /// </summary>
        /// <param name="newValue">The new value of the setting.</param>
        /// <param name="name">The name of the setting.</param>
        private void SetSetting(string? newValue, [CallerMemberName] string? name = null)
        {
            try
            {
                this.logger.LogInformation($"Setting {name} to '{newValue}'");
                using RegistryKey key = this.OpenRegistryKey();
                key.SetValue(name, newValue);
            }
            catch (ApplicationException e)
            {
                this.logger.LogError(e, $"Setting setting for {name}");
            }
        }

        private RegistryKey OpenRegistryKey()
        {
            return Registry.CurrentUser.CreateSubKey(AppPaths.RegistryPath)
                ?? throw new ApplicationException("Unable to read the registry.");
        }
    }
}
