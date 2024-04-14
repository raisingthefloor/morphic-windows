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

using System.Threading.Tasks;
using System.Windows;

namespace Morphic;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private Morphic.Controls.HybridTrayIcon HybridTrayIcon;

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

    public IWindowsAppSdkStatus WindowsAppSdkStatus { get; private set; } = new IWindowsAppSdkStatus.Uninitialized();

    private App()
    {
        // load our application's icon
        var morphicIconStream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Morphic.Assets.Icons.morphic.ico")!;
        System.Drawing.Icon morphicIcon = new(morphicIconStream);


        // create an instance of our tray icon (button)
        HybridTrayIcon = new()
        {
            Icon = morphicIcon,
            Text = "Morphic",
            TrayIconLocation = Controls.HybridTrayIcon.TrayIconLocationOption.NextToNotificationTray,
            Visible = false,
        };
    }

     #region Lifecycle

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        var isRunningAsPackagedAppResult = Morphic.WindowsNative.Packaging.Package.IsRunningAsPackagedApp();
        if (isRunningAsPackagedAppResult.IsError == true)
        {
            this.WindowsAppSdkStatus = new IWindowsAppSdkStatus.Disabled(new IWindowsAppSdkDisabledReason.CouldNotDetectIfRunningAsPackagedApp());
        }
        else
        {
            bool isRunningAsPackagedApp = isRunningAsPackagedAppResult.Value!;

            if (isRunningAsPackagedApp == true)
            {
                // change the Windows App SDK status to "initialized" (since it would have been automatically initialized by the app packaging startup)
                this.WindowsAppSdkStatus = new IWindowsAppSdkStatus.Initialized(manuallyInitialized: false);
            }
            else // if (isRunningAsPackagedApp == false)
            {
                // if our app is not running as a packaged app, then manually start up the Windows App SDK functionality using the appropriate bootstrapper
                //
                // initialize the Windows App SDK using the manual bootstrapper API
                // NOTE: the majorMinor version specified here is v1.5 (major 0x0001, minor 0x0005)
                int tryInitializeHResult;
                Microsoft.Windows.ApplicationModel.DynamicDependency.Bootstrap.TryInitialize(0x0001_0005, out tryInitializeHResult);
                if (tryInitializeHResult == 0)
                {
                    this.WindowsAppSdkStatus = new IWindowsAppSdkStatus.Initialized(manuallyInitialized: true);
                }
                else
                {
                    this.WindowsAppSdkStatus = new IWindowsAppSdkStatus.Disabled(new IWindowsAppSdkDisabledReason.InitializationFailed(hresult: tryInitializeHResult));
                }
            }
        }

        // show our tray icon (button)
        HybridTrayIcon.Visible = true;
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        // immediately hide our tray icon (and dispose of it for good measure, to help ensure that unmanaged resources are cleaned up)
        HybridTrayIcon.Visible = false;
        HybridTrayIcon.Dispose();

        if (this.WindowsAppSdkStatus is IWindowsAppSdkStatus.Initialized(var manuallyInitialized))
        {
            if (manuallyInitialized == true)
            {
                // release the Dynamic Dependency Lifetime Manager (DDLM) and clean up the Windows App SDK
                // see: https://learn.microsoft.com/en-us/windows/apps/api-reference/cs-bootstrapper-apis/microsoft.windows.applicationmodel.dynamicdependency/microsoft.windows.applicationmodel.dynamicdependency.bootstrap
                try
                {
                    // NOTE: Microsoft does not document any exceptions for this function; out of an abundance of caution, we are catching exceptions anyway
                    Microsoft.Windows.ApplicationModel.DynamicDependency.Bootstrap.Shutdown();
                    this.WindowsAppSdkStatus = new IWindowsAppSdkStatus.Uninitialized();
                }
                catch
                {
                    this.WindowsAppSdkStatus = new IWindowsAppSdkStatus.Disabled(new IWindowsAppSdkDisabledReason.UninitializationFailed());

                }
            }
        }
    }

     #endregion Lifecycle

     //

     #region Taskbar Tray Icon


     #endregion Taskbar Tray Icon

}
