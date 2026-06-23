// Copyright 2026 Raising the Floor - US, Inc.
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

namespace Morphic.SystemSettings;

using Morphic.Core;

// Launches a Windows Settings page via its ms-settings: protocol URI. Shared by the tray "More settings to
// make the computer easier" submenu and (later) the MorphicBar buttons' right-click "Settings" entries -- both
// open the same OS pages, so they funnel through one launcher (as Morphic 1.x did).
//
// Mechanism: ShellExecute the ms-settings: URI (Process.Start with UseShellExecute = true). This is the launch
// path the installer settled on for uiAccess builds: a uiAccess process can ShellExecute a protocol URI,
// whereas a plain CreateProcess fails with ERROR_ELEVATION_REQUIRED (740). It is PInvoke-free (UseShellExecute
// is the managed ShellExecuteEx wrapper), so there is nothing to route through CsWin32.
internal static class WindowsSettings
{
    // The Windows Settings pages Morphic links to; each maps to an ms-settings: URI (see UriFor).
    public enum Page
    {
        AllAccessibility,
        //
        ColorVision,
        Contrast,
        DarkMode,
        Display,
        Keyboard,
        Language,
        Magnifier,
        Mouse,
        NightLight,
        PointerSize,
        ReadAloud,
        Voice,
    }

    // Opens a known settings page.
    public static MorphicResult<MorphicUnit, MorphicUnit> Open(Page page)
    {
        return WindowsSettings.Open(WindowsSettings.UriFor(page));
    }

    // Opens an arbitrary ms-settings: URI -- for callers that carry a target from configuration rather than the
    // Page enum (e.g. the MorphicBar buttons' context menus, whose settings target comes from bar data).
    public static MorphicResult<MorphicUnit, MorphicUnit> Open(string msSettingsUri)
    {
        try
        {
            _ = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(msSettingsUri) { UseShellExecute = true });
            return MorphicResult.OkResult();
        }
        catch (System.Exception)
        {
            // A page that cannot be opened (a URI this OS does not recognize, a shell failure) must not crash
            // the app; the caller can surface or ignore the failure.
            return MorphicResult.ErrorResult();
        }
    }

    private static string UriFor(Page page)
    {
        return page switch
        {
            Page.AllAccessibility => WindowsSettings.AllAccessibilityUri(),
            //
            Page.ColorVision => "ms-settings:easeofaccess-colorfilter",
            Page.Contrast => "ms-settings:easeofaccess-highcontrast",
            Page.DarkMode => "ms-settings:colors",
            Page.Display => "ms-settings:display",
            Page.Keyboard => "ms-settings:easeofaccess-keyboard",
            Page.Language => "ms-settings:regionlanguage",
            Page.Magnifier => "ms-settings:easeofaccess-magnifier",
            Page.Mouse => "ms-settings:mousetouchpad",
            Page.NightLight => "ms-settings:nightlight",
            Page.PointerSize => "ms-settings:easeofaccess-mousepointer",
            Page.ReadAloud => "ms-settings:speech",
            Page.Voice => "ms-settings:easeofaccess-speechrecognition",
            _ => throw new System.ComponentModel.InvalidEnumArgumentException(nameof(page), (int)page, typeof(Page)),
        };
    }

    // "All accessibility settings" moved between Windows versions: Windows 11 uses the Accessibility hub
    // (ms-settings:easeofaccess); Windows 10 used Ease of Access > Display (ms-settings:easeofaccess-display).
    private static string AllAccessibilityUri()
    {
#if INCLUDE_WINDOWS_10_SUPPORT
        if (Morphic.WindowsNative.OsVersion.OsVersion.IsWindows11OrLater() == true)
        {
#endif
            return "ms-settings:easeofaccess";          // Windows 11 and newer
#if INCLUDE_WINDOWS_10_SUPPORT
        }
        else
        {
            return "ms-settings:easeofaccess-display";  // Windows 10 (legacy)
        }
#endif
    }
}
