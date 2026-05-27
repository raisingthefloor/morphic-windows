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

// Morphic.NotificationHelper.exe is a small unprivileged sibling of Morphic.exe whose sole
// purpose is to be the COM activator for app/toast notifications (and push notifications in
// the future). Morphic.exe ships with uiAccess=true in production; Explorer (Medium IL) cannot 
// CreateProcess a uiAccess (Medium+ IL) executable and so cannot launch Morphic.exe directly 
// as a toast activator (CreateProcess returns error 740). This helper is asInvoker / 
// uiAccess=false, which Explorer CAN launch, so we register THIS executable as the activator 
// instead.
//
// Invocation modes:
//   1. CLI from Morphic main: `Morphic.NotificationHelper.exe --show --aumid "<aumid>" --title "<title>" --body "<body>"`
//      Sets the AUMID, registers as the activator (idempotent registry write), shows the
//      toast, exits.
//   2. COM-activated by Windows on toast click: Windows launches us with no CLI args of our
//      own; instead, AppInstance.GetCurrent().GetActivatedEventArgs() returns kind
//      AppNotification. We log + exit. Real callback delivery back to Morphic main is
//      deferred until the first actionable toast consumer exists (i.e. via named-pipe IPC, etc.).
//   3. CLI uninstall cleanup: `Morphic.NotificationHelper.exe --unregister [--aumid "<aumid>"]`
//      Sets the AUMID, calls AppNotificationManager.Default.Unregister() to remove both
//      sides of the registration (AppUserModelId\<aumid>\Activator\CLSID AND
//      CLSID\{generated}\LocalServer32), exits. Designed to be invoked by the MSI
//      uninstaller as a deferred custom action sequenced BEFORE RemoveFiles, so the helper
//      exe itself still exists when the call is made. Failure (helper missing, Unregister
//      throw) is non-fatal for the uninstaller; WiX's ForceDeleteOnUninstall on the
//      AUMID-side key catches the most user-visible leftover as a backstop.

namespace Morphic.NotificationHelper;

internal static class Program
{
    private const string CompanionAppMessage = "This application is a companion app and cannot be launched independently.";

    // Process exit codes emitted to the OS. Values are part of the public-ish contract
    // (the MSI's WixQuietExec64 custom action captures the exit code in the install log;
    // future scripts may key off these), so don't renumber existing entries -- only add
    // new ones at the end. Gap at 6 is intentional, reserved for future use.
    private enum ExitCode
    {
        Success = 0,
        UnhandledException = 1,
        RegisterFailed = 2,
        ShowRequiresTitleOrBody = 3,
        NoModeSpecified = 4,
        UnregisterFailed = 5,
        CallerSignatureRejected = 7,
    }

