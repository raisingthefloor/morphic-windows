namespace Morphic.Core
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;

    public static class TypeConversion
    {
        /// <summary>
        /// Convert an object to another type.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="defaultValue">The value to return, if the conversion fails.</param>
        /// <typeparam name="T">The type to convert to.</typeparam>
        /// <returns>The converted value.</returns>
        public static T ConvertTo<T>(this object? value, T defaultValue = default)
        {
            return value.TryConvert(out T result) ? result : defaultValue;
        }

        /// <summary>
        /// Convert an object to another type.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="result">The converted value.</param>
        /// <typeparam name="T">The type to convert to.</typeparam>
        /// <returns>true if the conversion was successful.</returns>
        public static bool TryConvert<T>(this object? value, [NotNullWhen(true)] out T result)
        {
            bool success;
            object? resultObject;

            if (value == null)
            {
                result = default!;
                return false;
            }

            if (value is T v)
            {
                resultObject = v;
                success = true;
            }
            else if (typeof(T) == typeof(bool) && value is string stringValue)
            {
                // See if it's a false-like word, or a zero number.
                bool isFalse = new[] { "", "false", "no", "off" }.Contains(stringValue.ToLowerInvariant());
                if (isFalse)
                {
                    resultObject = false;
                }
                else if (double.TryParse(stringValue, NumberStyles.Any, null, out double number))
                {
                    resultObject = number != 0;
                }
                else
                {
                    // Anything else is true.
                    resultObject = true;
                }
                success = true;
            }
            else if (typeof(T) == typeof(string))
            {
                resultObject = value.ToString();
                success = true;
            }
            else
            {
                try
                {
                    resultObject = Convert.ChangeType(value, typeof(T));
                    success = true;
                }
                catch (Exception e) when (e is FormatException || e is InvalidCastException)
                {
                    resultObject = default!;
                    success = false;
                }
            }

            if (success && resultObject is T o)
            {
                result = o;
            }
            else
            {
                result = default!;
                success = false;
            }

            return success;
        }
    }
}
