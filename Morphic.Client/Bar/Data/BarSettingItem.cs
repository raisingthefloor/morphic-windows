namespace Morphic.Client.Bar.Data
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Extensions.DependencyInjection;
    using Newtonsoft.Json;
    using Settings.SettingsHandlers;
    using Settings.SolutionsRegistry;
    using UI.BarControls;

    [JsonTypeName("setting")]
    [BarControl(typeof(MultiButtonBarControl))]
    public class BarSettingItem : BarMultiButton
    {
        [JsonProperty("configuration.settingId", Required = Required.Always)]
        public string SettingId { get; }

        public Setting Setting { get; private set; }

        protected Solutions Solutions { get; private set; }

        public BarSettingItem(BarData bar, [JsonProperty("configuration.settingId")] string settingId) : base(bar)
        {
            this.Solutions = this.Bar.ServiceProvider.GetRequiredService<Solutions>();
            this.SettingId = settingId;

            this.ApplySetting();

        }

        private void ApplySetting()
        {
            this.Setting = this.Solutions.GetSetting(this.SettingId);

            if (this.Setting.Range != null)
            {
                this.Type = MultiButtonType.Additive;
                this.Buttons["dec"] = new ButtonInfo()
                {
                    Text = "+"
                };
                this.Buttons["inc"] = new ButtonInfo()
                {
                    Text = "-"
                };
            }
            else if (this.SettingId.EndsWith("enabled"))
            {
                this.Type = MultiButtonType.Toggle;
                this.Buttons["off"] = new ButtonInfo()
                {
                    Text = "Off"
                };
                this.Buttons["on"] = new ButtonInfo()
                {
                    Text = "On"
                };
            }


        }

        public override void Deserialized()
        {
            base.Deserialized();

        }
    }
}
