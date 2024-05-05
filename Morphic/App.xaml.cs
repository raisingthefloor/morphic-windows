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

using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Media;

namespace Morphic;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    // NOTE: we initialize this in the Application_Startup, so we set it to null -- but we initialize it during application startup so it should always be available
    internal Morphic.Controls.HybridTrayIcon HybridTrayIcon = null!;
    //
    // NOTE: we initialize this in the Application_Startup, so we set it to null -- but we initialize it during application startup so it should always be available
    private Morphic.Theme.ThemeManager ThemeManager = null!;
    //
    // NOTE: as we cannot initialize this object in the App() constructor, we set it to null -- but we initialize it during application startup so it should always be available
    private Morphic.MainMenu.MorphicMainMenu MorphicMainMenu = null!;

    private Morphic.MorphicBar.MorphicBarWindow? MorphicBarWindow = null;


    #region Lifecycle

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // before initializing any user interface, initialize our localization culture
        var currentUICulture = System.Threading.Thread.CurrentThread.CurrentUICulture;
        var iso639LanguageCode = Morphic.Localization.LocalizationManager.GetIso639LanguageCode(currentUICulture);
        //
        // NOTE: if the current culture is not supported (or if it's the same as the base culture), fail silently and use the base settings
        _ = Morphic.Localization.LocalizationManager.SetUICulture(App.Current.Resources, iso639LanguageCode);

        // create a single instance of our main menu
        this.MorphicMainMenu = new();
        //
        // NOTE: if the current culture is not supported (or if it's the same as the base culture), fail silently and use the base settings
        _ = Morphic.Localization.LocalizationManager.SetUICulture(this.MorphicMainMenu.Resources, iso639LanguageCode);

        // initialize our taskbar icon (button); this will not show the button
        this.InitTaskbarIconWithoutShowing();

        // initialize our theme manager; this will also set the initial theme for our application
        this.ThemeManager = new();

        /*** start up the Windows App SDK (if it's not already running) ***/
        // NOTE: we do not await the function call here, and we also do not listen for its result; if we want to know if the SDK is initialized, we can retrieve the WindowsAppSdkManager.WindowsAppSdkStatus value (or create and wire up an event)
        _ = Morphic.WindowsAppSdk.WindowsAppSdkManager.InitializeAsync();

        //

        // create our MorphicBar window instance
        this.MorphicBarWindow = new MorphicBar.MorphicBarWindow()
        {
            Visibility = Visibility.Hidden,
        };

        // show our taskbar icon (button)
        HybridTrayIcon.Visible = true;
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        // immediately hide our tray icon (and dispose of it for good measure, to help ensure that unmanaged resources are cleaned up)
        this.HybridTrayIcon.Visible = false;
        this.HybridTrayIcon.Dispose();

        // dispose of our theme manager; this will also disconnect its high contrast on/off change event; see notes in HighContrast class (and Win32 API notes), as this is recommended/required during application shutdown to avoid a resource leak
        this.ThemeManager.Dispose();

        // shutdown the Windows App SDK (NOTE: this is only applicable to unpackaged apps; for packaged apps or if the SDK is disabled then this will just turn OkResult)
        var windowsAppSdkShutdownResult = Morphic.WindowsAppSdk.WindowsAppSdkManager.Shutdown();
        Debug.Assert(windowsAppSdkShutdownResult.IsSuccess == true, "Error: failure while attempting to shut down Windows App SDK (during application exit)");
    }

    #endregion Lifecycle


    #region Taskbar Icon (Button)

    private void InitTaskbarIconWithoutShowing()
    {
        // get the default Morphic icon
        var morphicIconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Morphic.Assets.Icons.morphic.ico")!;
        System.Drawing.Icon morphicIcon = new(morphicIconStream);

        // create an instance of our tray icon (button)
        var hybridTrayIcon = new Morphic.Controls.HybridTrayIcon()
        {
            Icon = morphicIcon,
            Text = "Morphic",
            TrayIconLocation = Controls.HybridTrayIcon.TrayIconLocationOption.NextToNotificationTray,
            Visible = false,
        };
        this.HybridTrayIcon = hybridTrayIcon;

        // wire up click and right-click events for our hybrid tray icon
        this.HybridTrayIcon.Click += HybridTrayIcon_Click;
        this.HybridTrayIcon.SecondaryClick += HybridTrayIcon_SecondaryClick;
    }

    //

    // NOTE: this event is called on a non-UI thread
    private void HybridTrayIcon_Click(object? sender, System.EventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            switch (this.MorphicBarWindow!.Visibility)
            {
                case Visibility.Visible:
                    this.MorphicBarWindow!.Visibility = Visibility.Hidden;
                    break;
                case Visibility.Hidden:
                    this.MorphicBarWindow!.Visibility = Visibility.Visible;
                    break;
                case Visibility.Collapsed:
                    this.MorphicBarWindow!.Visibility = Visibility.Visible;
                    Debug.Assert(false, "MorphicBar should never be in the collapsed state.");
                    break;
                default:
                    throw new Exception("Invalid code path");
            }
        });
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

    #endregion Taskbar Icon (Button)

}
