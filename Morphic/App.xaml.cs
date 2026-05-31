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

    // Handler reference for the taskbar icon refresh; held so we can unsubscribe at shutdown.
    // See RefreshTaskbarIcon for why the taskbar icon needs to track HC state.
    private EventHandler<Morphic.SettingsUtils.CachedDarkModeStateChangedEventArgs>? _taskbarIconRefreshHandler;

    private Morphic.AboutWindow.AboutWindow? _aboutWindow;

    // The MorphicBar's per-bar manager. App holds the manager (not the bar directly) so that
    // bar-related coordination is owned by MorphicBarManager rather than spread across App.
    // Exposed publicly so callers (BarItemHandlers, BarItemDataFactory, etc.) can reach the
    // manager without App-side wrappers.
    private Morphic.MorphicBar.MorphicBarManager? _morphicBarManager;
    internal Morphic.MorphicBar.MorphicBarManager? MorphicBarManager => _morphicBarManager;

    // Persists + two-way-syncs the MorphicBar's visibility and docking location with the registry
    // (HKCU\Software\Raising the Floor\Morphic).
    private Morphic.AppRegistrySettings? _appRegistrySettings;

    // The Read Selected (read-aloud / TTS) controller. App owns it (mirroring MorphicBarManager)
    // because its lifetime is the whole app session and its embedded foreground-window tracker
    // must be constructed and disposed on the UI thread. We expose it via `internal` so that the 
	// bar's Play/Stop handlers can reach it.
    private Morphic.ReadAloud.ReadAloudController? _readAloudController;
    internal Morphic.ReadAloud.ReadAloudController? ReadAloudController => _readAloudController;

    internal static Morphic.MorphicBar.MorphicMainMenu MainMenu { get; private set; } = null!;
    //
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
        // Register Morphic with the Shell's app-notification subsystem. Must happen after
        // AumidHelper.Initialize (called from Program.Main) but before any toast is shown.
        // See Morphic.Notifications/ToastNotifications.cs for the registration details, the
        // env-based routing decision (in-process vs. shell-out to Morphic.NotificationHelper.exe
        // under uiAccess), and notes on the thread-pool threading of NotificationInvoked.
        Morphic.Notifications.ToastNotifications.Initialize();

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
        // This window hosts MorphicMainMenu's flyout for its lifetime. Alt+F4 (or any other user-
        // initiated close) with the flyout focused would otherwise destroy this window and break
        // every subsequent menu invocation. Disable user-close; OnShutdown calls
        // AllowCloseForShutdown() to bypass when we actually want to close it.
        _menuOwnerWindow.SetUserCloseEnabled(false);
        //
        // remove window chrome (minimize/maximize/close buttons); set the window to be 'always on top'; turn off the border and titlebar
        (_menuOwnerWindow.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter)?.IsAlwaysOnTop = true;
        _menuOwnerWindow.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(-10000, -10000, 0, 0)); // move off the main screen (unnecessary, but good for VS debugging so we don't get GUI debug overlays), make it zero pixels in size (also unnecessary, but a safeguard)
        _menuOwnerWindow.AppWindow.Show();

        // Construct the MorphicBarWindow and do all one-time setup before handing it to MorphicBarManager.
        // The local `morphicBarWindow` reference goes out of scope after the manager takes it; App keeps
        // only the manager reference (this.MorphicBarManager): all subsequent bar operations go through
        // manager wrappers (Show/Hide/Activate/IsVisible/etc.).
        var morphicBarWindow = Morphic.MorphicBar.MorphicBarWindow.CreateWithHiddenTaskbar();

        // Load the persisted bar settings (visibility + orientation + docking location) up front so
        // they can seed the bar's initial orientation (immediately below), initial dock position
        // (further below), and initial visibility (further below still). Two-way registry sync is
        // started later, once the bar is in this restored state.
        _appRegistrySettings = Morphic.AppRegistrySettings.Load();

        morphicBarWindow.Orientation = _appRegistrySettings.Orientation;
        morphicBarWindow.InitializeBarItems(App.CreateBasicBarItemsData());

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
        morphicBarWindow.AnimateMoveTo(hMonitor, _appRegistrySettings.Orientation, _appRegistrySettings.DockingLocation, TimeSpan.Zero);

        // Hand the configured bar to the manager. The manager subscribes to the bar's events.
        _morphicBarManager = new Morphic.MorphicBar.MorphicBarManager(morphicBarWindow);

        // Keep the tray-button tooltip in sync with the MorphicBar's visibility ("Show MorphicBar"
        // when hidden, "Hide MorphicBar" when visible). RefreshTaskbarButtonTooltip is also called
        // once here so the initial caption matches the bar's current state before any user
        // interaction.
        _morphicBarManager.BarVisibilityChanged += (_, _) => this.RefreshTaskbarButtonTooltip();
        this.RefreshTaskbarButtonTooltip();

        // Create the App-owned Read Selected controller (read-aloud / TTS). Constructed here on the
        // UI thread because it installs a system foreground-window hook that must live on the thread
        // with the running message pump. Disposed in PerformShutdownCleanup.
        _readAloudController = new Morphic.ReadAloud.ReadAloudController();

        // show our taskbar icon (button)
        this.TaskbarButton.SetVisible(true);

        // The Text Size +/- buttons reflect the DPI scale of the bar's current monitor. The
        // factory computed their initial state during CreateBasicBarItemsData -- which ran before
        // AnimateMoveTo, when the bar may have been on a different monitor with a different DPI
        // than the user's preferred dock corner. Refresh now that the bar is on its dock monitor
        // so the buttons reflect the right display from first frame.
        Morphic.MorphicBar.BarItemDataFactory.RefreshTextSizeButtonState();

        // Begin two-way registry sync BEFORE applying the initial visibility, so the show below flows
        // through the normal bar->registry path. The cache is seeded from Load(), so re-showing the
        // persisted-visible state is echo-suppressed (writes nothing), while a manual force-show of a
        // previously-hidden bar is persisted automatically. The bar's DispatcherQueue is the UI thread
        // the watcher marshals its ThreadPool callbacks onto.
        _appRegistrySettings.StartSync(_morphicBarManager, morphicBarWindow.DispatcherQueue);

        // Show the bar without activating it (monitor + dock corner were already applied above). WHICH
        // visibility we apply depends on how we were launched:
        //   * Autorun (the installer's Run key passes --run-after-login): respect the persisted state --
        //     show only if the bar was visible last session; if the user had hidden it, leave it hidden
        //     (the tray button's "Show MorphicBar" tooltip already reflects that).
        //   * Manual launch (Start Menu shortcut, double-clicked .exe, post-install launch): force-show
        //     the bar on the current monitor regardless of the persisted state; StartSync's handler then
        //     persists IsVisible=true so the next autorun restores it shown.
        // We never ACTIVATE at launch: activating would (a) be user-hostile by interrupting whatever the
        // user was doing in their previous foreground app, and (b) put the bar into a sticky Win32
        // "active" state from which the user's first Alt+Tab would fire no WM_ACTIVATE (OS sees it as
        // "already active"), breaking our initial-focus-ring logic. The bar is topmost anyway, so it's
        // still immediately visible.
        if (App.WasLaunchedByAutorun() == true)
        {
            if (_appRegistrySettings.IsBarVisible == true)
            {
                _morphicBarManager.ShowBar(activateWindow: false);
            }
        }
        else
        {
            _morphicBarManager.ShowBar(activateWindow: false);
        }
    }

    // The installer's HKLM Run key launches Morphic at logon with this flag; a manual launch (Start
    // Menu shortcut, double-clicked .exe, post-install launch) passes no flag. See the startup
    // visibility logic for how the two are treated differently.
    private const string RUN_AFTER_LOGIN_COMMAND_LINE_FLAG = "--run-after-login";

    // True when the OS started us at logon (the Run key passed --run-after-login); false for a manual
    // launch. Reads this process's own command line, so it reflects how THIS instance was started.
    private static bool WasLaunchedByAutorun()
    {
        var commandLineArguments = Environment.GetCommandLineArgs();
        // skip index 0 (the executable path)
        for (var index = 1; index < commandLineArguments.Length; index++)
        {
            if (string.Equals(commandLineArguments[index], RUN_AFTER_LOGIN_COMMAND_LINE_FLAG, StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }
        }
        return false;
    }

    // builds the basic set of MorphicBar items (as data, not controls); 
	// MorphicBarWindow.InitializeBarItems(...) is responsible for the actual controls.
    private static List<Morphic.MorphicBar.BarControls.IBarItemData> CreateBasicBarItemsData()
    {
        var items = new List<Morphic.MorphicBar.BarControls.IBarItemData>
        {
            // "Text Size" -- two pushbuttons (+ on the left, - on the right), equal width;
            //     - key invokes the decrement sub-button, + key invokes the increment sub-button
            Morphic.MorphicBar.BarItemDataFactory.CreateTextSizeButtonGroup(
                increaseAction: Morphic.MorphicBar.BarItemHandlers.IncreaseTextSizeButtonAction,
                decreaseAction: Morphic.MorphicBar.BarItemHandlers.DecreaseTextSizeButtonAction),

            // "Magnifier" -- two pushbuttons (Show on the left, Hide on the right), equal width
            Morphic.MorphicBar.BarItemDataFactory.CreateMagnifierButtonGroup(showAction: Morphic.MorphicBar.BarItemHandlers.ShowMagnifierButtonAction, hideAction: Morphic.MorphicBar.BarItemHandlers.HideMagnifierButtonAction),

            // "Snip" -- single label pushbutton
            Morphic.MorphicBar.BarItemDataFactory.CreateSnipButton(action: Morphic.MorphicBar.BarItemHandlers.SnipCopyButtonAction),

            // "Read Selected" -- two pushbuttons (Play / Stop), equal width. Play captures the text
            // selected in the user's previous foreground window and reads it aloud (WinRT speech
            // synthesis); Stop halts playback. Both dispatch into the App-owned ReadAloudController
            // via the handlers below.
            Morphic.MorphicBar.BarItemDataFactory.CreateReadSelectedButtonGroup(
                playAction: Morphic.MorphicBar.BarItemHandlers.ReadSelectedPlayButtonActionAsync,
                stopAction: Morphic.MorphicBar.BarItemHandlers.ReadSelectedStopButtonAction),

            // "Contrast & Color" -- 4 toggle buttons, per-content sized
            Morphic.MorphicBar.BarItemDataFactory.CreateContrastColorButtonGroup(
                contrastAction: Morphic.MorphicBar.BarItemHandlers.ContrastButtonAction,
                colorAction: Morphic.MorphicBar.BarItemHandlers.ColorButtonAction,
                darkAction: Morphic.MorphicBar.BarItemHandlers.DarkButtonAction,
                nightAction: Morphic.MorphicBar.BarItemHandlers.NightButtonAction),
        };

        return items;
    }

    // Set to true the first time PerformShutdownCleanup runs so any subsequent invocation
    // is a no-op. We deliberately call PerformShutdownCleanup from TWO places: the explicit
    // Shutdown() path (synchronously, before Exit) AND the DispatcherQueue.ShutdownStarting
    // event (as a backup for unexpected exit paths). The guard makes the second call safe.
    private bool _shutdownCleanupPerformed;

    // Synchronous tray-button + handler-unsubscribe + AppNotifications cleanup. Called from
    // Shutdown() BEFORE Application.Exit() so the user-visible tray icon and notification
    // registration go away immediately, even when an external lifecycle signal (e.g., the
    // Restart Manager-driven WM_ENDSESSION that fires during an MSI upgrade-over-running-
    // Morphic) terminates the process before the dispatcher's natural ShutdownStarting
    // event has a chance to run. Idempotent via _shutdownCleanupPerformed so the
    // App_ShutdownStarting backup path can call it without doing duplicate disposal.
    private void PerformShutdownCleanup()
    {
        if (_shutdownCleanupPerformed)
        {
            return;
        }
        _shutdownCleanupPerformed = true;

        // Stop any in-progress speech and remove the foreground-window hook. Done here (rather than
        // only in Shutdown) so it also runs on the dispatcher's backup shutdown path. Disposing on
        // the UI thread is required by the embedded foreground-window tracker; both callers of
        // PerformShutdownCleanup run on the UI thread.
        if (_readAloudController is not null)
        {
            _readAloudController.Dispose();
            _readAloudController = null;
        }

        if (_taskbarIconRefreshHandler is not null)
        {
            Morphic.SettingsUtils.CachedDarkModeState.StateChanged -= _taskbarIconRefreshHandler;
            _taskbarIconRefreshHandler = null;
        }

        // immediately hide our tray icon (and dispose of it for good measure, to help ensure that unmanaged resources are cleaned up)
        if (this.TaskbarButton is not null)
        {
            this.TaskbarButton.SetVisible(false);
            this.TaskbarButton.Dispose();
        }

        // Unsubscribe NotificationInvoked and Unregister the Shell's COM activator stub for
        // our AUMID. Strictly speaking the OS would clean both up on process exit, but doing
        // it explicitly here keeps the Shell's bookkeeping tidy and matches the symmetry of
        // Initialize being called explicitly during startup. Idempotent (guarded by an
        // _initialized flag inside ToastNotifications), so harmless if shutdown fires twice
        // or if Initialize was never called.
        Morphic.Notifications.ToastNotifications.Shutdown();
    }

    private void App_ShutdownStarting(DispatcherQueue sender, DispatcherQueueShutdownStartingEventArgs args)
    {
        // Backup path: ensures cleanup still runs if something exits the app via a code
        // path that doesn't go through App.Shutdown() (framework-initiated exit, etc.).
        // No-op if Shutdown() already ran it.
        this.PerformShutdownCleanup();
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
        _morphicBarManager?.HideBar();
    }

    private void MainMenu_ShowMorphicBarMenuItemClicked(object? sender, EventArgs e)
    {
        _morphicBarManager?.ShowBar();
    }

    internal void HandleRedirectedActivation()
    {
        // Called on a background thread when a second Morphic instance redirected its activation to
        // us (single-instance enforcement; see Program.OnAppInstanceActivated). Marshal to the UI
        // thread and show the MorphicBar, so re-launching Morphic acts as "show my MorphicBar"
        // rather than starting a duplicate.
        _menuOwnerWindow.DispatcherQueue.TryEnqueue(() =>
        {
            _morphicBarManager?.ShowBar();
        });
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

    // HRESULT 0x800710DD = "The WinUI Desktop Window object has already been closed."
    // Specifically raised by WinUI 3 when Close() is called on a Desktop Window whose
    // underlying COM object has already been destroyed by the framework. We swallow ONLY
    // this exact HRESULT (via `when` filter); any other COMException still propagates.
    private const int E_WinUIDesktopWindowAlreadyClosed = unchecked((int)0x800710DD);
	//
    internal void Shutdown()
    {
        Morphic.RmTraceLog.Log("App.Shutdown() begin");

        // NOTE: we should close all explicit windows in this function (required to allow the actual application Exit)
		//       [in contrast, accessory windows like the taskbar button are torn down automatically when the app exits]
        //
        // The Close() calls below are filtered for HRESULT 0x800710DD (see field above).
        // In the unpackaged build the windows are still live at this point and Close()
        // destroys them normally; the filter never matches and never swallows. In the
        // packaged (MSIX) build the AppX lifecycle has often already torn the windows down
        // by the time Shutdown runs, and Close() on an already-disposed Desktop Window
        // throws this specific HRESULT, which is safe to swallow: if the window is
        // already closed, our call had nothing more to do.

        try {
		    _aboutWindow?.Close();
		}
        catch (System.Runtime.InteropServices.COMException ex) when (ex.HResult == E_WinUIDesktopWindowAlreadyClosed) { }

        // Re-enable user close so the programmatic Close() actually destroys the window
        // (we'd previously disabled it to prevent Alt+F4 with the menu flyout focused from
        // destroying _menuOwnerWindow).
        try
        {
            _menuOwnerWindow.SetUserCloseEnabled(true);
            _menuOwnerWindow.Close();
        }
        catch (System.Runtime.InteropServices.COMException ex) when (ex.HResult == E_WinUIDesktopWindowAlreadyClosed) { }

        // Flush the bar's final visibility + docking state to the registry, then tear down the
        // registry sync. PersistFinalState writes the cached state (re-creating the key if an
        // external editor deleted it during this session); Dispose detaches the registry watcher and
        // unsubscribes from the bar manager's events. Done BEFORE the manager is disposed so those
        // event unsubscriptions still have a live manager to detach from.
        _appRegistrySettings?.PersistFinalState();
        _appRegistrySettings?.Dispose();
        _appRegistrySettings = null;

        // MorphicBarManager.Dispose closes out the MorphicBar and all its resources/events in a safe order.
        _morphicBarManager?.Dispose();
        _morphicBarManager = null;

        // Tray-button + AppNotifications cleanup. Done HERE (synchronously, before Exit)
        // rather than relying solely on App_ShutdownStarting (which fires asynchronously
        // from the dispatcher's own shutdown sequence) because RM-driven WM_ENDSESSION
        // shutdown gives the process only a few seconds before force-terminating; the
        // dispatcher's natural ShutdownStarting may not fire in time, which would strand
        // the tray icon visible after the rest of Morphic has exited. The method is
        // idempotent (guarded), so App_ShutdownStarting calling it as a backup is safe.
        this.PerformShutdownCleanup();

        Morphic.RmTraceLog.Log("App.Shutdown() end -> calling Application.Exit()");
        App.LogProcessThreadSnapshot("before Application.Exit()");
        this.Exit();
        Morphic.RmTraceLog.Log("App.Shutdown() returned from Application.Exit() (process still alive at this line)");
        App.LogProcessThreadSnapshot("immediately after Application.Exit() returned");

        // Schedule a follow-up snapshot ~500ms later, by which time WinUI 3 should have
        // unwound its dispatcher / message loop. If the process is STILL alive at that
        // point and threads remain, those surviving threads are what's keeping
        // Morphic.exe pinned (the actual installer "couldn't close it" bug). The work
        // item runs on a ThreadPool thread (background by definition) and explicitly
        // catches all exceptions so a teardown race can't leak. We use System.Threading
        // .Timer rather than Task.Delay so the timer survives main-thread teardown
        // (Task.Delay's continuation is anchored to the SynchronizationContext, which
        // is mid-teardown right now).
        try
        {
            System.Threading.Timer? delayedTimer = null;
            delayedTimer = new System.Threading.Timer(
                callback: _ =>
                {
                    try
                    {
                        App.LogProcessThreadSnapshot("+500ms after Application.Exit() (zombie checkpoint)");
                    }
                    catch (System.Exception ex)
                    {
                        try { Morphic.RmTraceLog.Log($"delayed snapshot threw: {ex.GetType().Name}: {ex.Message}"); } catch { }
                    }
                    finally
                    {
                        try { delayedTimer?.Dispose(); } catch { }
                    }
                },
                state: null,
                dueTime: System.TimeSpan.FromMilliseconds(500),
                period: System.Threading.Timeout.InfiniteTimeSpan);
        }
        catch (System.Exception ex)
        {
            try { Morphic.RmTraceLog.Log($"delayed snapshot scheduling threw: {ex.GetType().Name}: {ex.Message}"); } catch { }
        }
    }

    // Writes a one-line summary of the current process's threads to the RM trace log.
    // For each non-background managed thread (the ones that keep the process alive
    // after Application.Exit), logs an additional line with thread id + apartment +
    // state. Native (unmanaged) threads are summarized as a count only because
    // System.Diagnostics.Process surfaces them without start addresses we can resolve.
    private static void LogProcessThreadSnapshot(string label)
    {
        try
        {
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            int totalThreads = currentProcess.Threads.Count;
            Morphic.RmTraceLog.Log($"Thread snapshot {label}: pid={currentProcess.Id} totalThreads={totalThreads}");

            // Managed-thread inspection requires walking AppDomain... actually .NET has no
            // public API to enumerate all managed threads. We can only inspect threads we
            // explicitly created OR walk Process.Threads (which gives OS thread IDs but
            // doesn't tell us which are managed vs native, or background vs foreground).
            // The OS-level view is still useful: thread count + start time deltas tell us
            // whether new threads spawned between snapshots, and ThreadState lets us see
            // if any are Running vs Wait.
            foreach (System.Diagnostics.ProcessThread thread in currentProcess.Threads)
            {
                try
                {
                    string startTimeStr;
                    try { startTimeStr = thread.StartTime.ToString("HH:mm:ss.fff"); } catch { startTimeStr = "?"; }
                    Morphic.RmTraceLog.Log($"  os-thread id={thread.Id} state={thread.ThreadState} priority={thread.PriorityLevel} startTime={startTimeStr}");
                }
                catch (System.Exception threadIterationException)
                {
                    Morphic.RmTraceLog.Log($"  os-thread iteration error: {threadIterationException.GetType().Name}: {threadIterationException.Message}");
                }
            }
        }
        catch (System.Exception ex)
        {
            try { Morphic.RmTraceLog.Log($"LogProcessThreadSnapshot({label}) threw: {ex.GetType().Name}: {ex.Message}"); } catch { }
        }
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
        var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        _taskbarIconRefreshHandler = (_, _) =>
        {
            dispatcherQueue?.TryEnqueue(this.RefreshTaskbarIcon);
        };
        Morphic.SettingsUtils.CachedDarkModeState.StateChanged += _taskbarIconRefreshHandler;
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
        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, App.GetIconRelativePathForCurrentSystemTheme());
        _ = this.TaskbarButton.SetIconFromFile(iconPath, 256, 256);
    }

    // Selects the right contrast-variant icon resource for the current HC + dark state and
    // returns its relative path
    internal static string GetIconRelativePathForCurrentSystemTheme()
    {
        string resourceKey;
        if (Morphic.SettingsUtils.CachedDarkModeState.GetCurrentIsHighContrast())
        {
            resourceKey = Morphic.SettingsUtils.CachedDarkModeState.GetCurrentIsDark()
                ? "AppIconHighContrastBlackPath"
                : "AppIconHighContrastWhitePath";
        }
        else
        {
            resourceKey = "AppIconStandardContrastPath";
        }
        return (string)Microsoft.UI.Xaml.Application.Current.Resources[resourceKey];
    }

    // Updates the tray-button tooltip caption based on the MorphicBar's current visibility.
    // Mirrors the menu's Show/Hide MorphicBar item-label convention so the affordance is
    // self-describing wherever the user encounters it.
    private void RefreshTaskbarButtonTooltip()
    {
        if (this.TaskbarButton is null || _morphicBarManager is null)
        {
            return;
        }
        try
        {
            this.TaskbarButton.Text = _morphicBarManager.IsBarVisible ? "Hide MorphicBar" : "Show MorphicBar";
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // The bar window is being torn down: VisibilityChanged fired the visibility
            // transition mid-close, but accessing IsVisible (or writing TaskbarButton.Text
            // against a tray button whose native window is also closing) throws "WinUI
            // Desktop Window object has already been closed". The tooltip doesn't need to
            // update since the app is going away; swallow.
        }
    }

    private void TaskbarButton_MouseUp(object? sender, Controls.MouseEventArgs e)
    {
        // Marshal to the UI thread via _menuOwnerWindow's DispatcherQueue. The tray button event
        // can fire on a non-UI thread; bar operations (Show/Hide via the manager) must run on
        // the UI thread. _menuOwnerWindow is a hidden window on the same UI thread that owns
        // the bar, so its DispatcherQueue is the appropriate marshal target.
        _menuOwnerWindow.DispatcherQueue.TryEnqueue(() =>
        {
            if (_morphicBarManager is null)
            {
                return;
            }
            switch (e.Button)
            {
                case Controls.MouseButtons.Left:
                    switch (_morphicBarManager.IsBarVisible)
                    {
                        case true:
                            _morphicBarManager.HideBar();
                            break;
                        case false:
                            _morphicBarManager.ShowBar();
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
                            // The taskbar-adjacent position could not be computed (e.g. the taskbar handle or
                            // its monitor info was unavailable). Fall back through a chain of progressively
                            // less precise anchors so the menu always pops up somewhere sensible rather than
                            // silently failing.
                            if (Windows.Win32.PInvoke.GetCursorPos(out var cursorPosition) == true)
                            {
                                // Preferred fallback: the current cursor position.
                                popupPosition = cursorPosition;
                            }
                            else if (this.TaskbarButton.PositionAndSize is System.Drawing.Rectangle trayButtonRect)
                            {
                                // Next: anchor to the tray button's own rectangle (absolute screen pixels).
                                // The top-center sits just inside the screen from the taskbar, so the menu
                                // opens away from the taskbar edge, mirroring a normal taskbar context menu.
                                popupPosition = new System.Drawing.Point(trayButtonRect.Left + (trayButtonRect.Width / 2), trayButtonRect.Top);
                            }
                            else
                            {
                                // Last resort: a bottom corner of the primary display's work area, nearest
                                // where the notification tray normally lives. LTR layouts put the tray
                                // bottom-right; RTL layouts (Arabic, Hebrew, etc.) put it bottom-left, so flip
                                // the corner to match.
                                //
                                // WorkArea's right/bottom edges are EXCLUSIVE (one pixel past the last visible
                                // pixel); in universal/virtual-screen space that pixel can belong to an adjacent
                                // monitor, so subtract 1 to stay on this display. The LEFT edge is inclusive, so
                                // the RTL X uses WorkArea.X as-is (no -1).
                                var primaryWorkArea = Microsoft.UI.Windowing.DisplayArea.Primary.WorkArea;
                                bool isRightToLeft = _morphicBarManager?.IsRightToLeft == true;
                                popupPosition = (isRightToLeft == true)
                                    ? new System.Drawing.Point(primaryWorkArea.X, primaryWorkArea.Y + primaryWorkArea.Height - 1)
                                    : new System.Drawing.Point(primaryWorkArea.X + primaryWorkArea.Width - 1, primaryWorkArea.Y + primaryWorkArea.Height - 1);
                            }
                        }

                        // now pop up the main menu
                        App.MainMenu.Show(ownerWindow, _morphicBarManager?.IsBarVisible == true, popupPosition.X, popupPosition.Y);
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
