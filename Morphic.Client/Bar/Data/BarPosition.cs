// BarPosition.cs: The initial positioning of a bar.
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
    using System;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Windows;
    using System.Windows.Controls;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using UI.AppBarWindow;

    /// <summary>
    /// The position of a bar.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class BarPosition
    {
        private ExpanderRelative expanderRelative = ExpanderRelative.Both;
        private const string LEFT = "left";
        private const string TOP = "top";
        private const string RIGHT = "right";
        private const string BOTTOM = "bottom";
        private const string MIDDLE = "middle";

        /// <summary>
        /// The side of the screen where the bar will be docked, reserving the desktop work area.
        /// </summary>
        [JsonProperty("docked", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(StringEnumConverter))]
        public Edge DockEdge { get; set; } = Edge.None;

        /// <summary>
        /// The horizontal position of the bar. Can be "Left", "Middle", "Right", a number, or a percentage.
        /// Numbers or percentages can be negative (including -0), meaning distance from the right.
        /// Percentages specify the position of the middle of the bar.
        /// </summary>
        [JsonProperty("x")]
        public string? PrimaryXValue
        {
            set => this.ParsePosition(value ?? "0");
        }
        
        /// <summary>
        /// The vertical position of the bar. Can be "Top", "Middle", "Bottom", a number, or a percentage.
        /// Numbers or percentages can be negative (including -0), meaning distance from the bottom.
        /// Percentages specify the position of the middle of the bar.
        /// </summary>
        [JsonProperty("y")]
        public string? PrimaryYValue
        {
            set => this.ParsePosition(value ?? "0");
        }

        /// <summary>
        /// The initial orientation of the bar. Ignored if docked.
        /// </summary>
        [JsonProperty("horizontal")]
        public bool Horizontal
        {
            get => this.Orientation == System.Windows.Controls.Orientation.Horizontal;
            set => this.Orientation =
                value ? System.Windows.Controls.Orientation.Horizontal : System.Windows.Controls.Orientation.Vertical;
        }

        public Orientation Orientation { get; set; } = Orientation.Vertical;

        /// <summary>
        /// The horizontal/vertical position of the secondary bar, relative to the primary bar (when above or below it).
        /// </summary>
        [JsonProperty("secondary")]
        public string? SecondaryXyValues
        {
            set
            {
                this.SecondaryXValue = value;
                this.SecondaryYValue = value;
            }
        }

        /// <summary>
        /// The horizontal position of the secondary bar, relative to the primary bar (when above or below it).
        /// </summary>
        [JsonProperty("secondaryX")]
        public string? SecondaryXValue
        {
            set => this.ParsePosition(value ?? "0");
        }
        
        /// <summary>
        /// The vertical position of the secondary bar, relative to the primary bar (when beside it).
        /// </summary>
        [JsonProperty("secondaryY")]
        public string? SecondaryYValue
        {
            set => this.ParsePosition(value ?? "0");
        }

        /// <summary>
        /// The horizontal/vertical position of the expander button bar
        /// </summary>
        [JsonProperty("expander")]
        public string? ExpanderXyValues
        {
            set
            {
                this.ExpanderXValue = value;
                this.ExpanderYValue = value;
            }
        }

        /// <summary>
        /// The horizontal position of the expander button bar
        /// </summary>
        [JsonProperty("expanderX")]
        public string? ExpanderXValue
        {
            set => this.ParsePosition(value ?? "0");
        }
        
        /// <summary>
        /// The vertical position of the secondary bar
        /// </summary>
        [JsonProperty("expanderY")]
        public string? ExpanderYValue
        {
            set => this.ParsePosition(value ?? "0");
        }

        /// <summary>
        /// The bar that the expander position is relative to.
        /// </summary>
        [JsonProperty("expanderRelative")]
        [JsonConverter(typeof(StringEnumConverter))]
        public ExpanderRelative ExpanderRelative
        {
            set => this.expanderRelative = value;
            get => this.expanderRelative;
        }

        public RelativePosition Primary { get; set; } = new RelativePosition();
        public RelativePosition Secondary { get; set; } = new RelativePosition();
        public RelativePosition Expander { get; set; } = new RelativePosition();

        /// <summary>
        /// Gets the AxisPosition for the given json property.
        /// </summary>
        /// <param name="jsonPropertyName">Name of the json property (xxValue).</param>
        /// <returns>The AxisPosition.</returns>
        private AxisPosition GetAxisPositionFromName(string jsonPropertyName)
        {
            string backingPropertyName = jsonPropertyName.Substring(0, jsonPropertyName.Length - "XValue".Length);

            PropertyInfo property = this.GetType().GetProperty(backingPropertyName)
                                    ?? throw new ArgumentException(
                                        $"json property '{jsonPropertyName}' has no backing property.",
                                        nameof(jsonPropertyName));
            RelativePosition axisPosition = (property.GetValue(this) as RelativePosition)!;

            string axis = jsonPropertyName.Substring(backingPropertyName.Length, 1);
            return axis switch
            {
                "X" => axisPosition.X,
                "Y" => axisPosition.Y,
                _ => throw new InvalidOperationException($"Unable to get axis from property name {jsonPropertyName}")
            };
        }

        /// <summary>
        /// A tuple, for "X and Y" values.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public class RelativePosition
        {
            public AxisPosition X { get; set; } = new AxisPosition();
            public AxisPosition Y { get; set; } = new AxisPosition();

            /// <summary>
            /// Gets the desired initial position of a window, relative to the work area.
            /// </summary>
            /// <param name="workArea">The work-area for the window.</param>
            /// <param name="size">The window size.</param>
            /// <param name="clamp">true if setting the position of the secondary bar.</param>
            /// <returns>The location of the window.</returns>
            public Point GetPosition(Rect workArea, Size size)
            {
                Rect result = new Rect()
                {
                    X = this.X.GetAbsolute(workArea.Left, workArea.Right, size.Width),
                    Y = this.Y.GetAbsolute(workArea.Top, workArea.Bottom, size.Height),
                    Size = size
                };

                return result.Location;
            }
        }

        public class AxisPosition
        {
            /// <summary>
            /// The value.
            /// </summary>
            public double Value { get; set; }
            /// <summary>
            /// true if the number is a 0-1 percentage.
            /// </summary>
            public bool IsRelative { get; set; }

            /// <summary>
            /// Returns -1 or 1, depending on its sign (including -/+ zero). 
            /// </summary>
            public int Sign => this.Value == 0.0
                ? double.IsNegativeInfinity(1.0 / this.Value) ? -1 : 1
                : Math.Sign(this.Value);

            public bool IsNegative => this.Sign < 0;

            public AxisPosition()
            {
            }

            public AxisPosition(double value, bool isRelative)
            {
                this.Value = value;
                this.IsRelative = isRelative;
            }

            public static implicit operator double(AxisPosition axisPosition)
            {
                return axisPosition.Value;
            }

            /// <summary>
            /// Get the absolute position of this position, relative to the given range.
            /// </summary>
            /// <param name="min">The minimum value of the range.</param>
            /// <param name="max">The maximum value of the range.</param>
            /// <param name="clamp">true to ensure the result is witihn the range.</param>
            /// <returns></returns>
            public double GetAbsolute(double min, double max, bool clamp = true)
            {
                // Negative values are taken from the max.
                double offset = this.IsNegative ? max : min;
                
                double result = this.IsRelative
                    ? offset + this.Value * (max - min)
                    : offset + this.Value;
                
                return clamp
                    ? Math.Clamp(result, min, max)
                    : result;
            }

            public double GetAbsolute(double min, double max, double size)
            {
                double value = 0;
                if (min > max - size)
                {
                    // The outer area is smaller than the inner
                    if (this.IsRelative)
                    {
                        double v = Math.Abs(this.Value);
                        if (v < 0.501)
                        {
                            // left/top
                            value = min;
                        }
                        else
                        {
                            // right/bottom
                            value = max - size;
                        }
                    }
                    else
                    {
                        value = this.IsNegative ? max - size : min;
                    }
                }
                else
                {
                    value = this.GetAbsolute(min, max - size);

                    if (this.IsRelative)
                    {
                        //value -= size / 2;
                    }

                    value = Math.Clamp(value, min, max - size);
                }

                return value;
            }
        }

        private void ParsePosition(string origValue, [CallerMemberName] string propertyName = "")
        {
            if (!propertyName.EndsWith("Value"))
            {
                throw new ArgumentException("Property name should end with 'Value'", nameof(propertyName));
            }

            string value = origValue.Trim().ToLowerInvariant() switch
            {
                LEFT => "0",
                TOP => "0",
                RIGHT => "-0",
                BOTTOM => "-0",
                MIDDLE => "50%",
                _ => origValue
            };

            bool relative = value.EndsWith("%");
            if (relative)
            {
                value = value.Substring(0, value.Length - 1);
            }

            double num;
            if (!double.TryParse(value, out num))
            {
                throw new JsonException($"{propertyName}: Unrecognised positional value '{value}'.");
            }
            
            if (relative)
            {
                num /= 100;
            }

            // Set the backing property.
            AxisPosition axisPosition = this.GetAxisPositionFromName(propertyName);
            axisPosition.Value = num;
            axisPosition.IsRelative = relative;
        }
    }
}
