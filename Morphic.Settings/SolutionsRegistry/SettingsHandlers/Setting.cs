namespace Morphic.Settings.SolutionsRegistry.SettingsHandlers
{
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

        /// <summary>Gets the value of this setting.</summary>
        public Task<object?> GetValue()
        {
            return this.SettingGroup.SettingsHandler.Get(this);
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
}
