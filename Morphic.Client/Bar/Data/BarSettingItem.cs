namespace Morphic.Client.Bar.Data
{
    using Microsoft.Extensions.DependencyInjection;
    using Newtonsoft.Json;
    using Settings.SettingsHandlers;
    using Settings.SolutionsRegistry;
    using UI.BarControls;

    /// <summary>
    /// A bar item that handles a setting in the solutions registry.
    /// </summary>
    [JsonTypeName("setting")]
    [BarControl(typeof(MultiButtonBarControl))]
    public class BarSettingItem : BarMultiButton
    {
        [JsonProperty("configuration.settingId", Required = Required.Always)]
        public string? SettingId { get; }

        public Solutions Solutions { get; private set; }

        public BarSettingItem(BarData bar, [JsonProperty("configuration.settingId")] string settingId) : base(bar)
        {
            this.Solutions = this.Bar.ServiceProvider.GetRequiredService<Solutions>();
            this.SettingId = settingId;

            this.ApplySetting();
        }

        private void ApplySetting()
        {
            if (!string.IsNullOrEmpty(this.SettingId))
            {
                // Bar item is a pair of on/off or up/down buttons for a single setting.
                Setting setting = this.Solutions.GetSetting(this.SettingId);

                if (setting.Range is not null)
                {
                    this.Type = MultiButtonType.Additive;
                    this.Buttons["dec"] = new ButtonInfo()
                    {
                        Text = "-"
                    };
                    this.Buttons["inc"] = new ButtonInfo()
                    {
                        Text = "+"
                    };
                }
                else
                {
                    this.Type = MultiButtonType.Toggle;
                    this.Buttons["on"] = new ButtonInfo()
                    {
                        Text = "On"
                    };
                    this.Buttons["off"] = new ButtonInfo()
                    {
                        Text = "Off"
                    };
                }
            }
        }

        private void ToggleButtons()
        {
            foreach ((string? key, ButtonInfo? value) in this.Buttons)
            {
                value.Toggle = true;
            }
        }

        public override void Deserialized()
        {
            base.Deserialized();
            if (string.IsNullOrEmpty(this.SettingId))
            {
                this.ToggleButtons();
            }

        }
    }
}
