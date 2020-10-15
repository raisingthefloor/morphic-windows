// Converters.cs: Value converters for bindings in the UI
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

namespace Morphic.Client.Bar.UI
{
    using System;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;

    /// <summary>
    /// Converter which returns a value depending on whether or not the input value is false/null.
    /// </summary>
    public class Ternary : IValueConverter
    {
        /// <summary>
        /// The value to return if the input value is false, null, or empty string.
        /// </summary>
        public string? False { get; set; }

        /// <summary>
        /// The value to return if the input value is not null or false. Omit to return the input value.
        /// </summary>
        public string? True { get; set; }

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || value as bool? == false || value as string == string.Empty
                || value as Orientation? == Orientation.Horizontal)
            {
                return parameter ?? this.False;
            }
            else
            {
                return this.True ?? value;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Multiply a thickness by another thickness.
    /// </summary>
    public class ThicknessMultiplier : IValueConverter
    {
        public Thickness Multiplier { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Thickness result =  value as Thickness? ?? (Thickness)new ThicknessConverter().ConvertFrom(value);
            result.Left *= this.Multiplier.Left;
            result.Top *= this.Multiplier.Top;
            result.Right *= this.Multiplier.Right;
            result.Bottom *= this.Multiplier.Bottom;
            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
