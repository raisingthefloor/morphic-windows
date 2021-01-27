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

        public static ContextMenu? CreateContextMenu(Dictionary<string, string> items)
        {
            ContextMenu menu = new ContextMenu();

            foreach ((string? name, string? target) in items)
            {
                string? format, finalName;
                switch (name)
                {
                    case "learn":
                        format = BarContextMenu.LearnMoreFormat;
                        finalName = "_Learn more";
                        break;
                    case "demo":
                        format = BarContextMenu.DemoFormat;
                        finalName = "Quick _Demo video";
                        break;
                    case "settings":
                    case "setting":
                        format = BarContextMenu.SettingsFormat;
                        finalName = "_Settings";
                        break;
                    default:
                        format = null;
                        finalName = name;
                        break;
                }

                string finalTarget = (format == null)
                    ? target
                    : string.Format(format, target);

                MorphicMenuItem item = new MorphicMenuItem()
                {
                    Header = finalName,
                    Open = finalTarget,
                    ParentMenuType = MorphicMenuItem.MenuType.contextMenu
                };

                menu.Items.Add(item);
            }

            return menu.Items.Count > 0
                ? menu
                : null;
        }
    }
}
