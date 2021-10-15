namespace Morphic.Client.Bar.UI.BarControls
{
    using System.Collections.Generic;
    using System.Windows.Controls;
    using Menu;

    public class BarContextMenu
    {

        private static readonly string SettingsFormat = "ms-settings:{0}";
        private static readonly string DemoFormat = "https://morphic.org/rd/{0}-vid";
        private static readonly string LearnMoreFormat = "https://morphic.org/rd/{0}";

        public static ContextMenu? CreateContextMenu(Dictionary<string, string> items, string? telemetryCategory = null)
        {
            ContextMenu menu = new ContextMenu();

            foreach ((string? name, string? target) in items)
            {
                string? format, finalName;
                MorphicMenuItem.MorphicMenuItemTelemetryType? telemetryType;
                switch (name)
                {
                    case "learn":
                        format = BarContextMenu.LearnMoreFormat;
                        finalName = "_Learn more";
                        telemetryType = MorphicMenuItem.MorphicMenuItemTelemetryType.LearnMore;
                        break;
                    case "demo":
                        format = BarContextMenu.DemoFormat;
                        finalName = "Quick _Demo video";
                        telemetryType = MorphicMenuItem.MorphicMenuItemTelemetryType.QuickDemoVideo;
                        break;
                    case "settings":
                    case "setting":
                        format = BarContextMenu.SettingsFormat;
                        finalName = "_Settings";
                        telemetryType = MorphicMenuItem.MorphicMenuItemTelemetryType.Settings;
                        break;
                    default:
                        format = null;
                        finalName = name;
                        telemetryType = null;
                        break;
                }

                string finalTarget = (format is null)
                    ? target
                    : string.Format(format, target);

                MorphicMenuItem item = new MorphicMenuItem()
                {
                    Header = finalName,
                    Open = finalTarget,
                    ParentMenuType = MorphicMenuItem.MenuType.contextMenu,
                    TelemetryType = telemetryType,
                    TelemetryCategory = (telemetryType is not null) ? telemetryCategory : null
                };

                menu.Items.Add(item);
            }

            return menu.Items.Count > 0
                ? menu
                : null;
        }
    }
}
