// Copyright 2021 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-windows/blob/master/LICENSE.txt
//
// The R&D leading to these results received funding from the:
// * Rehabilitation Services Administration, US Dept. of Education under
//   grant H421A150006 (APCP)
// * National Institute on Disability, Independent Living, and
//   Rehabilitation Research (NIDILRR)
// * Administration for Independent Living & Dept. of Education under grants
//   H133E080022 (RERC-IT) and H133E130028/90RE5003-01-00 (UIITA-RERC)
// * European Union's Seventh Framework Programme (FP7/2007-2013) grant
//   agreement nos. 289016 (Cloud4all) and 610510 (Prosperity4All)
// * William and Flora Hewlett Foundation
// * Ontario Ministry of Research and Innovation
// * Canadian Foundation for Innovation
// * Adobe Foundation
// * Consumer Electronics Association Foundation

using Morphic.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Morphic.Settings
{
    internal class ConversionUtils
    {
        internal static IMorphicResult<byte> TryConvertObjectToByte(object? value)
        {
            if (value == null)
            {
                return IMorphicResult<byte>.ErrorResult();
            }

            // make sure the value fits within the allowed range
            if ((value.GetType() == typeof(sbyte)) ||
                (value.GetType() == typeof(short)) ||
                (value.GetType() == typeof(int)) ||
                (value.GetType() == typeof(long)))
            {
                // signed integers

                var valueAsLong = Convert.ToInt64(value);
                if (valueAsLong < 0)
                {
                    return IMorphicResult<byte>.ErrorResult();
                }
                if (valueAsLong > byte.MaxValue)
                {
                    return IMorphicResult<byte>.ErrorResult();
                }
            }
            else if ((value.GetType() == typeof(byte)) ||
                (value.GetType() == typeof(ushort)) ||
                (value.GetType() == typeof(uint)) ||
                (value.GetType() == typeof(ulong)))
            {
                // unsigned integers

                var valueAsUlong = Convert.ToUInt64(value);
                if (valueAsUlong > byte.MaxValue)
                {
                    return IMorphicResult<byte>.ErrorResult();
                }

            }
            else
            {
                // non-integer (i.e. unknown type)
                return IMorphicResult<byte>.ErrorResult();
            }

            var result = Convert.ToByte(value);
            return IMorphicResult<byte>.SuccessResult(result);
        }

        internal static IMorphicResult<uint> TryConvertObjectToUInt(object? value)
        {
            if (value == null)
            {
                return IMorphicResult<uint>.ErrorResult();
            }

            // make sure the value fits within the allowed range
            if ((value.GetType() == typeof(sbyte)) ||
                (value.GetType() == typeof(short)) ||
                (value.GetType() == typeof(int)) ||
                (value.GetType() == typeof(long)))
            {
                // signed integers

                var valueAsLong = Convert.ToInt64(value);
                if (valueAsLong < 0)
                {
                    return IMorphicResult<uint>.ErrorResult();
                }
                if (valueAsLong > uint.MaxValue)
                {
                    return IMorphicResult<uint>.ErrorResult();
                }
            }
            else if ((value.GetType() == typeof(byte)) ||
                (value.GetType() == typeof(ushort)) ||
                (value.GetType() == typeof(uint)) ||
                (value.GetType() == typeof(ulong)))
            {
                // unsigned integers

                var valueAsUlong = Convert.ToUInt64(value);
                if (valueAsUlong > uint.MaxValue)
                {
                    return IMorphicResult<uint>.ErrorResult();
                }

            }
            else
            {
                // non-integer (i.e. unknown type)
                return IMorphicResult<uint>.ErrorResult();
            }

            var result = Convert.ToUInt32(value);
            return IMorphicResult<uint>.SuccessResult(result);
        }

        internal static IMorphicResult<ulong> TryConvertObjectToULong(object? value)
        {
            if (value == null)
            {
                return IMorphicResult<ulong>.ErrorResult();
            }

            // make sure the value fits within the allowed range
            if ((value.GetType() == typeof(sbyte)) ||
                (value.GetType() == typeof(short)) ||
                (value.GetType() == typeof(int)) ||
                (value.GetType() == typeof(long)))
            {
                // signed integers

                var valueAsLong = Convert.ToInt64(value);
                if (valueAsLong < 0)
                {
                    return IMorphicResult<ulong>.ErrorResult();
                }
            }
            else if ((value.GetType() == typeof(byte)) ||
                (value.GetType() == typeof(ushort)) ||
                (value.GetType() == typeof(uint)) ||
                (value.GetType() == typeof(ulong)))
            {
                // unsigned integers

                var valueAsUlong = Convert.ToUInt64(value);
                if (valueAsUlong > ulong.MaxValue)
                {
                    return IMorphicResult<ulong>.ErrorResult();
                }

            }
            else
            {
                // non-integer (i.e. unknown type)
                return IMorphicResult<ulong>.ErrorResult();
            }

            var result = Convert.ToUInt64(value);
            return IMorphicResult<ulong>.SuccessResult(result);
        }

        internal static IMorphicResult<IntPtr> TryConvertObjectToIntPtr(object? value)
        {
            if (value == null)
            {
                return IMorphicResult<IntPtr>.ErrorResult();
            }

            Int64 INTPTR_MAX;
            switch (IntPtr.Size)
            {
                case 4:
                    INTPTR_MAX = Int32.MaxValue;
                    break;
                case 8:
                    INTPTR_MAX = Int64.MaxValue;
                    break;
                default:
                    Debug.Assert(false, "The bitness of this platform is unsupported");
                    return IMorphicResult<IntPtr>.ErrorResult();
            }

            // make sure the value fits within the allowed range
            if ((value.GetType() == typeof(sbyte)) ||
                (value.GetType() == typeof(short)) ||
                (value.GetType() == typeof(int)) ||
                (value.GetType() == typeof(long)))
            {
                // signed integers

                var valueAsLong = Convert.ToInt64(value);
                if (valueAsLong < 0)
                {
                    return IMorphicResult<IntPtr>.ErrorResult();
                }
                if (valueAsLong > INTPTR_MAX)
                {
                    return IMorphicResult<IntPtr>.ErrorResult();
                }
            }
            else if ((value.GetType() == typeof(byte)) ||
                (value.GetType() == typeof(ushort)) ||
                (value.GetType() == typeof(uint)) ||
                (value.GetType() == typeof(ulong)))
            {
                // unsigned integers

                var valueAsUlong = Convert.ToUInt64(value);
                if (valueAsUlong > (ulong)INTPTR_MAX)
                {
                    return IMorphicResult<IntPtr>.ErrorResult();
                }

            }
            else
            {
                // non-integer (i.e. unknown type)
                return IMorphicResult<IntPtr>.ErrorResult();
            }

            var result = new IntPtr(Convert.ToInt64(value));
            return IMorphicResult<IntPtr>.SuccessResult(result);
        }

        // NOTE: if the user calls this function with an integer, we validate that it is less than 2^52 (and greater than -(2^52)) to ensure that there is no loss in precision
        internal static IMorphicResult<double> TryConvertObjectToDouble(object? value)
        {
            if (value == null)
            {
                return IMorphicResult<double>.ErrorResult();
            }

            // make sure the value fits within the allowed range
            if ((value.GetType() == typeof(sbyte)) ||
                (value.GetType() == typeof(short)) ||
                (value.GetType() == typeof(int)) ||
                (value.GetType() == typeof(long)))
            {
                // signed integers

                var valueAsLong = Convert.ToInt64(value);
                if (valueAsLong <= -(((Int64)2) << 52))
                {
                    return IMorphicResult<double>.ErrorResult();
                }
                if (valueAsLong >= ((Int64)2) << 52)
                {
                    return IMorphicResult<double>.ErrorResult();
                }
            }
            else if ((value.GetType() == typeof(byte)) ||
                (value.GetType() == typeof(ushort)) ||
                (value.GetType() == typeof(uint)) ||
                (value.GetType() == typeof(ulong)))
            {
                // unsigned integers

                var valueAsUlong = Convert.ToUInt64(value);
                if (valueAsUlong >= ((Int64)2) << 52)
                {
                    return IMorphicResult<double>.ErrorResult();
                }
            }
            else if ((value.GetType() == typeof(float)) ||
                (value.GetType() == typeof(double)))
            {
                // floating-point values

                // NOTE: all single- and double-precision floating point values can be converted to double
            }
            else
            {
                // non-integer (i.e. unknown type)
                return IMorphicResult<double>.ErrorResult();
            }

            var result = Convert.ToDouble(value);
            return IMorphicResult<double>.SuccessResult(result);
        }
    }
}
