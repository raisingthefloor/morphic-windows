// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt
//
// The R&D leading to these results received funding from the:
// * Rehabilitation Services Administration, US Dept. of Education under 
//   grant H421A150006 (APCP)
// * National Institute on Disability, Independent Living, and 
//   Rehabilitation Research (NIDILRR)
// * Administration for Independent Living & Dept. of Education under grants 
//   H133E080022 (RERC-IT) and H133E130028/90RE5003-01-00 (UIITA-RERC)
// * European Union's Seventh Framework Programme (FP7/2007-2013) grant 
//   agreement nos. 289016 (Cloud4all) and 610510 (Prosperity4All)
// * William and Flora Hewlett Foundation
// * Ontario Ministry of Research and Innovation
// * Canadian Foundation for Innovation
// * Adobe Foundation
// * Consumer Electronics Association Foundation

using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

namespace Morphic.Core
{
    public class Preferences: IRecord
    {

        /// <summary>
        /// Default constructor
        /// </summary>
        public Preferences()
        {
        }

        /// <summary>
        /// Copy Constructor
        /// </summary>
        /// <param name="other"></param>
        public Preferences(Preferences other)
        {
            if (other.Default is Dictionary<string, SolutionPreferences> otherDefault)
            {
                Default = new Dictionary<string, SolutionPreferences>();
                foreach (var pair in otherDefault)
                {
                    Default.Add(pair.Key, new SolutionPreferences(pair.Value));
                }
            }
        }

        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("user_id")]
        public string? UserId { get; set; }

        /// <summary>The user's default preferences</summary>
        // Stored as a serialized JSON string in the mongo database because keys might contain dots,
        // and mongoDB doesn't allow dots in field keys.  Since we're unlikely to need to run queries
        // within the solution preferences, we don't lose any functionality by storing serialized JSON.
        [JsonPropertyName("default")]
        public Dictionary<string, SolutionPreferences>? Default { get; set; }

        public struct Key
        {
            public string Solution;
            public string Preference;

            public Key(string solution, string preference)
            {
                Solution = solution;
                Preference = preference;
            }

            public override string ToString()
            {
                return string.Format("{0}.{1}", Solution, Preference);
            }

            public override int GetHashCode()
            {
                return Solution.GetHashCode() ^ Preference.GetHashCode();
            }

            public override bool Equals(object? obj)
            {
                if (obj is Key other)
                {
                    return Solution == other.Solution && Preference == other.Preference;
                }
                return false;
            }
        }

        public void Set(Key key, object? value)
        {
            if (Default == null)
            {
                Default = new Dictionary<string, SolutionPreferences>();
            }
            if (!Default.ContainsKey(key.Solution))
            {
                Default[key.Solution] = new SolutionPreferences();
            }
            Default[key.Solution].Values[key.Preference] = value;
        }

        public object? Get(Key key)
        {
            if (Default != null)
            {
                if (Default.TryGetValue(key.Solution, out var preferencesSet))
                {
                    if (preferencesSet.Values.TryGetValue(key.Preference, out var value))
                    {
                        return value;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Remove the given key from these preferences
        /// </summary>
        /// <param name="key"></param>
        public void Remove(Key key)
        {
            if (Default != null)
            {
                if (Default.TryGetValue(key.Solution, out var preferencesSet))
                {
                    preferencesSet.Values.Remove(key.Preference);
                    if (preferencesSet.Values.Count == 0)
                    {
                        Default.Remove(key.Solution);
                    }
                }
            }
        }

        /// <summary>
        /// Get a flat set of preference values keyed by Preferences.Key
        /// </summary>
        /// <returns></returns>
        public Dictionary<Key, object?> GetValuesByKey()
        {
            var valuesByKey = new Dictionary<Key, object?>();
            if (Default != null)
            {
                foreach (var solutionPair in Default)
                {
                    foreach (var preferencePair in solutionPair.Value.Values)
                    {
                        var key = new Key(solutionPair.Key, preferencePair.Key);
                        valuesByKey.Add(key, preferencePair.Value);
                    }
                }
            }
            return valuesByKey;
        }
    }

    /// <summary>Stores preferences for a specific solution</summary>
    public class SolutionPreferences
    {
        /// <summary>Arbitrary preferences specific to the solution</summary>
        [JsonExtensionData]
        public Dictionary<string, object?> Values { get; set; } = new Dictionary<string, object?>();

        /// <summary>
        /// Default constructor
        /// </summary>
        public SolutionPreferences()
        {
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="other"></param>
        public SolutionPreferences(SolutionPreferences other)
        {
            Values = new Dictionary<string, object?>(other.Values);
        }
    }
}
