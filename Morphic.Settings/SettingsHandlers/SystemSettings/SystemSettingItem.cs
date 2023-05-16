/*
 * Handles a setting - a wrapper for ISettingItem.
 *
 * Copyright 2018 Raising the Floor - International
 *
 * Licensed under the New BSD license. You may not use this file except in
 * compliance with this License.
 *
 * The research leading to these results has received funding from the European Union's
 * Seventh Framework Programme (FP7/2007-2013)
 * under grant agreement no. 289016.
 *
 * You may obtain a copy of the License at
 * https://github.com/GPII/universal/blob/master/LICENSE.txt
 */

using SystemSettingsDataModel = SystemSettings.DataModel;

namespace Morphic.Settings.SettingsHandlers.SystemSettings
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Runtime.InteropServices.WindowsRuntime;
    using System.Threading.Tasks;
    using Microsoft.Win32;

    /// <summary>
    /// Handles a setting (a wrapper for ISettingItem).
    /// </summary>
    public class SystemSettingItem
    {
        /// <summary>An ISettingItem class that this class is wrapping.</summary>
        private SystemSettingsDataModel.ISettingItem SettingItem;

        /// <summary>The SystemSettings.DataModel.SettingType (WinRT-assigned setting type) of this setting.</summary>
        public SystemSettingsDataModel.SettingType SettingType { get; private set; }

        private SystemSettingsDataModel.SettingsDatabase? _settingsDatabase;

        /// <summary>
        /// Initializes a new instance of the SettingItem class.
        /// </summary>
        /// <param name="settingId">The setting ID.</param>
        public SystemSettingItem(string settingId)
        {
            if (_settingsDatabase is null)
            {
                _settingsDatabase = new SystemSettingsDataModel.SettingsDatabase();
            }

            if (settingId is null || settingId == string.Empty)
            {
                throw new SettingFailedException("Argument 'settingId' is null or empty");
            }

            SystemSettingsDataModel.ISettingItem? settingItem;
            try
            {
                settingItem = _settingsDatabase!.GetSetting(settingId);
            }
            catch (Exception ex)
            {
                // NOTE: when we move to .NET 6+, it's unknown as to whether or not the WinRT function will throw exceptions; we should capture them in any case
                throw new SettingFailedException("Could not get setting item", ex);
            }
            if (settingItem is null)
            {
                throw new SettingFailedException("Failed to find setting item");
            }

            this.SettingItem = settingItem!;
            this.SettingType = this.SettingItem.Type;
        }

        /// <summary>Gets the setting's value.</summary>
        /// <returns>The value.</returns>
        public Task<object?> GetValue()
        {
            return this.GetValue("Value");
        }

        /// <summary>Gets the setting's value.</summary>
        /// <param name="valueName">Value name (normally "Value")</param>
        /// <returns>The value.</returns>
        public async Task<object?> GetValue(string valueName)
        {
            // Wait for the setting to become available.
            await this.WaitForEnabled();
            return this.SettingItem.GetValue(valueName);
        }

        internal async Task WaitForEnabled()
        {
            for (int i = 0; i < 10; i++)
            {
                if (this.SettingItem.IsEnabled)
                {
                    break;
                }

                await Task.Delay(100);
            }
        }

        /// <summary>Sets the setting's value.</summary>
        /// <param name="newValue">The new value.</param>
        /// <returns>The previous value.</returns>
        public Task SetValue(object? newValue)
        {
            return this.SetValue("Value", newValue);
        }

        /// <summary>Sets the setting's value.</summary>
        /// <param name="valueName">Value name (normally "Value")</param>
        /// <param name="newValue">The new value.</param>
        /// <returns>The previous value.</returns>
        public async Task SetValue(string valueName, object? newValue)
        {
            // Wait for the setting to become available.
            await this.WaitForEnabled();
            this.SettingItem.SetValue(valueName, newValue);
        }

        ///// <summary>Gets a list of possible values.</summary>
        ///// <returns>An enumeration of possible values.</returns>
        //public IEnumerable<object> GetPossibleValues()
        //{
        //    IList<object> values;
        //    this.SettingItem.GetPossibleValues(out values);
        //    return values;
        //}

        ///// <summary>Invokes an Action setting.</summary>
        ///// <returns>The return value of the action function.</returns>
        //public long Invoke()
        //{
        //    return this.Invoke(IntPtr.Zero);
        //}

        ///// <summary>Invokes an Action setting.</summary>
        ///// <returns>The return value of the action function.</returns>
        //public long Invoke(long n)
        //{
        //    return this.Invoke(new IntPtr(n));
        //}

        ///// <summary>Invokes an Action setting.</summary>
        ///// <returns>The return value of the action function.</returns>
        //public long Invoke(string s)
        //{
        //    IntPtr hstring = WindowsRuntimeMarshal.StringToHString(s);
        //    try
        //    {
        //        return this.Invoke(hstring);
        //    }
        //    finally
        //    {
        //        WindowsRuntimeMarshal.FreeHString(hstring);
        //    }
        //}

   //     /// <summary>Invokes an Action setting.</summary>
   //     /// <returns>The return value of the action function.</returns>
   //     public long Invoke(IntPtr n)
   //     {
			//// NOTE: the Invoke function appears to be declared incorrectly; it should use Windows.Foundation.Rect (which exposes doubles) instead of this custom Rect struct (which uses floats)
   //         // return this.SettingItem.Invoke(n, new Windows.Foundation.Rect()).ToInt64();
   //         return this.SettingItem.Invoke(n, new SystemSettingsDataModel.Rect()).ToInt64();
   //     }

        /// <summary>Gets the "IsEnabled" value.</summary>
        /// <returns>The value of "IsEnabled".</returns>
        public bool IsEnabled()
        {
            return this.SettingItem.IsEnabled;
        }

        /// <summary>Gets the "IsApplicable" value.</summary>
        /// <returns>The value of "IsApplicable".</returns>
        public bool IsApplicable()
        {
            return this.SettingItem.IsApplicable;
        }

        /// <summary>Waits for the setting to finish updating (IsUpdating is false)</summary>
        /// <param name="timeout">Timeout in seconds.</param>
        /// <returns>IsUpdating value - so true means it timed-out</returns>
        public bool WaitForCompletion(int timeout)
        {
            const int interval = 100;
            timeout *= 1000 / interval;
            while (this.SettingItem.IsUpdating)
            {
                if (--timeout < 0)
                {
                    break;
                }

                System.Threading.Thread.Sleep(interval);
            }

            return this.SettingItem.IsUpdating;
        }
    }

    /// <summary>Thrown when a setting class was unable to have been initialised.</summary>
    [Serializable]
    public class SettingFailedException : Exception
    {
        public SettingFailedException() { }
        public SettingFailedException(string message)
            : base(FormatMessage(message)) { }

        public SettingFailedException(string message, bool win32)
            : base(FormatMessage(message, win32)) { }

        public SettingFailedException(string message, Exception inner)
            : base(FormatMessage(message ?? inner.Message), inner) { }

        protected SettingFailedException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }

        private static string FormatMessage(string message, bool win32 = false)
        {
            int lastError = win32 ? Marshal.GetLastWin32Error() : 0;
            return (lastError == 0)
                ? message
                : string.Format("{0} (win32 error {1})", message, lastError);
        }
    }
}
