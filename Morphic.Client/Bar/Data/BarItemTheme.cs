// BarItemTheme.cs: Describes the visual appearance of a bar and its items.
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
    using System.ComponentModel;
    using System.Linq;
    using System.Reflection;
    using System.Windows.Forms;
    using System.Windows.Media;
    using Newtonsoft.Json;

    /// <summary>
    /// Theme for a bar item.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class BarItemTheme : Theme
    {
        /// <summary>The theme for when the mouse is over the item.</summary>
        [JsonProperty("hover", ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public Theme Hover { get; set; } = new Theme();
        
        /// <summary>The theme for when the item has keyboard focus.</summary>
        [JsonProperty("focus", ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public Theme Focus { get; set; } = new Theme();

        /// <summary>The theme for when the item is being clicked (mouse is down).</summary>
        [JsonProperty("active", ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public Theme Active { get; set; } = new Theme();

        /// <summary>The theme for when the item is checked (toggle buttons).</summary>
        [JsonProperty("checked", ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public Theme Checked { get; set; } = new Theme();

        public BarItemTheme()
        {
        }

        public BarItemTheme(Theme theme)
        {
            this.Apply(theme);
        }

        public BarItemTheme Inherit(BarItemTheme theme)
        {
            this.Apply(theme);
            this.Hover.Apply(theme.Hover);
            this.Focus.Apply(theme.Focus);
            this.Active.Apply(theme.Active);
            this.Checked.Apply(theme.Checked);
            return this;
        }

        /// <summary>
        /// Generate the themes for the different states that are unset, based on the colour.
        /// </summary>
        /// <param name="force"></param>
        public void InferStateThemes(bool force = false)
        {
            if (force || this.Hover.Background is null)
            {
                this.Hover.Background = this.LightenColor(this.Background ?? Colors.Transparent, 0.25f);
            }

            if (force || this.Active.Background is null)
            {
                this.Active.Background = this.LightenColor(this.Background ?? Colors.Transparent, 0.5f);
            }

            if (force || this.Focus.Background is null)
            {
                this.Focus.Background = this.Hover.Background;
            }
        }

        private Color LightenColor(Color color, float amount)
        {
            System.Drawing.Color c =
                ControlPaint.Light(System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B), amount);
            return Color.FromArgb(c.A, c.R, c.G, c.B);
        }
    }

    /// <summary>
    /// Theme for the bar.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class BarTheme : Theme
    {
        
    }

    /// <summary>
    /// A theme.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Theme : INotifyPropertyChanged
    {
        /// <summary>Text colour.</summary>
        [JsonProperty("color")]
        public Color? TextColor { get; set; }

        [JsonProperty("background")]
        public Color? Background { get; set; }

        [JsonProperty("borderColor")]
        public Color? BorderColor { get; set; }
        
        [JsonProperty("focusDotColor")]
        public Color? FocusDotColor { get; set; }

        [JsonProperty("borderSize")]
        public double BorderSize { get; set; } = double.NaN;

        public static Theme DefaultBar()
        {
            return new Theme()
            {
                Background = Colors.White,
                TextColor = Colors.Black,
                BorderColor = Colors.Black,
                BorderSize = 1
            };
        }
        
        /// <summary>
        /// Default item theme.
        /// </summary>
        /// <returns></returns>
        public static Theme DefaultItem()
        {
            return new Theme()
            {
                Background = ColorConverter.ConvertFromString("#002957") as Color?,
                TextColor = Colors.White
            };
        }

        /// <summary>
        /// Default control button theme.
        /// </summary>
        /// <returns></returns>
        public static Theme DefaultControl()
        {
            return new Theme()
            {
                Background = Color.FromRgb(0, 129, 69),
                TextColor = Colors.White
            };
        }

        /// <summary>
        /// Sets the unset values of this instance using values of another.
        /// </summary>
        /// <param name="source">The instance to read values from.</param>
        /// <param name="all">true to set all values, false to set only values in this instance that are null.</param>
        public Theme Apply(Theme source, bool all = false)
        {
            foreach (PropertyInfo property in typeof(Theme).GetProperties().Where(p => p.CanWrite))
            {
                object? origValue = all ? null : property.GetValue(this);
                if (origValue is null || (origValue is double d && double.IsNaN(d)))
                {
                    object? newValue = property.GetValue(source);
                    property.SetValue(this, newValue);
                }

                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property.Name));
            }

            return this;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}