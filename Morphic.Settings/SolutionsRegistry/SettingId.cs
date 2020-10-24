namespace Morphic.Settings.SolutionsRegistry
{

    public class SettingId
    {
        public static SettingId ColorFiltersEnabled = new SettingId("com.microsoft.windows.colorFilters", "enabled");
        public static SettingId ColorFiltersFilterType = new SettingId("com.microsoft.windows.colorFilters", "filterType");
        public static SettingId HighContrastEnabled = new SettingId("com.microsoft.windows.highContrast", "enabled");
        public static SettingId NarratorEnabled = new SettingId("com.microsoft.windows.narrator", "enabled");

        public string Solution { get; }
        public string Setting { get; }

        public SettingId(string solutionId, string settingId)
        {
            this.Solution = solutionId;
            this.Setting = settingId;
        }

        public override string ToString()
        {
            return $"{this.Solution}/{this.Setting}";
        }
    }
}
