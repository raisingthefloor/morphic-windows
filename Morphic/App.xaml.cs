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

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Morphic.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

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

    private Morphic.AboutWindow.AboutWindow? _aboutWindow;

    private Morphic.MorphicBar.MorphicBarWindow _morphicBarWindow = null!;
    internal static Morphic.MorphicBar.MorphicMainMenu MainMenu { get; private set; } = null!;

    // we also create a single transparent window which can be used (to show popups and messageboxes, etc.); this is necessary for when no other window UI is visible, but for simplicity it'll be shared project-wide
    private Morphic.MorphicBar.TransparentWindow.TransparentWindow _menuOwnerWindow = null!;
    internal static Window MenuOwnerWindow => ((App)Application.Current)._menuOwnerWindow;

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

    private void BindHighContrastSysColorBrushes()
    {
        var highContrastResources = (Microsoft.UI.Xaml.ResourceDictionary)this.Resources.ThemeDictionaries["HighContrast"];
        Morphic.Theme.MorphicSysColorBrushBinder.Bind((Microsoft.UI.Xaml.Media.SolidColorBrush)highContrastResources["ThemeAwareBackground"], Morphic.Theme.MorphicSysColor.Window);
        Morphic.Theme.MorphicSysColorBrushBinder.Bind((Microsoft.UI.Xaml.Media.SolidColorBrush)highContrastResources["ThemeAwareBorder"], Morphic.Theme.MorphicSysColor.WindowText);
        Morphic.Theme.MorphicSysColorBrushBinder.Bind((Microsoft.UI.Xaml.Media.SolidColorBrush)highContrastResources["ThemeAwareMorphicBarMorphieTextForeground"], Morphic.Theme.MorphicSysColor.WindowText);

        // MorphicBarControlDisabledForeground is defined at the root (not in ThemeDictionaries) because
        // it's used in BOTH HC and non-HC modes (every disabled button shows gray text regardless
        // of theme). The binder makes it track GetSysColor(COLOR_GRAYTEXT), which gives the active
        // HC theme's GRAYTEXT under HC and Windows' standard disabled-text gray otherwise.
        Morphic.Theme.MorphicSysColorBrushBinder.Bind(
            (Microsoft.UI.Xaml.Media.SolidColorBrush)this.Resources["MorphicBarControlDisabledForeground"],
            Morphic.Theme.MorphicSysColor.GrayText);
    }

    //

    #region Lifecycle

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        // Wire up the HC ThemeAwareBackground brush placeholder declared in App.xaml. Done here
        // (rather than in the App constructor) because Resources.ThemeDictionaries is a WinRT
        // projection that isn't safely accessible until the framework finishes booting --
        // accessing it from the constructor throws COMException. OnLaunched runs after framework
        // init and before any windows are constructed, so this seeds the brush before first paint.
        this.BindHighContrastSysColorBrushes();

        // initialize our taskbar icon (button); it will start out in a hidden state
        this.InitTaskbarIconWithoutShowing();

        // create a single instance of the main menu
        this.InitMainMenu();

        // create a single instance of a transparent window (with pointer-click passthrough and _no_ ability to receive keyboard focus)
        _menuOwnerWindow = new();
        _menuOwnerWindow.DisableAcceptsFocus();
        _menuOwnerWindow.EnablePointerEventsPassthrough();
        //
        // remove window chrome (minimize/maximize/close buttons); set the window to be 'always on top'; turn off the border and titlebar
        (_menuOwnerWindow.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter)?.IsAlwaysOnTop = true;
        _menuOwnerWindow.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(-10000, -10000, 0, 0)); // move off the main screen (unnecessary, but good for VS debugging so we don't get GUI debug overlays), make it zero pixels in size (also unnecessary, but a safeguard)
        _menuOwnerWindow.AppWindow.Show();

        _morphicBarWindow = new();
