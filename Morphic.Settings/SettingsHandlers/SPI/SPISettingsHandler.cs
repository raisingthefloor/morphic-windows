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
                case "SPI_GETAUDIODESCRIPTION":
                    {
                        var (spiGetMorphicResult, spiGetValues) = SPISettingsHandler.SpiGetAudioDescription(settings);
                        success = spiGetMorphicResult.IsSuccess;
                        if (success == true)
                        {
                            values = spiGetValues;
                        }
                    }
                    break;
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
                case "SPI_GETKEYBOARDCUES":
                    {
                        var (spiGetMorphicResult, spiGetValues) = SPISettingsHandler.SpiGetKeyboardCues(settings);
                        success = spiGetMorphicResult.IsSuccess;
                        if (success == true)
                        {
                            values = spiGetValues;
                        }
                    }
                    break;
                case "SPI_GETKEYBOARDPREF":
                    {
                        var (spiGetMorphicResult, spiGetValues) = SPISettingsHandler.SpiGetKeyboardPref(settings);
                        success = spiGetMorphicResult.IsSuccess;
                        if (success == true)
                        {
                            values = spiGetValues;
                        }
                    }
                    break;
                case "SPI_GETMOUSEBUTTONSWAP":
                    {
                        var (spiGetMorphicResult, spiGetValues) = SPISettingsHandler.SystemMetricsGetMouseButtonSwap(settings);
                        success = spiGetMorphicResult.IsSuccess;
                        if (success == true)
                        {
                            values = spiGetValues;
                        }
                    }
                    break;
                case "SPI_GETMESSAGEDURATION":
                    {
                        var (spiGetMorphicResult, spiGetValues) = SPISettingsHandler.SpiGetMessageDuration(settings);
                        success = spiGetMorphicResult.IsSuccess;
                        if (success == true)
                        {
                            values = spiGetValues;
                        }
                    }
                    break;
                case "SPI_GETMOUSEKEYS":
                    {
                        var (spiGetMorphicResult, spiGetValues) = SPISettingsHandler.SpiGetMouseKeys(settings);
                        success = spiGetMorphicResult.IsSuccess;
                        if (success == true)
                        {
                            values = spiGetValues;
                        }
                    }
                    break;
                case "SPI_GETMOUSETRAILS":
                    {
                        var (spiGetMorphicResult, spiGetValues) = SPISettingsHandler.SpiGetMouseTrails(settings);
                        success = spiGetMorphicResult.IsSuccess;
                        if (success == true)
                        {
                            values = spiGetValues;
                        }
                    }
                    break;
                case "SPI_GETSTICKYKEYS":
                    {
                        var (spiGetMorphicResult, spiGetValues) = SPISettingsHandler.SpiGetStickyKeys(settings);
                        success = spiGetMorphicResult.IsSuccess;
                        if (success == true)
                        {
                            values = spiGetValues;
                        }
                    }
                    break;
                case "SPI_GETTOGGLEKEYS":
                    {
                        var (spiGetMorphicResult, spiGetValues) = SPISettingsHandler.SpiGetToggleKeys(settings);
                        success = spiGetMorphicResult.IsSuccess;
                        if (success == true)
                        {
                            values = spiGetValues;
                        }
                    }
                    break;
                case "SPI_GETWHEELSCROLLLINES":
                    {
                        var (spiGetMorphicResult, spiGetValues) = SPISettingsHandler.SpiGetWheelScrollLines(settings);
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

        private static (IMorphicResult, Values) SpiGetAudioDescription(IEnumerable<Setting> settings)
        {
            // uiParam
            // NOTE: in this implementation, we ignore the representation and pass in the actual audiodescription struct size in the API call
            //
            // pvParam
            // NOTE: in this implementation, we ignore the representation and create our own buffer
            //
            // fWinIni = flags
            // NOTE: in this implementation, we ignore the fWiniIni flags (since this is a get operation, and fWinIni flags are only for set operations)

            var internalGetAudioDescriptionResult = SPISettingsHandler.InternalSpiGetAudioDescription();
            if (internalGetAudioDescriptionResult.IsError == true)
            {
                // NOTE: we may want to consider returning a Values set which says "an internal error resulted in values not being returned"
                return (IMorphicResult.ErrorResult, new Values());
            }
            var audioDescriptionStruct = internalGetAudioDescriptionResult.Value!;

            //

            var success = true;
            var values = new Values();

            foreach (Setting setting in settings)
            {
                switch (setting.Name)
                {
                    case "AudioDescriptionOn":
                        var audioDescriptionOn = audioDescriptionStruct.Enabled;
                        values.Add(setting, audioDescriptionOn);
                        break;
                    default:
                        success = false;
                        values.Add(setting, null, Values.ValueType.NotFound);
                        continue;
                }
            }

            return ((success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult), values);
        }

        private static IMorphicResult<ExtendedPInvoke.AUDIODESCRIPTION> InternalSpiGetAudioDescription()
        {
            // uiParam
            // NOTE: in this implementation, we ignore the representation and pass in the actual audiodescription struct size in the API call
            //
            // pvParam
            // NOTE: in this implementation, we ignore the representation and create our own buffer
            //
            // fWinIni = flags
            // NOTE: in this implementation, we ignore the fWiniIni flags (since this is a get operation, and fWinIni flags are only for set operations)

            ExtendedPInvoke.AUDIODESCRIPTION result;

            var fWinIni = PInvoke.User32.SystemParametersInfoFlags.None;

            ExtendedPInvoke.AUDIODESCRIPTION pvParamAsAudioDescription = new ExtendedPInvoke.AUDIODESCRIPTION
            {
                cbSize = (uint)Marshal.SizeOf<ExtendedPInvoke.AUDIODESCRIPTION>()
            };

            var pointerToAudioDescription = Marshal.AllocHGlobal(Marshal.SizeOf<ExtendedPInvoke.AUDIODESCRIPTION>());
            try
            {
                Marshal.StructureToPtr(pvParamAsAudioDescription, pointerToAudioDescription, false);

                var spiResult = PInvoke.User32.SystemParametersInfo((PInvoke.User32.SystemParametersInfoAction)ExtendedPInvoke.SPI_GETAUDIODESCRIPTION, pvParamAsAudioDescription.cbSize, pointerToAudioDescription, fWinIni);
                if (spiResult == true)
                {
                    result = Marshal.PtrToStructure<ExtendedPInvoke.AUDIODESCRIPTION>(pointerToAudioDescription);
                }
                else
                {
                    return IMorphicResult<ExtendedPInvoke.AUDIODESCRIPTION>.ErrorResult();
                }
            }
            catch
            {
                return IMorphicResult<ExtendedPInvoke.AUDIODESCRIPTION>.ErrorResult();
            }
            finally
            {
                Marshal.FreeHGlobal(pointerToAudioDescription);
            }

            return IMorphicResult<ExtendedPInvoke.AUDIODESCRIPTION>.SuccessResult(result);
        }

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

        private static (IMorphicResult, Values) SpiGetKeyboardCues(IEnumerable<Setting> settings)
        {
            // uiParam
            // NOTE: in this implementation, we ignore the representation and pass in the actual bool type size in the API call
            //
            // pvParam
            // NOTE: in this implementation, we ignore the representation and create our own buffer
            //
            // fWinIni = flags
            // NOTE: in this implementation, we ignore the fWiniIni flags (since this is a get operation, and fWinIni flags are only for set operations)

            var internalGetKeyboardCuesResult = SPISettingsHandler.InternalSpiGetKeyboardCues();
            if (internalGetKeyboardCuesResult.IsError == true)
            {
                // NOTE: we may want to consider returning a Values set which says "an internal error resulted in values not being returned"
                return (IMorphicResult.ErrorResult, new Values());
            }
            var keyboardCuesValue = internalGetKeyboardCuesResult.Value!;

            //

            var success = true;
            var values = new Values();

            foreach (Setting setting in settings)
            {
                switch (setting.Name)
                {
                    case "UnderlineMenuShortcutsOn":
                        var underlineMenuShortcutsOn = keyboardCuesValue;
                        values.Add(setting, underlineMenuShortcutsOn);
                        break;
                    default:
                        success = false;
                        values.Add(setting, null, Values.ValueType.NotFound);
                        continue;
                }
            }

            return ((success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult), values);
        }

        private static IMorphicResult<bool> InternalSpiGetKeyboardCues()
        {
            // uiParam
            // NOTE: in this implementation, we ignore the representation and pass in the actual bool type size in the API call
            //
            // pvParam
            // NOTE: in this implementation, we ignore the representation and create our own buffer
            //
            // fWinIni = flags
            // NOTE: in this implementation, we ignore the fWiniIni flags (since this is a get operation, and fWinIni flags are only for set operations)

            bool result;

            var fWinIni = PInvoke.User32.SystemParametersInfoFlags.None;

            bool pvParamAsBool = false;
            var pointerToBool = Marshal.AllocHGlobal(Marshal.SizeOf<bool>());
            try
            {
                Marshal.StructureToPtr(pvParamAsBool, pointerToBool, false);

                var spiResult = PInvoke.User32.SystemParametersInfo(PInvoke.User32.SystemParametersInfoAction.SPI_GETKEYBOARDCUES, 0, pointerToBool, fWinIni);
                if (spiResult == true)
                {
                    result = Marshal.PtrToStructure<bool>(pointerToBool);
                }
                else
                {
                    return IMorphicResult<bool>.ErrorResult();
                }
            }
            catch
            {
                return IMorphicResult<bool>.ErrorResult();
            }
            finally
            {
                Marshal.FreeHGlobal(pointerToBool);
            }

            return IMorphicResult<bool>.SuccessResult(result);
        }

        private static (IMorphicResult, Values) SpiGetKeyboardPref(IEnumerable<Setting> settings)
        {
            // uiParam
            // NOTE: in this implementation, we ignore the representation and pass in the actual bool type size in the API call
            //
            // pvParam
            // NOTE: in this implementation, we ignore the representation and create our own buffer
            //
            // fWinIni = flags
            // NOTE: in this implementation, we ignore the fWiniIni flags (since this is a get operation, and fWinIni flags are only for set operations)

            var internalGetKeyboardPrefResult = SPISettingsHandler.InternalSpiGetKeyboardPref();
            if (internalGetKeyboardPrefResult.IsError == true)
            {
                // NOTE: we may want to consider returning a Values set which says "an internal error resulted in values not being returned"
                return (IMorphicResult.ErrorResult, new Values());
            }
            var keyboardPrefValue = internalGetKeyboardPrefResult.Value!;

            //

            var success = true;
            var values = new Values();

            foreach (Setting setting in settings)
            {
                switch (setting.Name)
                {
                    case "KeyboardPreferenceOn":
                        var keyboardPreferenceOn = keyboardPrefValue;
                        values.Add(setting, keyboardPreferenceOn);
                        break;
                    default:
                        success = false;
                        values.Add(setting, null, Values.ValueType.NotFound);
                        continue;
                }
            }

            return ((success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult), values);
        }

        private static IMorphicResult<bool> InternalSpiGetKeyboardPref()
        {
            // uiParam
            // NOTE: in this implementation, we ignore the representation and pass in the actual bool type size in the API call
            //
            // pvParam
            // NOTE: in this implementation, we ignore the representation and create our own buffer
            //
            // fWinIni = flags
            // NOTE: in this implementation, we ignore the fWiniIni flags (since this is a get operation, and fWinIni flags are only for set operations)

            bool result;

            var fWinIni = PInvoke.User32.SystemParametersInfoFlags.None;

            bool pvParamAsBool = false;
            var pointerToBool = Marshal.AllocHGlobal(Marshal.SizeOf<bool>());
            try
            {
                Marshal.StructureToPtr(pvParamAsBool, pointerToBool, false);

                var spiResult = PInvoke.User32.SystemParametersInfo(PInvoke.User32.SystemParametersInfoAction.SPI_GETKEYBOARDPREF, 0, pointerToBool, fWinIni);
                if (spiResult == true)
                {
                    result = Marshal.PtrToStructure<bool>(pointerToBool);
                }
                else
                {
                    return IMorphicResult<bool>.ErrorResult();
                }
            }
            catch
            {
                return IMorphicResult<bool>.ErrorResult();
            }
            finally
            {
                Marshal.FreeHGlobal(pointerToBool);
            }

            return IMorphicResult<bool>.SuccessResult(result);
        }

        private static (IMorphicResult, Values) SpiGetMessageDuration(IEnumerable<Setting> settings)
        {
            // uiParam
            // NOTE: in this implementation, we ignore the representation as it is unused
            //
            // pvParam
            // NOTE: in this implementation, we ignore the representation and create our own buffer
            //
            // fWinIni = flags
            // NOTE: in this implementation, we ignore the fWiniIni flags (since this is a get operation, and fWinIni flags are only for set operations)

            var internalGetMessageDurationResult = SPISettingsHandler.InternalSpiGetMessageDuration();
            if (internalGetMessageDurationResult.IsError == true)
            {
                // NOTE: we may want to consider returning a Values set which says "an internal error resulted in values not being returned"
                return (IMorphicResult.ErrorResult, new Values());
            }
            var messageDurationInSeconds = internalGetMessageDurationResult.Value!;

            //

            var success = true;
            var values = new Values();

            foreach (Setting setting in settings)
            {
                switch (setting.Name)
                {
                    case "Duration":
                        values.Add(setting, messageDurationInSeconds);
                        break;
                    default:
                        success = false;
                        values.Add(setting, null, Values.ValueType.NotFound);
                        continue;
                }
            }

            return ((success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult), values);
        }

        private static IMorphicResult<ulong> InternalSpiGetMessageDuration()
        {
            // uiParam
            // NOTE: in this implementation, we ignore the representation as it is unused
            //
            // pvParam
            // NOTE: in this implementation, we ignore the representation and create our own buffer
            //
            // fWinIni = flags
            // NOTE: in this implementation, we ignore the fWiniIni flags (since this is a get operation, and fWinIni flags are only for set operations)

            // OBSERVATION: the Microsoft documentation says that the value is a ULONG, but the GPII and modern solutions registry says UINT; we are following the Microsoft docs
            ulong result;

            var fWinIni = PInvoke.User32.SystemParametersInfoFlags.None;

            ulong pvParamAsUlong = 0;

            var pointerToUlong = Marshal.AllocHGlobal(Marshal.SizeOf<ulong>());
            try
            {
                Marshal.StructureToPtr(pvParamAsUlong, pointerToUlong, false);

                var spiResult = PInvoke.User32.SystemParametersInfo((PInvoke.User32.SystemParametersInfoAction)ExtendedPInvoke.SPI_GETMESSAGEDURATION, 0, pointerToUlong, fWinIni);
                if (spiResult == true)
                {
                    result = Marshal.PtrToStructure<ulong>(pointerToUlong);
                }
                else
                {
                    return IMorphicResult<ulong>.ErrorResult();
                }
            }
            catch
            {
                return IMorphicResult<ulong>.ErrorResult();
            }
            finally
            {
                Marshal.FreeHGlobal(pointerToUlong);
            }

            return IMorphicResult<ulong>.SuccessResult(result);
        }

        // NOTE: to get the current mouse button swap state, the GetSystemMetrics API is used instead of SystemParametersInfo; this deserves some thoughts around refactoring
        private static (IMorphicResult, Values) SystemMetricsGetMouseButtonSwap(IEnumerable<Setting> settings)
        {
            // uiParam
            // NOTE: in this implementation, we ignore the representation as this setting does not use the SystemParametersInfo API
            //
            // pvParam
            // NOTE: in this implementation, we ignore the representation as this setting does not use the SystemParametersInfo API
            //
            // fWinIni = flags
            // NOTE: in this implementation, we ignore the fWiniIni flags as this setting does not use the SystemParametersInfo API

            var internalGetMouseButtonSwapResult = SPISettingsHandler.InternalSystemMetricsGetMouseButtonSwap();
            if (internalGetMouseButtonSwapResult.IsError == true)
            {
                // NOTE: we may want to consider returning a Values set which says "an internal error resulted in values not being returned"
                return (IMorphicResult.ErrorResult, new Values());
            }
            var mouseButtonSwapValue = internalGetMouseButtonSwapResult.Value!;

            //

            var success = true;
            var values = new Values();

            foreach (Setting setting in settings)
            {
                switch (setting.Name)
                {
                    case "SwapMouseButtonsConfig":
                        var swapMouseButtonsConfig = mouseButtonSwapValue;
                        values.Add(setting, swapMouseButtonsConfig);
                        break;
                    default:
                        success = false;
                        values.Add(setting, null, Values.ValueType.NotFound);
                        continue;
                }
            }

            return ((success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult), values);
        }

        // NOTE: to get the current mouse button swap state, the GetSystemMetrics API is used instead of SystemParametersInfo; this deserves some thoughts around refactoring
        private static IMorphicResult<bool> InternalSystemMetricsGetMouseButtonSwap()
        {
            var getSystemMetricsResult = PInvoke.User32.GetSystemMetrics(PInvoke.User32.SystemMetric.SM_SWAPBUTTON);

            bool result = (getSystemMetricsResult != 0) ? true : false;

            return IMorphicResult<bool>.SuccessResult(result);
        }

        private static (IMorphicResult, Values) SpiGetMouseKeys(IEnumerable<Setting> settings)
        {
            // uiParam
            // NOTE: in this implementation, we ignore the representation and pass in the actual mousekeys struct size in the API call
            //
            // pvParam
            // NOTE: in this implementation, we ignore the representation and create our own buffer
            //
            // fWinIni = flags
            // NOTE: in this implementation, we ignore the fWiniIni flags (since this is a get operation, and fWinIni flags are only for set operations)

            var internalGetMouseKeysResult = SPISettingsHandler.InternalSpiGetMouseKeys();
            if (internalGetMouseKeysResult.IsError == true)
            {
                // NOTE: we may want to consider returning a Values set which says "an internal error resulted in values not being returned"
                return (IMorphicResult.ErrorResult, new Values());
            }
            var mouseKeys = internalGetMouseKeysResult.Value!;

            //

            var success = true;
            var values = new Values();

            foreach (Setting setting in settings)
            {
                switch (setting.Name)
                {
                    case "MouseKeysOn":
                        var mouseKeysOn = (mouseKeys.dwFlags & ExtendedPInvoke.MKF_MOUSEKEYSON) == ExtendedPInvoke.MKF_MOUSEKEYSON;
                        values.Add(setting, mouseKeysOn);
                        break;
                    case "MaxSpeed":
                        double maxSpeedAsDouble = (double)(mouseKeys.iMaxSpeed - 10) / 350;
                        values.Add(setting, maxSpeedAsDouble);
                        break;
                    default:
                        success = false;
                        values.Add(setting, null, Values.ValueType.NotFound);
                        continue;
                }
            }

            return ((success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult), values);
        }

        private static IMorphicResult<ExtendedPInvoke.MOUSEKEYS> InternalSpiGetMouseKeys()
        {
            // uiParam
            // NOTE: in this implementation, we ignore the representation and pass in the actual mousekeys struct size in the API call
            //
            // pvParam
            // NOTE: in this implementation, we ignore the representation and create our own buffer
            //
            // fWinIni = flags
            // NOTE: in this implementation, we ignore the fWiniIni flags (since this is a get operation, and fWinIni flags are only for set operations)

            ExtendedPInvoke.MOUSEKEYS result;

            var fWinIni = PInvoke.User32.SystemParametersInfoFlags.None;

            ExtendedPInvoke.MOUSEKEYS pvParamAsMouseKeys = new ExtendedPInvoke.MOUSEKEYS
            {
                cbSize = (uint)Marshal.SizeOf<ExtendedPInvoke.MOUSEKEYS>()
            };

            var pointerToMouseKeys = Marshal.AllocHGlobal(Marshal.SizeOf<ExtendedPInvoke.MOUSEKEYS>());
            try
            {
                Marshal.StructureToPtr(pvParamAsMouseKeys, pointerToMouseKeys, false);

                var spiResult = PInvoke.User32.SystemParametersInfo(PInvoke.User32.SystemParametersInfoAction.SPI_GETMOUSEKEYS, pvParamAsMouseKeys.cbSize, pointerToMouseKeys, fWinIni);
                if (spiResult == true)
                {
                    result = Marshal.PtrToStructure<ExtendedPInvoke.MOUSEKEYS>(pointerToMouseKeys);
                }
                else
                {
                    return IMorphicResult<ExtendedPInvoke.MOUSEKEYS>.ErrorResult();
                }
            }
            catch
            {
                return IMorphicResult<ExtendedPInvoke.MOUSEKEYS>.ErrorResult();
            }
            finally
            {
                Marshal.FreeHGlobal(pointerToMouseKeys);
            }

            return IMorphicResult<ExtendedPInvoke.MOUSEKEYS>.SuccessResult(result);
        }

        private static (IMorphicResult, Values) SpiGetMouseTrails(IEnumerable<Setting> settings)
        {
            // uiParam
            // NOTE: in this implementation, we ignore the representation as it is unused
            //
            // pvParam
            // NOTE: in this implementation, we ignore the representation and create our own buffer
            //
            // fWinIni = flags
            // NOTE: in this implementation, we ignore the fWiniIni flags (since this is a get operation, and fWinIni flags are only for set operations)

            var internalGetMouseTrailsResult = SPISettingsHandler.InternalSpiGetMouseTrails();
            if (internalGetMouseTrailsResult.IsError == true)
            {
                // NOTE: we may want to consider returning a Values set which says "an internal error resulted in values not being returned"
                return (IMorphicResult.ErrorResult, new Values());
            }
            var mouseTrailsValue = internalGetMouseTrailsResult.Value!;

            //

            var success = true;
            var values = new Values();

            foreach (Setting setting in settings)
            {
                switch (setting.Name)
                {
                    case "MouseTrails":
                        // NOTE: if this value is out or range, we should handle the error condition
                        if (mouseTrailsValue < 0 || mouseTrailsValue > 10)
                        {
                            Debug.Assert(false, "MouseTrails value is out of range 0...10");
                        } 
                        values.Add(setting, mouseTrailsValue);
                        break;
                    default:
                        success = false;
                        values.Add(setting, null, Values.ValueType.NotFound);
                        continue;
                }
            }

            return ((success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult), values);
        }

        private static IMorphicResult<uint> InternalSpiGetMouseTrails()
        {
            // uiParam
            // NOTE: in this implementation, we ignore the representation as it is unused
            //
            // pvParam
            // NOTE: in this implementation, we ignore the representation and create our own buffer
            //
            // fWinIni = flags
            // NOTE: in this implementation, we ignore the fWiniIni flags (since this is a get operation, and fWinIni flags are only for set operations)

            // OBSERVATION: we did not find the exact type required for this data in the Microsoft documentation; they said "integer" so we assume int32/uint32,
            //              and the GPII-ported solutions registry says uint (uint32)
            uint result;

            var fWinIni = PInvoke.User32.SystemParametersInfoFlags.None;

            uint pvParamAsUint = 0;

            var pointerToUint = Marshal.AllocHGlobal(Marshal.SizeOf<uint>());
            try
            {
                Marshal.StructureToPtr(pvParamAsUint, pointerToUint, false);

                var spiResult = PInvoke.User32.SystemParametersInfo(PInvoke.User32.SystemParametersInfoAction.SPI_GETMOUSETRAILS, 0, pointerToUint, fWinIni);
                if (spiResult == true)
                {
                    result = Marshal.PtrToStructure<uint>(pointerToUint);
                }
                else
                {
                    return IMorphicResult<uint>.ErrorResult();
                }
            }
            catch
            {
                return IMorphicResult<uint>.ErrorResult();
            }
            finally
            {
                Marshal.FreeHGlobal(pointerToUint);
            }

            return IMorphicResult<uint>.SuccessResult(result);
        }

        private static (IMorphicResult, Values) SpiGetStickyKeys(IEnumerable<Setting> settings)
        {
            // uiParam
            // NOTE: in this implementation, we ignore the representation and pass in the actual stickykeys struct size in the API call
            //
            // pvParam
            // NOTE: in this implementation, we ignore the representation and create our own buffer
            //
            // fWinIni = flags
            // NOTE: in this implementation, we ignore the fWiniIni flags (since this is a get operation, and fWinIni flags are only for set operations)

            var internalGetStickyKeysResult = SPISettingsHandler.InternalSpiGetStickyKeys();
            if (internalGetStickyKeysResult.IsError == true)
            {
                // NOTE: we may want to consider returning a Values set which says "an internal error resulted in values not being returned"
                return (IMorphicResult.ErrorResult, new Values());
            }
            var stickyKeys = internalGetStickyKeysResult.Value!;

            //

            var success = true;
            var values = new Values();

            foreach (Setting setting in settings)
            {
                switch (setting.Name)
                {
                    case "StickyKeysOn":
                        var stickyKeysOn = (stickyKeys.dwFlags & ExtendedPInvoke.SKF_STICKYKEYSON) == ExtendedPInvoke.SKF_STICKYKEYSON;
                        values.Add(setting, stickyKeysOn);
                        break;
                    default:
                        success = false;
                        values.Add(setting, null, Values.ValueType.NotFound);
                        continue;
                }
            }

            return ((success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult), values);
        }

        private static IMorphicResult<ExtendedPInvoke.STICKYKEYS> InternalSpiGetStickyKeys()
        {
            // uiParam
            // NOTE: in this implementation, we ignore the representation and pass in the actual stickykeys struct size in the API call
            //
            // pvParam
            // NOTE: in this implementation, we ignore the representation and create our own buffer
            //
            // fWinIni = flags
            // NOTE: in this implementation, we ignore the fWiniIni flags (since this is a get operation, and fWinIni flags are only for set operations)

            ExtendedPInvoke.STICKYKEYS result;

            var fWinIni = PInvoke.User32.SystemParametersInfoFlags.None;

            ExtendedPInvoke.STICKYKEYS pvParamAsStickyKeys = new ExtendedPInvoke.STICKYKEYS
            {
                cbSize = (uint)Marshal.SizeOf<ExtendedPInvoke.STICKYKEYS>()
            };

            var pointerToStickyKeys = Marshal.AllocHGlobal(Marshal.SizeOf<ExtendedPInvoke.STICKYKEYS>());
            try
            {
                Marshal.StructureToPtr(pvParamAsStickyKeys, pointerToStickyKeys, false);

                var spiResult = PInvoke.User32.SystemParametersInfo(PInvoke.User32.SystemParametersInfoAction.SPI_GETSTICKYKEYS, pvParamAsStickyKeys.cbSize, pointerToStickyKeys, fWinIni);
                if (spiResult == true)
                {
                    result = Marshal.PtrToStructure<ExtendedPInvoke.STICKYKEYS>(pointerToStickyKeys);
                }
                else
                {
                    return IMorphicResult<ExtendedPInvoke.STICKYKEYS>.ErrorResult();
                }
            }
            catch
            {
                return IMorphicResult<ExtendedPInvoke.STICKYKEYS>.ErrorResult();
            }
            finally
            {
                Marshal.FreeHGlobal(pointerToStickyKeys);
            }

            return IMorphicResult<ExtendedPInvoke.STICKYKEYS>.SuccessResult(result);
        }

        private static (IMorphicResult, Values) SpiGetToggleKeys(IEnumerable<Setting> settings)
        {
            // uiParam
            // NOTE: in this implementation, we ignore the representation and pass in the actual togglekeys struct size in the API call
            //
            // pvParam
            // NOTE: in this implementation, we ignore the representation and create our own buffer
            //
            // fWinIni = flags
            // NOTE: in this implementation, we ignore the fWiniIni flags (since this is a get operation, and fWinIni flags are only for set operations)

            var internalGetToggleKeysResult = SPISettingsHandler.InternalSpiGetToggleKeys();
            if (internalGetToggleKeysResult.IsError == true)
            {
                // NOTE: we may want to consider returning a Values set which says "an internal error resulted in values not being returned"
                return (IMorphicResult.ErrorResult, new Values());
            }
            var toggleKeys = internalGetToggleKeysResult.Value!;

            //

            var success = true;
            var values = new Values();

            foreach (Setting setting in settings)
            {
                switch (setting.Name)
                {
                    case "ToggleKeysOn":
                        var toggleKeysOn = (toggleKeys.dwFlags & ExtendedPInvoke.TKF_TOGGLEKEYSON) == ExtendedPInvoke.TKF_TOGGLEKEYSON;
                        values.Add(setting, toggleKeysOn);
                        break;
                    default:
                        success = false;
                        values.Add(setting, null, Values.ValueType.NotFound);
                        continue;
                }
            }

            return ((success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult), values);
        }

        private static IMorphicResult<ExtendedPInvoke.TOGGLEKEYS> InternalSpiGetToggleKeys()
        {
            // uiParam
            // NOTE: in this implementation, we ignore the representation and pass in the actual togglekeys struct size in the API call
            //
            // pvParam
            // NOTE: in this implementation, we ignore the representation and create our own buffer
            //
            // fWinIni = flags
            // NOTE: in this implementation, we ignore the fWiniIni flags (since this is a get operation, and fWinIni flags are only for set operations)

            ExtendedPInvoke.TOGGLEKEYS result;

            var fWinIni = PInvoke.User32.SystemParametersInfoFlags.None;

            ExtendedPInvoke.TOGGLEKEYS pvParamAsToggleKeys = new ExtendedPInvoke.TOGGLEKEYS
            {
                cbSize = (uint)Marshal.SizeOf<ExtendedPInvoke.TOGGLEKEYS>()
            };

            var pointerToToggleKeys = Marshal.AllocHGlobal(Marshal.SizeOf<ExtendedPInvoke.TOGGLEKEYS>());
            try
            {
                Marshal.StructureToPtr(pvParamAsToggleKeys, pointerToToggleKeys, false);

                var spiResult = PInvoke.User32.SystemParametersInfo(PInvoke.User32.SystemParametersInfoAction.SPI_GETTOGGLEKEYS, pvParamAsToggleKeys.cbSize, pointerToToggleKeys, fWinIni);
                if (spiResult == true)
                {
                    result = Marshal.PtrToStructure<ExtendedPInvoke.TOGGLEKEYS>(pointerToToggleKeys);
                }
                else
                {
                    return IMorphicResult<ExtendedPInvoke.TOGGLEKEYS>.ErrorResult();
                }
            }
            catch
            {
                return IMorphicResult<ExtendedPInvoke.TOGGLEKEYS>.ErrorResult();
            }
            finally
            {
                Marshal.FreeHGlobal(pointerToToggleKeys);
            }

            return IMorphicResult<ExtendedPInvoke.TOGGLEKEYS>.SuccessResult(result);
        }


        private static (IMorphicResult, Values) SpiGetWheelScrollLines(IEnumerable<Setting> settings)
        {
            // uiParam
            // NOTE: in this implementation, we ignore the representation as it is unused
            //
            // pvParam
            // NOTE: in this implementation, we ignore the representation and create our own buffer
            //
            // fWinIni = flags
            // NOTE: in this implementation, we ignore the fWiniIni flags (since this is a get operation, and fWinIni flags are only for set operations)

            var internalGetWheelScrollLinesResult = SPISettingsHandler.InternalSpiGetWheelScrollLine();
            if (internalGetWheelScrollLinesResult.IsError == true)
            {
                // NOTE: we may want to consider returning a Values set which says "an internal error resulted in values not being returned"
                return (IMorphicResult.ErrorResult, new Values());
            }
            var wheelScrollLinesValue = internalGetWheelScrollLinesResult.Value!;

            //

            var success = true;
            var values = new Values();

            foreach (Setting setting in settings)
            {
                switch (setting.Name)
                {
                    case "ScrollWheelModeConfig":

                        values.Add(setting, wheelScrollLinesValue);
                        break;
                    default:
                        success = false;
                        values.Add(setting, null, Values.ValueType.NotFound);
                        continue;
                }
            }

            return ((success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult), values);
        }

        private static IMorphicResult<uint> InternalSpiGetWheelScrollLine()
        {
            // uiParam
            // NOTE: in this implementation, we ignore the representation as it is unused
            //
            // pvParam
            // NOTE: in this implementation, we ignore the representation and create our own buffer
            //
            // fWinIni = flags
            // NOTE: in this implementation, we ignore the fWiniIni flags (since this is a get operation, and fWinIni flags are only for set operations)

            // OBSERVATION: we did not find the exact type required for this data in the Microsoft documentation; they said "integer" so we assume int32/uint32,
            //              and the GPII-ported solutions registry says uint (uint32)
            uint result;

            var fWinIni = PInvoke.User32.SystemParametersInfoFlags.None;

            uint pvParamAsUint = 0;

            var pointerToUint = Marshal.AllocHGlobal(Marshal.SizeOf<uint>());
            try
            {
                Marshal.StructureToPtr(pvParamAsUint, pointerToUint, false);

                var spiResult = PInvoke.User32.SystemParametersInfo(PInvoke.User32.SystemParametersInfoAction.SPI_GETWHEELSCROLLLINES, 0, pointerToUint, fWinIni);
                if (spiResult == true)
                {
                    result = Marshal.PtrToStructure<uint>(pointerToUint);
                }
                else
                {
                    return IMorphicResult<uint>.ErrorResult();
                }
            }
            catch
            {
                return IMorphicResult<uint>.ErrorResult();
            }
            finally
            {
                Marshal.FreeHGlobal(pointerToUint);
            }

            return IMorphicResult<uint>.SuccessResult(result);
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
                case "SPI_SETAUDIODESCRIPTION":
                    {
                        var spiSetMorphicResult = SPISettingsHandler.SpiSetAudioDescription(spiSettingGroup.fWinIni, values);
                        success = spiSetMorphicResult.IsSuccess;
                    }
                    break;
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
                case "SPI_SETKEYBOARDCUES":
                    {
                        var spiSetMorphicResult = SPISettingsHandler.SpiSetKeyboardCues(spiSettingGroup.fWinIni, values);
                        success = spiSetMorphicResult.IsSuccess;
                    }
                    break;
                case "SPI_SETKEYBOARDPREF":
                    {
                        var spiSetMorphicResult = SPISettingsHandler.SpiSetKeyboardPref(spiSettingGroup.fWinIni, values);
                        success = spiSetMorphicResult.IsSuccess;
                    }
                    break;
                case "SPI_SETMOUSEBUTTONSWAP":
                    {
                        var spiSetMorphicResult = SPISettingsHandler.SpiSetMouseButtonSwap(spiSettingGroup.fWinIni, values);
                        success = spiSetMorphicResult.IsSuccess;
                    }
                    break;
                case "SPI_SETMESSAGEDURATION":
                    {
                        var spiSetMorphicResult = SPISettingsHandler.SpiSetMessageDuration(spiSettingGroup.fWinIni, values);
                        success = spiSetMorphicResult.IsSuccess;
                    }
                    break;
                case "SPI_SETMOUSEKEYS":
                    {
                        var spiSetMorphicResult = SPISettingsHandler.SpiSetMouseKeys(spiSettingGroup.fWinIni, values);
                        success = spiSetMorphicResult.IsSuccess;
                    }
                    break;
                case "SPI_SETMOUSETRAILS":
                    {
                        var spiSetMorphicResult = SPISettingsHandler.SpiSetMouseTrails(spiSettingGroup.fWinIni, values);
                        success = spiSetMorphicResult.IsSuccess;
                    }
                    break;
                case "SPI_SETSTICKYKEYS":
                    {
                        var spiSetMorphicResult = SPISettingsHandler.SpiSetStickyKeys(spiSettingGroup.fWinIni, values);
                        success = spiSetMorphicResult.IsSuccess;
                    }
                    break;
                case "SPI_SETTOGGLEKEYS":
                    {
                        var spiSetMorphicResult = SPISettingsHandler.SpiSetToggleKeys(spiSettingGroup.fWinIni, values);
                        success = spiSetMorphicResult.IsSuccess;
                    }
                    break;
                case "SPI_SETWHEELSCROLLLINES":
                    {
                        var spiSetMorphicResult = SPISettingsHandler.SpiSetToggleKeys(spiSettingGroup.fWinIni, values);
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

        private static IMorphicResult SpiSetAudioDescription(string? fWinIniAsString, Values values)
        {
            var success = true;

            // capture the current AudioDescription struct data up-front
            var internalGetAudioDescriptionResult = SPISettingsHandler.InternalSpiGetAudioDescription();
            if (internalGetAudioDescriptionResult.IsError == true)
            {
                return IMorphicResult.ErrorResult;
            }
            var audioDescription = internalGetAudioDescriptionResult.Value!;

            foreach (var value in values)
            {
                switch (value.Key.Name)
                {
                    case "AudioDescriptionOn":
                        {
                            var valueAsNullableBool = value.Value as bool?;
                            if (valueAsNullableBool == null)
                            {
                                success = false;
                                continue;
                            }
                            var valueAsBool = valueAsNullableBool.Value;

                            audioDescription.Enabled = valueAsBool;
                        }
                        break;
                    default:
                        success = false;
                        continue;
                }
            }

            // uiParam
            // NOTE: in this implementation, we ignore the representation and pass in the actual audiodescription struct size in the API call
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

            var internalSetAudioDescriptionResult = SPISettingsHandler.InternalSpiSetAudioDescription(audioDescription, fWinIni);
            if (internalSetAudioDescriptionResult.IsError == true)
            {
                return IMorphicResult.ErrorResult;
            }

            return success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult;
        }

        private static IMorphicResult InternalSpiSetAudioDescription(ExtendedPInvoke.AUDIODESCRIPTION audioDescription, PInvoke.User32.SystemParametersInfoFlags fWinIni)
        {
            // sanity check
            if (audioDescription.cbSize != Marshal.SizeOf<ExtendedPInvoke.AUDIODESCRIPTION>())
            {
                throw new ArgumentException(nameof(audioDescription));
            }

            var success = true;

            var pointerToAudioDescription = Marshal.AllocHGlobal(Marshal.SizeOf<ExtendedPInvoke.AUDIODESCRIPTION>());
            try
            {
                Marshal.StructureToPtr(audioDescription, pointerToAudioDescription, false);

                var spiResult = PInvoke.User32.SystemParametersInfo((PInvoke.User32.SystemParametersInfoAction)ExtendedPInvoke.SPI_SETAUDIODESCRIPTION, audioDescription.cbSize, pointerToAudioDescription, fWinIni);
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
                Marshal.FreeHGlobal(pointerToAudioDescription);
            }

            return success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult;
        }

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
                            var valueAsBool = valueAsNullableBool.Value;

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

        private static IMorphicResult SpiSetKeyboardCues(string? fWinIniAsString, Values values)
        {
            var success = true;

            bool? keyboardCuesValue = null;

            foreach (var value in values)
            {
                switch (value.Key.Name)
                {
                    case "UnderlineMenuShortcutsOn":
                        {
                            var valueAsNullableBool = value.Value as bool?;
                            if (valueAsNullableBool == null)
                            {
                                success = false;
                                continue;
                            }
                            var valueAsBool = valueAsNullableBool.Value;

                            keyboardCuesValue = valueAsBool;
                        }
                        break;
                    default:
                        success = false;
                        continue;
                }
            }

            if (keyboardCuesValue.HasValue == true)
            {
                // uiParam
                // NOTE: in this implementation, we ignore the representation and pass in the actual bool type size in the API call
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

                var internalSetKeyboardCuesResult = SPISettingsHandler.InternalSpiSetKeyboardCues(keyboardCuesValue.Value, fWinIni);
                if (internalSetKeyboardCuesResult.IsError == true)
                {
                    return IMorphicResult.ErrorResult;
                }
            }
            else
            {
                success = false;
            }

            return success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult;
        }

        private static IMorphicResult InternalSpiSetKeyboardCues(bool keyboardCuesValue, PInvoke.User32.SystemParametersInfoFlags fWinIni)
        {
            var success = true;

            var keyboardCuesValueAsIntPtr = new IntPtr(keyboardCuesValue ? 1 : 0);

            var spiResult = PInvoke.User32.SystemParametersInfo(PInvoke.User32.SystemParametersInfoAction.SPI_SETKEYBOARDCUES, 0, keyboardCuesValueAsIntPtr, fWinIni);
            if (spiResult == false)
            {
                success = false;
            }

            return success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult;
        }

        private static IMorphicResult SpiSetKeyboardPref(string? fWinIniAsString, Values values)
        {
            var success = true;

            bool? keyboardPrefValue = null;

            foreach (var value in values)
            {
                switch (value.Key.Name)
                {
                    case "KeyboardPreferenceOn":
                        {
                            var valueAsNullableBool = value.Value as bool?;
                            if (valueAsNullableBool == null)
                            {
                                success = false;
                                continue;
                            }
                            var valueAsBool = valueAsNullableBool.Value;

                            keyboardPrefValue = valueAsBool;
                        }
                        break;
                    default:
                        success = false;
                        continue;
                }
            }

            if (keyboardPrefValue.HasValue == true)
            {
                // uiParam
                // NOTE: in this implementation, we ignore the representation and pass in the bool value in the API call
                //
                // pvParam
                // NOTE: in this implementation, we ignore the representation as this value should be null
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

                var internalSetKeyboardPrefResult = SPISettingsHandler.InternalSpiSetKeyboardPref(keyboardPrefValue.Value, fWinIni);
                if (internalSetKeyboardPrefResult.IsError == true)
                {
                    return IMorphicResult.ErrorResult;
                }
            }
            else
            {
                success = false;
            }

            return success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult;
        }

        private static IMorphicResult InternalSpiSetKeyboardPref(bool keyboardPrefValue, PInvoke.User32.SystemParametersInfoFlags fWinIni)
        {
            var success = true;

            var keyboardPrefValueAsUInt = (uint)(keyboardPrefValue ? 1 : 0);

            var spiResult = PInvoke.User32.SystemParametersInfo(PInvoke.User32.SystemParametersInfoAction.SPI_SETKEYBOARDPREF, keyboardPrefValueAsUInt, IntPtr.Zero, fWinIni);
            if (spiResult == false)
            {
                success = false;
            }

            return success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult;
        }

        private static IMorphicResult SpiSetMessageDuration(string? fWinIniAsString, Values values)
        {
            var success = true;

            // NOTE: since the data passed to/from the SPI function is a primitive type and not a struct, there is no need to read the value before writing

            ulong? messageDurationInSeconds = null;

            foreach (var value in values)
            {
                switch (value.Key.Name)
                {
                    case "Duration":
                        {
                            var convertValueToULongResult = ConversionUtils.TryConvertObjectToULong(value.Value);
                            if (convertValueToULongResult.IsError == true)
                            {
                                success = false;
                                continue;
                            }
                            var valueAsULong = convertValueToULongResult.Value!;

                            messageDurationInSeconds = valueAsULong;
                        }
                        break;
                    default:
                        success = false;
                        continue;
                }
            }

            // NOTE: we only try to set the value if we were passed a value in the group
            if (messageDurationInSeconds != null)
            {
                // uiParam
                // NOTE: in this implementation, we ignore the representation as it is not used
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

                var internalSetMessageDurationResult = SPISettingsHandler.InternalSpiSetMessageDuration(messageDurationInSeconds.Value, fWinIni);
                if (internalSetMessageDurationResult.IsError == true)
                {
                    return IMorphicResult.ErrorResult;
                }
            }
            else
            {
                success = false;
            }

            return success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult;
        }

        private static IMorphicResult InternalSpiSetMessageDuration(ulong messageDurationInSeconds, PInvoke.User32.SystemParametersInfoFlags fWinIni)
        {
            var success = true;

            var convertValueToIntPtrResult = ConversionUtils.TryConvertObjectToIntPtr(messageDurationInSeconds);
            if (convertValueToIntPtrResult.IsError == true)
            {
                return IMorphicResult.ErrorResult;
            }
            var messageDurationInSecondsAsIntPtr = convertValueToIntPtrResult.Value!;

            // OBSERVATION: the GPII and modern solutions registry indicates that this value is a UINT, but the Microsoft documentation says to pass it as an IntPtr (instead
            //              of passing a pointer to a value); so we're passing it as an IntPtr.
            // OBSERVATION: values of 1-4 are invalid and will be ignored by Windows; we should create some kind of sanity check, range or other filter for this parameter
            var spiResult = PInvoke.User32.SystemParametersInfo((PInvoke.User32.SystemParametersInfoAction)ExtendedPInvoke.SPI_SETMESSAGEDURATION, 0, messageDurationInSecondsAsIntPtr, fWinIni);
            if (spiResult == false)
            {
                success = false;
            }

            return success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult;
        }

        private static IMorphicResult SpiSetMouseButtonSwap(string? fWinIniAsString, Values values)
        {
            var success = true;

            bool? mouseButtonSwapValue = null;

            foreach (var value in values)
            {
                switch (value.Key.Name)
                {
                    case "SwapMouseButtonsConfig":
                        {
                            var valueAsNullableBool = value.Value as bool?;
                            if (valueAsNullableBool == null)
                            {
                                success = false;
                                continue;
                            }
                            var valueAsBool = valueAsNullableBool.Value;

                            mouseButtonSwapValue = valueAsBool;
                        }
                        break;
                    default:
                        success = false;
                        continue;
                }
            }

            if (mouseButtonSwapValue.HasValue == true)
            {
                // uiParam
                // NOTE: in this implementation, we ignore the representation and pass in the bool value in the API call
                //
                // pvParam
                // NOTE: in this implementation, we ignore the representation as this value should be null
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

                var internalSetMouseButtonSwapResult = SPISettingsHandler.InternalSpiSetMouseButtonSwap(mouseButtonSwapValue.Value, fWinIni);
                if (internalSetMouseButtonSwapResult.IsError == true)
                {
                    return IMorphicResult.ErrorResult;
                }
            }
            else
            {
                success = false;
            }

            return success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult;
        }

        private static IMorphicResult InternalSpiSetMouseButtonSwap(bool mouseButtonSwapValue, PInvoke.User32.SystemParametersInfoFlags fWinIni)
        {
            var success = true;

            var mouseButtonSwapValueAsUInt = (uint)(mouseButtonSwapValue ? 1 : 0);

            var spiResult = PInvoke.User32.SystemParametersInfo(PInvoke.User32.SystemParametersInfoAction.SPI_SETMOUSEBUTTONSWAP, mouseButtonSwapValueAsUInt, IntPtr.Zero, fWinIni);
            if (spiResult == false)
            {
                success = false;
            }

            return success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult;
        }

        private static IMorphicResult SpiSetMouseKeys(string? fWinIniAsString, Values values)
        {
            var success = true;

            // capture the current MouseKeys struct data up-front
            var internalGetMouseKeysResult = SPISettingsHandler.InternalSpiGetMouseKeys();
            if (internalGetMouseKeysResult.IsError == true)
            {
                return IMorphicResult.ErrorResult;
            }
            var mouseKeys = internalGetMouseKeysResult.Value!;

            foreach (var value in values)
            {
                switch (value.Key.Name)
                {
                    case "MouseKeysOn":
                        {
                            var valueAsNullableBool = value.Value as bool?;
                            if (valueAsNullableBool == null)
                            {
                                success = false;
                                continue;
                            }
                            var valueAsBool = valueAsNullableBool.Value;

                            if (valueAsBool == true)
                            {
                                mouseKeys.dwFlags |= ExtendedPInvoke.MKF_MOUSEKEYSON;
                            }
                            else
                            {
                                mouseKeys.dwFlags &= ~ExtendedPInvoke.MKF_MOUSEKEYSON;
                            }
                        }
                        break;
                    case "MaxSpeed":
                        {
                            var convertValueToDoubleResult = ConversionUtils.TryConvertObjectToDouble(value.Value);
                            if (convertValueToDoubleResult.IsError == true)
                            {
                                success = false;
                                continue;
                            }
                            var valueAsDouble = convertValueToDoubleResult.Value!;

                            // NOTE: we verify that the floating-point value is in the range of 0.0...1.0
                            if (valueAsDouble < 0.0 || valueAsDouble > 1.0)
                            {
                                success = false;
                                continue;
                            }

                            // convert the value to a range of 10-360
                            var mouseKeysMaxSpeedValue = (uint)(valueAsDouble * 350) + 10;

                            mouseKeys.iMaxSpeed = mouseKeysMaxSpeedValue;
                        }
                        break;
                    default:
                        success = false;
                        continue;
                }
            }

            // uiParam
            // NOTE: in this implementation, we ignore the representation and pass in the actual mousekeys struct size in the API call
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

            var internalSetMouseKeysResult = SPISettingsHandler.InternalSpiSetMouseKeys(mouseKeys, fWinIni);
            if (internalSetMouseKeysResult.IsError == true)
            {
                return IMorphicResult.ErrorResult;
            }

            return success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult;
        }

        private static IMorphicResult InternalSpiSetMouseKeys(ExtendedPInvoke.MOUSEKEYS mouseKeys, PInvoke.User32.SystemParametersInfoFlags fWinIni)
        {
            // sanity check
            if (mouseKeys.cbSize != Marshal.SizeOf<ExtendedPInvoke.MOUSEKEYS>())
            {
                throw new ArgumentException(nameof(mouseKeys));
            }

            var success = true;

            var pointerToMouseKeys = Marshal.AllocHGlobal(Marshal.SizeOf<ExtendedPInvoke.MOUSEKEYS>());
            try
            {
                Marshal.StructureToPtr(mouseKeys, pointerToMouseKeys, false);

                var spiResult = PInvoke.User32.SystemParametersInfo(PInvoke.User32.SystemParametersInfoAction.SPI_SETMOUSEKEYS, mouseKeys.cbSize, pointerToMouseKeys, fWinIni);
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
                Marshal.FreeHGlobal(pointerToMouseKeys);
            }

            return success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult;
        }

        private static IMorphicResult SpiSetMouseTrails(string? fWinIniAsString, Values values)
        {
            var success = true;

            // NOTE: since the data passed to/from the SPI function is a primitive type and not a struct, there is no need to read the value before writing

            uint? mouseTrailsValue = null;

            foreach (var value in values)
            {
                switch (value.Key.Name)
                {
                    case "MouseTrails":
                        {
                            var convertValueToUIntResult = ConversionUtils.TryConvertObjectToUInt(value.Value);
                            if (convertValueToUIntResult.IsError == true)
                            {
                                success = false;
                                continue;
                            }
                            var valueAsUInt = convertValueToUIntResult.Value!;

                            // validate the range
                            if (valueAsUInt < 0 || valueAsUInt > 10)
                            {
                                success = false;
                                continue;
                            }

                            mouseTrailsValue = valueAsUInt;
                        }
                        break;
                    default:
                        success = false;
                        continue;
                }
            }

            // NOTE: we only try to set the value if we were passed a value in the group
            if (mouseTrailsValue != null)
            {
                // uiParam
                // NOTE: in this implementation, we ignore the representation and pass in the bool value in the API call
                //
                // pvParam
                // NOTE: in this implementation, we ignore the representation as this value should be null
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

                var internalSetMouseTrailsResult = SPISettingsHandler.InternalSpiSetMouseTrails(mouseTrailsValue.Value, fWinIni);
                if (internalSetMouseTrailsResult.IsError == true)
                {
                    return IMorphicResult.ErrorResult;
                }
            }
            else
            {
                success = false;
            }

            return success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult;
        }

        private static IMorphicResult InternalSpiSetMouseTrails(uint mouseTrailsValue, PInvoke.User32.SystemParametersInfoFlags fWinIni)
        {
            var success = true;

            // OBSERVATION: the modern solutions registry indicates that this value should be get and set via pvParam, but the GPII solutions registry indicates that it should
            //              be get via pvParam and set via uiParam; Microsoft's documentation also says we should set via uiParam; we are following Microsoft's documentation.
            // OBSERVATION: values of 1 and 0 both mean "no mouse trails"; Windows appears to record any value set to "1" as "0"...so reading the value after setting 1 will return 0
            var spiResult = PInvoke.User32.SystemParametersInfo(PInvoke.User32.SystemParametersInfoAction.SPI_SETMOUSETRAILS, mouseTrailsValue, IntPtr.Zero, fWinIni);
            if (spiResult == false)
            {
                success = false;
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
                            var valueAsBool = valueAsNullableBool.Value;

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

        private static IMorphicResult SpiSetStickyKeys(string? fWinIniAsString, Values values)
        {
            var success = true;

            // capture the current StickyKeys struct data up-front
            var internalGetStickyKeysResult = SPISettingsHandler.InternalSpiGetStickyKeys();
            if (internalGetStickyKeysResult.IsError == true)
            {
                return IMorphicResult.ErrorResult;
            }
            var stickyKeys = internalGetStickyKeysResult.Value!;

            foreach (var value in values)
            {
                switch (value.Key.Name)
                {
                    case "StickyKeysOn":
                        {
                            var valueAsNullableBool = value.Value as bool?;
                            if (valueAsNullableBool == null)
                            {
                                success = false;
                                continue;
                            }
                            var valueAsBool = valueAsNullableBool.Value;

                            if (valueAsBool == true)
                            {
                                stickyKeys.dwFlags |= ExtendedPInvoke.SKF_STICKYKEYSON;
                            }
                            else
                            {
                                stickyKeys.dwFlags &= ~ExtendedPInvoke.SKF_STICKYKEYSON;
                            }
                        }
                        break;
                    default:
                        success = false;
                        continue;
                }
            }

            // uiParam
            // NOTE: in this implementation, we ignore the representation and pass in the actual stickykeys struct size in the API call
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

            var internalSetStickyKeysResult = SPISettingsHandler.InternalSpiSetStickyKeys(stickyKeys, fWinIni);
            if (internalSetStickyKeysResult.IsError == true)
            {
                return IMorphicResult.ErrorResult;
            }

            return success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult;
        }

        private static IMorphicResult InternalSpiSetStickyKeys(ExtendedPInvoke.STICKYKEYS stickyKeys, PInvoke.User32.SystemParametersInfoFlags fWinIni)
        {
            // sanity check
            if (stickyKeys.cbSize != Marshal.SizeOf<ExtendedPInvoke.STICKYKEYS>())
            {
                throw new ArgumentException(nameof(stickyKeys));
            }

            var success = true;

            var pointerToStickyKeys = Marshal.AllocHGlobal(Marshal.SizeOf<ExtendedPInvoke.STICKYKEYS>());
            try
            {
                Marshal.StructureToPtr(stickyKeys, pointerToStickyKeys, false);

                var spiResult = PInvoke.User32.SystemParametersInfo(PInvoke.User32.SystemParametersInfoAction.SPI_SETSTICKYKEYS, stickyKeys.cbSize, pointerToStickyKeys, fWinIni);
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
                Marshal.FreeHGlobal(pointerToStickyKeys);
            }

            return success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult;
        }

        private static IMorphicResult SpiSetToggleKeys(string? fWinIniAsString, Values values)
        {
            var success = true;

            // capture the current ToggleKeys struct data up-front
            var internalGetToggleKeysResult = SPISettingsHandler.InternalSpiGetToggleKeys();
            if (internalGetToggleKeysResult.IsError == true)
            {
                return IMorphicResult.ErrorResult;
            }
            var toggleKeys = internalGetToggleKeysResult.Value!;

            foreach (var value in values)
            {
                switch (value.Key.Name)
                {
                    case "ToggleKeysOn":
                        {
                            var valueAsNullableBool = value.Value as bool?;
                            if (valueAsNullableBool == null)
                            {
                                success = false;
                                continue;
                            }
                            var valueAsBool = valueAsNullableBool.Value;

                            if (valueAsBool == true)
                            {
                                toggleKeys.dwFlags |= ExtendedPInvoke.TKF_TOGGLEKEYSON;
                            }
                            else
                            {
                                toggleKeys.dwFlags &= ~ExtendedPInvoke.TKF_TOGGLEKEYSON;
                            }
                        }
                        break;
                    default:
                        success = false;
                        continue;
                }
            }

            // uiParam
            // NOTE: in this implementation, we ignore the representation and pass in the actual togglekeys struct size in the API call
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

            var internalSetToggleKeysResult = SPISettingsHandler.InternalSpiSetToggleKeys(toggleKeys, fWinIni);
            if (internalSetToggleKeysResult.IsError == true)
            {
                return IMorphicResult.ErrorResult;
            }

            return success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult;
        }

        private static IMorphicResult InternalSpiSetToggleKeys(ExtendedPInvoke.TOGGLEKEYS toggleKeys, PInvoke.User32.SystemParametersInfoFlags fWinIni)
        {
            // sanity check
            if (toggleKeys.cbSize != Marshal.SizeOf<ExtendedPInvoke.TOGGLEKEYS>())
            {
                throw new ArgumentException(nameof(toggleKeys));
            }

            var success = true;

            var pointerToToggleKeys = Marshal.AllocHGlobal(Marshal.SizeOf<ExtendedPInvoke.TOGGLEKEYS>());
            try
            {
                Marshal.StructureToPtr(toggleKeys, pointerToToggleKeys, false);

                var spiResult = PInvoke.User32.SystemParametersInfo(PInvoke.User32.SystemParametersInfoAction.SPI_SETTOGGLEKEYS, toggleKeys.cbSize, pointerToToggleKeys, fWinIni);
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
                Marshal.FreeHGlobal(pointerToToggleKeys);
            }

            return success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult;
        }

        private static IMorphicResult SpiSetWheelScrollLines(string? fWinIniAsString, Values values)
        {
            var success = true;

            // NOTE: since the data passed to/from the SPI function is a primitive type and not a struct, there is no need to read the value before writing

            uint? wheelScrollLinesValue = null;

            foreach (var value in values)
            {
                switch (value.Key.Name)
                {
                    case "ScrollWheelModeConfig":
                        {
                            var convertValueToUIntResult = ConversionUtils.TryConvertObjectToUInt(value.Value);
                            if (convertValueToUIntResult.IsError == true)
                            {
                                success = false;
                                continue;
                            }
                            var valueAsUInt = convertValueToUIntResult.Value!;

                            // validate the range
                            if (valueAsUInt < 0 || valueAsUInt > 10)
                            {
                                success = false;
                                continue;
                            }

                            wheelScrollLinesValue = valueAsUInt;
                        }
                        break;
                    default:
                        success = false;
                        continue;
                }
            }

            // NOTE: we only try to set the value if we were passed a value in the group
            if (wheelScrollLinesValue != null)
            {
                // uiParam
                // NOTE: in this implementation, we ignore the representation and pass in the bool value in the API call
                //
                // pvParam
                // NOTE: in this implementation, we ignore the representation as this value should be null
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

                var internalSetWheelScrollLinesResult = SPISettingsHandler.InternalSpiSetWheelScrollLines(wheelScrollLinesValue.Value, fWinIni);
                if (internalSetWheelScrollLinesResult.IsError == true)
                {
                    return IMorphicResult.ErrorResult;
                }
            }
            else
            {
                success = false;
            }

            return success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult;
        }

        private static IMorphicResult InternalSpiSetWheelScrollLines(uint wheelScrollLinesValue, PInvoke.User32.SystemParametersInfoFlags fWinIni)
        {
            var success = true;

            var spiResult = PInvoke.User32.SystemParametersInfo(PInvoke.User32.SystemParametersInfoAction.SPI_SETWHEELSCROLLLINES, wheelScrollLinesValue, IntPtr.Zero, fWinIni);
            if (spiResult == false)
            {
                success = false;
            }

            return success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult;
        }
    }
}
