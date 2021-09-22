namespace Morphic.Client.Bar.Data.Actions
{
    using Microsoft.Extensions.DependencyInjection;
    using Morphic.Core;
    using Newtonsoft.Json;
    using Settings.SettingsHandlers;
    using Settings.SolutionsRegistry;
    using System.Threading.Tasks;

    [JsonTypeName("setting")]
    public class SettingAction : BarAction
    {
        [JsonProperty("settingId", Required = Required.Always)]
        public string SettingId { get; set; } = string.Empty;

        public Setting? Setting { get; private set; }
        public Solutions Solutions { get; private set; } = null!;

        protected override Task<IMorphicResult> InvokeAsyncImpl(string? source = null, bool? toggleState = null)
        {
            Setting? setting;

            if (this.Setting == null && !string.IsNullOrEmpty(source))
            {
                setting = this.Solutions.GetSetting(source);
                setting.SetValueAsync(toggleState);
            }
            else
            {
                setting = this.Setting;
            }

            if (setting == null)
            {
                return Task.FromResult(IMorphicResult.SuccessResult);
            }

            switch (source)
            {
                case "inc":
                    return setting.Increment(1);
                case "dec":
                    return setting.Increment(-1);
                case "on":
                    return setting.SetValueAsync(true);
                case "off":
                    return setting.SetValueAsync(false);
            }

            return Task.FromResult(IMorphicResult.ErrorResult);
        }

        public async Task<bool> CanExecute(string id)
        {
            bool canExecute = true;

            if (Setting?.Range != null)
            {
                var range = Setting.Range;
                var idRequiresCountRefresh = Setting.Id == "zoom";

                var min = await range.GetMinInt(0, idRequiresCountRefresh);
                var max = await range.GetMaxInt(0, idRequiresCountRefresh) - 1;

                var currentValue = (int)(Setting.CurrentValue ?? 0);

                if (id == "inc" && currentValue >= max)
                {
                    canExecute = false;
                }
                else if (id == "dec" && currentValue <= min)
                {
                    canExecute = false;
                }
            }

            return canExecute;
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
