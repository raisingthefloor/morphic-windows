// BarPosition.cs: The initial positioning of a bar.
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
    using System.Runtime.CompilerServices;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using UI.AppBarWindow;

    /// <summary>
    /// The position of a bar.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class BarPosition
    {
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
        public string XValue
        {
            set => this.ParsePosition(value ?? "0");
        }
        
        /// <summary>
        /// The vertical position of the bar. Can be "Top", "Middle", "Bottom", a number, or a percentage.
        /// Numbers or percentages can be negative (including -0), meaning distance from the bottom.
        /// Percentages specify the position of the middle of the bar.
        /// </summary>
        [JsonProperty("y")]
        public string YValue
        {
            set => this.ParsePosition(value ?? "0");
        }
        
        /// <summary>
        /// The initial orientation of the bar. Ignored if docked.
        /// </summary>
        [JsonProperty("orientation")]
        public Orientation? Orientation { get; set; }

        private void ParsePosition(string origValue, [CallerMemberName] string? property = null)
        {
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
                throw new JsonException($"{property}: Unrecognised positional value '{value}'.");
            }
            
            if (relative)
            {
                num /= 100;
            }

            switch (property)
            {
                case nameof(this.XValue):
                    this.XIsRelative = relative;
                    this.X = num;
                    break;
                case nameof(this.YValue):
                    this.YIsRelative = relative;
                    this.Y = num;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(property));
            }
        }

        public bool XIsRelative { get; private set; }
        public bool YIsRelative { get; private set; }
        public double X { get; private set; }
        public double Y { get; private set; }

        /// <summary>
        /// Gets the desired initial position of a window.
        /// </summary>
        /// <param name="workArea">The work-area for the window.</param>
        /// <param name="size">The window size.</param>
        /// <returns>The location of the window.</returns>
        public Point GetPosition(Rect workArea, Size size)
        {
            Rect result = new Rect()
            {
                X = this.GetAbsolute(workArea.Left, workArea.Right, this.X, this.XIsRelative),
                Y = this.GetAbsolute(workArea.Top, workArea.Bottom, this.Y, this.YIsRelative),
                Size = size
            };

            if (this.XIsRelative)
            {
                result.X -= size.Width / 2;
            }

            if (this.YIsRelative)
            {
                result.Y -= size.Height / 2;
            }

            // Make sure the window is within the work area.
            if (result.Right > workArea.Right)
            {
                result.X = workArea.Right - result.Width;
            }
            
            if (result.Bottom > workArea.Bottom)
            {
                result.Y = workArea.Bottom - result.Height;
            }

            result.X = Math.Max(result.X, workArea.X);
            result.Y = Math.Max(result.Y, workArea.Y);

            return result.Location;
        }

        private double GetAbsolute(double min, double max, double value, bool percent = false)
        {
            // Get the sign, including negative 0.
            int sign = value == 0.0
                ? double.IsNegativeInfinity(1.0 / value) ? -1 : 1
                : Math.Sign(value);

            double offset = sign > 0 ? min : max;
            return percent
                ? offset + value * (max - min)
                : offset + value;
        }
    }

    public enum Position
    {
        Absolute = 0,
        Percent = 1,
        Left = 2,
        Top = 3,
        Right = 4,
        Bottom = 5,
        Center = 6,
        Centre = 6,
        Middle = 6
    }
}
