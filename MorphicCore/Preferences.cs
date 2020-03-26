using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace MorphicCore
{
    public class Preferences
    {
        public string Id { get; set; } = "";
        public string? UserId { get; set; }

        /// <summary>The user's default preferences</summary>
        // Stored as a serialized JSON string in the mongo database because keys might contain dots,
        // and mongoDB doesn't allow dots in field keys.  Since we're unlikely to need to run queries
        // within the solution preferences, we don't lose any functionality by storing serialized JSON.
        public Dictionary<string, SolutionPreferences>? Default { get; set; }

        public void Set(string solution, string preference, object? value)
        {
            if (Default == null)
            {
                Default = new Dictionary<string, SolutionPreferences>();
            }
            if (!Default.ContainsKey(solution))
            {
                Default[solution] = new SolutionPreferences();
            }
            Default[solution].Values[preference] = value;
        }

        public object? Get(string solution, string preference)
        {
            if (Default != null)
            {
                if (Default.TryGetValue(solution, out var preferencesSet))
                {
                    if (preferencesSet.Values.TryGetValue(preference, out var value))
                    {
                        return value;
                    }
                }
            }
            return null;
        }
    }

    /// <summary>Stores preferences for a specific solution</summary>
    public class SolutionPreferences
    {
        /// <summary>Arbitrary preferences specific to the solution</summary>
        [JsonExtensionData]
        public Dictionary<string, object?> Values { get; set; } = new Dictionary<string, object?>();
    }
}
