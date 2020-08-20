// BarData.cs: Information about a bar.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

namespace Morphic.Bar.Bar
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    /// <summary>
    /// Describes a bar.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class BarData : IDisposable, IDeserializable
    {
        private List<FileSystemWatcher> fileWatchers = new List<FileSystemWatcher>();

        public event EventHandler? ReloadRequired;

        /// <summary>
        /// Where the bar data was loaded from (a url or path).
        /// </summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// Bar identifier (currently unused by the client)
        /// </summary>
        [JsonProperty("id")]
        public string? Id { get; set; }

        /// <summary>
        /// Name of the bar (currently unused by the client)
        /// </summary>
        [JsonProperty("name")]
        public string? Name { get; set; }

        /// <summary>
        /// Size of everything.
        /// </summary>
        [JsonProperty("scale")]
        public double Scale { get; set; } = 1;

        /// <summary>
        /// What to do if all buttons do not fit.
        /// </summary>
        [JsonProperty("overflow")]
        public BarOverflow Overflow { get; set; } = BarOverflow.Resize;

        /// <summary>Initial bar positions.</summary>
        [JsonProperty("position", ObjectCreationHandling = ObjectCreationHandling.Reuse)]
        public BarPosition Position { get; set; } = new BarPosition();

        /// <summary>Initial bar positions.</summary>
        [JsonProperty("secondaryBar", ObjectCreationHandling = ObjectCreationHandling.Reuse)]
        public SecondaryBar SecondaryBar { get; set; } = new SecondaryBar();

        /// <summary>
        /// Base theme for bar items - items will take values from this if they haven't got their own.
        /// </summary>
        [JsonProperty("itemTheme", ObjectCreationHandling = ObjectCreationHandling.Reuse)]
        public BarItemTheme DefaultTheme { get; set; } = new BarItemTheme();

        /// <summary>
        /// Theme for the bar.
        /// </summary>
        [JsonProperty("barTheme", ObjectCreationHandling = ObjectCreationHandling.Reuse)]
        public Theme BarTheme { get; set; } = new Theme();

        [JsonProperty("sizes", ObjectCreationHandling = ObjectCreationHandling.Reuse)]
        public BarSizes Sizes { get; set; } = new BarSizes();

        /// <summary>
        /// Gets all items.
        /// </summary>
        [JsonProperty("items")]
        public List<BarItem> AllItems { get; set; } = new List<BarItem>();

        /// <summary>
        /// Gets the items for the main bar.
        /// </summary>
        public IEnumerable<BarItem> PrimaryItems => this.AllItems.Where(item => !item.Hidden && item.IsPrimary)
            .OrderByDescending(item => item.Priority);

        /// <summary>
        /// Gets the items for the additional buttons.
        /// </summary>
        public IEnumerable<BarItem> SecondaryItems => this.AllItems.Where(item => !item.Hidden && !item.IsPrimary)
            .OrderByDescending(item => item.IsPrimaryOriginal)
            .ThenByDescending(item => item.Priority);

        private ILogger logger = LogUtil.LoggerFactory.CreateLogger("Bar");

        /// <summary>
        /// Loads bar data from either a local file, or a url.
        /// </summary>
        /// <param name="barSource">The local path or remote url.</param>
        /// <param name="includeDefault">true to also include the default bar data.</param>
        /// <returns>The bar data</returns>
        public static BarData? Load(string barSource, bool includeDefault = true)
        {
            BarData? defaultBar;
            if (includeDefault)
            {
                defaultBar = BarData.Load(AppPaths.GetConfigFile("default-bar.json5", true), false);
            }
            else
            {
                defaultBar = null;
            }

            App.Current.Logger.LogInformation("Loading bar from {source}", barSource);
            Uri uri = MakeUrl(barSource);

            BarData? bar;

            using (TextReader reader = uri.IsFile
                ? File.OpenText(uri.LocalPath)
                : throw new NotImplementedException("only local files"))
            {
                bar = BarJson.Load(reader, defaultBar);
            }

            if (bar != null)
            {
                bar.Source = barSource;
                if (uri.IsFile)
                {
                    bar.AddWatcher(uri.LocalPath);
                }
            }

            return bar;
        }

        public void Deserialized()
        {
            // Make the theme of each item inherit the default theme.
            this.BarTheme.Apply(Theme.DefaultBar());
            this.DefaultTheme.Apply(Theme.DefaultItem());

            this.AllItems.ForEach(item => item.Deserialized(this));
        }

        /// <summary>
        /// Makes a url from a string containing a url or a local path (absolute or relative).
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static Uri MakeUrl(string input)
        {
            if (!Uri.TryCreate(input, UriKind.Absolute, out Uri? uri))
            {
                // Assume it's a relative path.
                string fullPath = Path.GetFullPath(input);
                uri = new Uri(fullPath);
            }

            return uri;
        }

        private void AddWatcher(string file)
        {
            string fullPath = Path.GetFullPath(file);
            string dir = Path.GetDirectoryName(fullPath)!;
            string filename = Path.GetFileName(fullPath);

            FileSystemWatcher watcher = new FileSystemWatcher(dir)
            {
                Filter = filename,
                NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.LastWrite | NotifyFilters.Size
                               | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            watcher.Changed += this.WatcherOnChanged; 
            watcher.Created += this.WatcherOnChanged; 
            watcher.Renamed += this.WatcherOnChanged; 
            
            this.fileWatchers.Add(watcher);
        }

        private CancellationTokenSource? changed;

        private async void WatcherOnChanged(object sender, FileSystemEventArgs e)
        {
            this.changed?.Cancel();
            this.changed = new CancellationTokenSource();
            
            try
            {
                // Wait for the change events to finish.
                await Task.Delay(1000, this.changed.Token);
                this.changed = null;
                App.Current.Dispatcher.Invoke(() => this.ReloadRequired?.Invoke(this, e));
            }
            catch (TaskCanceledException)
            {
                // Do nothing.
            }
        }

        public void Dispose()
        {
            this.fileWatchers.ForEach(fileWatcher =>
            {
                fileWatcher.EnableRaisingEvents = false;
                fileWatcher.Dispose();
            }); 
            this.fileWatchers.Clear();
        }
    }
}