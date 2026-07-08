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

namespace Morphic.Localization;

// Reading direction has TWO INDEPENDENT axes, and conflating them is the classic RTL bug:
//
//   * CONTENT (window FlowDirection) follows the APP's display language -- the same live preferred-language
//     signal the strings resolve through (GlobalizationPreferences, as in ResourceLanguage). So Morphic's UI
//     mirrors whenever Morphic is shown in an RTL language, even on a left-to-right Windows.
//
//   * SPATIAL placement that must line up with the SHELL -- the MorphicBar's docking edge and the
//     notification-tray corner -- follows the SYSTEM display language (GetUserDefaultUILanguage). The taskbar
//     and tray only mirror when the SYSTEM is RTL, regardless of Morphic's own language, so our spatial math
//     has to key off the same system signal to stay aligned with them.
//
// The two axes differ only when the app language and the system language disagree (e.g. an Arabic Morphic on
// an English/LTR Windows, or vice versa). When they agree -- the common case -- both axes are identical.
internal static class ReadingDirection
{
    // CONTENT axis -- the app's display language (live; the same source the strings resolve through).
    private static readonly bool s_appIsRightToLeft = ReadingDirection.ComputeAppIsRightToLeft();
    public static bool AppIsRightToLeft => ReadingDirection.s_appIsRightToLeft;

    // SPATIAL axis -- the system display language (logout-stable; what the shell mirrors from).
    private static readonly bool s_systemIsRightToLeft = ReadingDirection.ComputeSystemIsRightToLeft();
    public static bool SystemIsRightToLeft => ReadingDirection.s_systemIsRightToLeft;

    // Window CONTENT flow direction, from the APP axis.
    public static Microsoft.UI.Xaml.FlowDirection SessionFlowDirection =>
        ReadingDirection.s_appIsRightToLeft
            ? Microsoft.UI.Xaml.FlowDirection.RightToLeft
            : Microsoft.UI.Xaml.FlowDirection.LeftToRight;

    // Applies the CONTENT flow direction (APP axis) to a window's root content. Call right after
    // InitializeComponent (once Window.Content is set, and before any layout that depends on reading
    // direction). No-op if the content is not yet a FrameworkElement.
    public static void ApplyTo(Microsoft.UI.Xaml.Window window)
    {
        if (window.Content is Microsoft.UI.Xaml.FrameworkElement rootContent)
        {
            rootContent.FlowDirection = ReadingDirection.SessionFlowDirection;
        }
    }

    // Mirrors the window FRAME (title bar + caption buttons) for an RTL APP language by adding WS_EX_LAYOUTRTL
    // to the window's extended style. This is ORTHOGONAL to ApplyTo's content mirroring: per the Win32/WinUI
    // model WS_EX_LAYOUTRTL flips the FRAME while FlowDirection flips the CONTENT, so calling BOTH gives a
    // fully-mirrored window with no double-flip. Call in the window ctor, before the window is shown.
    //
    // KNOWN WinUI 3 caveat (microsoft-ui-xaml#8559): in RTL the caption-button HIT-BOXES may not be swapped,
    // so close/minimize can LOOK mirrored but not click. Verify on the target SDK; if it bites, just stop
    // calling this method -- content mirroring via ApplyTo is unaffected.
    public static void ApplyChromeMirroringTo(Microsoft.UI.Xaml.Window window)
    {
        if (ReadingDirection.AppIsRightToLeft == false)
        {
            return;
        }

        try
        {
            var windowHandle = (Windows.Win32.Foundation.HWND)WinRT.Interop.WindowNative.GetWindowHandle(window);
            nint currentExtendedStyle = Windows.Win32.PInvoke.GetWindowLongPtr(windowHandle, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
            nint mirroredExtendedStyle = currentExtendedStyle | (nint)(uint)Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_LAYOUTRTL;
            _ = Windows.Win32.PInvoke.SetWindowLongPtr(windowHandle, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, mirroredExtendedStyle);
        }
        catch (System.Exception)
        {
            // Leave LTR chrome on any failure; content mirroring (ApplyTo) is unaffected.
        }
    }

    // The app's CURRENT top preferred UI language -> RTL? Read from GlobalizationPreferences (the SAME live
    // source ResourceLanguage pins PrimaryLanguageOverride to), so content direction always matches the
    // rendered text and updates without a sign-out.
    private static bool ComputeAppIsRightToLeft()
    {
        try
        {
            var preferredLanguages = Windows.System.UserProfile.GlobalizationPreferences.Languages;
            if (preferredLanguages.Count > 0)
            {
                return System.Globalization.CultureInfo.GetCultureInfo(preferredLanguages[0]).TextInfo.IsRightToLeft;
            }
        }
        catch (System.Exception)
        {
        }
        return false;
    }

    // The user's Windows DISPLAY language (a LANGID, via CsWin32; logout-stable) -> RTL? This is the signal
    // the shell mirrors from, so spatial placement that must align with the taskbar/tray keys off it.
    private static bool ComputeSystemIsRightToLeft()
    {
        try
        {
            var languageId = Windows.Win32.PInvoke.GetUserDefaultUILanguage();
            return System.Globalization.CultureInfo.GetCultureInfo(languageId).TextInfo.IsRightToLeft;
        }
        catch (System.Exception)
        {
            return false;
        }
    }
}
