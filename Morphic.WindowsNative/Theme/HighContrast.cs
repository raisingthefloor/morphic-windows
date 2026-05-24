// Copyright 2020-2026 Raising the Floor - US, Inc.
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

using Morphic.Core;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Morphic.WindowsNative.Theme;

public class HighContrast
{
    // Lock used only by SetIsOn to ensure two concurrent setters don't interleave their
    // read-modify-write SPI sequences and clobber each other's flag updates. GetIsOn is a
    // single SPI call and needs no locking (SPI reads are thread-safe).
    private static readonly object _setIsOnLock = new();

    // Reads the current "high contrast is on" state by inspecting the HCF_HIGHCONTRASTON bit
    // of the HIGHCONTRASTW struct returned by SystemParametersInfo(SPI_GETHIGHCONTRAST).
    public static MorphicResult<bool, MorphicUnit> GetIsOn()
    {
        var highContrastInfo = new Windows.Win32.UI.Accessibility.HIGHCONTRASTW
        {
            cbSize = (uint)Marshal.SizeOf(typeof(Windows.Win32.UI.Accessibility.HIGHCONTRASTW)),
        };

        Windows.Win32.Foundation.BOOL getResult;
        unsafe
        {
            getResult = Windows.Win32.PInvoke.SystemParametersInfo(
                Windows.Win32.UI.WindowsAndMessaging.SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETHIGHCONTRAST,
                highContrastInfo.cbSize,
                &highContrastInfo,
                /* fWinIni unused for GET operations */ 0);
        }
        if (getResult == 0)
        {
            Debug.Assert(false, $"SystemParametersInfo(SPI_GETHIGHCONTRAST) failed with win32 error {Marshal.GetLastWin32Error()}");
            return MorphicResult.ErrorResult();
        }

        var isOn = (highContrastInfo.dwFlags & Windows.Win32.UI.Accessibility.HIGHCONTRASTW_FLAGS.HCF_HIGHCONTRASTON) == Windows.Win32.UI.Accessibility.HIGHCONTRASTW_FLAGS.HCF_HIGHCONTRASTON;
        return MorphicResult.OkResult(isOn);
    }

    // Toggles the system-wide high-contrast mode by flipping the HCF_HIGHCONTRASTON bit on the
    // current HIGHCONTRASTW struct. We do read-modify-write because we want to leave the other
    // flags (hotkey configuration, etc.) untouched.
    //
    // updateUserProfile (default false) controls SPIF_UPDATEINIFILE: when true, Windows also
    // persists the change to the user's profile so it survives logoff/login. When false, only
    // the current session is affected.
    public static MorphicResult<MorphicUnit, MorphicUnit> SetIsOn(bool isOn, bool updateUserProfile = false)
    {
        lock (_setIsOnLock)
        {
            var highContrastInfo = new Windows.Win32.UI.Accessibility.HIGHCONTRASTW
            {
                cbSize = (uint)Marshal.SizeOf(typeof(Windows.Win32.UI.Accessibility.HIGHCONTRASTW)),
            };

            // Read the current HIGHCONTRASTW state.
            Windows.Win32.Foundation.BOOL getResult;
            unsafe
            {
                getResult = Windows.Win32.PInvoke.SystemParametersInfo(
                    Windows.Win32.UI.WindowsAndMessaging.SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETHIGHCONTRAST,
                    highContrastInfo.cbSize,
                    &highContrastInfo,
                    /* fWinIni unused for GET operations */ 0);
            }
            if (getResult == 0)
            {
                Debug.Assert(false, $"SystemParametersInfo(SPI_GETHIGHCONTRAST) failed with win32 error {Marshal.GetLastWin32Error()}");
                return MorphicResult.ErrorResult();
            }

            // Flip the on/off bit, leaving the other flags untouched.
            //
            // NOTE: per Microsoft's documentation we do NOT set HCF_OPTION_NOTHEMECHANGE when
            // toggling high contrast mode -- the theme change is the desired side-effect.
            if (isOn)
            {
                highContrastInfo.dwFlags |= Windows.Win32.UI.Accessibility.HIGHCONTRASTW_FLAGS.HCF_HIGHCONTRASTON;
            }
            else
            {
                highContrastInfo.dwFlags &= ~Windows.Win32.UI.Accessibility.HIGHCONTRASTW_FLAGS.HCF_HIGHCONTRASTON;
            }

            // Write the updated state back.
            // SPIF_SENDWININICHANGE broadcasts WM_SETTINGCHANGE to top-level windows so that
            // anyone watching (including our own IsOnChanged via UserPreferenceChanged) sees the
            // update. SPIF_UPDATEINIFILE additionally persists the change to the user profile.
            var fWinIni = Windows.Win32.UI.WindowsAndMessaging.SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS.SPIF_SENDWININICHANGE;
            if (updateUserProfile)
            {
                fWinIni |= Windows.Win32.UI.WindowsAndMessaging.SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS.SPIF_UPDATEINIFILE;
            }

            Windows.Win32.Foundation.BOOL setResult;
            unsafe
            {
                setResult = Windows.Win32.PInvoke.SystemParametersInfo(
                    Windows.Win32.UI.WindowsAndMessaging.SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETHIGHCONTRAST,
                    highContrastInfo.cbSize,
                    &highContrastInfo,
                    fWinIni);
            }
            if (setResult == 0)
            {
                Debug.Assert(false, $"SystemParametersInfo(SPI_SETHIGHCONTRAST) failed with win32 error {Marshal.GetLastWin32Error()}");
                return MorphicResult.ErrorResult();
            }

            return MorphicResult.OkResult();
        }
    }
}
