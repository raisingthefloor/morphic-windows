// Copyright 2020-2024 Raising the Floor - US, Inc.
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
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Morphic.WindowsNative.Theme;

public class HighContrast
{
    public class HighContrastIsOnChangedEventArgs(bool isOn) : EventArgs
    {
        public bool IsOn = isOn;
    }
    public delegate void HighContrastIsOnChangedEventHandler(object? sender, HighContrastIsOnChangedEventArgs e);
    //
    private static HighContrastIsOnChangedEventHandler? s_highContrastIsOnChanged = null;
    private static bool s_HighContrastIsOnWatchEventIsActive = false;
    private static object s_HighContrastIsOnWatchEventLock = new();

    private static bool? s_highContrastIsOn;

    //

    // NOTE: it is the target event's responsibility to run any UI-related code on the main UI thread; this event should be considered to be fired from a background thread
    public static event HighContrastIsOnChangedEventHandler HighContrastIsOnChanged
    {
        add
        {
            var connectWatchEventResult = HighContrast.ConnectHighContrastIsOnWatchEventIfUninitialized();
            if (connectWatchEventResult.IsError == true)
            {
                return;
            }

            s_highContrastIsOnChanged += value;
        }
        remove
        {
            s_highContrastIsOnChanged -= value;

            if (s_highContrastIsOnChanged is null || s_highContrastIsOnChanged!.GetInvocationList().Length == 0)
            {
                s_highContrastIsOnChanged = null;

                HighContrast.DestroyHighContrastIsOnWatchEventIfUnused();
            }
        }
    }

    private static MorphicResult<MorphicUnit, MorphicUnit> ConnectHighContrastIsOnWatchEventIfUninitialized()
    {
        lock (s_HighContrastIsOnWatchEventLock)
        {
            if (s_HighContrastIsOnWatchEventIsActive == false)
            {
                // capture initial high contrast on/off state
                var getHighContrastIsOnResult = HighContrast.GetIsOn();
                if (getHighContrastIsOnResult.IsSuccess == true)
                {
                    s_highContrastIsOn = getHighContrastIsOnResult.Value!;
                }
                else
                {
                    return MorphicResult.ErrorResult();
                }

                // see: https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.systemevents.userpreferencechanged?view=dotnet-plat-ext-6.0
                //      NOTE: this strategy will only work if the message pump is running; we may want to consider creating a hidden window to ensure that we capture messages
                // NOTE: if we use the UserPreferenceChanged event handler, we must also detach our event handler when the application is disposed (see: https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.systemevents.displaysettingschanged?view=dotnet-plat-ext-3.1#microsoft-win32-systemevents-displaysettingschanged)
                try
                {
                    Morphic.WindowsNative.SystemEvents.UserPreferenceEvents.UserPreferenceChanged += UserPreferenceEvents_UserPreferenceChanged;
                }
                catch
                {
                    return MorphicResult.ErrorResult();
                }

                s_HighContrastIsOnWatchEventIsActive = true;
            }
        }

        return MorphicResult.OkResult();
    }

    private static void DestroyHighContrastIsOnWatchEventIfUnused()
    {
        lock (s_HighContrastIsOnWatchEventLock)
        {
            if (s_highContrastIsOnChanged is null || s_highContrastIsOnChanged!.GetInvocationList().Length == 0)
            {
                Morphic.WindowsNative.SystemEvents.UserPreferenceEvents.UserPreferenceChanged -= UserPreferenceEvents_UserPreferenceChanged;

                s_HighContrastIsOnWatchEventIsActive = false;
            }
        }
    }

    //

    private static void UserPreferenceEvents_UserPreferenceChanged(object? sender, SystemEvents.UserPreferenceEvents.UserPreferenceChangedEventArgs e)
    {
        bool mightBeHighContastEvent;
        switch (e.Category)
        {
            case Microsoft.Win32.UserPreferenceCategory.Accessibility:
            case Microsoft.Win32.UserPreferenceCategory.Color:
                mightBeHighContastEvent = true;
                break;
            default:
                mightBeHighContastEvent = false;
                break;
        }

        if (mightBeHighContastEvent == true)
        {
            // check to see if high contrast is on
            var getHighContrastIsOnResult = HighContrast.GetIsOn();
            if (getHighContrastIsOnResult.IsSuccess == true)
            {
                var highContrastIsOn = getHighContrastIsOnResult.Value!;
                if (s_highContrastIsOn != highContrastIsOn)
                {
                    s_highContrastIsOn = highContrastIsOn;

                    var highContrastIsOnChangedEventArgs = new HighContrastIsOnChangedEventArgs(highContrastIsOn);

                    // NOTE: to ensure that each event handler runs (even if one throws an exception), we send an event to each window separately in parallel
                    var invocationList = s_highContrastIsOnChanged?.GetInvocationList();
                    if (invocationList is not null)
                    {
                        foreach (HighContrastIsOnChangedEventHandler element in invocationList!)
                        {
                            Task.Run(() =>
                            {
                                // NOTE: it is the target event's responsibility to run any UI-related code on the main UI thread; this event should be considered to be fired from a background thread
                                element.Invoke(null /* static class, no so type instance */, highContrastIsOnChangedEventArgs);
                            });
                        }
                    }
                    //Task.Run(() =>
                    //{
                    //    s_highContrastChanged?.Invoke(null /* static class, no so type instance */, highContrastChangedEventArgs);
                    //});
                }
            }
            else
            {
                Debug.Assert(false, "Captured event that accessibility/color settings changed, but could not read current high contrast on/off state");
            }
        }
    }

    //

