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

        public async Task<bool> Increment(int direction)
        {
            if (this.Range != null)
            {
                int current = await this.GetValue<int>();
                current += Math.Sign(direction) * this.Range.IncrementValue;
                if (current > await this.Range.GetMin() && current < await this.Range.GetMax())
                {
                    return await this.SetValue(current);
                }
            }

            return false;
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
        private int? minValue;
        private int? maxValue;

        [JsonProperty("min", Required = Required.Always)]
        private Limit Min { get; set; } = null!;

        [JsonProperty("max", Required = Required.Always)]
        private Limit Max { get; set; } = null!;

        [JsonProperty("inc")]
        public int IncrementValue { get; private set; } = 1;

        [JsonProperty("live")]
        public bool Live { get; private set; }

        public async Task<int> GetMin(int defaultResult = 0)
        {
            if (this.Live || !this.minValue.HasValue)
            {
                this.minValue = await this.Min.Get(defaultResult);
            }

            return this.minValue.Value;
        }

        public async Task<int> GetMax(int defaultResult = 0)
        {
            if (this.Live || !this.maxValue.HasValue)
            {
                this.maxValue = await this.Max.Get(defaultResult);
            }

            return this.maxValue.Value;
        }

        public Setting Setting { get; private set; } = null!;

        public void Deserialized(Setting setting)
        {
            this.Setting = setting;
            this.Min.Deserialized(this.Setting);
            this.Max.Deserialized(this.Setting);
        }

        private class Limit
        {
            private Setting? setting;
            private Setting? parentSetting;
            private string? settingId;
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

                if (this.settingId != null && this.setting == null)
                {
                    this.setting = this.parentSetting?.SettingGroup.Solution.ResolveSettingId(this.settingId);
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

            public void Deserialized(Setting parent)
            {
                this.parentSetting = parent;
                if (this.expression != null)
                {
                    // Parse the expression.
                    Match match = this.parseLimit.Match(this.expression.Trim());
                    this.settingId = match.Groups["setting"].Value;
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
