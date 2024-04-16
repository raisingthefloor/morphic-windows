// Copyright 2020-2024 Raising the Floor - US, Inc.
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

using Morphic.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Morphic.WindowsAppSdk;

public interface IWindowsAppSdkStatus
{
    public record Disabled(IWindowsAppSdkDisabledReason reason) : IWindowsAppSdkStatus;
    public record Initialized(bool manuallyInitialized) : IWindowsAppSdkStatus;
    public record Uninitialized : IWindowsAppSdkStatus;
}
//
public interface IWindowsAppSdkDisabledReason
{
    public record CouldNotDetectIfRunningAsPackagedApp : IWindowsAppSdkDisabledReason;
    public record InitializationFailed(int hresult) : IWindowsAppSdkDisabledReason;
    public record UninitializationFailed() : IWindowsAppSdkDisabledReason;
}

internal static class WindowsAppSdkManager
{
    public static IWindowsAppSdkStatus WindowsAppSdkStatus { get; private set; } = new IWindowsAppSdkStatus.Uninitialized();

    // NOTE: this function returns a task so that the caller can optionally wait on the result
    // NOTE: this function will just return success [and set the status to 'not manually initialized'] if this app is a packaged app (i.e. it is safe to call from both packaged and unpackaged apps)
    public static Task<MorphicResult<MorphicUnit, MorphicUnit>> InitializeAsync()
    {
        /*** start up the Windows App SDK (if it's not already running) ***/
        var isRunningAsPackagedAppResult = Morphic.WindowsNative.Packaging.Package.IsRunningAsPackagedApp();
        if (isRunningAsPackagedAppResult.IsError == true)
        {
            WindowsAppSdkManager.WindowsAppSdkStatus = new IWindowsAppSdkStatus.Disabled(new IWindowsAppSdkDisabledReason.CouldNotDetectIfRunningAsPackagedApp());
            return Task.FromResult<MorphicResult<MorphicUnit, MorphicUnit>>(MorphicResult.ErrorResult());
        }
        else
        {
            bool isRunningAsPackagedApp = isRunningAsPackagedAppResult.Value!;

            if (isRunningAsPackagedApp == true)
            {
                // change the Windows App SDK status to "initialized" (since it would have been automatically initialized by the app packaging startup)
                WindowsAppSdkManager.WindowsAppSdkStatus = new IWindowsAppSdkStatus.Initialized(manuallyInitialized: false);
                return Task.FromResult<MorphicResult<MorphicUnit, MorphicUnit>>(MorphicResult.OkResult());
            }
            else // if (isRunningAsPackagedApp == false)
            {
                // NOTE: we start up the Windows App SDK asynchronously so that Morphic can start up right away, even if it takes a few seconds to start up the SDK
                // NOTE: we have also designed Morphic to gracefully degrade if the App SDK cannot be loaded; no critical Morphic behavior relies on the Windows App SDK
                var awaitableTask = Task.Run(MorphicResult<MorphicUnit, MorphicUnit> () =>
                {
                    // if our app is not running as a packaged app, then manually start up the Windows App SDK functionality using the appropriate bootstrapper
                    //
                    // initialize the Windows App SDK using the manual bootstrapper API
                    // NOTE: the majorMinor version specified here is v1.5 (major 0x0001, minor 0x0005)
                    int tryInitializeHResult;
                    Microsoft.Windows.ApplicationModel.DynamicDependency.Bootstrap.TryInitialize(0x0001_0005, out tryInitializeHResult);
                    if (tryInitializeHResult == 0)
                    {
                        WindowsAppSdkManager.WindowsAppSdkStatus = new IWindowsAppSdkStatus.Initialized(manuallyInitialized: true);
                        return MorphicResult.OkResult();
                    }
                    else
                    {
                        WindowsAppSdkManager.WindowsAppSdkStatus = new IWindowsAppSdkStatus.Disabled(new IWindowsAppSdkDisabledReason.InitializationFailed(hresult: tryInitializeHResult));
                        return MorphicResult.ErrorResult();
                    }
                });

                return awaitableTask;
            }
        }
    }

    // NOTE: this function will just return success [and set the status to 'not manually initialized'] if this app is a packaged app (i.e. it is safe to call from both packaged and unpackaged apps)
    public static MorphicResult<MorphicUnit, MorphicUnit> Shutdown()
    {
        if (WindowsAppSdkManager.WindowsAppSdkStatus is IWindowsAppSdkStatus.Initialized(var manuallyInitialized))
        {
            if (manuallyInitialized == true)
            {
                // release the Dynamic Dependency Lifetime Manager (DDLM) and clean up the Windows App SDK
                // see: https://learn.microsoft.com/en-us/windows/apps/api-reference/cs-bootstrapper-apis/microsoft.windows.applicationmodel.dynamicdependency/microsoft.windows.applicationmodel.dynamicdependency.bootstrap
                try
                {
                    // NOTE: Microsoft does not document any exceptions for this function; out of an abundance of caution, we are catching exceptions anyway
                    Microsoft.Windows.ApplicationModel.DynamicDependency.Bootstrap.Shutdown();
                    WindowsAppSdkManager.WindowsAppSdkStatus = new IWindowsAppSdkStatus.Uninitialized();
                    return MorphicResult.OkResult();
                }
                catch
                {
                    WindowsAppSdkManager.WindowsAppSdkStatus = new IWindowsAppSdkStatus.Disabled(new IWindowsAppSdkDisabledReason.UninitializationFailed());
                    return MorphicResult.ErrorResult();
                }
            }
        }

        // if the SDK was not initialized or if we're a managed app then just return success
        return MorphicResult.OkResult();
    }
}