    [System.STAThread]
    private static int Main(string[] args)
    {
        try
        {
            return (int)Program.Run(args);
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Morphic.NotificationHelper] Unhandled exception: {ex}");
            return (int)ExitCode.UnhandledException;
        }
    }

    private static ExitCode Run(string[] args)
    {
        string aumid = Morphic.WindowsNative.AppIdentity.AumidHelper.AppUserModelId;
        bool showRequested = false;
        bool unregisterRequested = false;
        string? title = null;
        string? body = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--show":
                    showRequested = true;
                    break;
                case "--unregister":
                    unregisterRequested = true;
                    break;
                case "--aumid":
                    if (i + 1 < args.Length) { aumid = args[++i]; }
                    break;
                case "--title":
                    if (i + 1 < args.Length) { title = args[++i]; }
                    break;
                case "--body":
                    if (i + 1 < args.Length) { body = args[++i]; }
                    break;
            }
        }

        // Set the AUMID before any AppNotificationManager call so toasts emitted from this
        // process are attributed to Morphic (not to an anonymous helper). The canonical
        // constant + Initialize implementation live in Morphic.WindowsNative.AppIdentity
        // .AumidHelper -- shared with Morphic.exe so both processes register under the same
        // identity. Pass the parsed `aumid` here (which defaults to AppUserModelId but can
        // be overridden via --aumid on the command line).
        Morphic.WindowsNative.AppIdentity.AumidHelper.Initialize(aumid);

        // CsWinRT projections (AppNotificationManager among them) require ComWrappers
        // initialization before any WinRT-projected type is touched. Morphic main does this
        // in its Program.Main before Application.Start; the helper does it here for the
        // same reason.
        WinRT.ComWrappersSupport.InitializeComWrappers();

        // Uninstall-cleanup path: ALWAYS takes priority over --show and over COM-activation
        // detection, since the uninstaller invokes us specifically to clean registry state
        // and any other side-effect (showing a toast, dispatching an activation) would be
        // surprising. Unregister cleans both the AppUserModelId\<aumid> tree and the
        // CLSID\{generated}\LocalServer32 tree. Safe to call even if Register was never
        // called (Unregister no-ops when there's nothing to remove).
        if (unregisterRequested)
        {
            try
            {
                Microsoft.Windows.AppNotifications.AppNotificationManager.Default.Unregister();
                return ExitCode.Success;
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Morphic.NotificationHelper] Unregister() failed (HRESULT 0x{(uint)ex.HResult:X8}); WiX ForceDeleteOnUninstall backstop should still clean the AUMID-side key.");
                return ExitCode.UnregisterFailed;
            }
        }

        // Subscribe BEFORE Register so the very first activation arriving after Register
        // can't slip through unsubscribed.
        Microsoft.Windows.AppNotifications.AppNotificationManager.Default.NotificationInvoked += Program.OnNotificationInvoked;
        try
        {
            // Register() is the load-bearing call: writes HKCU\Software\Classes\AppUserModelId
            // \<aumid>\Activator\CLSID and the matching HKCU\Software\Classes\CLSID\{generated}
            // \LocalServer32 -> THIS exe's full path. Idempotent; safe to call every launch.
            // Every helper launch overwrites LocalServer32 so the most-recently-launched
            // helper exe wins (which is what we want when the developer cycles between dev
            // and installed Morphic on the same machine; both call Register from their own
            // install location).
            Microsoft.Windows.AppNotifications.AppNotificationManager.Default.Register();
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[Morphic.NotificationHelper] Register() failed (HRESULT 0x{(uint)ex.HResult:X8}); aborting.");
            Microsoft.Windows.AppNotifications.AppNotificationManager.Default.NotificationInvoked -= Program.OnNotificationInvoked;
            return ExitCode.RegisterFailed;
        }

        // COM-activation path: Windows launched us because the user clicked a toast. We log
        // and exit; full callback dispatch back to Morphic main is deferred (no consumer
        // currently needs it, and the IPC channel is a bigger build).
        var appInstance = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent();
        var activationArgs = appInstance.GetActivatedEventArgs();
        if (activationArgs is not null
            && activationArgs.Kind == Microsoft.Windows.AppLifecycle.ExtendedActivationKind.AppNotification)
        {
            var notificationArgs = activationArgs.Data as Microsoft.Windows.AppNotifications.AppNotificationActivatedEventArgs;
            System.Diagnostics.Debug.WriteLine(
                $"[Morphic.NotificationHelper] COM-activated by toast click. Argument='{notificationArgs?.Argument}', Arguments.Count={notificationArgs?.Arguments.Count ?? 0}. Callback dispatch is deferred; exiting.");
            return ExitCode.Success;
        }

        // CLI mode: --show. The only mode currently supported. If a future caller needs a
        // different mode (e.g. persistent listener for push notifications), it gets added
        // as a sibling branch here; remember to apply the same CallerVerification check
        // for any new mode that exposes sensitive capability (sending under our AUMID,
        // receiving push payloads, etc.).
        if (showRequested)
        {
            // Refuse to fire a toast unless our caller is signed by a trusted Morphic
            // publisher cert. Without this, any other process on the machine could spawn
            // the helper with arbitrary --title/--body and emit Morphic-branded toasts
            // (spoofing). The check is bypassed in DEBUG builds so dev F5 with an
            // unsigned Morphic.exe still works; see CallerVerification for details and
            // for the rationale on what is/isn't gated.
            if (CallerVerification.IsCallerSignedBySameAuthenticodeCertificate() == false)
            {
                // Visible message for anyone who runs the helper directly from a console
                // (testers, curious users, etc.). When the router launches us with
                // CreateNoWindow=true the message goes to the hidden console host, but
                // that path doesn't have anyone watching anyway. Debug.WriteLine remains
                // for attached-debugger visibility.
                System.Console.Error.WriteLine(Program.CompanionAppMessage);
                System.Diagnostics.Debug.WriteLine(
                    "[Morphic.NotificationHelper] --show refused: caller did not pass the signed-by-Morphic check. See CallerVerification for details.");
                return ExitCode.CallerSignatureRejected;
            }

            if (title is null && body is null)
            {
                System.Diagnostics.Debug.WriteLine("[Morphic.NotificationHelper] --show requires --title and/or --body; nothing to display.");
                return ExitCode.ShowRequiresTitleOrBody;
            }

            // No SetAppLogoOverride; Morphic main's ToastNotifications.ShowText also skips
            // it. The Shell renders the small Morphic icon in the toast header from the
            // AUMID's associated visual identity (Start Menu shortcut for unpackaged
            // Morphic). An AppLogoOverride would add a redundant big square to the left
            // quarter of the toast body.
            var builder = new Microsoft.Windows.AppNotifications.Builder.AppNotificationBuilder();
            if (title is not null) { _ = builder.AddText(title); }
            if (body is not null) { _ = builder.AddText(body); }
            var notification = builder.BuildNotification();
            Microsoft.Windows.AppNotifications.AppNotificationManager.Default.Show(notification);
            return ExitCode.Success;
        }

        // Visible message for anyone who runs the helper directly with no mode (e.g.,
        // double-click from Explorer, or running with no args from a console). Matches the
        // rejection message shown when --show fails the caller-signature check; same
        // intent both times: explain that the helper isn't a standalone application. Only
        // reaches a visible console when the user actually has one (cmd/PowerShell);
        // launches from Explorer would see a brief console-host flash with this message.
        System.Console.Error.WriteLine(Program.CompanionAppMessage);
        System.Diagnostics.Debug.WriteLine("[Morphic.NotificationHelper] No mode specified (expected --show, --unregister, or COM activation); exiting.");
        return ExitCode.NoModeSpecified;
    }

    private static void OnNotificationInvoked(
        Microsoft.Windows.AppNotifications.AppNotificationManager sender,
        Microsoft.Windows.AppNotifications.AppNotificationActivatedEventArgs args)
    {
        // In-process activation (a click during the brief window between Show and process
        // exit). Logged and otherwise ignored in v1; real dispatch to Morphic main lives
        // in the future named-pipe IPC layer.
        System.Diagnostics.Debug.WriteLine(
            $"[Morphic.NotificationHelper] NotificationInvoked: Argument='{args.Argument}', Arguments.Count={args.Arguments.Count}");
    }
}
