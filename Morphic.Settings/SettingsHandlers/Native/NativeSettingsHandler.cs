namespace Morphic.Settings.SettingsHandlers.Native
{
    using Microsoft.Extensions.DependencyInjection;
    using Morphic.Core;
    using SolutionsRegistry;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    
    [SrService]
    class NativeSettingsHandler : SettingsHandler
    {
        private readonly IServiceProvider serviceProvider;

        private enum SolidColorComponent
        {
            Red,
            Green,
            Blue
        }

        public NativeSettingsHandler(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        public override async Task<(IMorphicResult, Values)> GetAsync(SettingGroup settingGroup, IEnumerable<Setting> settings)
        {
            var success = true;

            Values values = new Values();
            foreach (Setting setting in settings)
            {
                object? value = null;
                var getWasSuccessful = false;

                switch (setting.Name)
                {
                    case "DoubleClickTimeConfig":
                        {
                            var getResult = this.GetDoubleClickTime();
                            getWasSuccessful = getResult.IsSuccess;
                            value = getResult.Value;
                        }
                        break;
                    case "DoubleClickWidthConfig":
                        {
                            var getResult = this.GetDoubleClickWidth();
                            getWasSuccessful = getResult.IsSuccess;
                            value = getResult.Value;
                        }
                        break;
                    case "DoubleClickHeightConfig":
                        {
                            var getResult = this.GetDoubleClickHeight();
                            getWasSuccessful = getResult.IsSuccess;
                            value = getResult.Value;
                        }
                        break;
                    case "SolidColorConfigR":
                        {
                            // NOTE: we should rework the solutions registry so that red, green and blue are all retrieved in ONE call (as an RGB or ARGB value)
                            var getResult = this.GetSolidColorComponent(SolidColorComponent.Red);
                            getWasSuccessful = getResult.IsSuccess;
                            value = getResult.Value;
                        }
                        break;
                    case "SolidColorConfigG":
                        {
                            // NOTE: we should rework the solutions registry so that red, green and blue are all retrieved in ONE call (as an RGB or ARGB value)
                            var getResult = this.GetSolidColorComponent(SolidColorComponent.Green);
                            getWasSuccessful = getResult.IsSuccess;
                            value = getResult.Value;
                        }
                        break;
                    case "SolidColorConfigB":
                        {
                            // NOTE: we should rework the solutions registry so that red, green and blue are all retrieved in ONE call (as an RGB or ARGB value)
                            var getResult = this.GetSolidColorComponent(SolidColorComponent.Blue);
                            getWasSuccessful = getResult.IsSuccess;
                            value = getResult.Value;
                        }
                        break;
                    case "Volume":
                        {
                            var getResult = this.GetVolume();
                            getWasSuccessful = getResult.IsSuccess;
                            value = getResult.Value;
                        }
                        break;
                    default:
                        success = false;
                        values.Add(setting, null, Values.ValueType.NotFound);
                        continue;
                }

                if (getWasSuccessful == false)
                {
                    success = false;
                    // skip to the next setting
                    continue;
                }

                // NOTE: this is another area where changing the result of GetValue to an IMorphicResult could provide clear and granular success/error result
                values.Add(setting, value);
            }

            return ((success? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult), values);
        }

        //

        private IMorphicResult<uint> GetDoubleClickTime()
        {
            return IMorphicResult<uint>.SuccessResult(ExtendedPInvoke.GetDoubleClickTime());
        }

        private IMorphicResult<uint> GetDoubleClickWidth()
        {
            int getSystemMetricsResult = PInvoke.User32.GetSystemMetrics(PInvoke.User32.SystemMetric.SM_CXDOUBLECLK);
            if (getSystemMetricsResult == 0)
            {
                // NOTE: GetLastError would _not_ return any additional information
                return IMorphicResult<uint>.ErrorResult();
            }

            // make sure that our result is a non-negative integer (i.e. will fit in a uint result)
            if (getSystemMetricsResult < 0)
            {
                return IMorphicResult<uint>.ErrorResult();
            }

            var result = (uint)getSystemMetricsResult;
            return IMorphicResult<uint>.SuccessResult(result);
        }

        private IMorphicResult<uint> GetDoubleClickHeight()
        {
            int getSystemMetricsResult = PInvoke.User32.GetSystemMetrics(PInvoke.User32.SystemMetric.SM_CYDOUBLECLK);
            if (getSystemMetricsResult == 0)
            {
                // NOTE: GetLastError would _not_ return any additional information
                return IMorphicResult<uint>.ErrorResult();
            }

            // make sure that our result is a non-negative integer (i.e. will fit in a uint result)
            if (getSystemMetricsResult < 0)
            {
                return IMorphicResult<uint>.ErrorResult();
            }

            var result = (uint)getSystemMetricsResult;
            return IMorphicResult<uint>.SuccessResult(result);
        }

        // NOTE: we should rework the solutions registry so that red, green and blue are all retrieved in ONE call (as an RGB or ARGB value)
        private IMorphicResult<byte> GetSolidColorComponent(SolidColorComponent colorComponent)
        {
            var colorIndex = ExtendedPInvoke.COLOR_DESKTOP;

            byte result;

            var getSysColorResult = this.GetSysColor(colorIndex);
            if (getSysColorResult.IsError == true)
            {
                return IMorphicResult<byte>.ErrorResult();
            }
            var color = getSysColorResult.Value!;

            switch (colorComponent)
            {
                case SolidColorComponent.Red:
                    result = (byte)((color & 0xFF) >> 0);
                    break;
                case SolidColorComponent.Green:
                    result = (byte)((color & 0xFF00) >> 8);
                    break;
                case SolidColorComponent.Blue:
                    result = (byte)((color & 0xFF0000) >> 16);
                    break;
                default:
                    // unreachable code
                    Debug.Assert(false);
                    return IMorphicResult<byte>.ErrorResult();
            }

            return IMorphicResult<byte>.SuccessResult(result);
        }

        private IMorphicResult<uint> GetSysColor(int colorIndex)
        {
            // verify that the required syscolor is supported on this installation of Windows
            var sysColorBrush = ExtendedPInvoke.GetSysColorBrush(colorIndex);
            if (sysColorBrush == IntPtr.Zero)
            {
                return IMorphicResult<uint>.ErrorResult();
            }

            var color = ExtendedPInvoke.GetSysColor(colorIndex);

            return IMorphicResult<uint>.SuccessResult(color);
        }

        private IMorphicResult<double> GetVolume()
        {
            // OBSERVATION: if the system has no default audio output endpoint (or we cannot get the volume level), we currently have no way to return that error
            var audioEndpoint = Windows.Native.Audio.AudioEndpoint.GetDefaultAudioOutputEndpoint();
            // OBSERVATION: Morphic treats the volume as a double-precision floating point value, but we get and set it as its native single-precision floating point value
            var volumeLevelAsFloat = audioEndpoint.GetMasterVolumeLevel();

            return IMorphicResult<double>.SuccessResult((double)volumeLevelAsFloat);
        }

        //

        public override async Task<IMorphicResult> SetAsync(SettingGroup settingGroup, Values values)
        {
            var success = true;

            foreach ((Setting setting, object? value) in values)
            {
                switch (setting.Name)
                {
                    case "DoubleClickTimeConfig":
                        {
                            var convertToUintResult = this.TryConvertObjectToUInt(value);
                            if (convertToUintResult.IsError == true)
                            {
                                success = false;
                                break;
                            }
                            var valueAsUint = convertToUintResult.Value!;

                            var setResult = this.SetDoubleClickTime(valueAsUint);
                            if (setResult.IsError == true)
                            {
                                success = false;
                            }
                        }
                        break;
                    case "DoubleClickWidthConfig":
                        {
                            var convertToUintResult = this.TryConvertObjectToUInt(value);
                            if (convertToUintResult.IsError == true)
                            {
                                success = false;
                                break;
                            }
                            var valueAsUint = convertToUintResult.Value!;

                            var setResult = this.SetDoubleClickWidth(valueAsUint);
                            if (setResult.IsError == true)
                            {
                                success = false;
                            }
                        }
                        break;
                    case "DoubleClickHeightConfig":
                        {
                            var convertToUintResult = this.TryConvertObjectToUInt(value);
                            if (convertToUintResult.IsError == true)
                            {
                                success = false;
                                break;
                            }
                            var valueAsUint = convertToUintResult.Value!;

                            var setResult = this.SetDoubleClickHeight(valueAsUint);
                            if (setResult.IsError == true)
                            {
                                success = false;
                            }
                        }
                        break;
                    case "SolidColorConfigR":
                        {
                            // NOTE: we should rework the solutions registry so that red, green and blue are all set in ONE call (as an RGB or ARGB value)
                            var convertToByteResult = this.TryConvertObjectToByte(value);
                            if (convertToByteResult.IsError == true)
                            {
                                success = false;
                                break;
                            }
                            var valueAsByte = convertToByteResult.Value!;

                            var setResult = this.SetSolidColorComponent(SolidColorComponent.Red, valueAsByte);
                            if (setResult.IsError == true)
                            {
                                success = false;
                            }
                        }
                        break;
                    case "SolidColorConfigG":
                        {
                            // NOTE: we should rework the solutions registry so that red, green and blue are all set in ONE call (as an RGB or ARGB value)
                            var convertToByteResult = this.TryConvertObjectToByte(value);
                            if (convertToByteResult.IsError == true)
                            {
                                success = false;
                                break;
                            }
                            var valueAsByte = convertToByteResult.Value!;

                            var setResult = this.SetSolidColorComponent(SolidColorComponent.Green, valueAsByte);
                            if (setResult.IsError == true)
                            {
                                success = false;
                            }
                        }
                        break;
                    case "SolidColorConfigB":
                        {
                            // NOTE: we should rework the solutions registry so that red, green and blue are all set in ONE call (as an RGB or ARGB value)
                            var convertToByteResult = this.TryConvertObjectToByte(value);
                            if (convertToByteResult.IsError == true)
                            {
                                success = false;
                                break;
                            }
                            var valueAsByte = convertToByteResult.Value!;

                            var setResult = this.SetSolidColorComponent(SolidColorComponent.Blue, valueAsByte);
                            if (setResult.IsError == true)
                            {
                                success = false;
                            }
                        }
                        break;
                    case "Volume":
                        {
                            var convertToDoubleResult = this.TryConvertObjectToDouble(value);
                            if (convertToDoubleResult.IsError == true)
                            {
                                success = false;
                                break;
                            }
                            var valueAsDouble = convertToDoubleResult.Value!;

                            var setResult = this.SetVolume(valueAsDouble);
                            if (setResult.IsError == true)
                            {
                                success = false;
                            }
                        }
                        break;
                    default:
                        success = false;
                        // skip to the next setting
                        continue;
                }
            }

            return success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult;
        }

        //

        private IMorphicResult SetDoubleClickTime(uint value)
        {
            var setResult = ExtendedPInvoke.SetDoubleClickTime(value);
            if (setResult == false)
            {
                // NOTE: we could call GetLastError here to get the last error
                return IMorphicResult.ErrorResult;
            }

            return IMorphicResult.SuccessResult;
        }

        public IMorphicResult SetDoubleClickWidth(uint value)
        {
            // OBSERVATION: SystemParametersInfoFlags.None was used in Morphic Classic, but may not be what we want here
            var spiResult = PInvoke.User32.SystemParametersInfo(PInvoke.User32.SystemParametersInfoAction.SPI_SETDOUBLECLKWIDTH, value, IntPtr.Zero, PInvoke.User32.SystemParametersInfoFlags.None);
            if (spiResult == false)
            {
                // NOTE: we could call GetLastError here to get the last error
                return IMorphicResult.ErrorResult;
            }

            return IMorphicResult.SuccessResult;
        }

        public IMorphicResult SetDoubleClickHeight(uint value)
        {
            // OBSERVATION: SystemParametersInfoFlags.None was used in Morphic Classic, but may not be what we want here
            var spiResult = PInvoke.User32.SystemParametersInfo(PInvoke.User32.SystemParametersInfoAction.SPI_SETDOUBLECLKHEIGHT, value, IntPtr.Zero, PInvoke.User32.SystemParametersInfoFlags.None);
            if (spiResult == false)
            {
                // NOTE: we could call GetLastError here to get the last error
                return IMorphicResult.ErrorResult;
            }

            return IMorphicResult.SuccessResult;
        }

        // NOTE: we should rework the solutions registry so that red, green and blue are all set in ONE call (as an RGB or ARGB value)
        private IMorphicResult SetSolidColorComponent(SolidColorComponent colorComponent, byte value)
        {
            var colorIndex = ExtendedPInvoke.COLOR_DESKTOP;

            var getSysColorResult = this.GetSysColor(colorIndex);
            if (getSysColorResult.IsError == true)
            {
                return IMorphicResult.ErrorResult;
            }
            var color = getSysColorResult.Value!;

            switch (colorComponent)
            {
                case SolidColorComponent.Red:
                    {
                        color &= ~((uint)0xFF);
                        color |= (uint)(value & 0xFF) << 0;
                    }
                    break;
                case SolidColorComponent.Green:
                    {
                        color &= ~((uint)0xFF00);
                        color |= (uint)(value & 0xFF) << 8;
                    }
                    break;
                case SolidColorComponent.Blue:
                    {
                        color &= ~((uint)0xFF0000);
                        color |= (uint)(value & 0xFF) << 16;
                    }
                    break;
                default:
                    // invalid code path
                    Debug.Assert(false);
                    throw new Exception();
            }

            var setSysColorsResult = ExtendedPInvoke.SetSysColors(1, new int[] { colorIndex }, new uint[] { color });
            if (setSysColorsResult == false)
            {
                // NOTE: we could get additional error information via GetLastError
                return IMorphicResult.ErrorResult;
            }

            return IMorphicResult.SuccessResult;
        }

        private IMorphicResult SetVolume(double value)
        {
            // OBSERVATION: if the system has no default audio output endpoint (or we cannot set the volume level), we currently have no way to return that error
            var audioEndpoint = Windows.Native.Audio.AudioEndpoint.GetDefaultAudioOutputEndpoint();
            // OBSERVATION: Morphic treats the volume as a double-precision floating point value, but we get and set it as its native single-precision floating point value
            audioEndpoint.SetMasterVolumeLevel((float)value);

            return IMorphicResult.SuccessResult;
        }

        //

        private IMorphicResult<byte> TryConvertObjectToByte(object? value)
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

        private IMorphicResult<uint> TryConvertObjectToUInt(object? value)
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

        // NOTE: if the user calls this function with an integer, we validate that it is less than 2^52 (and greater than -(2^52)) to ensure that there is no loss in precision
        private IMorphicResult<double> TryConvertObjectToDouble(object? value)
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
                if (valueAsLong <= -(((Int64)2) << 52)) {
                    return IMorphicResult<double>.ErrorResult();
                }
                if (valueAsLong >= ((Int64)2) << 52) {
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

    [SettingsHandlerType("native", typeof(NativeSettingsHandler))]
    public class NativeSettingGroup : SettingGroup
    {

    }
}
