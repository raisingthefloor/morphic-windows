namespace Morphic.Settings.SettingsHandlers.Native
{
    using Microsoft.Extensions.DependencyInjection;
    using Morphic.Core;
    using SolutionsRegistry;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    
    [SrService]
    class NativeSettingsHandler : SettingsHandler
    {
        private readonly IServiceProvider serviceProvider;

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

        private IMorphicResult SetVolume(double value)
        {
            // OBSERVATION: if the system has no default audio output endpoint (or we cannot set the volume level), we currently have no way to return that error
            var audioEndpoint = Windows.Native.Audio.AudioEndpoint.GetDefaultAudioOutputEndpoint();
            // OBSERVATION: Morphic treats the volume as a double-precision floating point value, but we get and set it as its native single-precision floating point value
            audioEndpoint.SetMasterVolumeLevel((float)value);

            return IMorphicResult.SuccessResult;
        }

        //

        private IMorphicResult<uint> TryConvertObjectToUInt(object? value)
        {
            uint result;

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

            result = Convert.ToUInt32(value);
            return IMorphicResult<uint>.SuccessResult(result);
        }

        // NOTE: if the user calls this function with an integer, we validate that it is less than 2^52 (and greater than -(2^52)) to ensure that there is no loss in precision
        private IMorphicResult<double> TryConvertObjectToDouble(object? value)
        {
            double result;

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

            result = Convert.ToDouble(value);
            return IMorphicResult<double>.SuccessResult(result);
        }
    }

    [SettingsHandlerType("native", typeof(NativeSettingsHandler))]
    public class NativeSettingGroup : SettingGroup
    {

    }
}
