namespace Morphic.Client.Bar.Data.Actions
{
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

        public Setting? Setting { get; private set; }
        public Solutions Solutions { get; private set; } = null!;

        protected override Task<bool> InvokeImpl(string? source = null, bool? toggleState = null)
        {
            Setting? setting;

            if (this.Setting == null && !string.IsNullOrEmpty(source))
            {
                setting = this.Solutions.GetSetting(source);
                setting.SetValue(toggleState);
            }
            else
            {
                setting = this.Setting;
            }

            if (setting == null)
            {
                return Task.FromResult(true);
            }

            switch (source)
            {
                case "inc":
                    return setting.Increment(1);
                case "dec":
                    return setting.Increment(-1);
                case "on":
                    return setting.SetValue(true);
                case "off":
                    return setting.SetValue(false);
            }

            return Task.FromResult(false);
        }

        public override void Deserialized(BarData bar)
        {
            base.Deserialized(bar);

            this.Solutions = bar.ServiceProvider.GetRequiredService<Solutions>();
            if (!string.IsNullOrEmpty(this.SettingId))
            {
                this.Setting = this.Solutions.GetSetting(this.SettingId);
            }
        }
    }
}