_morphicBarWindow.Resize(733, 67); // 1100x100 pixels (at 150% zoom), the size of the legacy Morphic 1.0 MorphicBar
        _morphicBarWindow.Orientation = Orientation.Horizontal;
        _morphicBarWindow.InitializeBarItems(App.CreateBasicBarItemsData());

        // Keep the tray-button tooltip in sync with the MorphicBar's visibility ("Show
        // MorphicBar" when hidden, "Hide MorphicBar" when visible) the same way the
        // popup menu's item labels do. AppWindow.Changed fires for several reasons
        // (position, size, visibility, etc.); we filter on DidVisibilityChange so we
        // only refresh on the relevant transitions, no matter which code path (menu
        // item, tray click, etc.) triggered the show/hide. RefreshTaskbarButtonTooltip
        // is called once here so the initial caption matches the bar's current state
        // before any user interaction.
        _morphicBarWindow.AppWindow.Changed += this.OnMorphicBarWindowAppWindowChanged;
        this.RefreshTaskbarButtonTooltip();
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

    // builds the basic set of MorphicBar items (as data, not controls); 
	// MorphicBarWindow.InitializeBarItems(...) is responsible for the actual controls.
    private static List<Morphic.MorphicBar.BarControls.IBarItemData> CreateBasicBarItemsData()
    {
        var items = new List<Morphic.MorphicBar.BarControls.IBarItemData>
        {
            // "Magnifier" -- two pushbuttons (Show on the left, Hide on the right), equal width
            Morphic.MorphicBar.BarItemDataFactory.CreateMagnifierButtonGroup(showAction: Morphic.MorphicBar.BarItemHandlers.ShowMagnifierButtonAction, hideAction: Morphic.MorphicBar.BarItemHandlers.HideMagnifierButtonAction),

            // "Contrast & Color" -- 4 toggle buttons, per-content sized
            Morphic.MorphicBar.BarItemDataFactory.CreateContrastColorButtonGroup(
                contrastAction: Morphic.MorphicBar.BarItemHandlers.ContrastButtonAction,
                colorAction: Morphic.MorphicBar.BarItemHandlers.ColorButtonAction,
                darkAction: Morphic.MorphicBar.BarItemHandlers.DarkButtonAction,
                nightAction: Morphic.MorphicBar.BarItemHandlers.NightButtonAction),
        };

        return items;
    }

    private void App_ShutdownStarting(DispatcherQueue sender, DispatcherQueueShutdownStartingEventArgs args)
    {
        // immediately hide our tray icon (and dispose of it for good measure, to help ensure that unmanaged resources are cleaned up)
        this.TaskbarButton.SetVisible(false);
        this.TaskbarButton.Dispose();
    }

    #endregion Lifecycle


    #region Main Menu

    private void InitMainMenu()
    {
        var mainMenu = new Morphic.MorphicBar.MorphicMainMenu();

        mainMenu.ShowMorphicBarMenuItemClicked += MainMenu_ShowMorphicBarMenuItemClicked;
        mainMenu.HideMorphicBarMenuItemClicked += MainMenu_HideMorphicBarMenuItemClicked;
        //
        mainMenu.AboutMorphicMenuItemClicked += MainMenu_AboutMorphicMenuItemClicked;
        mainMenu.QuitMorphicMenuItemClicked += MainMenu_QuitMorphicMenuItemClicked;

        App.MainMenu = mainMenu;
    }

    private void MainMenu_HideMorphicBarMenuItemClicked(object? sender, EventArgs e)
    {
        _morphicBarWindow.AppWindow.Hide();
    }

    private void MainMenu_ShowMorphicBarMenuItemClicked(object? sender, EventArgs e)
    {
        _morphicBarWindow.AppWindow.Show();
    }

    private void MainMenu_AboutMorphicMenuItemClicked(object? sender, EventArgs e)
    {
        if (_aboutWindow is null)
        {
            _aboutWindow = new();
            _aboutWindow.Closed += (s, args) => { _aboutWindow = null; };
        }
        _aboutWindow!.Activate();
    }

    private void MainMenu_QuitMorphicMenuItemClicked(object? sender, EventArgs e)
    {
        this.Shutdown();
    }

    internal void Shutdown()
    {
        // NOTE: we should close all explicit windows in this function (required to allow the actual application Exit)
		//       [in contrast, accessory windows like the taskbar button are torn down automatically when the app exits]

        _aboutWindow?.Close();

        _menuOwnerWindow.Close();

        // Unsubscribe the tooltip-refresh handler before closing the MorphicBar window.
        // Otherwise the bar's Close fires AppWindow.Changed with DidVisibilityChange,
        // RefreshTaskbarButtonTooltip then tries to write TaskbarButton.Text against a
        // tray button whose native window is being torn down, and we get a COMException
        // ("WinUI Desktop Window object has already been closed").
        if (_morphicBarWindow is not null)
        {
            _morphicBarWindow.AppWindow.Changed -= this.OnMorphicBarWindowAppWindowChanged;
            _morphicBarWindow.Close();
        }

        this.Exit();
    }

    #endregion Main Menu


    #region Taskbar Icon (Button)

    private void InitTaskbarIconWithoutShowing()
    {
        // create an instance of our tray icon (button)
        this.TaskbarButton = new Morphic.Controls.TrayButton.TrayButton()
        {
            Text = "Show or hide MorphicBar", // default tooltip until we know the state of the MorphicBar's visibility
        };
        this.TaskbarButton.MouseUp += TaskbarButton_MouseUp;

        // Seed the icon from current HC state, then subscribe so any future HC transition swaps
        // the icon. Captured DispatcherQueue marshals the refresh onto the UI thread; the event
        // fires from CachedDarkModeState's worker thread.
        this.RefreshTaskbarIcon();
    }

    // Selects the right tray-icon variant for the current HC state and applies it.
    //   Non-HC                                          -> morphic-standardcontrast.ico  (full-color)
    //   HC + dark theme (HC Black, Aquatic, Dusk, etc.) -> morphic-highcontrastblack.ico (for HC Black-family themes; icon's pixels are light so it's visible on a dark taskbar)
    //   HC + light theme (HC White, Desert)             -> morphic-highcontrastwhite.ico (for HC White-family themes; icon's pixels are dark so it's visible on a light taskbar)
    // NOTE: the icon file names describe the TARGET HC theme family (the v1.x convention), not the
    // color of the pixels inside the file -- "highcontrastblack" is the icon FOR the HC Black-style
    // theme (and thus contains light pixels), and "highcontrastwhite" is the icon FOR the HC
    // White-style theme (and thus contains dark pixels).
    private void RefreshTaskbarIcon()
    {
        string iconFileName;
        if (Morphic.SettingsUtils.CachedDarkModeState.GetCurrentIsHighContrast())
        {
            iconFileName = Morphic.SettingsUtils.CachedDarkModeState.GetCurrentIsDark()
                ? "morphic-highcontrastblack.ico"
                : "morphic-highcontrastwhite.ico";
        }
        else
        {
            iconFileName = "morphic-standardcontrast.ico";
        }

        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", iconFileName);
        _ = this.TaskbarButton.SetIconFromFile(iconPath, 256, 256);
    }

    // Filters AppWindow.Changed for the one signal we care about: visibility transitions.
    // AppWindow.Changed fires for several reasons (position, size, etc.); only act when
    // DidVisibilityChange is true so we don't redundantly rewrite the tooltip on every
    // window move or resize.
    private void OnMorphicBarWindowAppWindowChanged(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowChangedEventArgs args)
    {
        if (args.DidVisibilityChange)
        {
            this.RefreshTaskbarButtonTooltip();
        }
    }

    // Updates the tray-button tooltip caption based on the MorphicBar's current visibility.
    // Mirrors the menu's Show/Hide MorphicBar item-label convention so the affordance is
    // self-describing wherever the user encounters it.
    private void RefreshTaskbarButtonTooltip()
    {
        if (this.TaskbarButton is null || _morphicBarWindow is null)
        {
            return;
        }
        try
        {
            this.TaskbarButton.Text = _morphicBarWindow.Visible ? "Hide MorphicBar" : "Show MorphicBar";
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // The bar window is being torn down: AppWindow.Changed fired the visibility
            // transition mid-close, but accessing _morphicBarWindow.Visible (or writing
            // TaskbarButton.Text against a tray button whose native window is also closing)
            // throws "WinUI Desktop Window object has already been closed". The tooltip
            // doesn't need to update since the app is going away; swallow.
        }
    }

    private void TaskbarButton_MouseUp(object? sender, Controls.MouseEventArgs e)
    {
        _morphicBarWindow.DispatcherQueue.TryEnqueue(() =>
        {
            switch (e.Button) 
            {
                case Controls.MouseButtons.Left:
                    switch (_morphicBarWindow.Visible)
                    {
                        case true:
                            _morphicBarWindow.AppWindow.Hide();
                            break;
                        case false:
                            _morphicBarWindow.AppWindow.Show();
                            break;
                    }
                    break;
                case Controls.MouseButtons.Right:
                    {
                        System.Drawing.Point popupPosition;

                        // choose an owner window; this will be the visible window which is used for rasterization scaling (and is required to be visible to show the menu)
                        Window ownerWindow = _menuOwnerWindow;
                        var rasterizationScale = ownerWindow.Content.XamlRoot.RasterizationScale;

                        var getPopupPositionResult = App.GetTaskbarAdjacentPopupPosition(rasterizationScale);
                        if (getPopupPositionResult.IsSuccess)
                        {
                            popupPosition = getPopupPositionResult!.Value;
                        }
                        else
                        {
                            var getCursorPosResult = Windows.Win32.PInvoke.GetCursorPos(out var cursorPosition);
                            if (getCursorPosResult == false)
                            {
                                Debug.Assert(false);
                                return;
                            }
                            popupPosition = cursorPosition;
                        }

                        // now pop up the main menu
                        App.MainMenu.Show(ownerWindow, _morphicBarWindow.Visible, popupPosition.X, popupPosition.Y);
                    }
                    break;
            }
        });
    }

    private static MorphicResult<System.Drawing.Point, MorphicUnit> GetTaskbarAdjacentPopupPosition(double rasterizationScale)
    {
        // get the RECT of the Windows taskbar
        var taskbar = Windows.Win32.PInvoke.FindWindow("Shell_TrayWnd", null);
        if (taskbar.IsNull)
        {
            Debug.Assert(false);
            return MorphicResult.ErrorResult();
        }
        var getWindowRectResult = Windows.Win32.PInvoke.GetWindowRect(taskbar, out var taskbarRect);
        if (getWindowRectResult == false)
        {
            Debug.Assert(false);
            return MorphicResult.ErrorResult();
        }

        // TASKBAR_PADDING_GAP is the amount of breathing room between the taskbar edge and the popup 
		// menu. This has been sized to closely match the taskbar-button tooltip's gap (see 
		// TrayButtonNativeWindow.ShowTooltipForCurrentHover), so that the menu and tooltip read as 
		// floating roughly the same distance from the taskbar.
        const int TASKBAR_PADDING_GAP = 6;
        int scaledTaskbarPaddingGap = (int)(TASKBAR_PADDING_GAP * rasterizationScale);

        // get the monitor handle associated with the taskbar (to determine its docking edge)
        var hMonitor = Windows.Win32.PInvoke.MonitorFromWindow(taskbar, Windows.Win32.Graphics.Gdi.MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONULL);
        if (hMonitor.IsNull)
        {
            Debug.Assert(false);
            return MorphicResult.ErrorResult();
        }
        var monitorInfo = new Windows.Win32.Graphics.Gdi.MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Windows.Win32.Graphics.Gdi.MONITORINFO>() };
        //
        // get the full rectangle fo the monitor associated with the taskbar
        var getMonitorInfoResult = Windows.Win32.PInvoke.GetMonitorInfo(hMonitor, ref monitorInfo);
        if (getMonitorInfoResult == false)
        {
            Debug.Assert(false);
            return MorphicResult.ErrorResult();
        }
        var monitorFullRect = monitorInfo.rcMonitor;

        // capture the current mouse position (as we'll want to use the current X or Y position)
        var getCursorPosResult = Windows.Win32.PInvoke.GetCursorPos(out var cursorPosition);
        if (getCursorPosResult == false)
        {
            Debug.Assert(false);
            return MorphicResult.ErrorResult();
        }

        // now pick a pop-up position which combines the X or Y of the cursor position with a padded offset from the taskbar
        int universalAbsoluteX;
        int universalAbsoluteY;
        //
        if (taskbarRect.Width > taskbarRect.Height)
        {
            // Horizontal taskbar (top or bottom) -- X follows the pointer position
            universalAbsoluteX = cursorPosition.X;

            if (taskbarRect.top == monitorFullRect.top)
            {
                // Docked at top -- show below the taskbar
                universalAbsoluteY = taskbarRect.bottom + scaledTaskbarPaddingGap;
            }
            else
            {
                // Docked at bottom -- show above the taskbar
                universalAbsoluteY = taskbarRect.top - scaledTaskbarPaddingGap;
            }
        }
        else
        {
            // Vertical taskbar (left or right) -- Y follows the pointer position
            universalAbsoluteY = cursorPosition.Y;

            if (taskbarRect.left == monitorFullRect.left)
            {
                // Docked at left -- show to the right of the taskbar
                universalAbsoluteX = taskbarRect.right + scaledTaskbarPaddingGap;
            }
            else
            {
                // Docked at right -- show to the left of the taskbar
                universalAbsoluteX = taskbarRect.left - scaledTaskbarPaddingGap;
            }
        }

        System.Drawing.Point result = new(universalAbsoluteX, universalAbsoluteY);
        return MorphicResult.OkResult(result);
    }

    #endregion Taskbar Icon (Button)

}
