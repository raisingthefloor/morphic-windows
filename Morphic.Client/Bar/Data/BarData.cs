// BarData.cs: Information about a bar.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

using Morphic.Service;

namespace Morphic.Client.Bar.Data
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Config;
    using Microsoft.Extensions.DependencyInjection;
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

        public BarData() : this(null)
        {
        }

        public BarData(IServiceProvider? serviceProvider)
        {
            this.ServiceProvider = serviceProvider ?? App.Current.ServiceProvider;
            SessionOptions sessionOptions = this.ServiceProvider.GetRequiredService<SessionOptions>();
            this.FrontEndUri = sessionOptions.FrontEndUri;
            this.BarEditorWebAppUri = sessionOptions.BarEditorWebAppUri;
        }

        public IServiceProvider ServiceProvider { get; set; }

        public Uri FrontEndUri { get; }

        public Uri BarEditorWebAppUri { get; }

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
        /// Title of the bar (the window caption)
        /// </summary>
        [JsonProperty("title")]
        public string? Title { get; set; } = "Custom MorphicBar";

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
        /// Base theme for the buttons in the multi-button bar items.
        /// </summary>
        [JsonProperty("controlTheme", ObjectCreationHandling = ObjectCreationHandling.Reuse)]
        public BarItemTheme ControlTheme { get; set; } = new BarItemTheme();

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
        /// Determines if an item should be on the primary bar.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>true if the item belongs on the primary bar.</returns>
        private bool IsPrimaryItem(BarItem item)
        {
            return !item.Hidden && item.IsPrimary && !item.Overflow;
        }

        /// <summary>
        /// Determines if an item should be on the secondary bar.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>true if the item belongs on the secondary bar.</returns>
        private bool IsSecondaryItem(BarItem item)
        {
            return !item.Hidden && !this.IsPrimaryItem(item);
        }

        /// <summary>
        /// Gets the items for the main bar.
        /// </summary>
        public IEnumerable<BarItem> PrimaryItems => this.AllItems.Where(this.IsPrimaryItem)
            .OrderByDescending(item => item.Priority);

        /// <summary>
        /// Gets the items for the additional buttons.
        /// </summary>
        public IEnumerable<BarItem> SecondaryItems => this.AllItems.Where(this.IsSecondaryItem)
            .OrderByDescending(item => item.IsPrimary)
            .ThenByDescending(item => item.Priority);

        public string? CommunityId { get; set; }

        private ILogger logger = App.Current.ServiceProvider.GetRequiredService<ILogger<BarData>>();

        /// <summary>
        /// Loads bar data from either a local file, or a url.
        /// </summary>
        /// <param name="serviceProvider">The service provider/</param>
        /// <param name="barSource">The local path or remote url.</param>
        /// <param name="content">The json content, if already loaded.</param>
        /// <param name="includeDefault">true to also include the default bar data.</param>
        /// <returns>The bar data</returns>
        public static BarData? Load(IServiceProvider serviceProvider, string barSource, string? content = null, bool includeDefault = true)
        {
            BarData? defaultBar;
            if (includeDefault)
            {
                defaultBar = BarData.Load(serviceProvider, AppPaths.GetConfigFile("default-bar.json5", true), null, false);
                // Mark the items as being from the default specification
                defaultBar?.AllItems.ForEach(item => item.IsDefault = true);

                // if extra bar items were specified in the config file, add them to the left side of the MorphicBar now
                var morphicBarExtraItems = ConfigurableFeatures.MorphicBarExtraItems;
                if (morphicBarExtraItems.Count > 0)
                {
                    List<BarItem> extraBarItems = new List<BarItem>();
                    foreach (var extraItemData in morphicBarExtraItems)
                    {
                        BarItem extraBarItem;

                        switch (extraItemData.type)
                        {
                            case "link":
                                {
                                    extraBarItem = new BarButton(defaultBar);
                                    extraBarItem.ToolTipHeader = extraItemData.tooltipHeader;
                                    extraBarItem.ToolTip = extraItemData.tooltipText;
                                    extraBarItem.Text = extraItemData.label ?? "";
                                    //
                                    extraBarItem.Action = new Morphic.Client.Bar.Data.Actions.WebAction();
                                    ((Morphic.Client.Bar.Data.Actions.WebAction)extraBarItem.Action!).UrlString = extraItemData.url ?? "";
                                }
                                break;
                            case "action":
                                {
                                    extraBarItem = new BarButton(defaultBar);
                                    extraBarItem.ToolTipHeader = extraItemData.tooltipHeader;
                                    extraBarItem.ToolTip = extraItemData.tooltipText;
                                    extraBarItem.Text = extraItemData.label ?? "";
                                    //
                                    var extraBarItemInternalAction = new Morphic.Client.Bar.Data.Actions.InternalAction();
                                    extraBarItemInternalAction.TelemetryEventName = "morphicBarExtraItem";
                                    extraBarItem.Action = extraBarItemInternalAction;
                                    ((Morphic.Client.Bar.Data.Actions.InternalAction)extraBarItem.Action!).FunctionName = extraItemData.function!;
                                }
                                break;
                            case "control":
                                {
                                    extraBarItem = new BarMultiButton(defaultBar);
                                    extraBarItem.ToolTipHeader = extraItemData.tooltipHeader;
                                    extraBarItem.ToolTip = extraItemData.tooltipText;
                                    //
                                    switch (extraItemData.feature)
                                    {
                                        case "usbopeneject":
                                            extraBarItem.Text = extraItemData.label ?? "USB Drives (All)";
                                            //
                                            var openAllUsbAction = new Morphic.Client.Bar.Data.Actions.InternalAction();
                                            openAllUsbAction.TelemetryEventName = "morphicBarExtraItem";
                                            openAllUsbAction.FunctionName = "openAllUsbDrives";
                                            var openButton = new BarMultiButton.ButtonInfo
                                            {
                                                Text = "Open",
                                                Action = openAllUsbAction,
                                                TelemetryCategory = "morphicBarExtraItem",
                                                Tooltip = "Open All USB Drives",
                                                Value = "openallusb"
                                            };
                                            //
                                            var ejectAllUsbAction = new Morphic.Client.Bar.Data.Actions.InternalAction();
                                            ejectAllUsbAction.TelemetryEventName = "morphicBarExtraItem";
                                            ejectAllUsbAction.FunctionName = "ejectAllUsbDrives";
                                            var ejectButton = new BarMultiButton.ButtonInfo
                                            {
                                                Text = "Eject",
                                                Action = ejectAllUsbAction,
                                                TelemetryCategory = "morphicBarExtraItem",
                                                Tooltip = "Eject All USB Drives",
                                                Value = "ejectallusb"
                                            };
                                            //
                                            ((BarMultiButton)extraBarItem).Buttons = new Dictionary<string, BarMultiButton.ButtonInfo>
                                            {
                                                { "open", openButton },
                                                { "eject", ejectButton }
                                            };
                                            //
                                            break;
                                        default:
                                            extraBarItem.Text = extraItemData.label ?? "";
                                            break;
                                    }
                                }
                                break;
                            default:
                                // unknown type; this should be an impossible code path
                                throw new NotImplementedException();
                        }
                        //extraBarItem.ColorValue = "#00FF00";
                        extraBarItem.IsPrimary = true;
                        //
                        defaultBar?.AllItems.Add(extraBarItem);
                    }

                    // add a spacer entry
                    BarButton spacerBarItem = new BarButton(defaultBar);
                    spacerBarItem.ToolTipHeader = "";
                    spacerBarItem.ToolTip = "";
                    spacerBarItem.Text = "";
                    spacerBarItem.ColorValue = "#FFFFFF";
                    spacerBarItem.IsPrimary = true;
                    //
                    defaultBar?.AllItems.Add(spacerBarItem);
                }
            }
            else
            {
                defaultBar = null;
            }

            App.Current.Logger.LogInformation("Loading bar from {source}", barSource);

            BarData? bar;

            using (TextReader reader = content == null
                ? (TextReader)File.OpenText(barSource)
                : new StringReader(content))
            {
                bar = BarJson.Load(serviceProvider, reader, defaultBar);
            }

            bar.Source = barSource;
            if (File.Exists(barSource))
            {
                bar.AddWatcher(barSource);
            }

            return bar;
        }

        private bool hasDeserialized;

        /// <summary>
        /// Called when the bar has been deserialised. This can be called twice, for the default bar and the user's bar.
        /// </summary>
        public void Deserialized()
        {
            // Make the theme of each item inherit the default theme.
            this.BarTheme.Apply(Theme.DefaultBar());
            this.DefaultTheme.Apply(Theme.DefaultItem());
            this.ControlTheme.Apply(Theme.DefaultControl()).Apply(this.DefaultTheme);

            if (this.hasDeserialized)
            {
                // If a bar has no primary items, it looks stupid. Make all the items primary, and make them over-flow to
                // the secondary bar.
                bool hasPrimary = this.PrimaryItems.Any(item => !item.IsDefault);
                if (!hasPrimary)
                {
                    foreach (BarItem item in this.SecondaryItems)
                    {
                        item.IsPrimary = true;
                    }

                    this.Overflow = BarOverflow.Secondary;
                }
            }

            this.AllItems.ForEach(item =>
            {
                item.IsDefault = !this.hasDeserialized;
                item.Deserialized();
            });

            this.hasDeserialized = true;
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