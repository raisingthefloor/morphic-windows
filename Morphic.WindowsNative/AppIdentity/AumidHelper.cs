// Copyright 2026 Raising the Floor - US, Inc.
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

namespace Morphic.WindowsNative.AppIdentity;

// Sets the process's AppUserModelID (AUMID) at startup. The AUMID is what the Shell uses
// to attribute toast notifications to Morphic, group taskbar/Alt+Tab entries, and identify
// Morphic for Start Menu pinning. For unpackaged apps (Morphic ships via MSI, not MSIX)
// the AUMID is not automatically inferred; we must explicitly set it.
//
// MUST be called as the very first thing in Program.Main, BEFORE
// Microsoft.UI.Xaml.Application.Start(...). Setting it later risks frameworks observing a
// default-or-empty AUMID and caching it for Shell-side bookkeeping.
//
// The same AUMID must also be set as the System.AppUserModel.ID property on the installer's
// Start Menu shortcut (see MorphicSetup/Package.wxs). The runtime call here covers the case
// where Morphic is launched outside the shortcut (dev runs from Visual Studio, direct .exe
// launch, task manager restart, etc.); the shortcut covers the case where Morphic is
// launched from Start Menu/taskbar before this code has a chance to run.
//
// Lives in Morphic.WindowsNative because AUMID is a Win32-level concept, not a
// notification-specific one (it's also used for Shell pinning, jump lists, taskbar
// grouping)
public static class AumidHelper
{
    // Reverse-domain style, ASCII only, no spaces. MUST match the System.AppUserModel.ID
    // property set on the installer's Start Menu shortcut. Changing this value after
    // shipping loses notification history and breaks Start Menu pin associations for
    // existing installations.
    public const string AppUserModelId = "RaisingTheFloor.Morphic";

    // Sets the current process's AUMID to the canonical Morphic value.
    public static void Initialize()
    {
        AumidHelper.Initialize(AumidHelper.AppUserModelId);
    }

    // Sets the current process's AUMID to a caller-supplied value. Used by
    // Morphic.NotificationHelper.exe so its --aumid override CLI flag can still flow through
    // the same code path. For Morphic.exe, the parameterless overload above is the right
    // choice; only the helper exposes a runtime AUMID override.
    public static void Initialize(string aumid)
    {
        var hresult = Windows.Win32.PInvoke.SetCurrentProcessExplicitAppUserModelID(aumid);
        // Failure here is a developer bug (malformed AUMID, broken shell32, ...) rather
        // than a runtime condition the user could meaningfully react to. We can't notify
        // the user via toast because the toast infrastructure depends on the AUMID we just
        // failed to set, and a msgbox at startup would be obnoxious. Debug.Assert surfaces
        // the failure in dev/debug builds; release builds silently continue (toasts will
        // simply be attributed to a generic identifier or suppressed entirely by the Shell).
        System.Diagnostics.Debug.Assert(
            hresult.Succeeded,
            $"SetCurrentProcessExplicitAppUserModelID failed with HRESULT 0x{(uint)hresult.Value:X8}");
    }
}
