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

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Morphic;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    // NOTE: we initialize this when the application starts up
    internal Morphic.Controls.TrayButton.TrayButton TaskbarButton = null!;

    private Morphic.MorphicBar.MorphicBarWindow? _morphicBarWindow;

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        this.InitializeComponent();

		// capture shutdown events (to clean up the tray icon, etc.)
        DispatcherQueue.GetForCurrentThread().ShutdownStarting += App_ShutdownStarting;
    }

    #region Lifecycle

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        // initialize our taskbar icon (button); it will start out in a hidden state
        this.InitTaskbarIconWithoutShowing();

        _morphicBarWindow = new();
_morphicBarWindow.Resize(733, 67); // 1100x100 pixels (at 150% zoom), the size of the legacy Morphic 1.0 MorphicBar
        _morphicBarWindow.Orientation = Orientation.Horizontal;
        //
        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", "morphic-standardcontrast.ico");
        _morphicBarWindow.SetIconFromFile(iconPath, 256, 256);

        // show our taskbar icon (button)
        this.TaskbarButton.SetVisible(true);

        // position MorphicBar at the correct dock location
        //
        // get the current monitor, based on the current mouse cursor relative position
        _ = Windows.Win32.PInvoke.GetCursorPos(out var currentPointerPosition);
        var hMonitor = Windows.Win32.PInvoke.MonitorFromPoint(currentPointerPosition, Windows.Win32.Graphics.Gdi.MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        if (hMonitor.IsNull)
        {
            Debug.Assert(false, "No monitor handle; this might be a headless system; aborting.");
            return;
        }
        // NOTE: if this does not "snap" to the correct location immediately (due to enqueueing the UI code), consider creating a special path that sets the window position without the animation code
        _morphicBarWindow.AnimateMoveTo(hMonitor, _morphicBarWindow.Orientation, MorphicBar.DockingLocation.FloatingBottomRight, TimeSpan.Zero);

        _morphicBarWindow.Activate();
    }

    private void App_ShutdownStarting(DispatcherQueue sender, DispatcherQueueShutdownStartingEventArgs args)
    {
        // immediately hide our tray icon (and dispose of it for good measure, to help ensure that unmanaged resources are cleaned up)
        this.TaskbarButton.SetVisible(false);
        this.TaskbarButton.Dispose();
    }

    #endregion Lifecycle


    #region Taskbar Icon (Button)

    private void InitTaskbarIconWithoutShowing()
    {
        // create an instance of our tray icon (button)
        var taskbarButton = new Morphic.Controls.TrayButton.TrayButton()
        {
            Text = "Morphic",
        };

        // load the icon from the app's assets (copied content)
        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", "morphic-standardcontrast.ico");
        _ = taskbarButton.SetIconFromFile(iconPath, 256, 256);

        this.TaskbarButton = taskbarButton;
        this.TaskbarButton.MouseUp += TaskbarButton_MouseUp;
    }

    private void TaskbarButton_MouseUp(object? sender, Controls.MouseEventArgs e)
    {
        _morphicBarWindow!.DispatcherQueue.TryEnqueue(() =>
        {
            switch (e.Button) 
            {
                case Controls.MouseButtons.Left:
                    switch (_morphicBarWindow!.Visible)
                    {
                        case true:
                            _morphicBarWindow!.AppWindow.Hide();
                            break;
                        case false:
                            _morphicBarWindow!.AppWindow.Show();
                            break;
                    }
                    break;
                case Controls.MouseButtons.Right:
                    this.Exit();
                    break;
            }
        });
    }

    //


    #endregion Taskbar Icon (Button)

}
