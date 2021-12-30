// BarMultiButton.cs: Bar item containing multiple buttons.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt


namespace Morphic.Client.Bar.Data
{
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using Actions;
    using Newtonsoft.Json;
    using UI.BarControls;

    /// <summary>
    /// Bar item that contains multiple buttons.
    /// </summary>
    [JsonTypeName("multi")]
    [BarControl(typeof(MultiButtonBarControl))]
    public class BarMultiButton : BarItem
    {
        public BarMultiButton(BarData bar) : base(bar)
        {

        }

        [JsonProperty("configuration.buttons")]
        public Dictionary<string, ButtonInfo> Buttons { get; set; } = new Dictionary<string, ButtonInfo>();

        [JsonProperty("configuration.autoSize")]
        public bool AutoSize { get; set; }

        /// <summary>
        /// Provides hints for keyboard usage.
        /// </summary>
        [JsonProperty("configuration.type")]
        public MultiButtonType Type { get; set; } = MultiButtonType.Auto;


        [JsonObject(MemberSerialization.OptIn)]
        public class ButtonInfo
        {
            private string? value;
            private string? uiName;
            private string? text;
            public BarMultiButton BarItem { get; internal set; } = null!;

            /// <summary>
            /// Display text.
            /// </summary>
            [JsonProperty("label")]
            public string Text
            {
                get => this.text ?? string.Empty;
                set => this.text = value;
            }

            /// <summary>
            /// Unique identifier. Of omitted, the key from BarMultiButton.Buttons is used.
            /// </summary>
            [JsonProperty("id")]
            public string Id { get; set; } = null!;

            /// <summary>
            /// The value to pass to the action when this button is clicked.
            /// Used by `kind = "internal"`, specifying "{button}" as an argument value will resolve to this value
            /// (or the id, if not set)
            /// </summary>
            [JsonProperty("value")]
            public string Value
            {
                get => this.value ?? this.Id;
                set => this.value = value;
            }

            [JsonProperty("action")]
            public BarAction? Action { get; set; }

            [JsonProperty("tooltip")]
            public string? Tooltip { get; set; }

            [JsonProperty("menu")]
            public Dictionary<string, string> Menu { get; set; } = new Dictionary<string, string>();

            [JsonProperty("telemetryCategory")]
            public string? TelemetryCategory { get; set; }

            [JsonProperty("uiName")]
            public string UiName
            {
                get
                {
                    string value = this.uiName ?? this.text ?? this.Tooltip ?? string.Empty;
                    value = value switch
                    {
                        "+" => "up",
                        "-" => "down",
                        _ => value
                    };
                    return value;
                }
                set => this.uiName = value;
            }

            public bool Toggle { get; set; }
        }

        public override void Deserialized()
        {
            base.Deserialized();

            foreach (var (key, buttonInfo) in this.Buttons)
            {
                if ((buttonInfo.Action is null) || (buttonInfo.Action is NoOpAction))
                {
                    buttonInfo.Action = this.Action;
                }
                buttonInfo.BarItem = this;
                buttonInfo.UiName = this.UiName + " " + buttonInfo.UiName;
                if (string.IsNullOrEmpty(buttonInfo.Id))
                {
                    buttonInfo.Id = key;
                }
            }

            if (this.Type == MultiButtonType.Auto)
            {
                this.Type = MultiButtonType.Buttons;
                if (this.Buttons.Count == 2)
                {
                    // Detect if it's an additive/toggle button pair, based on the text
                    Regex additive = new Regex("^([-+]|in|out|up|down|(in|de)c(rease)?)$", RegexOptions.IgnoreCase);
                    Regex toggle = new Regex("^(on|off|yes|no|true|false|(en|dis)abled?)$", RegexOptions.IgnoreCase);

                    foreach (ButtonInfo buttonInfo in this.Buttons.Values)
                    {
                        if (additive.IsMatch(buttonInfo.Text) || additive.IsMatch(buttonInfo.Value))
                        {
                            this.Type = MultiButtonType.Additive;
                            break;
                        }
                        else if (toggle.IsMatch(buttonInfo.Text) || additive.IsMatch(buttonInfo.Value))
                        {
                            this.Type = MultiButtonType.Toggle;
                            break;
                        }
                    }
                }
            }
        }
    }

    public enum MultiButtonType
    {
        Auto,
        /// <summary>Just buttons</summary>
        Buttons,
        /// <summary>-/+</summary>
        Additive,
        /// <summary>On/Off</summary>
        Toggle
    }
}
