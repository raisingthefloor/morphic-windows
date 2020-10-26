namespace Morphic.Settings.SettingsHandlers.Process
{
    using System.Collections.Generic;
    using Newtonsoft.Json;

    [SettingsHandlerType("process", typeof(ProcessSettingsHandler))]
    public class ProcessSettingGroup : SettingGroup
    {
        [JsonProperty("args")]
        public List<string> Arguments { get; private set; } = new List<string>();

        [JsonProperty("env")]
        public Dictionary<string, string> Environment { get; private set; } = new Dictionary<string, string>();
    }
}
