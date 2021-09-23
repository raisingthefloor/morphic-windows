
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
            var success = true;
            Values values = new Values();

            var spiSettingGroupAsOptional = settingGroup as SPISettingGroup;
            if (spiSettingGroupAsOptional == null)
            {
                Debug.Assert(false);
                return (IMorphicResult.ErrorResult, values);
            }
            var spiSettingGroup = spiSettingGroupAsOptional!;

            // NOTE: SPISettingsHandlers, in our current implementation, allow exactly one setting (no more, no fewer) per call
            var iSetting = 1;
            foreach (Setting setting in settings)
            {
                if (iSetting > 1)
                {
                    Debug.Assert(false, "More than one setting provided to settings handler for this SettingGroup; this is currently not supported in the SPISettingsHandler.");
                    // OBSERVATION: in production, we'd generally prefer to crash or something similar in this kind of bug situation
                    return (IMorphicResult.ErrorResult, values);
                }

                object? value = null;
                var getWasSuccessful = false;

                switch (spiSettingGroup.getAction)
                {
                    case "SPI_GETDESKWALLPAPER":
                        {
                            // uiParam = length of pvParam buffer (up to MAX_PATH)
                            var uiParam = (spiSettingGroup.uiParam as uint?) ?? ExtendedPInvoke.MAX_PATH;
                            uiParam = Math.Min(uiParam, ExtendedPInvoke.MAX_PATH);
                            //
                            // pvParam = representation of the required buffer
                            // NOTE: in this implementation, we ignore the representation and create our own buffer
                            //
                            // fWinIni = flags
                            // OBSERVATION: for security purposes, we may want to consider hard-coding these flags or otherwise limiting them
                            // NOTE: we should review and sanity-check the setting in the solutions registry
                            var fWinIni = PInvoke.User32.SystemParametersInfoFlags.None;
                            var parseFlagsResult = SPISettingsHandler.ParseWinIniFlags(spiSettingGroup.fWinIni);
                            if (parseFlagsResult.IsSuccess == true)
                            {
                                fWinIni = parseFlagsResult.Value!;
                            }

                            var pointerToPvParam = Marshal.AllocHGlobal((int)uiParam * sizeof(char));
                            try
                            {
                                getWasSuccessful = PInvoke.User32.SystemParametersInfo(PInvoke.User32.SystemParametersInfoAction.SPI_GETDESKWALLPAPER, uiParam, pointerToPvParam, fWinIni);
                                if (getWasSuccessful == true)
                                {
                                    value = Marshal.PtrToStringUni(pointerToPvParam);
                                }
                            }
                            finally
                            {
                                Marshal.FreeHGlobal(pointerToPvParam);
                            }
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

                iSetting += 1;
            }

            return ((success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult), values);
        }

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
            var success = true;

            var spiSettingGroupAsOptional = settingGroup as SPISettingGroup;
            if (spiSettingGroupAsOptional == null)
            {
                Debug.Assert(false);
                return IMorphicResult.ErrorResult;
            }
            var spiSettingGroup = spiSettingGroupAsOptional!;

            // NOTE: SPISettingsHandlers, in our current implementation, allow exactly one setting/value (no more, no fewer) per call
            var iValue = 1;
            foreach (var value in values)
            {
                if (iValue > 1)
                {
                    Debug.Assert(false, "More than one vaule/setting provided to settings handler for this SettingGroup; this is currently not supported in the SPISettingsHandler.");
                    // OBSERVATION: in production, we'd generally prefer to crash or something similar in this kind of bug situation
                    return IMorphicResult.ErrorResult;
                }

                switch (spiSettingGroup.setAction)
                {
                    case "SPI_SETDESKWALLPAPER":
                        {
                            var valueAsNullableString = value.Value as string;
                            if (valueAsNullableString == null)
                            {
                                success = false;
                                break;
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
                            var parseFlagsResult = SPISettingsHandler.ParseWinIniFlags(spiSettingGroup.fWinIni);
                            if (parseFlagsResult.IsSuccess == true)
                            {
                                fWinIni = parseFlagsResult.Value!;
                            }

                            // TODO: validate the value (which is a path, which must point to a safe place to retrieve images, which must point to a folder and file where the user has read access, and which must be an image type compatible with using as wallpaper)

                            var pointerToPvParam = Marshal.StringToHGlobalUni(valueAsString);
                            try
                            {
                                var setResult = PInvoke.User32.SystemParametersInfo(PInvoke.User32.SystemParametersInfoAction.SPI_SETDESKWALLPAPER, uiParam, pointerToPvParam, fWinIni);
                                if (setResult == false)
                                {
                                    success = false;
                                }
                            }
                            finally
                            {
                                Marshal.FreeHGlobal(pointerToPvParam);
                            }
                        }
                        break;
                    default:
                        success = false;
                        // skip to the next setting
                        continue;
                }


                iValue += 1;
            }

            return success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult;
        }
    }
}
