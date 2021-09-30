namespace Morphic.Settings.SettingsHandlers.XML
{
    using Newtonsoft.Json;
    using System.Collections.Generic;

    [SettingsHandlerType("XML", typeof(XMLSettingsHandler))]
    class XMLSettingGroup : SettingGroup
    {
        [JsonProperty("encoding")]
        public string encoding { get; private set; } = "";

        [JsonProperty("xml-tag")]
        public string xmlTag { get; private set; } = "";
    }
}
