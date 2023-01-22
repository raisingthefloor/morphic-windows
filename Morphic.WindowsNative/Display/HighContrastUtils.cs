// Copyright 2020-2022 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-windowsnative-lib-cs/blob/main/LICENSE
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

namespace Morphic.WindowsNative.Display;

using Morphic.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

public class HighContrastUtils
{
    private static Object s_modifyHighContrastLock = new Object();

    private struct HighContrastInfo
    {
        public bool FeatureIsOn;
        public bool FeatureCanBeTurnedOnAndOff;
        public bool HotKeyIsEnabled;
        public bool HotKeyPresentsConfirmationDialog;
        public bool HotKeyPlaysSound;
        //public bool ShowsVisualIndicatorWhenOn;
        public bool HotKeyCanBeEnabled { get; init; }
        //
        public String? DefaultColorScheme;
    }
    private static MorphicResult<HighContrastInfo, MorphicUnit> GetHighContrastInfo()
    {
        var highContrastInfo = ExtendedPInvoke.HIGHCONTRAST.InitializeNew();
        IntPtr pointerToHighContrastInfo = Marshal.AllocHGlobal((int)highContrastInfo.cbSize);
        Marshal.StructureToPtr(highContrastInfo, pointerToHighContrastInfo, false);
        try
        {
            var spiSuccess = PInvoke.User32.SystemParametersInfo(PInvoke.User32.SystemParametersInfoAction.SPI_GETHIGHCONTRAST, highContrastInfo.cbSize, pointerToHighContrastInfo, PInvoke.User32.SystemParametersInfoFlags.None);
            if (spiSuccess == false)
            {
                return MorphicResult.ErrorResult();
            }

            highContrastInfo = Marshal.PtrToStructure<ExtendedPInvoke.HIGHCONTRAST>(pointerToHighContrastInfo);
        }
        finally
        {
            Marshal.FreeHGlobal(pointerToHighContrastInfo);
        }

        var result = new HighContrastInfo()
        {
            FeatureIsOn = ((highContrastInfo.dwFlags & ExtendedPInvoke.HighContrastFlags.HCF_HIGHCONTRASTON) == ExtendedPInvoke.HighContrastFlags.HCF_HIGHCONTRASTON),
            FeatureCanBeTurnedOnAndOff = ((highContrastInfo.dwFlags & ExtendedPInvoke.HighContrastFlags.HCF_AVAILABLE) == ExtendedPInvoke.HighContrastFlags.HCF_AVAILABLE),
            HotKeyIsEnabled = ((highContrastInfo.dwFlags & ExtendedPInvoke.HighContrastFlags.HCF_HOTKEYACTIVE) == ExtendedPInvoke.HighContrastFlags.HCF_HOTKEYACTIVE),
            HotKeyPresentsConfirmationDialog = ((highContrastInfo.dwFlags & ExtendedPInvoke.HighContrastFlags.HCF_CONFIRMHOTKEY) == ExtendedPInvoke.HighContrastFlags.HCF_CONFIRMHOTKEY),
            HotKeyPlaysSound = ((highContrastInfo.dwFlags & ExtendedPInvoke.HighContrastFlags.HCF_HOTKEYSOUND) == ExtendedPInvoke.HighContrastFlags.HCF_HOTKEYSOUND),
            //ShowsVisualIndicatorWhenOn = ((highContrastInfo.dwFlags & ExtendedPInvoke.HighContrastFlags.HCF_INDICATOR) == ExtendedPInvoke.HighContrastFlags.HCF_INDICATOR),
            HotKeyCanBeEnabled = ((highContrastInfo.dwFlags & ExtendedPInvoke.HighContrastFlags.HCF_HOTKEYAVAILABLE) == ExtendedPInvoke.HighContrastFlags.HCF_HOTKEYAVAILABLE),
            //
            DefaultColorScheme = highContrastInfo.lpszDefaultScheme
        };
        return MorphicResult.OkResult(result);
    }

    //

