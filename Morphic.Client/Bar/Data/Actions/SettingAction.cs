namespace Morphic.Client.Bar.Data.Actions
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Newtonsoft.Json;
    using Settings.SettingsHandlers;
    using Settings.SolutionsRegistry;

    [JsonTypeName("setting")]
    public class SettingAction : BarAction
    {
        [JsonProperty("settingId", Required = Required.Always)]
        public string SettingId { get; set; } = string.Empty;

        public Setting Setting { get; private set; } = null!;
        public Solutions Solutions { get; private set; } = null!;

        protected override Task<bool> InvokeImpl(string? source = null)
        {
            switch (source)
            {
                case "inc":
                    return this.Setting.Increment(1);
                case "dec":
                    return this.Setting.Increment(-1);
                case "on":
                    return this.Setting.SetValue(true);
                case "off":
                    return this.Setting.SetValue(false);
            }

            return Task.FromResult(false);
        }

        public override void Deserialized(BarData bar)
        {
            base.Deserialized(bar);

            this.Solutions = bar.ServiceProvider.GetRequiredService<Solutions>();
            this.Setting = this.Solutions.GetSetting(this.SettingId);

        }

    }
}