    public static MorphicResult<bool, IWin32ApiError> GetIsOn()
    {
        var getHighContrastInfoResult = HighContrast.GetHighContrastInfo();
        if (getHighContrastInfoResult.IsError == true)
        {
            switch (getHighContrastInfoResult.Error!) 
            {
                case IWin32ApiError.Win32Error(Win32ErrorCode: var win32ErrorCode):
                    return MorphicResult.ErrorResult<IWin32ApiError>(new IWin32ApiError.Win32Error(win32ErrorCode));
                default:
                    throw new MorphicUnhandledErrorException();
            }
        }
        var highContrastInfo = getHighContrastInfoResult.Value!;
        var highContrastIsOn = highContrastInfo.IsOn;

        return MorphicResult.OkResult(highContrastIsOn);
    }

    public static MorphicResult<String?, IWin32ApiError> GetHighContrastModeDefaultColorScheme()
    {
        var getHighContrastInfoResult = HighContrast.GetHighContrastInfo();
        if (getHighContrastInfoResult.IsError == true)
        {
            return MorphicResult.ErrorResult(getHighContrastInfoResult.Error!);
        }
        var highContrastInfo = getHighContrastInfoResult.Value!;

        return MorphicResult.OkResult(highContrastInfo.DefaultColorScheme);
    }

    //

    private struct HighContrastInfo
    {
        public bool IsOn;
        public bool FeatureCanBeTurnedOnAndOff;
        public bool HotKeyIsEnabled;
        public bool HotKeyPresentsConfirmationDialog;
        public bool HotKeyPlaysSound;
        //public bool ShowsVisualIndicatorWhenOn;
        public bool HotKeyCanBeEnabled { get; init; }
        //
        public String? DefaultColorScheme;
    }
    private static MorphicResult<HighContrastInfo, IWin32ApiError> GetHighContrastInfo()
    {
        var highContrastInfo = new Windows.Win32.UI.Accessibility.HIGHCONTRASTW()
        {
            cbSize = (uint)Marshal.SizeOf(typeof(Windows.Win32.UI.Accessibility.HIGHCONTRASTW)),
        };

        Windows.Win32.Foundation.BOOL systemParametersInfoResult;
        unsafe
        {
            // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-systemparametersinfow
            systemParametersInfoResult = Windows.Win32.PInvoke.SystemParametersInfo(Windows.Win32.UI.WindowsAndMessaging.SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETHIGHCONTRAST, highContrastInfo.cbSize, &highContrastInfo, (Windows.Win32.UI.WindowsAndMessaging.SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS)0 /* unused for 'get' operation */);
        }
        if (systemParametersInfoResult == 0)
        {
            var win32ErrorCode = (Windows.Win32.Foundation.WIN32_ERROR)System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            return MorphicResult.ErrorResult<IWin32ApiError>(new IWin32ApiError.Win32Error((uint)win32ErrorCode));
        }

        // convert the PWSTR to a C# String
        var defaultSchemeAsCharSpan = highContrastInfo.lpszDefaultScheme.AsSpan();
        var defaultScheme = new string(defaultSchemeAsCharSpan.ToArray(), 0, defaultSchemeAsCharSpan.Length);

        // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-highcontrastw
        var result = new HighContrastInfo()
        {
            IsOn = ((highContrastInfo.dwFlags & Windows.Win32.UI.Accessibility.HIGHCONTRASTW_FLAGS.HCF_HIGHCONTRASTON) == Windows.Win32.UI.Accessibility.HIGHCONTRASTW_FLAGS.HCF_HIGHCONTRASTON),
            FeatureCanBeTurnedOnAndOff = ((highContrastInfo.dwFlags & Windows.Win32.UI.Accessibility.HIGHCONTRASTW_FLAGS.HCF_AVAILABLE) == Windows.Win32.UI.Accessibility.HIGHCONTRASTW_FLAGS.HCF_AVAILABLE),
            HotKeyIsEnabled = ((highContrastInfo.dwFlags & Windows.Win32.UI.Accessibility.HIGHCONTRASTW_FLAGS.HCF_HOTKEYACTIVE) == Windows.Win32.UI.Accessibility.HIGHCONTRASTW_FLAGS.HCF_HOTKEYACTIVE),
            HotKeyPresentsConfirmationDialog = ((highContrastInfo.dwFlags & Windows.Win32.UI.Accessibility.HIGHCONTRASTW_FLAGS.HCF_CONFIRMHOTKEY) == Windows.Win32.UI.Accessibility.HIGHCONTRASTW_FLAGS.HCF_CONFIRMHOTKEY),
            HotKeyPlaysSound = ((highContrastInfo.dwFlags & Windows.Win32.UI.Accessibility.HIGHCONTRASTW_FLAGS.HCF_HOTKEYSOUND) == Windows.Win32.UI.Accessibility.HIGHCONTRASTW_FLAGS.HCF_HOTKEYSOUND),
            //ShowsVisualIndicatorWhenOn = ((highContrastInfo.dwFlags & Windows.Win32.UI.Accessibility.HIGHCONTRASTW_FLAGS.HCF_INDICATOR) == Windows.Win32.UI.Accessibility.HIGHCONTRASTW_FLAGS.HCF_INDICATOR),
            HotKeyCanBeEnabled = ((highContrastInfo.dwFlags & Windows.Win32.UI.Accessibility.HIGHCONTRASTW_FLAGS.HCF_HOTKEYAVAILABLE) == Windows.Win32.UI.Accessibility.HIGHCONTRASTW_FLAGS.HCF_HOTKEYAVAILABLE),
            //
            DefaultColorScheme = defaultScheme
        };
        return MorphicResult.OkResult(result);
    }
}
