namespace Morphic.Client.Menu
{
    using Config;
    using CountlySDK;
    using System;
    using System.Diagnostics;
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

        public enum MorphicMenuItemTelemetryType
        {
            Settings,
            LearnMore,
            QuickDemoVideo
        }
        public MorphicMenuItemTelemetryType? TelemetryType;
        public string? TelemetryCategory;

        /// <summary>Show the item only if one or more of these features are enabled.</summary>
        public Features Features
        {
            set
            {
                if (!value.AnyEnabled())
                {
                    this.Visibility = Visibility.Collapsed;
                }
            }
        }

        /// <summary>Show the item only if all of these features are enabled.</summary>
        public Features AllFeatures
        {
            set
            {
                if (!value.IsEnabled())
                {
                    this.Visibility = Visibility.Collapsed;
                }
            }
        }

        public MorphicMenuItem()
        {
            this.Click += this.OnClick;
        }

        private async void OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is MorphicMenuItem item)
            {
                if (!string.IsNullOrEmpty(item.Open))
                {
                    Process.Start(new ProcessStartInfo(this.Open)
                    {
                        UseShellExecute = true
                    });
                }

                if (this.Dialog != null)
                {
                    App.Current.Dialogs.OpenDialog(this.Dialog!);
                }

            }

            string? eventSource = null;
            switch (this.ParentMenuType)
            {
                case MenuType.mainMenu:
                    eventSource = "iconMenu";
                    break;
                case MenuType.contextMenu:
                    eventSource = "contextMenu";
                    break;
            }


            switch (((MorphicMenuItem)sender).TelemetryType)
            {
                case MorphicMenuItemTelemetryType.Settings:
                    {
                        var segmentation = new Segmentation();
                        var settingCategoryName = ((MorphicMenuItem)sender).TelemetryCategory;
                        if (settingCategoryName != null)
                        {
                            segmentation.Add("category", settingCategoryName);
                        }
                        //
                        segmentation.Add("eventSource", eventSource);
                        //
                        await Countly.RecordEvent("systemSettings", 1, segmentation);
                        //await Countly.RecordEvent("systemSettings" + settingCategoryName);
                    }
                    break;
                case MorphicMenuItemTelemetryType.LearnMore:
                    {
                        var segmentation = new Segmentation();
                        var settingCategoryName = ((MorphicMenuItem)sender).TelemetryCategory;
                        if (settingCategoryName != null)
                        {
                            segmentation.Add("category", settingCategoryName);
                        }
                        //
                        segmentation.Add("eventSource", eventSource);
                        //
                        await Countly.RecordEvent("learnMore", 1, segmentation);
                    }
                    break;
                case MorphicMenuItemTelemetryType.QuickDemoVideo:
                    {
                        var segmentation = new Segmentation();
                        var settingCategoryName = ((MorphicMenuItem)sender).TelemetryCategory;
                        if (settingCategoryName != null)
                        {
                            segmentation.Add("category", settingCategoryName);
                        }
                        //
                        segmentation.Add("eventSource", eventSource);
                        //
                        await Countly.RecordEvent("quickDemoVideo", 1, segmentation);
                    }
                    break;
                default:
                    // handle menu "open settings" items
                    // NOTE: we may want to create a separate "telemetry type" and embed it in the menu xaml itself (so that we don't have to compare against open paths here)
                    {
                        string? settingCategoryName = null;
                        switch (((MorphicMenuItem)sender).Open)
                        {
                            case "ms-settings:colors":
                                settingCategoryName = "darkMode";
                                break;
                            case "ms-settings:display":
                                settingCategoryName = "textSize";
                                break;
                            case "ms-settings:easeofaccess-display":
                                settingCategoryName = "allAccessibility";
                                break;
                            case "ms-settings:easeofaccess-colorfilter":
                                settingCategoryName = "colorFilter";
                                break;
                            case "ms-settings:easeofaccess-cursorandpointersize":
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
                        if (settingCategoryName != null)
                        {
                            var segmentation = new Segmentation();
                            segmentation.Add("category", settingCategoryName);
                            segmentation.Add("eventSource", eventSource);
                            //
                            await Countly.RecordEvent("systemSettings", 1, segmentation);
                            //await Countly.RecordEvent("systemSettings" + settingCategoryName);
                        }
                    }
                    break;
            }
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
