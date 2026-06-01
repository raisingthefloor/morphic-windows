// Copyright 2020-2026 Raising the Floor - US, Inc.
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

using System;
using System.Collections.Generic;
using System.Text;

#if DISABLE_XAML_GENERATED_MAIN
namespace Morphic;

public class Program
{
//    const uint WINUI_MAJOR_VERSION = 1;
//    const uint WINUI_MINOR_VERSION = 8;
//    const uint WINUI_MAJOR_MINOR_VERSION = (WINUI_MAJOR_VERSION << 16) | WINUI_MINOR_VERSION;

    [STAThread]
    static void Main(string[] args)
    {
        // Set the process AppUserModelID before any framework code runs. Must happen here
        // (Main, before Microsoft.UI.Xaml.Application.Start) rather than in App.xaml.cs so
        // that any Shell-side caching during WinUI initialization picks up the correct AUMID.
        // See Morphic.WindowsNative/AppIdentity/AumidHelper.cs for the value and rationale.
        Morphic.WindowsNative.AppIdentity.AumidHelper.Initialize();

        // Single-instance gate: if another Morphic is already running, hand our activation
        // to it (which surfaces its MorphicBar) and exit instead of starting a second copy.
        if (Program.DecideRedirection() == true)
        {
            // Secondary instance: we have handed our activation to the primary (or timed out trying).
            // Force-terminate rather than `return`, so that a redirect thread-pool task still blocked
            // inside RedirectActivationToAsync (which can hang indefinitely) can never keep this
            // window-less process alive as a zombie. A secondary instance has nothing to tear down.
            System.Environment.Exit(0);
        }

//        bool bootstrapInitialized = false;

//        var isRunningAsPackagedAppResult = Morphic.WindowsNative.Packaging.Package.IsRunningAsPackagedApp();
//        if (isRunningAsPackagedAppResult.IsSuccess == false)
//        {
//            throw new InvalidOperationException("Morphic could not detect package identity (or if it is running as a packaged or unpackaged app); the application cannot start up.");
//        }
//        var isRunningAsPackagedApp = isRunningAsPackagedAppResult.Value!;
//        if (isRunningAsPackagedApp == false)
//        {
//            // for unpackaged apps: initialize the Windows App SDK via bootstrap (so that WinRT activation factories can locate the native DLLs, etc.)
//            Microsoft.Windows.ApplicationModel.DynamicDependency.Bootstrap.Initialize(WINUI_MAJOR_MINOR_VERSION);
//            bootstrapInitialized = true;
//        }
//
        // execute application
        // see: https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/applifecycle/applifecycle-single-instance
//        try
//        {
            WinRT.ComWrappersSupport.InitializeComWrappers();

            Microsoft.UI.Xaml.Application.Start((p) =>
            {
                var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                System.Threading.SynchronizationContext.SetSynchronizationContext(context);
                new App();
            });
//        }
//        finally
//        {
//            if (bootstrapInitialized)
//            {
//                // Release the DDLM and clean up.
//                Microsoft.Windows.ApplicationModel.DynamicDependency.Bootstrap.Shutdown();
//            }
//        }
    }

    private static bool DecideRedirection()
    {
        // The first Morphic process to register this key becomes the primary instance. Any later
        // process that registers the same key is told it is not current, so it redirects its
        // activation to the primary (which surfaces its MorphicBar) and then exits.
        //
        // The key is suffixed differently for Debug vs Release builds so a Debug build launched from
        // Visual Studio and an installed Release build do NOT treat each other as the same instance.
        // Without distinct keys, starting a Debug session while an installed Morphic was running (or
        // vice-versa) would redirect into the other process, and the build under test would never
        // actually launch. (The key is per-Windows-user.)
#if DEBUG
        const string SINGLE_INSTANCE_KEY = "Morphic-SingleInstance-Debug";
#else
        const string SINGLE_INSTANCE_KEY = "Morphic-SingleInstance";
#endif

        var activationArguments = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs();
        var keyInstance = Microsoft.Windows.AppLifecycle.AppInstance.FindOrRegisterForKey(SINGLE_INSTANCE_KEY);

        if (keyInstance.IsCurrent == true)
        {
            // We are the primary instance: listen for activations redirected here by later instances.
            keyInstance.Activated += Program.OnAppInstanceActivated;
            return false;
        }

        // We are a secondary instance: redirect to the primary and tell Main to exit.
        Program.RedirectActivationTo(activationArguments, keyInstance);
        return true;
    }

    private static void OnAppInstanceActivated(object? sender, Microsoft.Windows.AppLifecycle.AppActivationArguments args)
    {
        // Fires on a background thread when a secondary instance redirects to us. Bridge into the
        // running App, which marshals to the UI thread and shows the MorphicBar.
        (Microsoft.UI.Xaml.Application.Current as App)?.HandleRedirectedActivation();
    }

    private static void RedirectActivationTo(Microsoft.Windows.AppLifecycle.AppActivationArguments args, Microsoft.Windows.AppLifecycle.AppInstance keyInstance)
    {
        // RedirectActivationToAsync marshals to the primary instance over COM. We must not block this
        // STA thread on it directly (that would deadlock the COM call), so we run the redirect on a
        // thread-pool thread and pump COM here via CoWaitForMultipleObjects until it signals done.
        using var redirectCompletedEvent = new System.Threading.ManualResetEvent(false);
        _ = System.Threading.Tasks.Task.Run(() =>
        {
            keyInstance.RedirectActivationToAsync(args).AsTask().Wait();
            _ = redirectCompletedEvent.Set();
        });

        // Bounded wait, NOT infinite. If RedirectActivationToAsync never completes (observed when the
        // primary instance isn't servicing the handoff), an infinite wait left this secondary instance
        // hung forever as a window-less zombie that never exited -- which presents to the user as
        // "Morphic didn't shut down." On timeout we give up on the redirect and let Main exit anyway
        // (Environment.Exit), so a stalled handoff costs at most a lost re-launch surface, never a
        // lingering process. The redirect thread-pool task is a background thread, abandoned on exit.
        const uint CWMO_DEFAULT = 0;
        const uint REDIRECT_WAIT_TIMEOUT_MILLISECONDS = 5000;
        var waitHandles = new Windows.Win32.Foundation.HANDLE[]
        {
            new Windows.Win32.Foundation.HANDLE(redirectCompletedEvent.SafeWaitHandle.DangerousGetHandle()),
        };
        _ = Windows.Win32.PInvoke.CoWaitForMultipleObjects(CWMO_DEFAULT, REDIRECT_WAIT_TIMEOUT_MILLISECONDS, waitHandles, out _);
    }
}
#endif