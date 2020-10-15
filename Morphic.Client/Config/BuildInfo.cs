namespace Morphic.Client.Config
{
    using System.IO;
    using System.Reflection;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public class BuildInfo
    {
        public static BuildInfo Current { get; } = BuildInfo.FromJsonFile(AppPaths.GetAppFile("build-info.json"));

        [JsonIgnore]
        public Features Features { get; } = new Features();

        [JsonIgnore]
        public string Version => Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown version";

        [JsonIgnore]
        public string InformationalVersion =>
            Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? "unknown version";

        [JsonPropertyName("buildTime")]
        public string BuildTime { get; set; } = null!;

        [JsonPropertyName("commit")]
        public string Commit { get; set; } = null!;

        protected BuildInfo()
        {
        }
        
        public static BuildInfo FromJsonFile(string path)
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<BuildInfo>(json);
        }
    }
}
