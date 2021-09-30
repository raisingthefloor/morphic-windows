namespace Morphic.Settings.SettingsHandlers.SPI
{
    using Newtonsoft.Json;
    using System.Collections.Generic;

    [SettingsHandlerType("SPI", typeof(SPISettingsHandler))]
    public class SPISettingGroup : SettingGroup
    {
        [JsonProperty("getAction")]
        public string getAction { get; private set; } = "";

        [JsonProperty("setAction")]
        public string setAction { get; private set; } = "";

        [JsonProperty("uiParam")]
        public object? uiParam { get; private set; } = null;

        [JsonProperty("pvParam")]
        public Dictionary<string, object> pvParam { get; private set; } = new Dictionary<string, object>();

        [JsonProperty("fWinIni")]
        public string fWinIni { get; private set; } = "";
    }
}
