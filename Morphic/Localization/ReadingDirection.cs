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

// Session reading direction for window LAYOUT (FlowDirection). WinUI renders translated text from the
// live MRT language, but it does NOT mirror window layout from the language, so without this an Arabic
// (or Farsi/Urdu/...) session shows translated text inside a left-to-right layout. We drive FlowDirection
// off the user's Windows DISPLAY language (GetUserDefaultUILanguage), which changes only at sign-out --
// the same signal the shell itself mirrors from -- so Morphic's layout flips exactly when the rest of the
// desktop does, rather than the instant a preferred language is added mid-session. Cached because the
// display language cannot change within a session.
//
// Mental model: TEXT follows the live MRT language; LAYOUT follows the session display language. The two
// differ only between adding a preferred language and signing out, which is precisely when Windows itself
// shows translated text in an unmirrored layout, so we match it.
internal static class ReadingDirection
{
    private static readonly bool s_sessionIsRightToLeft = ReadingDirection.ComputeSessionIsRightToLeft();

    public static bool SessionIsRightToLeft => ReadingDirection.s_sessionIsRightToLeft;

    public static Microsoft.UI.Xaml.FlowDirection SessionFlowDirection =>
        ReadingDirection.s_sessionIsRightToLeft
            ? Microsoft.UI.Xaml.FlowDirection.RightToLeft
            : Microsoft.UI.Xaml.FlowDirection.LeftToRight;

    // Applies the session reading direction to a window's root content. Call right after
    // InitializeComponent (once Window.Content is set, and before any layout that depends on reading
    // direction). No-op if the content is not yet a FrameworkElement.
    public static void ApplyTo(Microsoft.UI.Xaml.Window window)
    {
        if (window.Content is Microsoft.UI.Xaml.FrameworkElement rootContent)
        {
            rootContent.FlowDirection = ReadingDirection.SessionFlowDirection;
        }
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern ushort GetUserDefaultUILanguage();

    private static bool ComputeSessionIsRightToLeft()
    {
        try
        {
            ushort languageId = ReadingDirection.GetUserDefaultUILanguage();
            return System.Globalization.CultureInfo.GetCultureInfo(languageId).TextInfo.IsRightToLeft;
        }
        catch (System.Exception)
        {
            return false;
        }
    }
}
