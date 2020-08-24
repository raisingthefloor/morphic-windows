// BarItem.cs: An item on a bar.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

namespace Morphic.Bar.Bar
{
    using System;
    using System.Reflection;
    using System.Windows.Media;
    using Actions;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using UI.BarControls;

    /// <summary>
    /// A bar item.
    ///
    /// For items of kind == "action", configuration.identifier is used to lookup an item from actions.js. The object
    /// from there is merged onto this, just before deserialisation.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    [JsonConverter(typeof(TypedJsonConverter), "widget", "button")]
    public class BarItem
    {
        protected ILogger Logger = App.Current.ServiceProvider.GetRequiredService<ILogger<BarItem>>();
        private string? text;

        /// <summary>
        /// The bar that owns this item (set after deserialisation).
        /// </summary>
        public BarData Bar { get; set; } = null!;

        /// <summary>
        /// true if the item is to be displayed on the pull-out bar.
        /// </summary>
        [JsonProperty("is_primary")]
        public bool IsPrimary { get; set; }

        /// <summary>
        /// The original value of IsPrimary (IsPrimary can change if an item over-flows)
        /// </summary>
        public bool IsPrimaryOriginal { get; set; }

        /// <summary>
        /// true if this item is a built-in item, from the default bar json.
        /// </summary>
        public bool IsDefault { get; set; }

        /// <summary>
        /// Don't over-flow this item to the secondary bar.
        /// </summary>
        [JsonProperty("no_overflow")]
        public bool NoOverflow { get; set; }

        [JsonProperty("configuration", ObjectCreationHandling = ObjectCreationHandling.Replace)]
        [JsonConverter(typeof(TypedJsonConverter), "kind", "null")]
        public BarAction Action { get; set; } = new NoOpAction();

        /// <summary>
        /// The text displayed on the item.
        /// </summary>
        [JsonProperty("configuration.label")]
        public string Text
        {
            get => this.text ?? this.DefaultText ?? string.Empty;
            set => this.text = value;
        }

        /// <summary>
        /// The text displayed on the item, if Text is not set.
        /// </summary>
        [JsonProperty("configuration.defaultLabel")]
        public string? DefaultText { get; set; }

        /// <summary>
        /// Tooltip main text (default is the this.Text).
        /// </summary>
        [JsonProperty("configuration.toolTipHeader")]
        public string? ToolTip { get; set; }

        /// <summary>
        /// Tooltip smaller text.
        /// </summary>
        [JsonProperty("configuration.toolTipInfo")]
        public string? ToolTipInfo { get; set; }

        /// <summary>
        /// The background colour (setter from json to allow empty strings).
        /// </summary>
        [JsonProperty("configuration.color")]
        public string ColorValue
        {
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    if (ColorConverter.ConvertFromString(value) is Color color)
                    {
                        this.Color = color;
                    }
                }
            }
            get => "";
        }

        /// <summary>
        /// The background colour.
        /// </summary>
        public Color Color
        {
            get => this.Theme.Background ?? Colors.Transparent;
            set
            {
                this.Theme.Background = value;
                this.Theme.InferStateThemes(true);
            }
        }

        /// <summary>
        /// Don't display this item.
        /// </summary>
        [JsonProperty("hidden")]
        public bool Hidden { get; set; }
        
        /// <summary>
        /// Theme for the item.
        /// </summary>
        [JsonProperty("theme", DefaultValueHandling = DefaultValueHandling.Populate)]
        public BarItemTheme Theme { get; set; } = new BarItemTheme();

        /// <summary>
        /// Items are sorted by this.
        /// </summary>
        [JsonProperty("priority")]
        public int Priority { get; set; }

        [JsonProperty("configuration.size")]
        public BarItemSize Size { get; set; } = BarItemSize.Large;

        /// <summary>
        /// The type of control used. This is specified by using BarControl attribute in a subclass of this.
        /// </summary>
        public Type ControlType => this.GetType().GetCustomAttribute<BarControlAttribute>()?.Type!;

        /// <summary>
        /// Called when the bar has loaded.
        /// </summary>
        /// <param name="bar"></param>
        public virtual void Deserialized(BarData bar)
        {
            this.Bar = bar;
            // Inherit the default theme
            this.Theme.Inherit(bar.DefaultTheme);
            this.Theme.InferStateThemes();

            this.IsPrimaryOriginal = this.IsPrimary;
        }
    }

    /// <summary>
    /// Image bar item.
    /// </summary>
    [JsonTypeName("image")]
    [BarControl(typeof(ImageBarControl))]
    public class BarImage : BarButton
    {
    }
    
    /// <summary>
    /// Used by a BarItem subclass to identify the control used to display the item.
    /// </summary>
    public class BarControlAttribute : Attribute
    {
        public Type Type { get; }

        public BarControlAttribute(Type type)
        {
            this.Type = type;
        }
    }
}