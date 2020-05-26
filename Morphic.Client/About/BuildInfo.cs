using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Morphic.Client.About
{
    public class BuildInfo
    {

        [JsonPropertyName("version")]
        public string Version { get; set; } = null!;

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
