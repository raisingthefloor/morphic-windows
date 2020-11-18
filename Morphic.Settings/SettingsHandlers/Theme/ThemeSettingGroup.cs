namespace Morphic.Settings.SettingsHandlers.Theme
{
    using Newtonsoft.Json;
    using Resolvers;

    [SettingsHandlerType("themeSettings", typeof(ThemeSettingsHandler))]
    public class ThemeSettingGroup : SettingGroup
    {
        /// <summary>The file that contains the current .theme file used by the system.</summary>
        [JsonProperty("currentTheme", Required = Required.Always)]
        public ResolvingString CurrentTheme { get; set; } = null!;

        /// <summary>The file that contains the current .theme file used by the system.</summary>
        [JsonProperty("savedTheme", Required = Required.Always)]
        public ResolvingString SavedTheme { get; set; } = null!;
    }
}
