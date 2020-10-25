namespace Morphic.Settings.SettingsHandlers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    [JsonObject(MemberSerialization.OptIn)]
    public class Setting
    {
        public SettingGroup SettingGroup { get; private set; } = null!;
        public string Id { get; private set; } = null!;

        [JsonProperty("name")]
        public string Name { get; private set; } = string.Empty;

        [JsonProperty("dataType")]
        public string? Type { get; private set; }

        [JsonProperty("range")]
        public SettingRange? Range { get; private set; }

        /// <summary>Gets the value of this setting.</summary>
        public Task<object?> GetValue()
        {
            return this.SettingGroup.SettingsHandler.Get(this);
        }

        /// <summary>Gets the value of this setting.</summary>
        public async Task<T> GetValue<T>(T defaultValue = default)
        {
            object? value = await this.GetValue();
            if (value is T v)
            {
                return v;
            }
            else
            {
                return defaultValue;
            }
        }

        /// <summary>Sets the value of this setting.</summary>
        public Task<bool> SetValue(object? newValue)
        {
            return this.SettingGroup.SettingsHandler.Set(this, newValue);
        }

        public virtual void Deserialized(SettingGroup settingGroup, string settingId)
        {
            this.SettingGroup = settingGroup;
            this.Id = settingId;
            if (string.IsNullOrEmpty(this.Name))
            {
                this.Name = this.Id;
            }

            this.Range?.Deserialized(this);
        }

        public static explicit operator Setting(string compact)
        {
            string[] parts = compact.Split(':', 2);
            Setting setting = new Setting()
            {
                Name = parts[0],
            };
            if (parts.Length > 1)
            {
                setting.Type = parts[2];
            }

            return setting;
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class SettingRange
    {
        [JsonProperty("from", Required = Required.Always)]
        private Limit From { get; set; } = null!;

        [JsonProperty("to", Required = Required.Always)]
        private Limit To { get; set; } = null!;

        [JsonProperty("inc")]
        public int Increment { get; private set; }

        public Task<int> GetMin(int defaultResult = 0)
        {
            return this.From.Get(defaultResult);
        }
        public Task<int> GetMax(int defaultResult = 0)
        {
            return this.To.Get(defaultResult);
        }

        public Setting Setting { get; private set; } = null!;

        public void Deserialized(Setting setting)
        {
            this.Setting = setting;
            this.From.Deserialized(this.Setting);
        }

        private class Limit
        {
            private Setting? setting;
            private string? expression;
            private int? value;
            private int increment;
            private Limit? defaultValue;

            // "settingId [ (+|-) increment] [ ? default]"
            private readonly Regex parseLimit = new Regex(@"^(?<setting>\S+)(\s+(?<sign>[-+])\s*(?<increment>\S+)?(\s*\?\s*(?<default>\S+)))?$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

            protected Limit()
            {
            }

            public async Task<int> Get(int defaultResult = 0)
            {
                if (this.value.HasValue)
                {
                    return this.value.Value;
                }

                if (this.setting != null)
                {
                    int num = await this.setting.GetValue(this.defaultValue?.value ?? int.MinValue);
                    if (num != int.MinValue)
                    {
                        return num;
                    }
                }

                if (this.defaultValue != null)
                {
                    return await this.defaultValue.Get();
                }

                return defaultResult;
            }

            private static Limit FromString(string expr)
            {
                if (int.TryParse(expr, out int number))
                {
                    return new Limit() { value = number };
                }

                return new Limit() { expression = expr };
            }

            public static implicit operator Limit(int number) => new Limit() { value = number };
            public static implicit operator Limit(long number) => new Limit() { value = (int)number };
            public static implicit operator Limit(string expr) => FromString(expr);

            public void Deserialized(Setting parentSetting)
            {
                if (this.expression != null)
                {
                    // Parse the expression.
                    Match match = this.parseLimit.Match(this.expression.Trim());
                    string settingPath = match.Groups["setting"].Value;
                    if (match.Groups["increment"].Success)
                    {
                        this.increment = ((IConvertible)match.Groups["increment"].Value).ToInt32(null);
                        if (match.Groups["sign"].Value == "-")
                        {
                            this.increment = -this.increment;
                        }

                        if (match.Groups["default"].Success)
                        {
                            this.defaultValue = FromString(match.Groups["default"].Value);
                        }
                    }

                    this.setting = parentSetting.SettingGroup.Solution.ResolveSettingId(settingPath);
                }
            }
        }

    }

    public static class SettingExtensions
    {
        public static Dictionary<string, Setting> ToDict(this IEnumerable<Setting> settings)
        {
            return settings.ToDictionary(setting => setting.Name, setting => setting);
        }
    }
}
