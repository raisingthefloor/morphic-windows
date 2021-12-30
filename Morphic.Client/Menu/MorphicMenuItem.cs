namespace Morphic.Client.Menu
{
    using Config;
    using CountlySDK;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;

    public class MorphicMenuItem : MenuItem
    {
        /// <summary>Shell action to open when the item is clicked.</summary>
        public string Open { get; set; }

        public enum MenuType
        {
            contextMenu,
            mainMenu
        }
        public MenuType ParentMenuType = MenuType.mainMenu;

        public Type? Dialog { get; set; }
        public string? DialogAction { get; set; }

        public enum MorphicMenuItemTelemetryType
        {
            Settings,
            LearnMore,
            QuickDemoVideo
        }
        public MorphicMenuItemTelemetryType? TelemetryType;
        public string? TelemetryCategory;

        public MorphicMenuItem()
        {
            this.Click += this.OnClick;
        }

        internal static void OpenMenuItemPath(string openPath)
        {
            Process.Start(new ProcessStartInfo(openPath)
            {
                UseShellExecute = true
            });
        }

        internal static async Task RecordMenuItemTelemetryAsync(string? openPath, MorphicMenuItem.MenuType parentMenuType, MorphicMenuItemTelemetryType? telemetryType, string? telemetryCategory)
        {
            string? eventSource = null;
            switch (parentMenuType)
            {
                case MenuType.mainMenu:
                    eventSource = "iconMenu";
                    break;
                case MenuType.contextMenu:
                    eventSource = "contextMenu";
                    break;
            }


            switch (telemetryType)
            {
                case MorphicMenuItemTelemetryType.Settings:
                    {
                        var segmentation = new Segmentation();
                        var settingCategoryName = telemetryCategory;
                        if (settingCategoryName is not null)
                        {
                            segmentation.Add("category", settingCategoryName);
                        }
                        //
                        segmentation.Add("eventSource", eventSource);
                        //
                        await App.Current.Countly_RecordEventAsync("systemSettings", 1, segmentation);
                        //await App.Current.Countly_RecordEventAsync("systemSettings" + settingCategoryName);
                    }
                    break;
                case MorphicMenuItemTelemetryType.LearnMore:
                    {
                        var segmentation = new Segmentation();
                        var settingCategoryName = telemetryCategory;
                        if (settingCategoryName is not null)
                        {
                            segmentation.Add("category", settingCategoryName);
                        }
                        //
                        segmentation.Add("eventSource", eventSource);
                        //
                        await App.Current.Countly_RecordEventAsync("learnMore", 1, segmentation);
                    }
                    break;
                case MorphicMenuItemTelemetryType.QuickDemoVideo:
                    {
                        var segmentation = new Segmentation();
                        var settingCategoryName = telemetryCategory;
                        if (settingCategoryName is not null)
                        {
                            segmentation.Add("category", settingCategoryName);
                        }
                        //
                        segmentation.Add("eventSource", eventSource);
                        //
                        await App.Current.Countly_RecordEventAsync("quickDemoVideo", 1, segmentation);
                    }
                    break;
                default:
                    // handle menu "open settings" items
                    // NOTE: we may want to create a separate "telemetry type" and embed it in the menu xaml itself (so that we don't have to compare against open paths here)
                    {
                        string? settingCategoryName = null;
                        switch (openPath)
                        {
                            case "ms-settings:colors":
                                settingCategoryName = "darkMode";
                                break;
                            case "ms-settings:display":
                                settingCategoryName = "textSize";
                                break;
                            case "ms-settings:easeofaccess":
                            case "ms-settings:easeofaccess-display":
                                settingCategoryName = "allAccessibility";
                                break;
                            case "ms-settings:easeofaccess-colorfilter":
                                settingCategoryName = "colorFilter";
                                break;
                            case "ms-settings:easeofaccess-cursorandpointersize":
                            case "ms-settings:easeofaccess-MousePointer":
                                settingCategoryName = "pointerSize";
                                break;
                            case "ms-settings:easeofaccess-highcontrast":
                                settingCategoryName = "highContrast";
                                break;
                            case "ms-settings:easeofaccess-keyboard":
                                settingCategoryName = "keyboard";
                                break;
                            case "ms-settings:easeofaccess-magnifier":
                                settingCategoryName = "magnifier";
                                break;
                            case "ms-settings:mousetouchpad":
                                settingCategoryName = "mouse";
                                break;
                            case "ms-settings:nightlight":
                                settingCategoryName = "nightMode";
                                break;
                            case "ms-settings:regionlanguage":
                                settingCategoryName = "language";
                                break;
                            case "ms-settings:speech":
                                settingCategoryName = "readAloud";
                                break;
                            case null:
                                // unknown (i.e. no data)
                                break;
                            default:
                                Debug.Assert(false, "Unknown menu item (i.e. no telemetry)");
                                break;
                        }
                        if (settingCategoryName is not null)
                        {
                            var segmentation = new Segmentation();
                            segmentation.Add("category", settingCategoryName);
                            segmentation.Add("eventSource", eventSource);
                            //
                            await App.Current.Countly_RecordEventAsync("systemSettings", 1, segmentation);
                            //await App.Current.Countly_RecordEventAsync("systemSettings" + settingCategoryName);
                        }
                    }
                    break;
            }
        }

        private async void OnClick(object sender, RoutedEventArgs e)
        {
            string? openPath = null;

            if (sender is MorphicMenuItem item)
            {
                if (!string.IsNullOrEmpty(item.Open))
                {
                    openPath = item.Open;
                    MorphicMenuItem.OpenMenuItemPath(openPath!);
                }

                if (this.Dialog is not null)
                {
                    var args = new Dictionary<string, object?>();
                    if (this.DialogAction is not null)
                    {
                        args["action"] = this.DialogAction!;
                    }
                    await App.Current.Dialogs.OpenDialogAsync(this.Dialog!, args);
                }

            }

            await RecordMenuItemTelemetryAsync(openPath, ((MorphicMenuItem)sender).ParentMenuType, ((MorphicMenuItem)sender).TelemetryType, ((MorphicMenuItem)sender).TelemetryCategory);
        }
    }

    /// <summary>
    /// A menu header item - a separator with text.
    /// </summary>
    public class MorphicMenuHeader : Separator
    {
        public string? Header { get; set; } = null;

        public override void EndInit()
        {
            base.EndInit();

            // Add the header label
            ControlTemplate template = new ControlTemplate(this.GetType());
            FrameworkElementFactory factory = new FrameworkElementFactory(typeof(Label));
            factory.SetValue(ContentControl.ContentProperty, this.Header);
            factory.SetValue(Label.FontWeightProperty, FontWeights.Bold);
            factory.SetValue(Label.ForegroundProperty, SystemColors.MenuTextBrush);
            factory.SetValue(Label.BackgroundProperty, SystemColors.MenuBarBrush);
            template.VisualTree = factory;
            this.Template = template;
        }
    }

}
