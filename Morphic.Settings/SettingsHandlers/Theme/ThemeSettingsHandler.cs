namespace Morphic.Settings.SettingsHandlers.Theme
{
    using Morphic.WindowsNative;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using DotNetWindowsRegistry;
    using Ini;
    using Microsoft.Extensions.Logging;
    using Microsoft.Win32;
    using SolutionsRegistry;
    using System.Threading;
    using System.Diagnostics;

    [SrService]
    public class ThemeSettingsHandler : FixedSettingsHandler
    {
        private readonly ILogger<ThemeSettingsHandler> logger;
        private readonly IRegistry registry;

        private readonly static SemaphoreSlim s_setHighContrastSemaphore = new SemaphoreSlim(1, 1);

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
        private async Task SaveCurrentTheme(string currentThemeFile, string saveAs)
        {
            this.logger.LogInformation($"Saving current theme, using {currentThemeFile}.");

            bool isValid = false;
            Dictionary<string, string>? themeData = null;

            try
            {
                // Read the .theme file that's currently being used.
                Ini themeReader = new();
                await themeReader.ReadFile(currentThemeFile);
                themeData = themeReader.ReadData();
                isValid = themeData.ContainsKey("MasterThemeSelector.MTSM");
            }
            catch (IOException e)
            {
                this.logger.LogInformation(e, $"Unable to read {currentThemeFile}.");
            }

            if (!isValid || themeData is null)
            {
                string defaultTheme = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    "resources\\Themes\\aero.theme");
                if (currentThemeFile != defaultTheme)
                {
                    // OBSERVATION: this code is re-entrant; also note that if the aero.theme file is corrupt...this might reenter infinitely until the stack was full
                    await this.SaveCurrentTheme(defaultTheme, saveAs);
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

            // if the directory for the saveAs file doesn't exist, create it now
            var saveAsPath = Path.GetDirectoryName(saveAs);
            if (Directory.Exists(saveAsPath) == false)
            {
                Directory.CreateDirectory(saveAsPath);
            }

            Ini writer = new();
            await writer.ReadFile(currentThemeFile);
            writer.WriteData(themeData!);
            await writer.WriteFile(saveAs);

            // Make windows use this theme file when restoring high-contrast.
            using IRegistryKey highContrastKey =
                hKeyCurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\HighContrast");
            highContrastKey.SetValue("Pre-High Contrast Scheme", saveAs);
        }

        [Setter("highContrastEnabled")]
        public async Task<bool> SetHighContrast(Setting setting, object? newValue)
        {
            // capture (or wait on) our "set high contrast" semaphore; we'll release this in the finally block
            await s_setHighContrastSemaphore.WaitAsync();
            try
            {
                var getHighContrastModeIsOnResult = Morphic.WindowsNative.Display.HighContrastUtils.GetHighContrastModeIsOn();
                if (getHighContrastModeIsOnResult.IsError == true)
                {
                    Debug.Assert(false, "ERROR: Could not determine if high contrast mode is on or not.");
                    return false;
                }
                var highContrastModeIsOn = getHighContrastModeIsOnResult.Value!;

                bool newOnState;
                var newValueAsBool = newValue as bool?;
                if (newValueAsBool.HasValue == true)
                {
                    newOnState = newValueAsBool.Value;
                }
                else
                {
                    // default to true (if newValue was null)
                    newOnState = true;
                }
				
                if (!highContrastModeIsOn)
                {
                    // Save the current theme, otherwise windows will use the last saved theme when restoring the contrast
                    // mode.
                    ThemeSettingGroup settingGroup = (ThemeSettingGroup)setting.SettingGroup;
                    await this.SaveCurrentTheme(settingGroup.CurrentTheme, settingGroup.SavedTheme);
                }

                var setHighContrastModeIsOnResult = Morphic.WindowsNative.Display.HighContrastUtils.SetHighContrastModeIsOn(newOnState);
                if (setHighContrastModeIsOnResult.IsError == true)
                {
                    Debug.Assert(false, "ERROR: Could not turn high contrast mode on/off.");
                    return false;
                }

                return true;
            }
            finally
            {
                s_setHighContrastSemaphore.Release();
            }
        }

        [Getter("highContrastEnabled")]
        public async Task<object?> GetHighContrast(Setting setting)
        {
            var getHighContrastModeIsOnResult = Morphic.WindowsNative.Display.HighContrastUtils.GetHighContrastModeIsOn();
            if (getHighContrastModeIsOnResult.IsError == true)
            {
                Debug.Assert(false, "ERROR: Could not determine if high contrast mode is on or not.");
                return null;
            }
            var highContrastModeIsOn = getHighContrastModeIsOnResult.Value!;

            return highContrastModeIsOn;
        }
    }
}
