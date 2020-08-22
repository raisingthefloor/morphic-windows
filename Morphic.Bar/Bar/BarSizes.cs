// BarSizes.cs: Sizing configuration for the bar.
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
    using System.Windows;
    using Newtonsoft.Json;

    [JsonObject(MemberSerialization.OptIn)]
    public class BarSizes
    {
        /// <summary>Padding between edge of bar and items.</summary>
        [JsonProperty("windowPadding")]
        public Thickness WindowPadding { get; set; } = new Thickness(0);

        /// <summary>Spacing between items.</summary>
        [JsonProperty("itemSpacing")]
        public double ItemSpacing { get; set; } = 1;

        /// <summary>Item width.</summary>
        [JsonProperty("itemWidth")]
        public double ItemWidth { get; set; } = 100;

        /// <summary>Maximum Button Item title lines.</summary>
        [JsonProperty("buttonTextLines")]
        public int ButtonTextLines { get; set; } = 2;

        /// <summary>
        /// Button Item padding between edge and title. And for the top, between circle and title
        /// </summary>
        [JsonProperty("buttonPadding")]
        public Thickness ButtonTextPadding { get; set; } = new Thickness(10);

        /// <summary>Button Item circle image diameter (a fraction relative to the itemWidth).</summary>
        [JsonProperty("buttonCircleDiameter")]
        public double ButtonCircleDiameterField { get; set; } = 0.666d;

        /// <summary>Button Item circle image diameter.</summary>
        public double ButtonCircleDiameter => Math.Floor(this.ItemWidth * this.ButtonCircleDiameterField);

        public double ButtonImageSize =>
            Math.Sqrt(Math.Pow(this.ButtonCircleDiameter - this.CircleBorderWidth * 4, 2) / 2);

        /// <summary>Button Item circle overlap with rectangle (a fraction relative to buttonImageSize).</summary>
        [JsonProperty("buttonImageOverlap")]
        public double ButtonImageOverlapField { get; set; } = 0.333d;

        /// <summary>Button Item circle overlap with rectangle.</summary>
        public double ButtonImageOverlap => Math.Floor(this.ButtonCircleDiameter * this.ButtonImageOverlapField);
        /// <summary>Space between the top of the image and the button rectangle.</summary>
        public double ButtonImageOffset => Math.Floor(this.ButtonCircleDiameter * (1 - this.ButtonImageOverlapField));

        [JsonProperty("buttonFontSize")]
        public double ButtonFontSize { get; set; } = 14;

        [JsonProperty("buttonFontWeight")]
        public FontWeight ButtonFontWeight { get; set; } = FontWeights.Normal;

        [JsonProperty("circleBorderWidth")]
        public double CircleBorderWidth { get; set; } = 2;

        [JsonProperty("buttonCornerRadius")]
        public double ButtonCornerRadius { get; set; } = 10;

        [JsonProperty("controlLabelFontSize")]
        public double ControlLabelFontSize { get; set; } = 14;

        [JsonProperty("controlLabelPadding")]
        public Thickness ControlLabelPadding { get; set; } = new Thickness(0, 5, 0, 5);

        [JsonProperty("controlItemMargin")]
        public Thickness ControlItemMargin { get; set; } = new Thickness(10);

        [JsonProperty("controlItemFontSize")]
        public double ControlItemFontSize { get; set; } = 14;

        [JsonProperty("controlItemHeight")]
        public double ControlItemHeight { get; set; } = 30;

        [JsonProperty("controlImageSize")]
        public double ControlImageSize { get; set; } = 15;


    }
}
