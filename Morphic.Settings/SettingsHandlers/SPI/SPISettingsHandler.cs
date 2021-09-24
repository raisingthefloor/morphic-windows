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

namespace Morphic.Settings.SettingsHandlers.SPI
{
    using Morphic.Core;
    using Morphic.Settings.SolutionsRegistry;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;

    [SrService]
    class SPISettingsHandler: SettingsHandler
    {
        private readonly IServiceProvider serviceProvider;

        public SPISettingsHandler(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        //

        public override async Task<(IMorphicResult, Values)> GetAsync(SettingGroup settingGroup, IEnumerable<Setting> settings)
        {
            bool success;
            Values values = new Values();

            var spiSettingGroupAsOptional = settingGroup as SPISettingGroup;
            if (spiSettingGroupAsOptional == null)
            {
                Debug.Assert(false);
                return (IMorphicResult.ErrorResult, values);
            }
            var spiSettingGroup = spiSettingGroupAsOptional!;

            switch (spiSettingGroup.getAction)
            {
                case "SPI_GETDESKWALLPAPER":
                    {
                        var (spiGetMorphicResult, spiGetValues) = SPISettingsHandler.SpiGetDesktopWallpaper(spiSettingGroup.uiParam, settings);
                        success = spiGetMorphicResult.IsSuccess;
                        if (success == true)
                        {
                            values = spiGetValues;
                        }
                    }
                    break;
                case "SPI_GETFILTERKEYS":
                    {
                        var (spiGetMorphicResult, spiGetValues) = SPISettingsHandler.SpiGetFilterKeys(settings);
                        success = spiGetMorphicResult.IsSuccess;
                        if (success == true)
                        {
                            values = spiGetValues;
                        }
                    }
                    break;
                case "SPI_GETHIGHCONTRAST":
                    {
                        var (spiGetMorphicResult, spiGetValues) = SPISettingsHandler.SpiGetHighContrast(settings);
                        success = spiGetMorphicResult.IsSuccess;
                        if (success == true)
                        {
                            values = spiGetValues;
                        }
                    }
                    break;
                default:
                    success = false;
                    foreach (var setting in settings)
                    {
                        values.Add(setting, null, Values.ValueType.NotFound);
                    }
                    break;
            }

            return ((success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult), values);
        }

        //

        private static (IMorphicResult, Values) SpiGetDesktopWallpaper(object? uiParamAsObject, IEnumerable<Setting> settings)
        {
            var values = new Values();

            // uiParam = length of pvParam buffer (up to MAX_PATH)
            var uiParam = (uiParamAsObject as uint?) ?? ExtendedPInvoke.MAX_PATH;
            uiParam = Math.Min(uiParam, ExtendedPInvoke.MAX_PATH);
            //
            // pvParam = representation of the required buffer
            // NOTE: in this implementation, we ignore the representation and create our own buffer
            //
            // fWinIni = flags
            // NOTE: in this implementation, we ignore the fWiniIni flags (since this is a get operation, and fWinIni flags are only for set operations)
            var fWinIni = PInvoke.User32.SystemParametersInfoFlags.None;

            string pvParamAsString;

            var pointerToPvParam = Marshal.AllocHGlobal((int)uiParam * sizeof(char));
            try
            {
                var spiResult = PInvoke.User32.SystemParametersInfo(PInvoke.User32.SystemParametersInfoAction.SPI_GETDESKWALLPAPER, uiParam, pointerToPvParam, fWinIni);
                if (spiResult == true)
                {
                    pvParamAsString = Marshal.PtrToStringUni(pointerToPvParam)!;
                }
                else
                {
                    return (IMorphicResult.ErrorResult, values);
                }
            }
            catch
            {
                return (IMorphicResult.ErrorResult, values);
            }
            finally
            {
                Marshal.FreeHGlobal(pointerToPvParam);
            }

            //

            var success = true;

            foreach (Setting setting in settings)
            {
                switch (setting.Name)
                {
                    case "ImageConfig":
                        // NOTE: this is another area where changing the result of GetValue to an IMorphicResult could provide clear and granular success/error result
                        values.Add(setting, pvParamAsString);
                        break;
                    default:
                        success = false;
                        values.Add(setting, null, Values.ValueType.NotFound);
                        continue;
                }
            }

            return ((success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult), values);
        }

        private static (IMorphicResult, Values) SpiGetFilterKeys(IEnumerable<Setting> settings)
        {
            // uiParam
            // NOTE: in this implementation, we ignore the representation and pass in the actual filterkeys struct size in the API call
            //
            // pvParam
            // NOTE: in this implementation, we ignore the representation and create our own buffer
            //
            // fWinIni = flags
            // NOTE: in this implementation, we ignore the fWiniIni flags (since this is a get operation, and fWinIni flags are only for set operations)

            var internalGetFilterKeysResult = SPISettingsHandler.InternalSpiGetFilterKeys();
            if (internalGetFilterKeysResult.IsError == true)
            {
                // NOTE: we may want to consider returning a Values set which says "an internal error resulted in values not being returned"
                return (IMorphicResult.ErrorResult, new Values());
            }
            var filterKeys = internalGetFilterKeysResult.Value!;

            //

            var success = true;
            var values = new Values();

            foreach (Setting setting in settings)
            {
                switch (setting.Name)
                {
                    case "FilterKeysEnable":
                        var filterKeysOn = (filterKeys.dwFlags & ExtendedPInvoke.FKF_FILTERKEYSON) == ExtendedPInvoke.FKF_FILTERKEYSON;
                        values.Add(setting, filterKeysOn);
                        break;
                    case "SlowKeysInterval":
                        // NOTE: in the GPII win32.json5 registry, it appears that this value might have been returned as "0" if filterkeys was disabled
                        var slowKeysInterval = filterKeys.iWaitMSec;
                        values.Add(setting, slowKeysInterval);
                        break;
                    default:
                        success = false;
                        values.Add(setting, null, Values.ValueType.NotFound);
                        continue;
                }
            }

            return ((success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult), values);
        }

        private static IMorphicResult<ExtendedPInvoke.FILTERKEYS> InternalSpiGetFilterKeys()
        {
            // uiParam
            // NOTE: in this implementation, we ignore the representation and pass in the actual filterkeys struct size in the API call
            //
            // pvParam
            // NOTE: in this implementation, we ignore the representation and create our own buffer
            //
            // fWinIni = flags
            // NOTE: in this implementation, we ignore the fWiniIni flags (since this is a get operation, and fWinIni flags are only for set operations)

            ExtendedPInvoke.FILTERKEYS result;

            var fWinIni = PInvoke.User32.SystemParametersInfoFlags.None;

            ExtendedPInvoke.FILTERKEYS pvParamAsFilterKeys = new ExtendedPInvoke.FILTERKEYS
            {
                cbSize = (uint)Marshal.SizeOf<ExtendedPInvoke.FILTERKEYS>()
            };

            var pointerToFilterKeys = Marshal.AllocHGlobal(Marshal.SizeOf<ExtendedPInvoke.FILTERKEYS>());
            try
            {
                Marshal.StructureToPtr(pvParamAsFilterKeys, pointerToFilterKeys, false);

                var spiResult = PInvoke.User32.SystemParametersInfo(PInvoke.User32.SystemParametersInfoAction.SPI_GETFILTERKEYS, pvParamAsFilterKeys.cbSize, pointerToFilterKeys, fWinIni);
                if (spiResult == true)
                {
                    result = Marshal.PtrToStructure<ExtendedPInvoke.FILTERKEYS>(pointerToFilterKeys);
                }
                else
                {
                    return IMorphicResult<ExtendedPInvoke.FILTERKEYS>.ErrorResult();
                }
            }
            catch
            {
                return IMorphicResult<ExtendedPInvoke.FILTERKEYS>.ErrorResult();
            }
            finally
            {
                Marshal.FreeHGlobal(pointerToFilterKeys);
            }

            return IMorphicResult<ExtendedPInvoke.FILTERKEYS>.SuccessResult(result);
        }

        private static (IMorphicResult, Values) SpiGetHighContrast(IEnumerable<Setting> settings)
        {
            // uiParam
            // NOTE: in this implementation, we ignore the representation and pass in the actual highcontrast struct size in the API call
            //
            // pvParam
            // NOTE: in this implementation, we ignore the representation and create our own buffer
            //
            // fWinIni = flags
            // NOTE: in this implementation, we ignore the fWiniIni flags (since this is a get operation, and fWinIni flags are only for set operations)

            var internalGetHighContrastResult = SPISettingsHandler.InternalSpiGetHighContrast();
            if (internalGetHighContrastResult.IsError == true)
            {
                // NOTE: we may want to consider returning a Values set which says "an internal error resulted in values not being returned"
                return (IMorphicResult.ErrorResult, new Values());
            }
            var highContrast = internalGetHighContrastResult.Value!;

            //

            var success = true;
            var values = new Values();

            foreach (Setting setting in settings)
            {
                switch (setting.Name)
                {
                    case "HighContrastOn":
                        var highContrastOn = (highContrast.dwFlags & ExtendedPInvoke.HighContrastFlags.HCF_HIGHCONTRASTON) == ExtendedPInvoke.HighContrastFlags.HCF_HIGHCONTRASTON;
                        values.Add(setting, highContrastOn);
                        break;
                    default:
                        success = false;
                        values.Add(setting, null, Values.ValueType.NotFound);
                        continue;
                }
            }

            return ((success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult), values);
        }

        private static IMorphicResult<ExtendedPInvoke.HIGHCONTRAST> InternalSpiGetHighContrast()
        {
            // uiParam
            // NOTE: in this implementation, we ignore the representation and pass in the actual highcontrast struct size in the API call
            //
            // pvParam
            // NOTE: in this implementation, we ignore the representation and create our own buffer
            //
            // fWinIni = flags
            // NOTE: in this implementation, we ignore the fWiniIni flags (since this is a get operation, and fWinIni flags are only for set operations)

            ExtendedPInvoke.HIGHCONTRAST result;

            var fWinIni = PInvoke.User32.SystemParametersInfoFlags.None;

            ExtendedPInvoke.HIGHCONTRAST pvParamAsHighContrast = ExtendedPInvoke.HIGHCONTRAST.CreateNew();

            var pointerToHighContrast = Marshal.AllocHGlobal(Marshal.SizeOf<ExtendedPInvoke.HIGHCONTRAST>());
            try
            {
                Marshal.StructureToPtr(pvParamAsHighContrast, pointerToHighContrast, false);

                var spiResult = PInvoke.User32.SystemParametersInfo(PInvoke.User32.SystemParametersInfoAction.SPI_GETHIGHCONTRAST, pvParamAsHighContrast.cbSize, pointerToHighContrast, fWinIni);
                if (spiResult == true)
                {
                    result = Marshal.PtrToStructure<ExtendedPInvoke.HIGHCONTRAST>(pointerToHighContrast);
                }
                else
                {
                    return IMorphicResult<ExtendedPInvoke.HIGHCONTRAST>.ErrorResult();
                }
            }
            catch
            {
                return IMorphicResult<ExtendedPInvoke.HIGHCONTRAST>.ErrorResult();
            }
            finally
            {
                Marshal.FreeHGlobal(pointerToHighContrast);
            }

            return IMorphicResult<ExtendedPInvoke.HIGHCONTRAST>.SuccessResult(result);
        }

        //

        // OBSERVATION: this information is compiled in the solutions registry, but we should consider hard-coding it instead for security; for now it's compiled into code.
        private static IMorphicResult<PInvoke.User32.SystemParametersInfoFlags> ParseWinIniFlags(string fWinIniAsString)
        {
            var result = PInvoke.User32.SystemParametersInfoFlags.None;

            if (fWinIniAsString != String.Empty)
            {
                var stringComponents = fWinIniAsString.Split('|');
                foreach (String stringComponent in stringComponents)
                {
                    switch (stringComponent)
                    {
                        case "SPIF_SENDCHANGE":
                            result |= PInvoke.User32.SystemParametersInfoFlags.SPIF_SENDCHANGE;
                            break;
                        case "SPIF_SENDWININICHANGE":
                            result |= PInvoke.User32.SystemParametersInfoFlags.SPIF_SENDWININICHANGE;
                            break;
                        case "SPIF_UPDATEINIFILE":
                            result |= PInvoke.User32.SystemParametersInfoFlags.SPIF_UPDATEINIFILE;
                            break;
                        default:
                            Debug.Assert(false, "Invalid fWinIni option: " + stringComponent);
                            throw new ArgumentOutOfRangeException(nameof(fWinIniAsString));
                    }
                }
            }

            return IMorphicResult<PInvoke.User32.SystemParametersInfoFlags>.SuccessResult(result);
        }

        //

        public override async Task<IMorphicResult> SetAsync(SettingGroup settingGroup, Values values)
        {
            bool success;

            var spiSettingGroupAsOptional = settingGroup as SPISettingGroup;
            if (spiSettingGroupAsOptional == null)
            {
                Debug.Assert(false);
                return IMorphicResult.ErrorResult;
            }
            var spiSettingGroup = spiSettingGroupAsOptional!;

            switch (spiSettingGroup.setAction)
            {
                case "SPI_SETDESKWALLPAPER":
                    {
                        var spiSetMorphicResult = SPISettingsHandler.SpiSetDesktopWallpaper(spiSettingGroup.fWinIni, values);
                        success = spiSetMorphicResult.IsSuccess;
                    }
                    break;
                case "SPI_SETFILTERKEYS":
                    {
                        var spiSetMorphicResult = SPISettingsHandler.SpiSetFilterKeys(spiSettingGroup.fWinIni, values);
                        success = spiSetMorphicResult.IsSuccess;
                    }
                    break;
                case "SPI_SETHIGHCONTRAST":
                    {
                        var spiSetMorphicResult = SPISettingsHandler.SpiSetHighContrast(spiSettingGroup.fWinIni, values);
                        success = spiSetMorphicResult.IsSuccess;
                    }
                    break;
                default:
                    success = false;
                    break;
            }

            return success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult;
        }

        //

        private static IMorphicResult SpiSetDesktopWallpaper(string? fWinIniAsString, Values values)
        {
            var success = true;

            foreach (var value in values)
            {
                switch(value.Key.Name)
                {
                    case "ImageConfig":
                        {
                            var valueAsNullableString = value.Value as string;
                            if (valueAsNullableString == null)
                            {
                                success = false;
                                continue;
                            }
                            var valueAsString = valueAsNullableString!;

                            // uiParam = length of pvParam buffer (up to MAX_PATH)
                            var uiParam = (uint)valueAsString.Length + 1; /* + 1 = null terminator */
                            uiParam = Math.Min(uiParam, ExtendedPInvoke.MAX_PATH);
                            //
                            // pvParam = representation of the required buffer
                            // NOTE: in this implementation, we ignore the representation and create our own buffer
                            //
                            // fWinIni = flags
                            // OBSERVATION: for security purposes, we may want to consider hard-coding these flags or otherwise limiting them
                            // NOTE: we should review and sanity-check the setting in the solutions registry
                            var fWinIni = PInvoke.User32.SystemParametersInfoFlags.None;
                            if (fWinIniAsString != null)
                            {
                                var parseFlagsResult = SPISettingsHandler.ParseWinIniFlags(fWinIniAsString);
                                if (parseFlagsResult.IsSuccess == true)
                                {
                                    fWinIni = parseFlagsResult.Value!;
                                }
                            }

                            // TODO: validate the value (which is a path, which must point to a safe place to retrieve images, which must point to a folder and file where the user has read access, and which must be an image type compatible with using as wallpaper)

                            var pointerToPvParam = Marshal.StringToHGlobalUni(valueAsString);
                            try
                            {
                                var spiResult = PInvoke.User32.SystemParametersInfo(PInvoke.User32.SystemParametersInfoAction.SPI_SETDESKWALLPAPER, uiParam, pointerToPvParam, fWinIni);
                                if (spiResult == false)
                                {
                                    success = false;
                                }
                            }
                            catch
                            {
                                success = false;
                            }
                            finally
                            {
                                Marshal.FreeHGlobal(pointerToPvParam);
                            }
                        }
                        break;
                    default:
                        success = false;
                        continue;
                }
            }

            return success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult;
        }

        private static IMorphicResult SpiSetFilterKeys(string? fWinIniAsString, Values values)
        {
            var success = true;

            // capture the current FilterKeys struct data up-front
            var internalGetFilterKeysResult = SPISettingsHandler.InternalSpiGetFilterKeys();
            if (internalGetFilterKeysResult.IsError == true)
            {
                return IMorphicResult.ErrorResult;
            }
            var filterKeys = internalGetFilterKeysResult.Value!;

            foreach (var value in values)
            {
                switch (value.Key.Name)
                {
                    case "FilterKeysEnable":
                        {
                            var valueAsNullableBool = value.Value as bool?;
                            if (valueAsNullableBool == null)
                            {
                                success = false;
                                continue;
                            }
                            var valueAsBool = valueAsNullableBool!;

                            if (valueAsBool == true)
                            {
                                filterKeys.dwFlags |= ExtendedPInvoke.FKF_FILTERKEYSON;
                            }
                            else
                            {
                                filterKeys.dwFlags &= ~ExtendedPInvoke.FKF_FILTERKEYSON;
                            }
                        }
                        break;
                    case "SlowKeysInterval":
                        {
                            var convertValueToUIntResult = ConversionUtils.TryConvertObjectToUInt(value.Value);
                            if (convertValueToUIntResult.IsError == true)
                            {
                                success = false;
                                continue;
                            }
                            var valueAsUInt = convertValueToUIntResult.Value!;

                            filterKeys.iWaitMSec = valueAsUInt;
                        }
                        break;
                    default:
                        success = false;
                        continue;
                }
            }

            // uiParam
            // NOTE: in this implementation, we ignore the representation and pass in the actual filterkeys struct size in the API call
            //
            // pvParam
            // NOTE: in this implementation, we ignore the representation and create our own buffer
            //
            // fWinIni = flags
            // OBSERVATION: for security purposes, we may want to consider hard-coding these flags or otherwise limiting them
            // NOTE: we should review and sanity-check the setting in the solutions registry
            var fWinIni = PInvoke.User32.SystemParametersInfoFlags.None;
            if (fWinIniAsString != null)
            {
                var parseFlagsResult = SPISettingsHandler.ParseWinIniFlags(fWinIniAsString);
                if (parseFlagsResult.IsSuccess == true)
                {
                    fWinIni = parseFlagsResult.Value!;
                }
            }

            var internalSetFilterKeysResult = SPISettingsHandler.InternalSpiSetFilterKeys(filterKeys, fWinIni);
            if (internalSetFilterKeysResult.IsError == true)
            {
                return IMorphicResult.ErrorResult;
            }

            return success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult;
        }

        private static IMorphicResult InternalSpiSetFilterKeys(ExtendedPInvoke.FILTERKEYS filterKeys, PInvoke.User32.SystemParametersInfoFlags fWinIni)
        {
            // sanity check
            if (filterKeys.cbSize != Marshal.SizeOf<ExtendedPInvoke.FILTERKEYS>()) {
                throw new ArgumentException(nameof(filterKeys));
            }

            var success = true;

            var pointerToFilterKeys = Marshal.AllocHGlobal(Marshal.SizeOf<ExtendedPInvoke.FILTERKEYS>());
            try
            {
                Marshal.StructureToPtr(filterKeys, pointerToFilterKeys, false);

                var spiResult = PInvoke.User32.SystemParametersInfo(PInvoke.User32.SystemParametersInfoAction.SPI_SETFILTERKEYS, filterKeys.cbSize, pointerToFilterKeys, fWinIni);
                if (spiResult == false)
                {
                    success = false;
                }
            }
            catch
            {
                success = false;
            }
            finally
            {
                Marshal.FreeHGlobal(pointerToFilterKeys);
            }

            return success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult;
        }

        private static IMorphicResult SpiSetHighContrast(string? fWinIniAsString, Values values)
        {
            var success = true;

            // capture the current HighContrast struct data up-front
            var internalGetHighContrastResult = SPISettingsHandler.InternalSpiGetHighContrast();
            if (internalGetHighContrastResult.IsError == true)
            {
                return IMorphicResult.ErrorResult;
            }
            var highContrast = internalGetHighContrastResult.Value!;

            foreach (var value in values)
            {
                switch (value.Key.Name)
                {
                    case "HighContrastOn":
                        {
                            var valueAsNullableBool = value.Value as bool?;
                            if (valueAsNullableBool == null)
                            {
                                success = false;
                                continue;
                            }
                            var valueAsBool = valueAsNullableBool!;

                            if (valueAsBool == true)
                            {
                                highContrast.dwFlags |= ExtendedPInvoke.HighContrastFlags.HCF_HIGHCONTRASTON;
                            }
                            else
                            {
                                highContrast.dwFlags &= ~ExtendedPInvoke.HighContrastFlags.HCF_HIGHCONTRASTON;
                            }
                        }
                        break;
                    default:
                        success = false;
                        continue;
                }
            }

            // uiParam
            // NOTE: in this implementation, we ignore the representation and pass in the actual highcontrast struct size in the API call
            //
            // pvParam
            // NOTE: in this implementation, we ignore the representation and create our own buffer
            //
            // fWinIni = flags
            // OBSERVATION: for security purposes, we may want to consider hard-coding these flags or otherwise limiting them
            // NOTE: we should review and sanity-check the setting in the solutions registry
            var fWinIni = PInvoke.User32.SystemParametersInfoFlags.None;
            if (fWinIniAsString != null)
            {
                var parseFlagsResult = SPISettingsHandler.ParseWinIniFlags(fWinIniAsString);
                if (parseFlagsResult.IsSuccess == true)
                {
                    fWinIni = parseFlagsResult.Value!;
                }
            }

            var internalSetHighContrastResult = SPISettingsHandler.InternalSpiSetHighContrast(highContrast, fWinIni);
            if (internalSetHighContrastResult.IsError == true)
            {
                return IMorphicResult.ErrorResult;
            }

            return success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult;
        }

        private static IMorphicResult InternalSpiSetHighContrast(ExtendedPInvoke.HIGHCONTRAST highContrast, PInvoke.User32.SystemParametersInfoFlags fWinIni)
        {
            // sanity check
            if (highContrast.cbSize != Marshal.SizeOf<ExtendedPInvoke.HIGHCONTRAST>())
            {
                throw new ArgumentException(nameof(highContrast));
            }

            var success = true;

            var pointerToHighContrast = Marshal.AllocHGlobal(Marshal.SizeOf<ExtendedPInvoke.HIGHCONTRAST>());
            try
            {
                Marshal.StructureToPtr(highContrast, pointerToHighContrast, false);

                var spiResult = PInvoke.User32.SystemParametersInfo(PInvoke.User32.SystemParametersInfoAction.SPI_SETHIGHCONTRAST, highContrast.cbSize, pointerToHighContrast, fWinIni);
                if (spiResult == false)
                {
                    success = false;
                }
            }
            catch
            {
                success = false;
            }
            finally
            {
                Marshal.FreeHGlobal(pointerToHighContrast);
            }

            return success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult;
        }


    }
}
