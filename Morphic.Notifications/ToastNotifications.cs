// Copyright 2026 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-notifications-lib-cs/blob/master/LICENSE.txt
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

namespace Morphic.Notifications;

// Router/facade for app-toast notifications. 
//
// Lives in its own library (Morphic.Notifications) rather than in Morphic.WindowsNative
// so that consumers of Morphic.WindowsNative (which may not be WinAppSDK consumers) do not
// inherit a transitive Microsoft.WindowsAppSDK dependency. AppNotificationManager is
// WinAppSDK-only; pushing that into the lower-level Win32-wrapper library would cascade.
//
// Registration must happen AFTER the AUMID has been set
// (Morphic.WindowsNative.AppIdentity.AumidHelper.Initialize, called from Program.Main)
// and BEFORE any ShowText call. Conventionally invoked from App.OnLaunched.
//
// The NotificationInvoked event (in-process route only) is RAISED ON A THREAD-POOL
// THREAD, not the UI thread. Handlers that touch WinUI controls must marshal to the UI
// thread via DispatcherQueue.TryEnqueue before doing so.
public static class ToastNotifications
{
    private const string HelperExeFileName = "Morphic.NotificationHelper.exe";

    private static bool _initialized;
    private static NotificationRouting _routing = NotificationRouting.Disabled;
    private static string? _helperExePath;
    //
    // True when the host process has package identity (running as MSIX). Captured during
    // DetermineRouting and used by both InitializeInProcess and Shutdown to gate calls
    // that are documented as "for unpackaged apps only" (Register / Unregister). In
    // packaged apps the COM activator registration comes from the MSIX manifest's
    // windows.comServer extension, NOT from runtime HKCU writes; calling Register in
    // a packaged process is a no-op at best.
    private static bool _isPackaged;

