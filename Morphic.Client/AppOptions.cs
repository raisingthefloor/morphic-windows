namespace Morphic.Client
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using Microsoft.Win32;

    /// <summary>
    /// User options for the application.
    /// </summary>
    public class AppOptions : INotifyPropertyChanged
    {
        public static AppOptions Current => App.Shared.AppOptions;

        private Dictionary<string, object> cache = new Dictionary<string, object>();

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


        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Open the registry key for this application.
        /// </summary>
        /// <returns></returns>
        private RegistryKey OpenKey()
        {
            return Registry.CurrentUser.CreateSubKey(@"Software\Raising the Floor\Morphic")!;
        }

        /// <summary>
        /// Get a value from the registry.
        /// </summary>
        protected virtual T GetValue<T>(T defaultValue, [CallerMemberName] string name = null!)
            where T : struct
        {
            object? value;
            if (!this.cache.TryGetValue(name, out value))
            {
                using RegistryKey key = this.OpenKey();
                value = key.GetValue(name, defaultValue);
            }

            object? result = null;
            if (value is T)
            {
                result = value;
            }
            else if (typeof(T) == typeof(int))
            {
                result = ((IConvertible)value).ToInt32(null);
            }
            else if (typeof(T) == typeof(bool))
            {
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
            using RegistryKey key = this.OpenKey();
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
}
