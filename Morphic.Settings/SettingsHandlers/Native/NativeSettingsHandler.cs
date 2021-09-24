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
                            var convertToUintResult = ConversionUtils.TryConvertObjectToUInt(value);
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
                            var convertToUintResult = ConversionUtils.TryConvertObjectToUInt(value);
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
                            var convertToUintResult = ConversionUtils.TryConvertObjectToUInt(value);
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
                            var convertToByteResult = ConversionUtils.TryConvertObjectToByte(value);
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
                            var convertToByteResult = ConversionUtils.TryConvertObjectToByte(value);
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
                            var convertToByteResult = ConversionUtils.TryConvertObjectToByte(value);
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
                            var convertToDoubleResult = ConversionUtils.TryConvertObjectToDouble(value);
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

    }
}
