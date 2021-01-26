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

        public Type? Dialog { get; set; }

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

            var segmentation = new Segmentation();
            string? settingCategoryName = null;
            switch (((MorphicMenuItem)sender).Open)
            {
                case "ms-settings:colors":
                    settingCategoryName = "DarkMode";
                    break;
                case "ms-settings:display":
                    settingCategoryName = "TextSize"; // right-click settings
                    break;
                case "ms-settings:easeofaccess-display":
                    settingCategoryName = "AllAccessibility";
                    break;
                case "ms-settings:easeofaccess-colorfilter":
                    settingCategoryName = "ColorFilter";
                    break;
                case "ms-settings:easeofaccess-cursorandpointersize":
                    settingCategoryName = "PointerSize";
                    break;
                case "ms-settings:easeofaccess-highcontrast":
                    settingCategoryName = "HighContrast";
                    break;
                case "ms-settings:easeofaccess-keyboard":
                    settingCategoryName = "Keyboard";
                    break;
                case "ms-settings:easeofaccess-magnifier":
                    settingCategoryName = "Magnifier";
                    break;
                case "ms-settings:mousetouchpad":
                    settingCategoryName = "Mouse";
                    break;
                case "ms-settings:nightlight":
                    settingCategoryName = "NightMode";
                    break;
                case "ms-settings:regionlanguage":
                    settingCategoryName = "Language";
                    break;
                case "ms-settings:speech":
                    settingCategoryName = "ReadAloud";
                    break;
                case null:
                    // unknown (i.e. no data)
                    break;
                default:
                    Debug.Assert(false, "Unknown menu item (i.e. no telemetry)");
                    break;
            }
            //if (settingCategoryName != null)
            //{
            //    segmentation.Add("Category", settingCategoryName);
            //}
            //await Countly.RecordEvent("openSystemSettings", 1, segmentation);
            await Countly.RecordEvent("openSystemSettings" + settingCategoryName);
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
