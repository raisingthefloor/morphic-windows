using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
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
    // NOTE: we initialize this in the Application_Startup, so we set it to null -- but we initialize it during application startup so it should always be available
    internal Morphic.Controls.HybridTrayIcon HybridTrayIcon = null!;

    private Morphic.MorphicBar.MorphicBarWindow? MorphicBarWindow = null;

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        this.InitializeComponent();

        DispatcherQueue.GetForCurrentThread().ShutdownStarting += App_ShutdownStarting;
    }

    #region Lifecycle

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        // initialize our taskbar icon (button); this will not show the button
        this.InitTaskbarIconWithoutShowing();

        // create our MorphicBar window instance
        this.MorphicBarWindow = new MorphicBar.MorphicBarWindow();

        // show our taskbar icon (button)
        HybridTrayIcon.Visible = true;
    }

    private void App_ShutdownStarting(DispatcherQueue sender, DispatcherQueueShutdownStartingEventArgs args)
    {
        // immediately hide our tray icon (and dispose of it for good measure, to help ensure that unmanaged resources are cleaned up)
        this.HybridTrayIcon.Visible = false;
        this.HybridTrayIcon.Dispose();
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
            Visible = false, // NOTE: default state; setting this is not necessary, but is done for clarity
        };
        this.HybridTrayIcon = hybridTrayIcon;

        // wire up click and right-click events for our hybrid tray icon
        this.HybridTrayIcon.Click += HybridTrayIcon_Click;
    }

    //

    // NOTE: this event is called on a non-UI thread
    private void HybridTrayIcon_Click(object? sender, System.EventArgs e)
    {
        _ = this.MorphicBarWindow!.DispatcherQueue.TryEnqueue(() =>
        {
            if (this.MorphicBarWindow!.Visible == true)
            {
                this.MorphicBarWindow!.AppWindow.Hide();
            }
            else
            {
                this.MorphicBarWindow!.AppWindow.Show();
            }
        });
    }

    // OBSERVATION: this function could reasonably be moved to a shared helper class
    private static System.Windows.Rect? ConvertRectToDisplayScaledRectOrNull(System.Windows.Rect? rect)
    {
        System.Windows.Rect? scaledBoundingRectangle = null;
        if (rect is not null)
        {
            var getDisplayForPointResult = Morphic.WindowsNative.Display.Display.GetDisplayForPoint(new System.Drawing.Point((int)rect.Value!.X, (int)rect.Value!.Y));
            if (getDisplayForPointResult.IsSuccess == true)
            {
                var displayForPoint = getDisplayForPointResult.Value!;

                var getScalePercentageResult = displayForPoint.GetScalePercentage();
                if (getScalePercentageResult.IsSuccess == true)
                {
                    var scalePercentage = getScalePercentageResult.Value!;

                    scaledBoundingRectangle = new System.Windows.Rect(
                        rect.Value!.X / scalePercentage,
                        rect.Value!.Y / scalePercentage,
                        rect.Value!.Width / scalePercentage,
                        rect.Value!.Height / scalePercentage
                    );
                }
            }
        }

        return scaledBoundingRectangle;
    }

    #endregion Taskbar Icon (Button)

}
