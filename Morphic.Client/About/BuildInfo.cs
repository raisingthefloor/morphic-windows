using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Reflection;

namespace Morphic.Client.About
{
    public class BuildInfo
    {

        public string Version
        {
            get
            {
                return Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown version";
            }
        }

        public string InformationalVersion
        {
            get
            {
                return Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown version";
            }
        }

        [JsonPropertyName("buildTime")]
        public string BuildTime { get; set; } = null!;

        [JsonPropertyName("commit")]
        public string Commit { get; set; } = null!;

        public static BuildInfo FromJsonFile(string path)
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<BuildInfo>(json);
        }
    }
}