    public static MorphicResult<bool, MorphicUnit> GetHighContrastModeIsOn()
    {
        var getHighContrastInfoResult = HighContrastUtils.GetHighContrastInfo();
        if (getHighContrastInfoResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        var highContrastInfo = getHighContrastInfoResult.Value!;

        return MorphicResult.OkResult(highContrastInfo.FeatureIsOn);
    }

    public static MorphicResult<MorphicUnit, MorphicUnit> SetHighContrastModeIsOn(bool isOn, bool updateUserProfile = false)
    {
        // NOTE: we lock on s_modifyHighContrastLock here to ensure that our application doesn't modify the system's HIGHCONTRAST settings while we're reading, updating and rewriting them
        lock (s_modifyHighContrastLock)
        {
            // set up a HIGHCONTRAST structure; we'll use this to get and then update the high contrast settings
            var highContrastInfo = ExtendedPInvoke.HIGHCONTRAST.InitializeNew();
            IntPtr pointerToHighContrastInfo = Marshal.AllocHGlobal((int)highContrastInfo.cbSize);
            Marshal.StructureToPtr(highContrastInfo, pointerToHighContrastInfo, false);
            try
            {
                // get the current high contrast settings (which we will update in a moment)
                var spiSuccess = PInvoke.User32.SystemParametersInfo(PInvoke.User32.SystemParametersInfoAction.SPI_GETHIGHCONTRAST, highContrastInfo.cbSize, pointerToHighContrastInfo, PInvoke.User32.SystemParametersInfoFlags.None);
                if (spiSuccess == false)
                {
                    return MorphicResult.ErrorResult();
                }

                highContrastInfo = Marshal.PtrToStructure<ExtendedPInvoke.HIGHCONTRAST>(pointerToHighContrastInfo);

                // set/clear the "high contrast on" flag
                if (isOn == true)
                {
                    highContrastInfo.dwFlags |= ExtendedPInvoke.HighContrastFlags.HCF_HIGHCONTRASTON;
                }
                else
                {
                    highContrastInfo.dwFlags &= ~ExtendedPInvoke.HighContrastFlags.HCF_HIGHCONTRASTON;
                }

                // NOTE: per Microsoft's documentation, we are _not_ setting the HCF_OPTION_NOTHEMECHANGE flag when toggling the high contrast mode; note that the HCF_OPTION_NOTHEMECHANGE
                //       setting also only appeared in the Windows SDK as of Windows 10 2004 (build 19041) and newer
                // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-highcontrastw

                Marshal.StructureToPtr(highContrastInfo, pointerToHighContrastInfo, false);

                // write the new settings
                // NOTE: we ask the system to broadcast the contrast change to all top-level windows.  Often/usually we won't 't want to save the (system-wide) setting to the user's profile;
                //       if our caller wants to also save the setting to the user profile, then set the 'updateUserProfile' argument to true.
                var fWinIni = PInvoke.User32.SystemParametersInfoFlags.SPIF_SENDWININICHANGE;
                if (updateUserProfile == true)
                {
                    fWinIni |= PInvoke.User32.SystemParametersInfoFlags.SPIF_UPDATEINIFILE;
                }
                spiSuccess = PInvoke.User32.SystemParametersInfo(PInvoke.User32.SystemParametersInfoAction.SPI_SETHIGHCONTRAST, highContrastInfo.cbSize, pointerToHighContrastInfo, fWinIni);
                if (spiSuccess == false)
                {
                    return MorphicResult.ErrorResult();
                }

                return MorphicResult.OkResult();
            }
            finally
            {
                Marshal.FreeHGlobal(pointerToHighContrastInfo);
            }
        }
    }

    //

    public static MorphicResult<String?, MorphicUnit> GetHighContrastModeDefaultColorScheme()
    {
        var getHighContrastInfoResult = HighContrastUtils.GetHighContrastInfo();
        if (getHighContrastInfoResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        var highContrastInfo = getHighContrastInfoResult.Value!;

        return MorphicResult.OkResult(highContrastInfo.DefaultColorScheme);
    }

    public static MorphicResult<MorphicUnit, MorphicUnit> SetHighContrastModeDefaultColorScheme(String defaultColorScheme, bool updateUserProfile = false)
    {
        if (defaultColorScheme is null)
        {
            throw new ArgumentNullException(nameof(defaultColorScheme));
        }

        // NOTE: we lock on s_modifyHighContrastLock here to ensure that our application doesn't modify the system's HIGHCONTRAST settings while we're reading, updating and rewriting them
        lock (s_modifyHighContrastLock)
        {
            // set up a HIGHCONTRAST structure; we'll use this to get and then update the high contrast settings
            var highContrastInfo = ExtendedPInvoke.HIGHCONTRAST.InitializeNew();
            IntPtr pointerToHighContrastInfo = Marshal.AllocHGlobal((int)highContrastInfo.cbSize);
            Marshal.StructureToPtr(highContrastInfo, pointerToHighContrastInfo, false);
            try
            {
                // get the current high contrast settings (which we will update in a moment)
                var spiSuccess = PInvoke.User32.SystemParametersInfo(PInvoke.User32.SystemParametersInfoAction.SPI_GETHIGHCONTRAST, highContrastInfo.cbSize, pointerToHighContrastInfo, PInvoke.User32.SystemParametersInfoFlags.None);
                if (spiSuccess == false)
                {
                    return MorphicResult.ErrorResult();
                }

                highContrastInfo = Marshal.PtrToStructure<ExtendedPInvoke.HIGHCONTRAST>(pointerToHighContrastInfo);

                // update the default color scheme
                highContrastInfo.lpszDefaultScheme = defaultColorScheme;

                // NOTE: as we are changing the theme, we do not set the HCF_OPTION_NOTHEMECHANGE flag; also note that the HCF_OPTION_NOTHEMECHANGE setting also only appears in the
                //       Windows SDK as of Windows 10 2004 (build 19041) and newer
                // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-highcontrastw

                Marshal.StructureToPtr(highContrastInfo, pointerToHighContrastInfo, false);

                // write the new settings
                // NOTE: we ask the system to broadcast the contrast change to all top-level windows.  Often/usually we won't 't want to save the (system-wide) setting to the user's profile;
                //       if our caller wants to also save the setting to the user profile, then set the 'updateUserProfile' argument to true.
                var fWinIni = PInvoke.User32.SystemParametersInfoFlags.SPIF_SENDWININICHANGE;
                if (updateUserProfile == true)
                {
                    fWinIni |= PInvoke.User32.SystemParametersInfoFlags.SPIF_UPDATEINIFILE;
                }
                spiSuccess = PInvoke.User32.SystemParametersInfo(PInvoke.User32.SystemParametersInfoAction.SPI_SETHIGHCONTRAST, highContrastInfo.cbSize, pointerToHighContrastInfo, fWinIni);
                if (spiSuccess == false)
                {
                    return MorphicResult.ErrorResult();
                }

                return MorphicResult.OkResult();
            }
            finally
            {
                Marshal.FreeHGlobal(pointerToHighContrastInfo);
            }
        }
    }
}
