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
            return !item.Hidden && !item.Overflow;
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

        // OBSERVATION: the usage of the "Overflow" bool in sorting and the general nomenclature of "Overflow" and "Priority" leave room for bugs;
		//              at first glance, "Overflow" should be a filter for secondary items rather than a descending sort order and
		//              "Priority" (which is the index value) seems like it should ordered in ascending order;
		//              we should rethink our terminology and refactor the logic here and the variable naming and logic related to these variables
        //
        /// <summary>
        /// Gets the items for the additional buttons.
        /// </summary>
        public IEnumerable<BarItem> SecondaryItems => this.AllItems.Where(this.IsSecondaryItem)
            .OrderByDescending(item => item.Overflow)
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

                // OBSERVATION: we need a better way to determine if this is the basic bar or another bar
                var isBasicBar = (barSource == AppPaths.GetConfigFile("basic-bar.json5", true));
                //
                if (isBasicBar == true)
                {
                    // if extra bar items were specified in the config file, add them to the left side of the MorphicBar now
                    var morphicBarExtraItems = ConfigurableFeatures.MorphicBarExtraItems;
                    if (morphicBarExtraItems.Count > 0)
                    {
                        List<BarItem> extraBarItems = new List<BarItem>();
                        foreach (var extraItemData in morphicBarExtraItems)
                        {
                            BarItem extraBarItem;
                            var extraBarItemShouldBeAdded = false;

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
                                        extraBarItemShouldBeAdded = true;
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
                                        extraBarItemShouldBeAdded = true;
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
                                                {
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
                                                    extraBarItemShouldBeAdded = true;
                                                }
                                                break;
                                            case "volume":
                                                {
                                                    extraBarItem.Text = extraItemData.label ?? "Volume";
                                                    //
                                                    var volumeUpAction = new Morphic.Client.Bar.Data.Actions.InternalAction();
                                                    volumeUpAction.TelemetryEventName = "volumeUp";
                                                    volumeUpAction.FunctionName = "volumeUp";
                                                    var volumeUpButton = new BarMultiButton.ButtonInfo
                                                    {
                                                        Text = "+",
                                                        Action = volumeUpAction,
                                                        TelemetryCategory = "volumeUp",
                                                        Tooltip = "Increases the volume|Makes all sounds louder.|Volume cannot go louder",
                                                        Value = "volumeUp"
                                                    };
                                                    //
                                                    var volumeDownAction = new Morphic.Client.Bar.Data.Actions.InternalAction();
                                                    volumeDownAction.TelemetryEventName = "volumeDown";
                                                    volumeDownAction.FunctionName = "volumeDown";
                                                    var volumeDownButton = new BarMultiButton.ButtonInfo
                                                    {
                                                        Text = "-",
                                                        Action = volumeDownAction,
                                                        TelemetryCategory = "volumeDown",
                                                        Tooltip = "Decreases the volume|Makes all sounds quieter.|Volume cannot go quieter",
                                                        Value = "volumeDown"
                                                    };
                                                    //
                                                    var volumeMuteAction = new Morphic.Client.Bar.Data.Actions.InternalAction();
                                                    volumeMuteAction.TelemetryEventName = "volumeMute";
                                                    volumeMuteAction.FunctionName = "volumeMute";
                                                    var volumeMuteButton = new BarMultiButton.ButtonInfo
                                                    {
                                                        Text = "Mute",
                                                        Action = volumeMuteAction,
                                                        TelemetryCategory = "volumeMute",
                                                        Tooltip = "Mutes all sounds from your computer|Mutes your speakers - but does NOT mute your microphone.",
                                                        Value = "volumeMute"
                                                    };
                                                    //
                                                    ((BarMultiButton)extraBarItem).Buttons = new Dictionary<string, BarMultiButton.ButtonInfo>
                                                    {
                                                        { "volumeUp", volumeUpButton },
                                                        { "volumeDown", volumeDownButton },
                                                        { "volumeMute", volumeMuteButton }
                                                    };
                                                    ((BarMultiButton)extraBarItem).AutoSize = true;
                                                    //
                                                    extraBarItemShouldBeAdded = true;
                                                }
                                                break;
                                            case "wordsimplify":
                                                {
                                                    extraBarItem.Text = extraItemData.label ?? "Word Simplify";
                                                    //
                                                    var basicWordRibbonAction = new Morphic.Client.Bar.Data.Actions.InternalAction();
                                                    basicWordRibbonAction.TelemetryEventName = "morphicBarExtraItem"; // basicWordRibbonToggle
                                                    basicWordRibbonAction.FunctionName = "basicWordRibbon";
                                                    var basicWordRibbonButton = new BarMultiButton.ButtonInfo
                                                    {
                                                        Text = "Basic",
                                                        Action = basicWordRibbonAction,
                                                        TelemetryCategory = "morphicBarExtraItem",
                                                        Toggle = true,
                                                        Tooltip = "Adds a new 'Basic Items' ribbon to Word|Gives you a new simpler ribbon with just the basic items on it.",
                                                        Value = "basicwordribbon"
                                                    };
                                                    //
                                                    var essentialsWordRibbonAction = new Morphic.Client.Bar.Data.Actions.InternalAction();
                                                    essentialsWordRibbonAction.TelemetryEventName = "morphicBarExtraItem"; // essentialsWordRibbonToggle
                                                    essentialsWordRibbonAction.FunctionName = "essentialsWordRibbon";
                                                    var essentialsWordRibbonButton = new BarMultiButton.ButtonInfo
                                                    {
                                                        Text = "Essentials",
                                                        Action = essentialsWordRibbonAction,
                                                        TelemetryCategory = "morphicBarExtraItem",
                                                        Toggle = true,
                                                        Tooltip = "Adds a new 'Essential Items' ribbon to Word|Gives you a new ribbon with essential items gathered from all other ribbons.",
                                                        Value = "essentialswordribbon"
                                                    };
                                                    //
                                                    ((BarMultiButton)extraBarItem).Buttons = new Dictionary<string, BarMultiButton.ButtonInfo>
                                                    {
                                                        { "basic", basicWordRibbonButton },
                                                        { "essentials", essentialsWordRibbonButton }
                                                    };
                                                    ((BarMultiButton)extraBarItem).AutoSize = true;
                                                    //
                                                    // NOTE: we shouldonly show this item if Word is actually installed
                                                    extraBarItemShouldBeAdded = Morphic.Integrations.Office.WordRibbon.IsOfficeInstalled();
                                                }
                                                break;
                                            default:
                                                extraBarItem.Text = extraItemData.label ?? "";
                                                // NOTE: we don't know what this button is, so do not show it
                                                extraBarItemShouldBeAdded = false;
                                                break;
                                        }
                                    }
                                    break;
                                default:
                                    // unknown type; this should be an impossible code path
                                    throw new NotImplementedException();
                            }
                            //extraBarItem.ColorValue = "#00FF00";
                            //
                            if (extraBarItemShouldBeAdded == true)
                            {
                                defaultBar?.AllItems.Add(extraBarItem);
                            }
                        }

                        // add a spacer entry
                        BarButton spacerBarItem = new BarButton(defaultBar);
                        spacerBarItem.ToolTipHeader = "";
                        spacerBarItem.ToolTip = "";
                        spacerBarItem.Text = "";
                        spacerBarItem.ColorValue = "#FFFFFF";
                        //
                        defaultBar?.AllItems.Add(spacerBarItem);
                    }
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