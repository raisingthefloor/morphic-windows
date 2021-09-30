namespace Morphic.Settings.SettingsHandlers.WMI
{
    using System.Collections.Generic;
    using Newtonsoft.Json;

    [SettingsHandlerType("WMI", typeof(WMISettingsHandler))]
    public class WMISettingGroup : SettingGroup
    {
        [JsonProperty("namespace")]
        public string wmiNamespace { get; private set; } = "";

        [JsonProperty("getClassName")]
        public string getClassName { get; private set; } = "";

        [JsonProperty("getProperty")]
        public string getProperty { get; private set; } = "";

        [JsonProperty("getExtra")]
        public string getExtra { get; private set; } = "";

        [JsonProperty("setClassName")]
        public string setClassName { get; private set; } = "";

        [JsonProperty("setMethod")]
        public string setMethod { get; private set; } = "";

        [JsonProperty("setParams")]
        public object[] setParams { get; private set; } = { };

        [JsonProperty("setReturnVal")]
        public object[] setReturnVal { get; private set; } = { };

        [JsonProperty("settingType")]
        public string settingType { get; private set; } = "";
    }
}