    private enum NotificationRouting
    {
        Disabled,
        InProcess,
        Helper,
    }

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _routing = ToastNotifications.DetermineRouting();
        switch (_routing)
        {
            case NotificationRouting.InProcess:
                ToastNotifications.InitializeInProcess();
                break;
            case NotificationRouting.Helper:
                // No in-process registration needed: the helper EXE Registers itself on
                // every invocation, so HKCU\Software\Classes\CLSID\{...}\LocalServer32
                // points at the helper, not at us. Morphic main calling Register() here
                // would clobber that to point at Morphic.exe (uiAccess), and Explorer
                // could never activate it -- exactly the broken state we're working around.
                break;
            case NotificationRouting.Disabled:
                System.Diagnostics.Debug.WriteLine(
                    "[ToastNotifications] Routing decision: Disabled. ShowText will silently drop calls.");
                break;
        }
        _initialized = true;
    }

    public static void Shutdown()
    {
        if (!_initialized)
        {
            return;
        }
        if (_routing == NotificationRouting.InProcess)
        {
            AppNotificationManager.Default.NotificationInvoked -= ToastNotifications.OnNotificationInvoked;

            // Packaged apps: skip Unregister. The symmetric Register call was skipped in
            // InitializeInProcess for the same reason; calling Unregister here in a
            // packaged process crashes with STATUS_INVALID_PARAMETER (0xC000000D) deep in
            // ntdll during shutdown. Microsoft's docs corroborate that Register/Unregister
            // are for unpackaged apps only; the packaged path uses MSIX manifest extensions.
            if (_isPackaged == false)
            {
                try
                {
                    AppNotificationManager.Default.Unregister();
                }
                catch (System.Runtime.InteropServices.COMException ex)
                {
                    // Defensive: even unpackaged Unregister could throw during teardown
                    // (transient COM failures, Shell already torn down, etc.). Don't let
                    // a shutdown-path exception escape; the OS would clean up the
                    // registration on process exit regardless.
                    System.Diagnostics.Debug.WriteLine(
                        $"[ToastNotifications] Unregister() failed (HRESULT 0x{(uint)ex.HResult:X8}); continuing shutdown.");
                }
            }
        }
        // Helper-routing shutdown is a no-op: each helper invocation is its own process
        // lifecycle, and the cleanup-on-uninstall path runs `helper.exe --unregister` from
        // the MSI custom action, not from Morphic main's shutdown.
        _initialized = false;
    }

    // Shows a simple two-line toast: title (rendered prominently) over body (smaller).
    // Safe to call from any thread. If called before Initialize, the call is dropped with
    // a debug log rather than risking a use-before-Register exception.
    public static void ShowText(string title, string body)
    {
        if (!_initialized)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[ToastNotifications] ShowText('{title}') called before Initialize; dropping.");
            return;
        }

        switch (_routing)
        {
            case NotificationRouting.InProcess:
                ToastNotifications.ShowTextInProcess(title, body);
                break;
            case NotificationRouting.Helper:
                ToastNotifications.ShowTextViaHelper(title, body);
                break;
            case NotificationRouting.Disabled:
                System.Diagnostics.Debug.WriteLine(
                    $"[ToastNotifications] ShowText('{title}') dropped (routing=Disabled).");
                break;
        }
    }

    private static NotificationRouting DetermineRouting()
    {
        // Packaged (MSIX): in-process AppNotificationManager works correctly because the
        // OS handles COM activation via the MSIX manifest's extensions, not via
        // CreateProcess of the executable. Capture _isPackaged so the in-process init
        // and shutdown paths can skip Register/Unregister (which are documented as
        // unpackaged-only and observed to crash during shutdown in packaged builds).
        var isPackagedResult = Morphic.WindowsNative.Packaging.Package.IsRunningAsPackagedApp();
        if (isPackagedResult.IsSuccess == true && isPackagedResult.Value == true)
        {
            _isPackaged = true;
            return NotificationRouting.InProcess;
        }

        // Unpackaged + uiAccess=true: helper-routed. This is the load-bearing branch -- the
        // whole reason this class exists in its current form.
        var isUiAccessResult = Morphic.WindowsNative.Process.UiAccess.IsCurrentProcessUiAccess();
        if (isUiAccessResult.IsSuccess == true && isUiAccessResult.Value == true)
        {
            return ToastNotifications.TryConfigureHelperPath();
        }

        // Unpackaged + non-uiAccess (dev/F5/non-secure-location installs): in-process works
        // because the calling process is plain Medium IL, so Explorer can CreateProcess it
        // as a COM activator without issue.
        return NotificationRouting.InProcess;
    }

    private static NotificationRouting TryConfigureHelperPath()
    {
        var candidate = System.IO.Path.Combine(System.AppContext.BaseDirectory, ToastNotifications.HelperExeFileName);
        if (System.IO.File.Exists(candidate) == false)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[ToastNotifications] Helper EXE not found at expected path '{candidate}'. Toasts will be disabled. Ensure the {ToastNotifications.HelperExeFileName} project's output is being copied alongside Morphic.exe.");
            return NotificationRouting.Disabled;
        }
        _helperExePath = candidate;
        return NotificationRouting.Helper;
    }

    private static void InitializeInProcess()
    {
        // Per Microsoft's self-contained deployment docs, AppNotificationManager has a hard
        // dependency on the Windows App Runtime "Singleton" MSIX package -- not on any DLL
        // we could deploy app-locally. The Singleton package is installed by
        // WindowsAppRuntimeInstall.exe (which our installer chains in production) but is
        // absent on machines that haven't run that installer. IsSupported() is the
        // documented dynamic check for this: it returns true only when the Singleton
        // package is registered for the current user and the AppNotifications surface is
        // therefore usable. Returning false on a stock machine without the runtime is
        // expected, not a fault; we just disable in-process routing and let ShowText no-op.
        if (AppNotificationManager.IsSupported() == false)
        {
            System.Diagnostics.Debug.WriteLine(
                "[ToastNotifications] AppNotificationManager.IsSupported() returned false; toast notifications disabled. Most likely cause: the Windows App Runtime (specifically the Singleton MSIX package) isn't installed for the current user. The Morphic installer chains WindowsAppRuntimeInstall.exe to provision it; if you reached this from F5 Debug, install the Runtime separately from https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads.");
            _routing = NotificationRouting.Disabled;
            return;
        }
        // Subscribe FIRST, then Register: ensures the very first activation that arrives
        // after Register completes is delivered to our handler. Doing it in the opposite
        // order opens a race where a notification activation between Register and the
        // subscription would be lost. (For packaged apps we skip Register entirely, but
        // we still subscribe so in-process activations are delivered.)
        AppNotificationManager.Default.NotificationInvoked += ToastNotifications.OnNotificationInvoked;

        if (_isPackaged)
        {
            // Packaged apps: skip Register. Activator registration is supplied by the
            // MSIX manifest's windows.comServer extension; calling Register at runtime
            // is documented as unnecessary for packaged apps. Empirically the symmetric
            // Unregister at shutdown also crashes in packaged with STATUS_INVALID_
            // PARAMETER (0xC000000D), so we skip both. The Shell still delivers
            // NotificationInvoked to our handler subscribed above.
            return;
        }

        try
        {
            AppNotificationManager.Default.Register();
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            // Defensive: even with IsSupported()==true, Register can theoretically still
            // throw (loader-path quirks, transient Shell COM activation failures, etc.).
            // Roll back the NotificationInvoked subscription so it doesn't dangle on the
            // AppNotificationManager singleton, switch routing to Disabled, and continue:
            // lacking toasts is far better than failing to start the entire app.
            AppNotificationManager.Default.NotificationInvoked -= ToastNotifications.OnNotificationInvoked;
            System.Diagnostics.Debug.WriteLine(
                $"[ToastNotifications] Register() failed (HRESULT 0x{(uint)ex.HResult:X8}) despite IsSupported()==true; toast notifications disabled.");
            _routing = NotificationRouting.Disabled;
        }
    }

    private static void ShowTextInProcess(string title, string body)
    {
        // No SetAppLogoOverride call: the Shell renders the small Morphic icon in the
        // toast header automatically from the AUMID's associated visual identity (Start
        // Menu shortcut icon for unpackaged Morphic, manifest Square*x*Logo PNGs for
        // packaged). That single header icon is the only branding we want on these
        // simple text toasts; an AppLogoOverride would add a redundant big square to the
        // left quarter of the toast body.
        var notification = new Microsoft.Windows.AppNotifications.Builder.AppNotificationBuilder()
            .AddText(title)
            .AddText(body)
            .BuildNotification();
        AppNotificationManager.Default.Show(notification);
    }

    private static void ShowTextViaHelper(string title, string body)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = _helperExePath!,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        // ArgumentList does proper escaping for spaces, quotes, etc. Far safer than
        // formatting into Arguments by hand.
        startInfo.ArgumentList.Add("--show");
        startInfo.ArgumentList.Add("--aumid");
        startInfo.ArgumentList.Add(Morphic.WindowsNative.AppIdentity.AumidHelper.AppUserModelId);
        startInfo.ArgumentList.Add("--title");
        startInfo.ArgumentList.Add(title);
        startInfo.ArgumentList.Add("--body");
        startInfo.ArgumentList.Add(body);

        try
        {
            // Fire-and-forget: don't wait on the helper, don't read its stdout. The toast
            // is queued by the Shell as soon as helper calls Show(), which happens within
            // milliseconds of process start; we have no use for the helper's exit code on
            // the success path. Dispose the Process handle promptly so we don't pile up
            // child handles if many toasts are emitted in quick succession.
            using var process = System.Diagnostics.Process.Start(startInfo);
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[ToastNotifications] Helper launch failed for '{title}': {ex.Message}");
        }
    }

    // Fires when the user clicks a toast (the body, or any button on it). For toasts launched
    // while the app was not running, the activation arrives via the AppActivationArguments
    // path in OnLaunched instead; this handler covers the in-running-process case. ONLY
    // relevant to the InProcess routing branch -- the Helper branch's activations land in
    // the helper EXE, not here.
    //
    // args.Argument is the top-level "click anywhere on toast" argument (string, set via
    // AppNotificationBuilder.AddArgument on the root). args.Arguments is the IDictionary
    // of "key=value" pairs from any button's AddArgument calls. With no buttons or top-level
    // arguments, both are empty / null and this handler has nothing to dispatch on -- the
    // user just clicked the toast and the only effect needed is the app coming forward,
    // which the Shell handles automatically.
    private static void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[ToastNotifications] Invoked: Argument='{args.Argument}', Arguments.Count={args.Arguments.Count}");
        // Future per-argument dispatch goes here. Remember to marshal to UI thread before
        // touching WinUI controls:
        //   _dispatcherQueue.TryEnqueue(() => { /* UI work */ });
    }
}
