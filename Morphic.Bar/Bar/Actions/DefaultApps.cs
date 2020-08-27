namespace Morphic.Bar.Bar.Actions
{
    using System.Collections.Generic;
    using System.IO;
    using Newtonsoft.Json;

    /// <summary>
    /// default-apps.json5
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class DefaultApps : IDeserializable
    {
        private static DefaultApps apps;

        static DefaultApps()
        {
            string defaultAppsFile = AppPaths.GetConfigFile("default-apps.json5", true);

            using StreamReader reader = File.OpenText(defaultAppsFile);
            apps = BarJson.Load<DefaultApps>(reader);
        }

        protected DefaultApps()
        {
        }

        public static BarAction? GetDefaultApp(string name)
        {
            if (!apps.Defaults.TryGetValue(name, out BarAction? action))
            {

            }

            return action;
        }

        [JsonProperty("defaults")]
        public Dictionary<string, BarAction> Defaults = new Dictionary<string, BarAction>();

        public void Deserialized()
        {
        }
    }
}
