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
    //
    // NOTE: as we cannot initialize this object in the App() constructor, we make it nullable -- but we initialize it during application startup so it should always be available
    private Morphic.MainMenu.MorphicMainMenu? MorphicMainMenu = null;

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
        // create a single instance of our main menu
        this.MorphicMainMenu = new Morphic.MainMenu.MorphicMainMenu();

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

    // NOTE: this event is called on a non-UI thread
    private void HybridTrayIcon_Click(object? sender, System.EventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            MessageBox.Show("tray button clicked");
        });
        //throw new System.NotImplementedException();
    }

    // NOTE: this event is called on a non-UI thread
    private void HybridTrayIcon_SecondaryClick(object? sender, System.EventArgs e)
    {
        // capture the position and size of the tray icon, if possible (so that we can align the menu to its corner)
        System.Windows.Rect? physicalBoundingRectangle = null;
        //
        var getPositionsAndSizesResult = this.HybridTrayIcon.GetPositionsAndSizes();
        if (getPositionsAndSizesResult.IsSuccess)
        {
            var positionsAndSizes = getPositionsAndSizesResult.Value!;
            if (positionsAndSizes.Count == 1)
            {
                var positionAndSize = positionsAndSizes[0];
                physicalBoundingRectangle = new System.Windows.Rect(positionAndSize.X, positionAndSize.Y, positionAndSize.Width, positionAndSize.Height);
            }
            else
            {
                Debug.Assert(false, "Could not get positions and sizes of tray icon(s); this is to be expected if we cannot capture the rectangle (which may be the case if we're putting the icon in the system tray itself)");
            }
        }

        // NOTE: if we cannot calculate the scaled bounding rectangle for the Morphic tray button, scaledBoundingRectangle will remain null.
        Rect? scaledBoundingRectangle = null;
        if (physicalBoundingRectangle is not null)
        {
            var getDisplayForPointResult = Morphic.WindowsNative.Display.Display.GetDisplayForPoint(new System.Drawing.Point((int)physicalBoundingRectangle.Value!.X, (int)physicalBoundingRectangle.Value!.Y));
            if (getDisplayForPointResult.IsSuccess == true)
            {
                var displayForPoint = getDisplayForPointResult.Value!;

                var getScalePercentageResult = displayForPoint.GetScalePercentage();
                if (getScalePercentageResult.IsSuccess == true)
                {
                    var scalePercentage = getScalePercentageResult.Value!;

                    scaledBoundingRectangle = new Rect(
                        physicalBoundingRectangle.Value!.X / scalePercentage,
                        physicalBoundingRectangle.Value!.Y / scalePercentage,
                        physicalBoundingRectangle.Value!.Width / scalePercentage,
                        physicalBoundingRectangle.Value!.Height / scalePercentage
                    );
                }
            }
        }

        Application.Current.Dispatcher.Invoke(() =>
        {
            if (scaledBoundingRectangle is not null)
            {
                // if we can find the virtual (scaled) rectangle of our tray button, pop up above it instead
                //
                // NOTE: the passed-in rect must be divided by the current screen scaling before being passed into this function (as WPF will not recognize the absolute position correctly otherwise)
                this.MorphicMainMenu!.Show(new Morphic.MainMenu.MorphicMainMenu.IShowPlacement.ScaledAbsolutePosition(scaledBoundingRectangle.Value!));
            }
            else
            {
                // otherwise, show the pop-up menu at the current mouse cursor position
                this.MorphicMainMenu!.Show(new Morphic.MainMenu.MorphicMainMenu.IShowPlacement.MouseCursor());
            }
        });
    }

     #endregion Taskbar Tray Icon

}
