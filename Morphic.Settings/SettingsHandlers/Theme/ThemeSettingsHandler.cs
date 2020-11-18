namespace Morphic.Settings.SettingsHandlers.Theme
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Windows.Native;
    using DotNetWindowsRegistry;
    using Ini;
    using Microsoft.Extensions.Logging;
    using Microsoft.Win32;
    using SolutionsRegistry;

    [SrService]
    public class ThemeSettingsHandler : FixedSettingsHandler
    {
        private readonly ILogger<ThemeSettingsHandler> logger;
        private readonly IRegistry registry;

        public ThemeSettingsHandler(ILogger<ThemeSettingsHandler> logger, IRegistry registry)
        {
            this.logger = logger;
            this.registry = registry;
        }

        /// <summary>
        /// Saves the active changes to the current theme.
        ///
        /// This is needed to be performed before enabling high-contrast because when high-contrast is de-activated it
        /// loads the settings (such as the wallpaper) from the last used .theme file, rather than the applied settings.
        ///
        /// These means, any unsaved theme customisations will be lost.
        ///
        /// Theme files are described in https://docs.microsoft.com/en-us/windows/desktop/controls/themesfileformat-overview
        /// </summary>
        /// <param name="currentThemeFile">
        /// The current theme file used by the OS. This will be used as a base to create a new theme file, and currently
        /// applied settings will be added to it.</param>
        /// <param name="saveAs">The file to write the saved theme to.</param>
        private void SaveCurrentTheme(string currentThemeFile, string saveAs)
        {
            this.logger.LogInformation($"Saving current theme, using {currentThemeFile}.");

            bool isValid = false;
            Dictionary<string, string>? themeData = null;

            try
            {
                // Read the .theme file that's currently being used.
                IniFileReader themeReader = new IniFileReader();
                themeReader.SetFile(currentThemeFile);
                themeData = themeReader.ReadData();
                isValid = themeData.ContainsKey("MasterThemeSelector.MTSM");
            }
            catch (IOException e)
            {
                this.logger.LogInformation(e, $"Unable to read {currentThemeFile}.");
            }

            if (!isValid || themeData == null)
            {
                string defaultTheme = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    "resources\\Themes\\aero.theme");
                if (currentThemeFile != defaultTheme)
                {
                    this.SaveCurrentTheme(defaultTheme, saveAs);
                }
                return;
            }
            else if (themeData.ContainsKey("VisualStyles.HighContrast"))
            {
                // Only save the current theme if it is not high-contrast.
                return;
            }

            using IRegistryKey hKeyCurrentUser =
                this.registry.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);

            // Wallpaper
            using IRegistryKey desktopKey = hKeyCurrentUser.OpenSubKey(@"Control Panel\Desktop", false);

            themeData["Theme.DisplayName"] = "morphic";

                themeData["Control Panel\\Desktop.Wallpaper"] =
                desktopKey.GetValue("WallPaper")?.ToString() ?? string.Empty;
            themeData["Control Panel\\Desktop.TileWallpaper"] =
                desktopKey.GetValue("TileWallPaper")?.ToString() ?? string.Empty;
            themeData["Control Panel\\Desktop.WallpaperStyle"] =
                desktopKey.GetValue("WallPaperStyle")?.ToString() ?? string.Empty;

            using IRegistryKey wallpapersKey =
                hKeyCurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers");
            string? backgroundType = wallpapersKey.GetValue("backgroundType")?.ToString();
            if (backgroundType != "2")
            {
                // It's not a slide-show, remove the entire section.
                themeData.Keys.Where(k => k.StartsWith("Slideshow"))
                    .ToList()
                    .ForEach(k => themeData.Remove(k));
            }

            // Colours
            using IRegistryKey colorsKey = hKeyCurrentUser.OpenSubKey(@"Control Panel\Colors");
            foreach (string valueName in colorsKey.GetValueNames())
            {
                if (colorsKey.GetValue(valueName, null) is string value)
                {
                    themeData[$"Control Panel\\Colors.{valueName}"] = value;
                }
            }

            IniFileWriter writer = new IniFileWriter();
            writer.SetFile(currentThemeFile);
            writer.Write(themeData!);
            writer.SaveAs(saveAs);

            // Make windows use this theme file when restoring high-contrast.
            using IRegistryKey highContrastKey =
                hKeyCurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\HighContrast");
            highContrastKey.SetValue("Pre-High Contrast Scheme", saveAs);
        }

        [Setter("highContrastEnabled")]
        public Task<bool> SetHighContrast(Setting setting, object? newValue)
        {
            Spi.HighContrastOptions options = Spi.Instance.GetHighContrast();

            bool enable = newValue as bool? == true;
            bool currentlyEnabled = (options & Spi.HighContrastOptions.HCF_HIGHCONTRASTON) != 0;
            if (!currentlyEnabled)
            {
                // Save the current theme, otherwise windows will use the last saved theme when restoring the contrast
                // mode.
                ThemeSettingGroup settingGroup = (ThemeSettingGroup)setting.SettingGroup;
                this.SaveCurrentTheme(settingGroup.CurrentTheme, settingGroup.SavedTheme);
            }

            if (enable)
            {
                options |= Spi.HighContrastOptions.HCF_HIGHCONTRASTON;
            }
            else
            {
                options &= ~Spi.HighContrastOptions.HCF_HIGHCONTRASTON;
            }

            Spi.Instance.SetHighContrast(options);

            return Task.FromResult(true);
        }

        [Getter("highContrastEnabled")]
        public async Task<object?> GetHighContrast(Setting setting)
        {
            Spi.HighContrastOptions options = Spi.Instance.GetHighContrast();
            return (options & Spi.HighContrastOptions.HCF_HIGHCONTRASTON) != 0;
        }
    }
}
