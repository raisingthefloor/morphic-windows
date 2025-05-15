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
                                case "application":
                                    {
                                        extraBarItem = new BarButton(defaultBar);
                                        extraBarItem.ToolTipHeader = extraItemData.tooltipHeader;
                                        extraBarItem.ToolTip = extraItemData.tooltipText;
                                        extraBarItem.Text = extraItemData.label ?? "";
                                        //
                                        var extraBarItemApplicationAction = new Morphic.Client.Bar.Data.Actions.ApplicationAction();
                                        extraBarItemApplicationAction.TelemetryEventName = "morphicBarExtraItem";
                                        extraBarItem.Action = extraBarItemApplicationAction;
                                        ((Morphic.Client.Bar.Data.Actions.ApplicationAction)extraBarItem.Action!).ImageIsCollapsed = true; // for horizontal bars, we don't want an image to be visible
                                        ((Morphic.Client.Bar.Data.Actions.ApplicationAction)extraBarItem.Action!).ExeName = extraItemData.appId ?? "";
                                        extraBarItemShouldBeAdded = ((Morphic.Client.Bar.Data.Actions.ApplicationAction)extraBarItem.Action!).IsAvailable;
                                    }
                                    break;
                                case "link":
                                    {
                                        extraBarItem = new BarButton(defaultBar);
                                        extraBarItem.ToolTipHeader = extraItemData.tooltipHeader;
                                        extraBarItem.ToolTip = extraItemData.tooltipText;
                                        extraBarItem.Text = extraItemData.label ?? "";
                                        //
                                        var extraBarItemWebAction = new Morphic.Client.Bar.Data.Actions.WebAction();
                                        extraBarItemWebAction.TelemetryEventName = "morphicBarExtraItem";
                                        extraBarItem.Action = extraBarItemWebAction;
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
                                                    extraBarItem.Text = extraItemData.label ?? "{{QuickStrip_UsbOpenEject_Title}}";
                                                    //
                                                    var openAllUsbAction = new Morphic.Client.Bar.Data.Actions.InternalAction();
                                                    openAllUsbAction.TelemetryEventName = "morphicBarExtraItem";
                                                    openAllUsbAction.FunctionName = "openAllUsbDrives";
                                                    var openButton = new BarMultiButton.ButtonInfo
                                                    {
                                                        Text = "{{QuickStrip_UsbOpenEject_Open_Title}}",
                                                        Action = openAllUsbAction,
                                                        TelemetryCategory = "morphicBarExtraItem",
                                                        Tooltip = "{{QuickStrip_UsbOpenEject_Open_HelpTitle}}",
                                                        Value = "openallusb"
                                                    };
                                                    //
                                                    var ejectAllUsbAction = new Morphic.Client.Bar.Data.Actions.InternalAction();
                                                    ejectAllUsbAction.TelemetryEventName = "morphicBarExtraItem";
                                                    ejectAllUsbAction.FunctionName = "ejectAllUsbDrives";
                                                    var ejectButton = new BarMultiButton.ButtonInfo
                                                    {
                                                        Text = "{{QuickStrip_UsbOpenEject_Eject_Title}}",
                                                        Action = ejectAllUsbAction,
                                                        TelemetryCategory = "morphicBarExtraItem",
                                                        Tooltip = "{{QuickStrip_UsbOpenEject_Eject_HelpTitle}}",
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
                                            case "voice":
                                                {
                                                    // NOTE: in the future, this control may become a 2-button 'voice' section; if not, we may want to alias this as both "voice" and "voicecontrol"s
                                                    extraBarItem.Text = extraItemData.label ?? "{{QuickStrip_VoiceControl_Title}}";
                                                    //
                                                    var turnOnVoiceAccessAction = new Morphic.Client.Bar.Data.Actions.InternalAction();
                                                    turnOnVoiceAccessAction.TelemetryEventName = "morphicBarExtraItem";
                                                    turnOnVoiceAccessAction.FunctionName = "voiceAccessOn";
                                                    var onButton = new BarMultiButton.ButtonInfo
                                                    {
                                                        Text = "{{QuickStrip_VoiceControl_On_Title}}",
                                                        Action = turnOnVoiceAccessAction,
                                                        TelemetryCategory = "morphicBarExtraItem",
                                                        Tooltip = "{{QuickStrip_VoiceControl_On_HelpTitle}}",
                                                        Value = "voicecontrolon"
                                                    };
                                                    //
                                                    var turnOffVoiceAccessAction = new Morphic.Client.Bar.Data.Actions.InternalAction();
                                                    turnOffVoiceAccessAction.TelemetryEventName = "morphicBarExtraItem";
                                                    turnOffVoiceAccessAction.FunctionName = "voiceAccessOff";
                                                    var offButton = new BarMultiButton.ButtonInfo
                                                    {
                                                        Text = "{{QuickStrip_VoiceControl_Off_Title}}",
                                                        Action = turnOffVoiceAccessAction,
                                                        TelemetryCategory = "morphicBarExtraItem",
                                                        Tooltip = "{{QuickStrip_VoiceControl_Off_HelpTitle}}",
                                                        Value = "voicecontroloff"
                                                    };
                                                    //
                                                    ((BarMultiButton)extraBarItem).Buttons = new Dictionary<string, BarMultiButton.ButtonInfo>
                                                    {
                                                        { "on", onButton },
                                                        { "off", offButton }
                                                    };
                                                    //
                                                    extraBarItemShouldBeAdded = true;
                                                }
                                                break;
                                            case "volume":
                                                {
                                                    extraBarItem.Text = extraItemData.label ?? "{{QuickStrip_Volume_Title}}";
                                                    //
                                                    var volumeUpAction = new Morphic.Client.Bar.Data.Actions.InternalAction();
                                                    volumeUpAction.TelemetryEventName = "volumeUp";
                                                    volumeUpAction.FunctionName = "volumeUp";
                                                    var volumeUpButton = new BarMultiButton.ButtonInfo
                                                    {
                                                        Text = "+",
                                                        Action = volumeUpAction,
                                                        TelemetryCategory = "volumeUp",
                                                        Tooltip = "{{QuickStrip_Volume_Up_HelpTitle}}|{{QuickStrip_Volume_Up_HelpMessage}}|{{QuickStrip_Volume_Up_LimitTitle}}",
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
                                                        Tooltip = "{{QuickStrip_Volume_Down_HelpTitle}}|{{QuickStrip_Volume_Down_HelpMessage}}|{{QuickStrip_Volume_Down_LimitTitle}}",
                                                        Value = "volumeDown"
                                                    };
                                                    //
                                                    var volumeMuteAction = new Morphic.Client.Bar.Data.Actions.InternalAction();
                                                    volumeMuteAction.TelemetryEventName = "volumeMute";
                                                    volumeMuteAction.FunctionName = "volumeMute";
                                                    var volumeMuteButton = new BarMultiButton.ButtonInfo
                                                    {
                                                        Text = "{{QuickStrip_Volume_Mute_Title}}",
                                                        Action = volumeMuteAction,
                                                        TelemetryCategory = "volumeMute",
                                                        Toggle = true,
                                                        Tooltip = "{{QuickStrip_Volume_Mute_HelpTitle}}|{{QuickStrip_Volume_Mute_HelpMessage}}",
                                                        Value = "volumeMute"
                                                    };
                                                    //
                                                    ((BarMultiButton)extraBarItem).Buttons = new Dictionary<string, BarMultiButton.ButtonInfo>
                                                    {
                                                        { "volumeUp", volumeUpButton },
                                                        { "volumeDown", volumeDownButton },
                                                        { "volumeMute", volumeMuteButton }
                                                    };
                                                    ((BarMultiButton)extraBarItem).Menu = new Dictionary<string, string>()
                                                    {
                                                        { "setting", "sound" },
                                                        { "learn", "volmute" },
                                                        { "demo", "volmute" }
                                                    };
                                                    ((BarMultiButton)extraBarItem).AutoSize = true;
                                                    //
                                                    extraBarItemShouldBeAdded = true;
                                                }
                                                break;
                                            case "wordsimplify":
                                                {
                                                    extraBarItem.Text = extraItemData.label ?? "{{QuickStrip_WordSimplify_Title}}";
                                                    //
                                                    var basicWordRibbonAction = new Morphic.Client.Bar.Data.Actions.InternalAction();
                                                    basicWordRibbonAction.TelemetryEventName = "morphicBarExtraItem"; // basicWordRibbonToggle
                                                    basicWordRibbonAction.FunctionName = "basicWordRibbon";
                                                    var basicWordRibbonButton = new BarMultiButton.ButtonInfo
                                                    {
                                                        Text = "{{QuickStrip_WordSimplify_Basic_Title}}",
                                                        Action = basicWordRibbonAction,
                                                        TelemetryCategory = "morphicBarExtraItem",
                                                        Toggle = true,
                                                        Tooltip = "{{QuickStrip_WordSimplify_Basic_HelpTitle}}|{{QuickStrip_WordSimplify_Basic_HelpMessage}}",
                                                        Value = "basicwordribbon"
                                                    };
                                                    //
                                                    var essentialsWordRibbonAction = new Morphic.Client.Bar.Data.Actions.InternalAction();
                                                    essentialsWordRibbonAction.TelemetryEventName = "morphicBarExtraItem"; // essentialsWordRibbonToggle
                                                    essentialsWordRibbonAction.FunctionName = "essentialsWordRibbon";
                                                    var essentialsWordRibbonButton = new BarMultiButton.ButtonInfo
                                                    {
                                                        Text = "{{QuickStrip_WordSimplify_Essentials_Title}}",
                                                        Action = essentialsWordRibbonAction,
                                                        TelemetryCategory = "morphicBarExtraItem",
                                                        Toggle = true,
                                                        Tooltip = "{{QuickStrip_WordSimplify_Essentials_HelpTitle}}|{{QuickStrip_WordSimplify_Essentials_HelpMessage}}",
                                                        Value = "essentialswordribbon"
                                                    };
                                                    //
                                                    ((BarMultiButton)extraBarItem).Buttons = new Dictionary<string, BarMultiButton.ButtonInfo>
                                                    {
                                                        { "basic", basicWordRibbonButton },
                                                        { "essentials", essentialsWordRibbonButton }
                                                    };
                                                    ((BarMultiButton)extraBarItem).Menu = new Dictionary<string, string>()
                                                    {
                                                        { "learn", "wordsimplify" },
                                                        { "demo", "wordsimplify" }
                                                    };
                                                    ((BarMultiButton)extraBarItem).AutoSize = true;
                                                    //
                                                    // NOTE: we should only show this item if Word is actually installed
                                                    // NOTE: if Word is not installed, Morphic.Integrations.Office may fail to load as a DLL (or otherwise throw NotImplementedException); ideally we'd
													//       dynamically load the DLL only if Word was installed...and would perhaps move .IsOfficeInstalled into a DLL which wasn't reliant on Office being installed.
                                                    try
                                                    {
                                                        extraBarItemShouldBeAdded = Morphic.Integrations.Office.WordRibbon.IsOfficeInstalled();
                                                    }
                                                    catch (NotImplementedException)
                                                    {
                                                        extraBarItemShouldBeAdded = false;
                                                    }
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
                        // TODO: we need to make this button invisible to mouse input (so that dragging in this position will still drag the MorphicBar)
                        BarButton spacerBarItem = new BarButton(defaultBar);
                        spacerBarItem.ToolTipHeader = "";
                        spacerBarItem.ToolTip = "";
                        spacerBarItem.Text = "";
                        spacerBarItem.ColorValue = "#00FFFFFF";
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

            using (TextReader reader = content is null
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