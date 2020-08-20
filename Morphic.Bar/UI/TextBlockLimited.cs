// TextBlockLimited.cs: Over-rides TextBlock to add MaxLines property.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

namespace Morphic.Bar.UI
{
    using System;
    using System.ComponentModel;
    using System.Windows;
    using System.Windows.Controls;

    /// <summary>
    /// A TextBlock whose height can be limited by the number of text lines.
    /// </summary>
    public class TextBlockLimited : TextBlock
    {
        public static readonly DependencyProperty MaxLinesProperty = DependencyProperty.Register("MaxLines",
            typeof(int), typeof(TextBlockLimited), new PropertyMetadata(new PropertyChangedCallback(OnMaxLinesChanged)));

        /// <summary>
        /// Maximum number of text lines to display.
        /// </summary>
        public int MaxLines
        {
            get => (int)this.GetValue(MaxLinesProperty);
            set => this.SetValue(MaxLinesProperty, value);
        }

        private static void OnMaxLinesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBlockLimited textBlock)
            {
                textBlock.UpdateMaxHeight();
            }
        }

        public TextBlockLimited()
        {
            DependencyPropertyDescriptor.FromProperty(FontSizeProperty, this.GetType())
                .AddValueChanged(this, this.OnFontSizeChanged);
        }

        private void OnFontSizeChanged(object? sender, EventArgs e)
        {
            this.UpdateMaxHeight();
        }

        private void UpdateMaxHeight()
        {
            const double pointsToPixels = (96.0 / 72.0);
            double lineHeight = double.IsNaN(this.LineHeight)
                ? Math.Ceiling(this.FontFamily.LineSpacing * this.FontSize * pointsToPixels)
                : this.LineHeight;
            this.MaxHeight = lineHeight * this.MaxLines + this.Padding.Bottom + this.Padding.Top;
        }
    }
}
