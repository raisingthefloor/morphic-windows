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

// Pins the app's MRT resource language to the user's current top preferred UI language, with en-US as the
// GUARANTEED final fallback.
//
// WHY: by default MRT resolves a string against the user's full preferred-language LIST, so a string missing
// in the top language can fall back to ANY other language the user has installed -- we observed German leaking
// into a Spanish session (German was installed, and MRT chose it over English for the strings Spanish had not
// yet translated). Setting PrimaryLanguageOverride to a single language collapses the resolution list to
// [primaryLanguage -> region truncations (es-ES -> es) -> en-US default], so a missing string ALWAYS falls
// back to English, never to an unrelated translated language.
//
// We read the LIVE top preferred language (GlobalizationPreferences), so this responds to a language change on
// the next app launch WITHOUT requiring a sign-out. NOTE: ReadingDirection still keys layout off
// GetUserDefaultUILanguage (logon-cached), so the two can briefly diverge if the display language is changed
// without signing out; they reconcile after sign-out. Align ReadingDirection to the same live signal later if
// that edge case ever matters.
//
// Must be called ONCE at startup, BEFORE any localized UI (menu, bar, about) loads its resources -- the call
// site is the App() ctor, before InitializeComponent.
internal static class ResourceLanguage
{
    public static void ApplyDisplayLanguageWithEnglishFallback()
    {
        try
        {
            // The user's CURRENT top preferred UI language as a BCP-47 tag (e.g. "es-ES"). We deliberately read
            // GlobalizationPreferences (the LIVE preferred-languages list) and NOT GetUserDefaultUILanguage: the
            // latter is LOGON-CACHED and does not reflect a display-language change until the user signs out and
            // back in, so it would pin the app to the pre-change language. We hit exactly that -- the app rendered
            // all-English while Spanish was active, because no sign-out had happened. The top preferred language
            // updates immediately and is the same signal other apps follow.
            var preferredLanguages = Windows.System.UserProfile.GlobalizationPreferences.Languages;
            var tag = (preferredLanguages.Count > 0) ? preferredLanguages[0] : null;
            if (!string.IsNullOrEmpty(tag))
            {
                // One primary language + the en-US default IS the entire fallback chain; no other installed
                // language can be selected to satisfy a string that is missing in the primary.
                //
                // This MUST be Microsoft.Windows.Globalization (the Windows App SDK class). It is what drives
                // MRT Core resolution for BOTH ResourceLoader and XAML x:Uid in an unpackaged app. The UWP
                // Windows.Globalization.ApplicationLanguages class compiles fine but is a NO-OP here (it never
                // reaches App SDK MRT Core), which is exactly why an es display gap was still resolving to an
                // installed de. Pairs with the call site running in the App() ctor BEFORE InitializeComponent:
                // the override must be set before any resources load, or the fallback chain is already cached.
                Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = tag;
            }
        }
        catch (System.Exception)
        {
            // If the preferred language can't be determined, leave MRT's default resolution in place rather
            // than risk forcing the app to the wrong language.
        }
    }
}
