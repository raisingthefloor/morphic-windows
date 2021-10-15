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

        protected override Task<MorphicResult<MorphicUnit, MorphicUnit>> InvokeAsyncImpl(string? source = null, bool? toggleState = null)
        {
            Setting? setting;

            if (this.Setting is null && !string.IsNullOrEmpty(source))
            {
                setting = this.Solutions.GetSetting(source);
                // OBSERVATION: we do not await on this call; we may want to do so
                setting.SetValueAsync(toggleState);
            }
            else
            {
                setting = this.Setting;
            }

            if (setting is null)
            {
                MorphicResult<MorphicUnit, MorphicUnit> okResult = MorphicResult.OkResult();
                return Task.FromResult(okResult);
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

            MorphicResult<MorphicUnit, MorphicUnit> errorResult = MorphicResult.ErrorResult();
            return Task.FromResult(errorResult);
        }

        public async Task<bool> CanExecute(string id)
        {
            bool canExecute = true;

            if (Setting?.Range is not null)
            {
                var range = Setting.Range;
                var idRequiresCountRefresh = Setting.Id == "zoom";

                var min = await range.GetMin(0, idRequiresCountRefresh);
                var max = await range.GetMax(0, idRequiresCountRefresh) - 1;

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
