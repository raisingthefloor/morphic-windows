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
}
#endif