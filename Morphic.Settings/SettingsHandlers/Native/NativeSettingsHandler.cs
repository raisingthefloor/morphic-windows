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
                    default:
                        success = false;
                        // skip to the next setting
                        continue;
                }
            }

            return success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult;
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
    }

    [SettingsHandlerType("native", typeof(NativeSettingsHandler))]
    public class NativeSettingGroup : SettingGroup
    {

    }
}
