// BarManager.cs: Loads and shows bar.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt


namespace Morphic.Bar
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Windows;
    using Bar;
    using Core;
    using Core.Community;
    using Microsoft.Extensions.Logging;
    using UI;
    using SystemJson = System.Text.Json;

    /// <summary>
    /// Looks after the bar.
    /// </summary>
    public class BarManager
    {
        private PrimaryBarWindow? barWindow;
        private ILogger Logger;

        public BarManager(ILogger? logger = null)
        {
            this.Logger = logger ?? App.Current.Logger;
        }

        /// <summary>
        /// Loads and shows a bar.
        /// </summary>
        /// <param name="userBar">Bar data object from Morphic.Core</param>
        public void LoadBar(UserBar userBar)
        {

            // Serialise the bar data so it can be loaded with a better deserialiser.
            SystemJson.JsonSerializerOptions serializerOptions = new SystemJson.JsonSerializerOptions();
            serializerOptions.Converters.Add(new JsonElementInferredTypeConverter());
            serializerOptions.Converters.Add(
                new SystemJson.Serialization.JsonStringEnumConverter(SystemJson.JsonNamingPolicy.CamelCase));
            string barJson = SystemJson.JsonSerializer.Serialize(userBar, serializerOptions);

            // Dump to a file, for debugging.
            string barFile = AppPaths.GetConfigFile("last-bar.json5");
            File.WriteAllText(barFile, barJson);

            this.LoadBar(barFile, barJson);
        }

        /// <summary>
        /// Loads and shows a bar.
        /// </summary>
        /// <param name="path">JSON file containing the bar data.</param>
        /// <param name="content">The file content (if it's already loaded).</param>
        public void LoadBar(string path, string? content = null)
        {
            BarData? bar = null;
            try
            {
                bar = BarData.Load(path, content);
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                this.Logger.LogError(e, "Problem loading the bar.");
            }

            if (this.barWindow != null)
            {
                this.CloseBar();
            }

            if (bar != null)
            {
                this.barWindow = new PrimaryBarWindow(bar);
                this.barWindow.Show();
                bar.ReloadRequired += this.OnBarOnReloadRequired;
            }
        }

        /// <summary>
        /// Show a bar that's already loaded.
        /// </summary>
        public void ShowBar()
        {
            if (this.barWindow != null)
            {
                this.barWindow.Visibility = Visibility.Visible;
                this.barWindow.Focus();
            }
        }

        public void HideBar()
        {
            this.barWindow?.Hide();
            this.barWindow?.OtherWindow?.Hide();

        }

        private void OnBarOnReloadRequired(object? sender, EventArgs args)
        {
            if (sender is BarData bar)
            {
                string source = bar.Source;

                this.CloseBar();
                this.LoadBar(source);
            }
        }

        /// <summary>
        /// Closes the bar.
        /// </summary>
        public void CloseBar()
        {
            if (this.barWindow != null)
            {
                BarData bar = this.barWindow.Bar;
                this.barWindow.IsClosing = true;
                this.barWindow.Close();
                this.barWindow = null;
                bar.Dispose();
            }
        }
    }
}
