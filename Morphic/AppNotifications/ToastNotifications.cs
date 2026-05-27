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

using Microsoft.Windows.AppNotifications;

namespace Morphic.AppNotifications;

internal static class ToastNotifications
{
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }
        if (!AppNotificationManager.IsSupported())
        {
            System.Diagnostics.Debug.WriteLine(
                "[ToastNotifications] AppNotificationManager.IsSupported() returned false; toast notifications disabled. Most likely cause: the Windows App Runtime (specifically the Singleton MSIX package) isn't installed for the current user. The Morphic installer chains WindowsAppRuntimeInstall.exe to provision it; if you reached this from F5 Debug, install the Runtime separately from https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads.");
            return;
        }
        AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;
        try
        {
            AppNotificationManager.Default.Register();
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            AppNotificationManager.Default.NotificationInvoked -= OnNotificationInvoked;
            System.Diagnostics.Debug.WriteLine(
                $"[ToastNotifications] Register() failed (HRESULT 0x{(uint)ex.HResult:X8}) despite IsSupported()==true; toast notifications disabled.");
            return;
        }
        _initialized = true;
    }

    public static void Shutdown()
    {
        if (!_initialized)
        {
            return;
        }
        AppNotificationManager.Default.NotificationInvoked -= OnNotificationInvoked;
        AppNotificationManager.Default.Unregister();
        _initialized = false;
    }

    private static void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[ToastNotifications] Invoked: Argument='{args.Argument}', Arguments.Count={args.Arguments.Count}");
    }
    public static void ShowText(string title, string body)
    {
        if (!_initialized)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[ToastNotifications] ShowText('{title}') called before Initialize; dropping.");
            return;
        }
        var notification = new Microsoft.Windows.AppNotifications.Builder.AppNotificationBuilder()
            .AddText(title)
            .AddText(body)
            .BuildNotification();
        AppNotificationManager.Default.Show(notification);
    }
}
