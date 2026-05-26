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
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Morphic.Core;
using Morphic.MorphicBar.BarControls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Morphic.MorphicBar;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MorphicBarWindow : Morphic.Controls.Windowing.TransparentBaseWindow, IDisposable
{
    private bool disposedValue;

    private IntPtr _hIconRawHandle = IntPtr.Zero;
    DummyWindow _dummyParentWindow;

    private Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue;

    // animation timer for moving the window
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _moveAnimationTimer;

    // logical (96 DPI) window size -- scaled by the current monitor's DPI
    private uint _logicalLength = MorphicBarWindowMetrics.DefaultLogicalLength;
    private uint _logicalThickness = MorphicBarWindowMetrics.DefaultLogicalThickness;

    // variables to enable full-window click-and-drag
    private Windows.Graphics.PointInt32 _dragStartWindowPosition;
    private Windows.Foundation.Point _dragStartPointerPosition;
    private bool _isDraggingWindow = false;
	//
    // MorphicBar's (corner, orientation) at drag start; the accidental-drag gate compares the proposed
    // target against this to detect accidental (small, same-corner) orientation flips.  Set in 
	// PointerPressed, cleared back to null (i.e. operation completed, no longer dragging) in PointerReleased.
    private (Morphic.MorphicBar.DockingLocation DockingLocation, Microsoft.UI.Xaml.Controls.Orientation Orientation)? _dragStartDockedState;

    // Sticky one-way ratchet: false until IsAccidentalDragOperation returns false for the first
    // time this drag, then stays true for the rest of the drag (reset to false at next
    // PointerPressed). Once the gate has been passed, the layout preview is allowed to show; we
    // intentionally do NOT re-engage the gate if the user later drags back to short range.
    private bool _dragHasPassedAccidentalGate = false;

    // Started at PointerPressed; used by IsAccidentalDragOperation's time-based bypass.
    private System.Diagnostics.Stopwatch? _dragStopwatch;

    // Single-shot timer that fires once at ACCIDENTAL_DRAG_HOLD_THRESHOLD after PointerPressed.
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _accidentalDragBypassTimer;

    // Tunable thresholds for accidental-drag detection. Adjust these to make the gate more
    // or less aggressive at suppressing small flip-zone drags.
    //   * ACCIDENTAL_DRAG_THRESHOLD_DIPS: drag distance < this value (in device-independent
    //     pixels, multiplied by the monitor's rasterization scale to compare against
    //     physical-pixel drag distance) is treated as accidental. Picks an absolute "small
    //     gesture" floor that scales naturally with DPI.
    //   * ACCIDENTAL_DRAG_THRESHOLD_MONITOR_DIAGONAL_FRACTION: drag distance < (fraction *
    //     monitorDiagonalPhysical) is treated as accidental. Scales with screen size so
    //     huge displays get a slightly more generous floor and tiny ones don't go too tight.
    //   * The effective threshold is the LARGER of the two -- belt-and-suspenders against
    //     unusual DPI/screen-size combinations. The two values above are tuned to be roughly
    //     equivalent on typical displays (1080p-4K at 100-200% scale), so neither dominates
    //     absurdly; one acts as a floor for outlier configurations.
    //   * ACCIDENTAL_DRAG_HOLD_THRESHOLD: after this much time has elapsed since the user
    //     pressed the pointer, the gate is fully bypassed -- any drag is treated as
    //     intentional. Lets a user who deliberately holds-and-drags commit a small-range
    //     flip without having to pull the cursor past the distance threshold.
    private const double ACCIDENTAL_DRAG_THRESHOLD_DIPS = 50;
    private const double ACCIDENTAL_DRAG_THRESHOLD_MONITOR_DIAGONAL_FRACTION = 0.02;
    private static readonly TimeSpan ACCIDENTAL_DRAG_HOLD_THRESHOLD = TimeSpan.FromMilliseconds(650);

    // the layout preview window lets us show the user where the window will move to if they release the mouse cursor
    private Morphic.MorphicBar.LayoutPreviewWindow.LayoutPreviewWindow _layoutPreviewWindow = null!;
    private Orientation? _layoutPreviewWindowOrientation = null;
    private DockingLocation? _layoutPreviewDockingLocation = null;
    private Windows.Win32.Foundation.RECT? _lastLayoutPreviewTargetPosition = null;

    // orientation of the MorphicBar
    private Microsoft.UI.Xaml.Controls.Orientation _orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal;
    public event EventHandler<Microsoft.UI.Xaml.Controls.Orientation>? OrientationChanged;

    private Morphic.MorphicBar.DockingLocation _dockingLocation = DockingLocation.FloatingBottomRight; // default location
    public event EventHandler<Morphic.MorphicBar.DockingLocation>? DockingLocationChanged;

    // NOTE: as we are handling sizing ourselves, we need to manage size scaling ourselves; this tracks the latest screen scale (so that we know if we need to resize our window)
    private double? _lastRasterizationScale = null;

    // Tracks which monitor the bar belongs to. Kept in sync at the entry of AnimateMoveTo and
    // re-verified on demand via GetVerifiedCurrentMonitorHandle. Holding our own handle (instead
    // of re-querying the window's current position each time) means a DPI/rasterization-scale
    // change does not silently relocate us to a different display.
    private Windows.Win32.Graphics.Gdi.HMONITOR _currentMonitorHandle;

    // Master registry of all bar item controls created from the last InitializeBarItems call. Items
    // here are NOT necessarily currently present in BarItemsPanel.Children -- we trim (move)
    // controls between "displayed" (those that fit within the current screen's working area, in a single
	// bar) and controls that need to be hidden (or overflowed onto an overflow panel)
    private readonly System.Collections.Generic.List<Microsoft.UI.Xaml.FrameworkElement> _allBarItemControls = new();

    // Per-instance subclass on the bar's HWND. Handles WM_ACTIVATE (drives initial-focus logic;
    // WinUI's Window.Activated is unreliable for borderless+topmost+owned windows like ours) and
    // WM_CLOSE (when _userCloseEnabled=false, Alt+F4 turns into Hide; set true via
    // SetUserCloseEnabled(true) right before Close() to allow programmatic shutdown through).
    // Field keeps the delegate alive while the subclass is installed (GC pinning).
    private Windows.Win32.UI.Shell.SUBCLASSPROC? _subclassProc;
    private bool _userCloseEnabled;  // default false: Alt+F4 -> Hide
    //
    // Time-based suppression of the Programmatic/Pointer -> Keyboard focus upgrade. Set by
    // RunWithBarHiddenAsync at the START of the flow so it covers the entire span: deferred
    // updates fired by the click activation, by the Show()'s WM_ACTIVATE after the action, and
    // any in-between transitions are all suppressed. Time-based (not one-shot) because action()
    // can take seconds (e.g. the snip overlay) and multiple WM_ACTIVATEs may fire in that window.
    // Stored as Environment.TickCount64 (system-uptime ms, monotonic) to avoid DateTime.Now's
    // clock-skew / DST issues.
    private long _suppressUpgradeUntilTickCount64;

    //
    // Item lengths are NOT cached: they depend on the bar's effective thickness (since a narrower
    // bar can cause text wrapping that makes items taller). MeasureBarForOrientation performs a
    // fresh two-pass measurement each time it's called.

    public MorphicBarWindow()
    {
        InitializeComponent();

        // apply the initial orientation-specific layout via the same helpers used on orientation
        // change; the Orientation setter only fires when the value changes, so without this call
        // the bar items panel and logo button would initially render at default Margin
        this.UpdateBarItemsPanelLayout(_orientation);
        this.UpdateMorphicMenuButtonLayout(_orientation);

        _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        var hwnd = (Windows.Win32.Foundation.HWND)WinRT.Interop.WindowNative.GetWindowHandle(this);

        // record the initial monitor (wherever the window-manager placed us at creation time);
        // this is a placeholder that gets overwritten by the first AnimateMoveTo call from App.xaml.cs
        _currentMonitorHandle = Windows.Win32.PInvoke.MonitorFromWindow(
            hwnd,
            Windows.Win32.Graphics.Gdi.MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);

        // create a dummy "parent window" for the layout preview window (so that this window doesn't show up in the taskbar)
        _dummyParentWindow = new DummyWindow();
        _ = _dummyParentWindow.SetAsParentHwnd(hwnd);

        // create a layout preview window; we'll need this whenever the MorphicBar is moved; this is created up front, as it can take a little time to create the window
        _layoutPreviewWindow = new();

        this.InitializeBorderlessWindowProperties(hwnd);

        // if the user clicks on the window, let them drag it (i.e. release the pointer capture and forward the left-click as if it's a "caption bar" left-click instead)
        this.InitializePointerPressAndDrag(this.Content);

        // Install the WM_ACTIVATE + WM_CLOSE subclass. See _subclassProc field comment for why.
        _subclassProc = this.SubclassWndProc;
        var setSubclassResult = Windows.Win32.PInvoke.SetWindowSubclass(hwnd, _subclassProc, uIdSubclass: 0, dwRefData: 0);
        System.Diagnostics.Debug.Assert(setSubclassResult);

        (this.Content as Grid)!.Loaded += RootGrid_Loaded;
        this.Closed += MorphicBarWindow_Closed;
    }

    // Toggles whether Alt+F4 / shell close from the user actually destroys the window. Default
    // is false (WM_CLOSE -> Hide instead). Set to true right before programmatic shutdown's
    // Close() so the close goes through normally.
    internal void SetUserCloseEnabled(bool enabled)
    {
        _userCloseEnabled = enabled;
    }

    // Suppresses the Programmatic/Pointer -> Keyboard focus upgrade for `duration`. Used by
    // RunWithBarHiddenAsync to cover the full span of a mouse-driven bar action (click activation
    // + the action itself, which may take seconds + the Show()'s WM_ACTIVATE after the action).
    internal void SuppressFocusUpgradeFor(TimeSpan duration)
    {
        var until = Environment.TickCount64 + (long)duration.TotalMilliseconds;
        if (until > _suppressUpgradeUntilTickCount64)
        {
            _suppressUpgradeUntilTickCount64 = until;
        }
    }

    // Unhooks the subclass before the HWND is destroyed.
    private void MorphicBarWindow_Closed(object sender, Microsoft.UI.Xaml.WindowEventArgs args)
    {
        if (_subclassProc is not null)
        {
            var hwnd = (Windows.Win32.Foundation.HWND)WinRT.Interop.WindowNative.GetWindowHandle(this);
            _ = Windows.Win32.PInvoke.RemoveWindowSubclass(hwnd, _subclassProc, uIdSubclass: 0);
            _subclassProc = null;
        }
    }

    private Windows.Win32.Foundation.LRESULT SubclassWndProc(
        Windows.Win32.Foundation.HWND hwnd,
        uint msg,
        Windows.Win32.Foundation.WPARAM wParam,
        Windows.Win32.Foundation.LPARAM lParam,
        nuint uIdSubclass,
        nuint dwRefData)
    {
        if (msg == Windows.Win32.PInvoke.WM_ACTIVATE)
        {
            // wParam low word: WA_INACTIVE (0), WA_ACTIVE (1), WA_CLICKACTIVE (2).
            ushort activationLowWord = unchecked((ushort)(uint)(nuint)wParam.Value);
            if (activationLowWord == Windows.Win32.PInvoke.WA_ACTIVE)
            {
                this.ScheduleDeferredFocusUpdate(WindowActivationState.CodeActivated);
            }
            else if (activationLowWord == Windows.Win32.PInvoke.WA_CLICKACTIVE)
            {
                this.ScheduleDeferredFocusUpdate(WindowActivationState.PointerActivated);
            }
        }
        else if (msg == Windows.Win32.PInvoke.WM_CLOSE && _userCloseEnabled == false)
        {
            // Alt+F4 -> Hide instead of destroy. Deferred via dispatcher to avoid re-entrant
            // message processing; wrapped in try/catch so a teardown race doesn't throw.
            _ = this.DispatcherQueue.TryEnqueue(() =>
            {
                try { this.AppWindow.Hide(); }
                catch (System.Runtime.InteropServices.COMException) { }
            });
            return new Windows.Win32.Foundation.LRESULT(0);
        }
        return Windows.Win32.PInvoke.DefSubclassProc(hwnd, msg, wParam, lParam);
    }

    /// <summary>
    /// Replaces the bar's items with controls built from the supplied data list. The bar window
    /// owns control creation and orientation propagation; the caller (typically App) only supplies
    /// data classes, keeping bar contents authoring outside the window class.
    /// </summary>
    /// <param name="items">Heterogeneous list of bar item data (BarButtonData, BarMultiButtonData, ...).</param>
    public void InitializeBarItems(IEnumerable<IBarItemData> items)
    {
        // tear down: clear the live bar AND the master cache; the previous controls (if any) are
        // dropped and will be GC'd once their event subscriptions release
        this.BarItemsPanel.Children.Clear();
        _allBarItemControls.Clear();

        // materialize the input so we can length-check it; enforce MorphicBarWindowMetrics.MaxBarItemCount by silently
        // truncating excess.
        var itemsList = items.ToList();
        if (itemsList.Count > MorphicBarWindowMetrics.MaxBarItemCount)
        {
            itemsList = itemsList.GetRange(0, MorphicBarWindowMetrics.MaxBarItemCount);
        }

        // build all the controls up front; each one's Orientation is synced with the bar's current
        // orientation (and lays out correctly for that orientation when it loads)
        foreach (var data in itemsList)
        {
            IBarItemControl control;
            switch (data)
            {
                case BarButtonData buttonData:
                    control = new BarButtonControl { Data = buttonData };
                    break;
                case BarMultiButtonData multiButtonData:
                    control = new BarMultiButtonControl { Data = multiButtonData };
                    break;
                default:
                    throw new MorphicUnhandledCaseException(data);
            }
            control.Orientation = _orientation;
            _allBarItemControls.Add((FrameworkElement)control);
        }

        // schedule the bar-level measure-and-resize for the moment every new item has fired Loaded.
        // Loaded fires once an element is in a live visual tree and its initial measure pass is
        // complete, which is the strongest guarantee WinUI gives us that DesiredSize is meaningful.
        int pendingLoadedCount = _allBarItemControls.Count;
        if (pendingLoadedCount == 0)
        {
            // no items to wait for: still re-fit the bar (collapses chrome around an empty panel)
            this.MeasureAndResize();
			
            // re-seed focus (for accessiblity); with no items, SetInitialFocus falls back to the logo button
            this.SetInitialFocus();
			
            return;
        }
        foreach (var control in _allBarItemControls)
        {
            RoutedEventHandler? handler = null;
            handler = (sender, e) =>
            {
                // unwire this handler (as it should only be called once)
                ((FrameworkElement)sender).Loaded -= handler;

                pendingLoadedCount--;
                if (pendingLoadedCount == 0)
                {
                    // every control is now in the live visual tree; MeasureAndResize runs the
                    // full pipeline (two-pass measure -> trim -> resize) via AnimateMoveTo
                    this.MeasureAndResize();
                }
            };
            control.Loaded += handler;

            // add to BarItemsPanel so the control loads and we can measure it. The trim step (above)
            // moves anything that doesn't fit out of BarItemsPanel into the cache.
            this.BarItemsPanel.Children.Add(control);
        }

        // Re-seed focus on the (new) first item. Items are in the visual tree at this point
        // (added to BarItemsPanel.Children above) even though their Loaded events may not have
        // fired yet -- the focus subsystem looks at the visual tree, not Loaded state, so this
        // is fine. If the bar isn't currently active, the Focus call is a harmless no-op;
        // ScheduleDeferredFocusUpdate (from the next WM_ACTIVATE) will set focus again when the
        // bar next becomes active.
        this.SetInitialFocus();
    }

    // Returns true once every control in _allBarItemControls has fired its Loaded event (and is
    // therefore safely measurable). Until then, measurements may return zero or template-pending
    // values, so any code that makes sizing/trimming decisions should bail and let the eventual
    // post-Loaded MeasureAndResize handle it.
    private bool AllBarItemsLoaded()
    {
        foreach (var control in _allBarItemControls)
        {
            if (!control.IsLoaded) { return false; }
        }
        return true;
    }

    //
	
    // Sets each item's Visibility so the first `fittingCount` entries of _allBarItemControls are
    // Visible and the rest are Collapsed. Items remain parented to BarItemsPanel.Children at all
    // times so they stay loaded and measurable (otherwise BarButtonControl's MeasureForOrientation
    // would return zero for unparented items, and Pass 2 trim math would silently let everything
    // through).
    //
    // BarItemsPanel uses StackPanel.Spacing, which only adds spacing between consecutive Visible
    // children, so Collapsed items contribute neither layout space nor spacing.
    private void SyncDisplayedItemPrefix(int fittingCount)
    {
        if (fittingCount < 0) { fittingCount = 0; }
        if (fittingCount > _allBarItemControls.Count) { fittingCount = _allBarItemControls.Count; }

        for (int i = 0; i < _allBarItemControls.Count; i++)
        {
            _allBarItemControls[i].Visibility = (i < fittingCount) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    // Drives the initial-focus decision after a WM_ACTIVATE arrives via SubclassWndProc. Always
    // DEFERRED via DispatcherQueue.TryEnqueue because at WM_ACTIVATE time:
    //   * XamlRoot can be null (initial activation arrives before the XAML tree is connected).
    //   * GetForegroundWindow does NOT yet return our HWND (OS hasn't finalized the transition).
    // Both settle by the next dispatcher cycle; re-evaluating from a clean slate handles both.
    // activationState carries the semantic distinction: PointerActivated -> mouse (no ring),
    // CodeActivated -> keyboard/programmatic (ring if foreground). Caller is responsible for
    // translating WM_ACTIVATE's wParam (WA_ACTIVE / WA_CLICKACTIVE) into the right value.
    private void ScheduleDeferredFocusUpdate(WindowActivationState activationState)
    {
        _ = this.DispatcherQueue.TryEnqueue(() =>
        {
            // Window may have been torn down between schedule and fire. Wrap the whole body --
            // even `this.Content` getter can throw COMException ("WinUI Desktop Window object has
            // already been closed") if the bar Close()'d after enqueue. Swallow to keep shutdown
            // quiet; nothing here is correctness-critical past teardown.
            try
            {
                this.RunDeferredFocusUpdate(activationState);
            }
            catch (System.Runtime.InteropServices.COMException)
            {
            }
        });
    }

    private void RunDeferredFocusUpdate(WindowActivationState activationState)
    {
        if (this.Content?.XamlRoot is not XamlRoot xamlRoot)
        {
            return;
        }
		
        // Live mouse-button check: if ANY mouse button is currently pressed, the user is mid-
        // click on the bar -- treat as mouse-driven regardless of what WM_ACTIVATE reported.
        // The bar's first-click activation fires WA_ACTIVE (not WA_CLICKACTIVE), so we can't
        // rely on wParam alone. 0x01=LBUTTON, 0x02=RBUTTON, 0x04=MBUTTON; high bit (0x8000) set when down.
        bool mousePressed = (Windows.Win32.PInvoke.GetAsyncKeyState(0x01) & 0x8000) != 0
            || (Windows.Win32.PInvoke.GetAsyncKeyState(0x02) & 0x8000) != 0
            || (Windows.Win32.PInvoke.GetAsyncKeyState(0x04) & 0x8000) != 0;
        System.Diagnostics.Debug.WriteLine($"[Focus] RunDeferredFocusUpdate({activationState}) mousePressed={mousePressed}");
        if (mousePressed)
        {
            activationState = WindowActivationState.PointerActivated;
            // Aggressively clear any Keyboard FocusState in the bar tree. WinUI's default-
            // activation focus may have placed Keyboard focus on a button before our deferred
            // update runs, and FocusManager.GetFocusedElement may not return that button
            // (it can return a parent ScrollViewer instead), so we walk the whole tree.
            this.DowngradeKeyboardFocusedControlsInBar();
        }
		
        // If a bar control is already focused, don't move focus -- but UPGRADE its FocusState
        // to Keyboard if appropriate. Common case: a SetInitialFocus call during init
        // (RootGrid_Loaded) placed focus on the first button silently (Programmatic, no ring).
        // When the user later Alt+Tab's into the bar, we want the ring to appear on whatever
        // button is already focused, without jumping focus around.
        //
        // Upgrade only when:
        //   * the activation is keyboard-style (not PointerActivated -- a mouse-click that
        //     happens to land on a button shouldn't be promoted to a keyboard ring)
        //   * the current FocusState is NOT already Keyboard (no work to do otherwise)
        //   * the bar IS the OS-level foreground window (otherwise we'd be lying about
        //     keyboard focus when we don't actually have it)
        var focused = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(xamlRoot) as DependencyObject;
        if (focused is not null && this.IsBarOwnedElement(focused))
        {
            if (focused is Control focusedControl)
            {
                // Promote to Keyboard for keyboard-style activation (CodeActivated) when the bar
                // is foreground and we're not in a suppress window (set by RunWithBarHiddenAsync
                // to cover its own re-Show).
                if (activationState != WindowActivationState.PointerActivated
                    && focusedControl.FocusState != FocusState.Keyboard
                    && Environment.TickCount64 > _suppressUpgradeUntilTickCount64
                    && this.IsBarForegroundWindow())
                {
                    _ = focusedControl.Focus(FocusState.Keyboard);
                }
                // Mirror: DEMOTE to Pointer for mouse-driven activation when the focused control
                // currently has Keyboard FocusState. WinUI's default activation focus places
                // Keyboard focus on the first focusable button as a side effect of WA_CLICKACTIVE,
                // which lights up the keyboard ring on a mouse-driven activation -- exactly wrong.
                // Downgrading to Pointer suppresses the ring (Pointer state renders no ring).
                else if (activationState == WindowActivationState.PointerActivated
                    && focusedControl.FocusState == FocusState.Keyboard)
                {
                    _ = focusedControl.Focus(FocusState.Pointer);
                }
            }
            return;
        }
        // Initial-focus path also respects the suppress timer. If PointerPressed just set the
        // suppress (user is mouse-pressing the bar), don't seed a keyboard ring on the first
        // button just because no element is focused in the bar's main visual tree. This also
        // covers the case where focus is on a non-IsBarOwnedElement-recognized subtree (e.g.,
        // an internal ScrollViewer) -- we'd fall through here and put a ring on the first
        // button otherwise.
        if (Environment.TickCount64 <= _suppressUpgradeUntilTickCount64)
        {
            return;
        }
        FocusState focusState;
        if (activationState == WindowActivationState.PointerActivated)
        {
            // Mouse activation: no ring -- WinUI convention is mouse activation doesn't show
            // the keyboard focus ring.
            focusState = FocusState.Pointer;
        }
        else
        {
            // CodeActivated path: show the ring ONLY if this bar window is now the OS-level
            // foreground window. Distinguishes "user Alt+Tab'd to us" (foreground == ourHwnd,
            // ring) from spurious activations where focus-stealing was denied (silent).
            focusState = this.IsBarForegroundWindow() ? FocusState.Keyboard : FocusState.Programmatic;
        }
        this.SetInitialFocus(focusState);
    }

    // Places keyboard focus on a sensible "first interactable" element inside the bar. Order of preference:
    //   1. First focusable descendant of BarItemsPanel (the first inner button of the first bar
    //      item; FocusManager walks the visual tree, so we don't need per-item knowledge of which
    //      inner control to focus).
    //   2. Morphic logo button (always present, used as the fallback when the bar has no items or
    //      none with focusable content).
    //
    // focusState determines whether the focus ring shows:
    //   * FocusState.Keyboard    -> ring shown (only appropriate when the bar genuinely has
    //                               keyboard focus, e.g., user Alt+Tab'd in)
    //   * FocusState.Programmatic -> silent focus, no ring (right for startup-time calls where
    //                               we're seeding focus but the bar isn't necessarily foreground)
    //   * FocusState.Pointer     -> silent focus, semantically for mouse activation
    // The default (Programmatic) is the safe choice; ScheduleDeferredFocusUpdate passes Keyboard
    // when the WM_ACTIVATE handler confirms the bar is the OS-level foreground window.
    internal void SetInitialFocus(FocusState focusState = FocusState.Programmatic)
    {
        // The focus-setting block needs a live visual tree connected to a XamlRoot.
        // FindFirstFocusableElement and Focus() both throw if called before that connection
        // (e.g., from InitializeBarItems during initial setup, before RootGrid_Loaded fires).
        // Gate the work behind XamlRoot availability rather than early-returning so any future
        // non-focus-setting code added to this method would still run.
        if (this.Content?.XamlRoot is not null)
        {
            var firstFocusable = Microsoft.UI.Xaml.Input.FocusManager.FindFirstFocusableElement(this.BarItemsPanel);
            if (firstFocusable is Control firstControl)
            {
                _ = firstControl.Focus(focusState);
            }
            else
            {
                // no bar items (or none with focusable content) -> fall back to the Morphic logo button
                _ = this.MorphicMenuButton.Focus(focusState);
            }
        }
    }

    // Walks the bar's visual tree and downgrades any Control with FocusState=Keyboard to
    // FocusState=Pointer. Called from RunDeferredFocusUpdate when we detect a mouse-driven
    // activation, and from MorphicBarManager.ShowBar(activateWindow:true) to defuse any
    // Keyboard ring left from a prior keyboard session before re-showing the bar. WinUI's
    // FocusManager.GetFocusedElement may return a parent element (ScrollViewer, etc.) instead
    // of the actual Keyboard-focused button, so a brute-force tree walk is needed.
    internal void DowngradeKeyboardFocusedControlsInBar()
    {
        if (this.Content is not DependencyObject root)
        {
            return;
        }
        var queue = new System.Collections.Generic.Queue<DependencyObject>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current is Control control && control.FocusState == FocusState.Keyboard)
            {
                System.Diagnostics.Debug.WriteLine($"[Focus] Downgrading {control.GetType().Name} Keyboard -> Pointer");
                _ = control.Focus(FocusState.Pointer);
            }
            int childCount = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(current);
            for (int i = 0; i < childCount; i++)
            {
                queue.Enqueue(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(current, i));
            }
        }
    }

    // True if this bar's HWND is the OS-level foreground window right now. Used to decide whether
    // to show the keyboard focus ring (only appropriate when WE actually have keyboard focus -- if
    // some other window is foreground, the ring would be a lie).
    private bool IsBarForegroundWindow()
    {
        var barHwnd = (Windows.Win32.Foundation.HWND)WinRT.Interop.WindowNative.GetWindowHandle(this);
        return Windows.Win32.PInvoke.GetForegroundWindow() == barHwnd;
    }

    // Walks the visual-parent chain of `element` to determine whether it lives inside this bar
    // window's content tree. Used by ScheduleDeferredFocusUpdate to decide whether the framework
    // has already placed focus on a bar control (leave alone) or focus is elsewhere (place
    // initial focus ourselves).
    private bool IsBarOwnedElement(DependencyObject element)
    {
        var current = element;
        while (current is not null)
        {
            if (current == this.Content)
            {
                return true;
            }
            current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    private void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        // record the current rasterization scale and subscribe to changes (e.g. monitor switch)
        _lastRasterizationScale = this.Content.XamlRoot.RasterizationScale;
        this.Content.XamlRoot.Changed += (s, e) =>
        {
            this.RasterizationScaleChanged();
        };

        // do a fresh measure-and-resize now that the visual tree is fully loaded; any earlier
        // measurement during construction or InitializeBarItems may have been against partially-
        // realized children, so this is the authoritative initial sizing pass
        this.MeasureAndResize();

        // DO NOT pre-seed focus here. Calling Focus() on a bar control during startup pulls the
        // bar to OS-level foreground as a side effect (we don't activate it explicitly, but the
        // Focus call does so transitively). That leaves the bar "already foreground" by the time
        // the user does their first Alt+Tab into it -- which is a no-op transition that fires no
        // WM_ACTIVATE / EVENT_SYSTEM_FOREGROUND, so our focus-ring logic never gets to upgrade
        // the FocusState to Keyboard. Leaving the bar un-focused at startup means the user's
        // first Alt+Tab is a REAL activation transition; the deferred-focus path takes the
        // "no element focused" branch and calls SetInitialFocus(Keyboard), which places focus on
        // the first button with the ring visible. (InitializeBarItems still calls SetInitialFocus
        // to re-seed after item rebuilds; that runs in response to user actions, not at startup.)
    }

    private void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // dispose managed state (managed objects)
                _dummyParentWindow.Dispose();
            }

            // free unmanaged resources (unmanaged objects) and override finalizer
            if (_hIconRawHandle != IntPtr.Zero)
            {
                _ = Windows.Win32.PInvoke.DestroyIcon((Windows.Win32.UI.WindowsAndMessaging.HICON)_hIconRawHandle);
            }

            // set large fields to null
            // [none]

            disposedValue = true;
        }
    }

    // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    ~MorphicBarWindow()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /* public methods */

    public MorphicResult<MorphicUnit, MorphicUnit> SetIconFromFile(string filePath, int width, int height)
    {
        IntPtr oldIconRawHandle = _hIconRawHandle;
        try
        {
            var hwnd = (Windows.Win32.Foundation.HWND)WinRT.Interop.WindowNative.GetWindowHandle(this);

            // load the .ico file as an HICON using LoadImage
            var hIcon = Windows.Win32.PInvoke.LoadImage(
                null,
                filePath,
                Windows.Win32.UI.WindowsAndMessaging.GDI_IMAGE_TYPE.IMAGE_ICON,
                width,
                height,
                Windows.Win32.UI.WindowsAndMessaging.IMAGE_FLAGS.LR_LOADFROMFILE
            );
            if (hIcon.IsInvalid)
            {
                Debug.Assert(false, "Could not load icon from file: " + filePath);
                return MorphicResult.ErrorResult();
            }
            // NOTE: there's a bug in SafeFileHandle which tries to clean up icons incorrectly, so prevent the safe handle from trying to free it
            // NOTE: this icon MUST be cleaned up in the Dispose pattern, in the unmanaged resource section
            _hIconRawHandle = hIcon.DangerousGetHandle();
            hIcon.SetHandleAsInvalid();

            // update the icons (big and small) for the window
            _ = Windows.Win32.PInvoke.SendMessage(hwnd, Windows.Win32.PInvoke.WM_SETICON, Windows.Win32.PInvoke.ICON_BIG, (Windows.Win32.Foundation.LPARAM)_hIconRawHandle);
            _ = Windows.Win32.PInvoke.SendMessage(hwnd, Windows.Win32.PInvoke.WM_SETICON, Windows.Win32.PInvoke.ICON_SMALL, (Windows.Win32.Foundation.LPARAM)_hIconRawHandle);
        }
        finally
        {
            // if the icon is changing, destroy the previous icon
            if (oldIconRawHandle != IntPtr.Zero)
            {
                _ = Windows.Win32.PInvoke.DestroyIcon((Windows.Win32.UI.WindowsAndMessaging.HICON)oldIconRawHandle);
            }
        }

        return MorphicResult.OkResult();
    }

    public void Resize(uint logicalLength, uint logicalThickness)
    {
        _logicalLength = logicalLength;
        _logicalThickness = logicalThickness;

        if (_lastRasterizationScale is not null)
        {
            this.UpdateAppWindowSizeUsingRasterizationScale(_lastRasterizationScale!.Value);
        }
    }

    /// <summary>
    /// Brings the bar to the target (monitor, orientation, docking location) state. Measures the
    /// bar for the target orientation on the target monitor, computes the docking rect, and animates
    /// the AppWindow to it. Size and position animate together via AnimationUtils.AnimateMoveTo.
    /// A duration of TimeSpan.Zero snaps instantly (no animation). If called while a previous
    /// animation is in progress, that animation is cancelled and a new one starts from the window's
    /// current state.
    ///
    /// This is the single entry point for any change to monitor / orientation / docking location.
    /// MeasureAndResize is a convenience wrapper for the common "stay where we are, just re-fit"
    /// case.
    /// </summary>
    // snapResizeAtPoint (optional): if provided, the window is instantly resized to the target
    // size at this physical-pixel point (keeping the point at the same proportional position
    // within the window) BEFORE the animation begins.
    internal void AnimateMoveTo(Windows.Win32.Graphics.Gdi.HMONITOR hMonitor, Microsoft.UI.Xaml.Controls.Orientation targetOrientation, DockingLocation targetDockingLocation, TimeSpan duration, System.Drawing.Point? snapResizeAtPoint = null)
    {
        // stop any existing timer
        _moveAnimationTimer?.Stop();
        _moveAnimationTimer = null;

        // record the destination monitor; this is the single normal path that legitimately changes
        // which monitor we are on (both intentional moves and drag-release end up here)
        _currentMonitorHandle = hMonitor;

        // apply the target orientation via the property setter so its layout helpers run
        // (UpdateMorphicMenuButtonLayout / UpdateBarItemsPanelLayout); the setter is a no-op
        // when the orientation already matches
        this.Orientation = targetOrientation;

        // record the destination docking location
        _dockingLocation = targetDockingLocation;

        // single combined two-pass measurement: returns the bar's logical size AND the count of
        // items that fit. Pass 1 of MeasureBarForOrientation determines bar thickness; Pass 2
        // re-measures each item with that thickness as the cross-axis constraint, capturing any
        // text wrapping in headers/buttons that the unconstrained Pass 1 would have missed.
        var (logicalLength, logicalThickness, fittingCount) = this.MeasureBarForOrientation(hMonitor, targetOrientation);
        _logicalLength = logicalLength;
        _logicalThickness = logicalThickness;

        // sync BarItemsPanel.Children to the leading prefix of _allBarItemControls that fits.
        // Trimmed items remain in the _allBarItemControls cache (unparented for now; will move
        // into the overflow window once that exists). Items that fit but were previously trimmed
        // get inserted back into BarItemsPanel.
        this.SyncDisplayedItemPrefix(fittingCount);

        // compute the target rect (already in physical pixels; GetRectForDockingLocation multiplies
        // by the monitor's rasterization scale internally)
        var getRectForDockingLocationResult = LayoutUtils.GetRectForDockingLocation(targetDockingLocation, targetOrientation, logicalLength, logicalThickness, hMonitor);
        if (getRectForDockingLocationResult.IsError)
        {
            Debug.Assert(false);
            return;
        }
        var targetRect = getRectForDockingLocationResult.Value!;
        var targetPosition = new Windows.Graphics.PointInt32(targetRect.X, targetRect.Y);
        var targetSize = new Windows.Graphics.SizeInt32(targetRect.Width, targetRect.Height);

        // Snap-resize step (typically used for orientation-flip on drag release): instantly
        // resize the window to the target size at the supplied anchor point, keeping that point
        // at the same proportional position within the window. After this, AppWindow.Size already
        // matches targetSize, so AnimationUtils.AnimateMoveTo will detect sizeChanging=false and
        // animate position only -- no visible resize during the animation.
        if (snapResizeAtPoint is { } anchorPoint)
        {
            var currentPosition = this.AppWindow.Position;
            var currentSize = this.AppWindow.Size;
            int snapX, snapY;
            bool anchorIsOverWindow = anchorPoint.X >= currentPosition.X && anchorPoint.X < currentPosition.X + currentSize.Width
                                   && anchorPoint.Y >= currentPosition.Y && anchorPoint.Y < currentPosition.Y + currentSize.Height;
            if (anchorIsOverWindow)
            {
                // keep the anchor point at the same proportional position within the (new-sized) window
                double proportionX = (double)(anchorPoint.X - currentPosition.X) / currentSize.Width;
                double proportionY = (double)(anchorPoint.Y - currentPosition.Y) / currentSize.Height;
                snapX = anchorPoint.X - (int)(proportionX * targetSize.Width);
                snapY = anchorPoint.Y - (int)(proportionY * targetSize.Height);
            }
            else
            {
                // fallback (anchor not over window): center the new size on the window's current center
                int currentCenterX = currentPosition.X + (currentSize.Width / 2);
                int currentCenterY = currentPosition.Y + (currentSize.Height / 2);
                snapX = currentCenterX - (targetSize.Width / 2);
                snapY = currentCenterY - (targetSize.Height / 2);
            }
            this.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(snapX, snapY, targetSize.Width, targetSize.Height));
        }

        // start the new animation (size + position interpolate together; TimeSpan.Zero snaps).
        // If snapResizeAtPoint was used above, AppWindow.Size already matches targetSize and
        // AnimationUtils.AnimateMoveTo's sizeChanging check will short-circuit the size interpolation.
        _moveAnimationTimer = AnimationUtils.AnimateMoveTo(_dispatcherQueue, this.AppWindow, targetPosition, targetSize, duration);
    }

    internal void AnimateStop()
    {
        _moveAnimationTimer?.Stop();
        _moveAnimationTimer = null;
    }

    /* properties */

    public Microsoft.UI.Xaml.Controls.Orientation Orientation
    {
        get => _orientation;
        set
        {
            if (_orientation != value)
            {
                _orientation = value;

                // reposition the inner chrome to match the new orientation; the bar window's
                // own size and position are NOT touched here. Callers who want the bar to
                // re-fit and re-dock for the new orientation should go through AnimateMoveTo
                // (or MeasureAndResize for the in-place case); AnimateMoveTo itself uses this
                // setter, so internal orientation changes get the full flow automatically.
                this.UpdateMorphicMenuButtonLayout(value);
                this.UpdateBarItemsPanelLayout(value);

                OrientationChanged?.Invoke(this, value);
            }
        }
    }

    /* events */

    private void RasterizationScaleChanged()
    {
        var rasterizationScale = this.Content.XamlRoot.RasterizationScale;
        if (rasterizationScale == _lastRasterizationScale)
        {
            return;
        }
        _lastRasterizationScale = rasterizationScale;

        // dispatch the re-fit asynchronously so it runs after WinUI finishes its own DPI handling.
        // MeasureAndResize uses GetVerifiedCurrentMonitorHandle internally, so a DPI change is
        // interpreted as "same monitor, new scale" rather than "follow the cursor to a new monitor"
        // (unless the cached handle has actually gone stale).
        this.DispatcherQueue.TryEnqueue(() =>
        {
            this.MeasureAndResize();
        });
    }

    /* callbacks */

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        this.AppWindow.Hide();
    }
	
	/* Morphic logo menu handling */

    // handle both click (left-click) and right tapping (right-click) of the Morphic logo button
    private void MorphicMenuButton_Click(object sender, RoutedEventArgs e)
    {
        this.ShowMorphicMenu();
    }

    private void MorphicMenuButton_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
    {
        e.Handled = true;
        this.ShowMorphicMenu();
    }

    private void ShowMorphicMenu()
    {
        var hwnd = new Windows.Win32.Foundation.HWND(WinRT.Interop.WindowNative.GetWindowHandle(this));
        var buttonPosition = this.MorphicMenuButton.TransformToVisual(this.Content).TransformPoint(new Windows.Foundation.Point(0, 0));
        var rasterizationScale = this.Content.XamlRoot.RasterizationScale;
        var isRtl = this.MorphicMenuButton.FlowDirection == FlowDirection.RightToLeft;

        // determine which half of the monitor the bar is on, so we can open the menu _away_ from the bar
        var windowPos = this.AppWindow.Position;
        var windowSize = this.AppWindow.Size;
        var windowCenterX = windowPos.X + (windowSize.Width / 2);
        var windowCenterY = windowPos.Y + (windowSize.Height / 2);
        //
        var hMonitor = Windows.Win32.PInvoke.MonitorFromWindow(hwnd, Windows.Win32.Graphics.Gdi.MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        var monitorInfo = new Windows.Win32.Graphics.Gdi.MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Windows.Win32.Graphics.Gdi.MONITORINFO>() };
        Windows.Win32.PInvoke.GetMonitorInfo(hMonitor, ref monitorInfo);
        var monitorCenterX = monitorInfo.rcWork.left + (monitorInfo.rcWork.Width / 2);
        var monitorCenterY = monitorInfo.rcWork.top + (monitorInfo.rcWork.Height / 2);

        // anchor the menu to the corner of the button that opens the menu away from the bar
        double anchorX;
        double anchorY;
        switch (this._orientation)
        {
            case Orientation.Horizontal:
                {
                    // open above if bar is in the bottom half, below if in the top half
                    bool openAbove = windowCenterY > monitorCenterY;
                    anchorX = isRtl
                        ? buttonPosition.X + this.MorphicMenuButton.ActualWidth
                        : buttonPosition.X;
                    anchorY = openAbove
                        ? buttonPosition.Y
                        : buttonPosition.Y + this.MorphicMenuButton.ActualHeight;
                }
                break;
            case Orientation.Vertical:
                {
                    // open to the left if bar is on the right half, to the right if on the left half
                    bool openLeft = windowCenterX > monitorCenterX;
                    anchorX = openLeft
                        ? buttonPosition.X
                        : buttonPosition.X + this.MorphicMenuButton.ActualWidth;
                    anchorY = buttonPosition.Y;
                }
                break;
            default:
                anchorX = buttonPosition.X;
                anchorY = buttonPosition.Y;
                break;
        }

        // convert from DIPs to physical pixels, then to screen coordinates
        var clientPoint = new System.Drawing.Point(
            (int)(anchorX * rasterizationScale),
            (int)(anchorY * rasterizationScale));
        _ = Windows.Win32.PInvoke.ClientToScreen(hwnd, ref clientPoint);

        // NOTE: always show the menu via the transparent window to avoid XamlRoot conflicts
        // (a MenuFlyout can only be associated with one XamlRoot at a time).
        // returnFocusTo: the logo button is the originating control for keyboard menu invocations
        // (Space/Enter on the logo). Restoring focus to it on menu close keeps keyboard navigation
        // coherent: ESC dismisses the menu and the user is back on the logo button, ready to
        // re-open the menu, tab to the bar items, or press Esc again to send focus elsewhere.
        App.MainMenu.Show(App.MenuOwnerWindow, this.Visible, clientPoint.X, clientPoint.Y, returnFocusTo: this.MorphicMenuButton);
    }

    /* layout methods */

    // Single-source margin values for the bar items panel per orientation. Consumed by
    // UpdateBarItemsPanelLayout (to apply the live Margin) and by MeasureBarForOrientation
    // (to compose the bar's desired size for an arbitrary orientation without mutating the live
    // panel). If we ever expose these to XAML too, route XAML through the same source.
    //
    // Vertical mode's 25px top reserves space for the CloseButton, which overlaps the items
    // panel area at the top-right corner.
    private static Thickness GetBarItemsPanelMargin(Orientation orientation)
    {
        return orientation switch
        {
            Orientation.Horizontal => MorphicBarWindowMetrics.ItemsPanelMarginHorizontal,
            Orientation.Vertical => MorphicBarWindowMetrics.ItemsPanelMarginVertical,
            _ => throw new System.ComponentModel.InvalidEnumArgumentException(
                nameof(orientation), (int)orientation, orientation.GetType()),
        };
    }

    // Single-source margin values for the Morphic logo (menu) button per orientation. Same
    // consumer story as GetBarItemsPanelMargin above.
    private static Thickness GetMorphicMenuButtonMargin(Orientation orientation)
    {
        return orientation switch
        {
            Orientation.Horizontal => MorphicBarWindowMetrics.MenuButtonMarginHorizontal,
            Orientation.Vertical => MorphicBarWindowMetrics.MenuButtonMarginVertical,
            _ => throw new System.ComponentModel.InvalidEnumArgumentException(
                nameof(orientation), (int)orientation, orientation.GetType()),
        };
    }
	
	//

    // Flows the bar items vertically or horizontally to follow the bar's orientation, and propagates
    // the orientation down to each IBarItemControl child so the items can adapt their own layout.
    private void UpdateBarItemsPanelLayout(Orientation orientation)
    {
        switch (orientation)
        {
            case Orientation.Horizontal:
                this.BarItemsPanel.Orientation = Orientation.Horizontal;
                // column 0 only (leave column 1 for the logo button, column 2 for the close button)
                Grid.SetColumn(this.BarItemsPanel, 0);
                Grid.SetColumnSpan(this.BarItemsPanel, 1);
                Grid.SetRow(this.BarItemsPanel, 0);
                Grid.SetRowSpan(this.BarItemsPanel, 1);
                break;
            case Orientation.Vertical:
                this.BarItemsPanel.Orientation = Orientation.Vertical;
                // span all three columns on row 0; logo button sits below in row 1
                Grid.SetColumn(this.BarItemsPanel, 0);
                Grid.SetColumnSpan(this.BarItemsPanel, 3);
                Grid.SetRow(this.BarItemsPanel, 0);
                Grid.SetRowSpan(this.BarItemsPanel, 1);
                break;
            default:
                throw new System.ComponentModel.InvalidEnumArgumentException(
                    nameof(orientation), (int)orientation, orientation.GetType());
        }
        this.BarItemsPanel.Margin = GetBarItemsPanelMargin(orientation);

        foreach (var child in this.BarItemsPanel.Children)
        {
            if (child is IBarItemControl barItem)
            {
                barItem.Orientation = orientation;
            }
        }
    }

    // NOTE: this was attempted using pure XAML and an 'orientation' trigger, but ultimately this layout worked better when set manually from code
    private void UpdateMorphicMenuButtonLayout(Orientation orientation)
    {
        switch (orientation)
        {
            case Orientation.Horizontal:
                // Morphic logo (menu) button in column 1, to the left of the close button (right if rtl)
                Grid.SetColumn(this.MorphicMenuButton, 1);
                Grid.SetColumnSpan(this.MorphicMenuButton, 1);
                Grid.SetRow(this.MorphicMenuButton, 0);
                this.LogoColumn.Width = GridLength.Auto;
                this.LogoRow.Height = new GridLength(0);
                this.MorphicMenuButton.HorizontalAlignment = HorizontalAlignment.Center;
                this.MorphicMenuButton.VerticalAlignment = VerticalAlignment.Center;
                break;
            case Orientation.Vertical:
                // Morphic logo (menu) button in row 1 at the bottom, spanning all columns
                Grid.SetColumn(this.MorphicMenuButton, 0);
                Grid.SetColumnSpan(this.MorphicMenuButton, 3);
                Grid.SetRow(this.MorphicMenuButton, 1);
                this.LogoColumn.Width = new GridLength(0);
                this.LogoRow.Height = GridLength.Auto;
                this.MorphicMenuButton.HorizontalAlignment = HorizontalAlignment.Center;
                this.MorphicMenuButton.VerticalAlignment = VerticalAlignment.Center;
                break;
        }
        this.MorphicMenuButton.Margin = GetMorphicMenuButtonMargin(orientation);
    }

    /* helper methods */

    private void UpdateAppWindowSizeUsingRasterizationScale(double rasterizationScale)
    {
        int physicalWidth;
        int physicalHeight;
        switch (_orientation)
        {
            case Orientation.Horizontal:
                physicalWidth = (int)(_logicalLength * rasterizationScale);
                physicalHeight = (int)(_logicalThickness * rasterizationScale);
                break;
            case Orientation.Vertical:
                physicalWidth = (int)(_logicalThickness * rasterizationScale);
                physicalHeight = (int)(_logicalLength * rasterizationScale);
                break;
            default:
                throw new Exception("invalid code path");
        }

        this.AppWindow.Resize(new Windows.Graphics.SizeInt32(physicalWidth, physicalHeight));
    }

    // Measures the bar (via MeasureBarForOrientation) for the supplied monitor at the bar's
    // current orientation, then applies the result via Resize. Use this wherever the bar needs to
    // re-fit its contents: after items change, after orientation change, after a DPI/rasterization
    // change on the same monitor, or before/after a monitor switch (the caller is responsible for
    // having already set _orientation to the desired value).
	//
    // Convenience wrapper around AnimateMoveTo for the common case of "stay where we are, just
    // re-fit to whatever size the bar's contents now want." Uses the cached current monitor,
    // current orientation, and current docking location, so the bar visually stays put except
    // for the size change and any docking-edge realignment caused by the size change.
    //
    // Duration defaults to TimeSpan.Zero (snap, no animation). Pass a non-zero duration for a
    // smooth transition (e.g. ~150ms for an orientation flip).
    internal void MeasureAndResize(TimeSpan duration = default)
    {
        this.AnimateMoveTo(this.GetVerifiedCurrentMonitorHandle(), _orientation, _dockingLocation, duration);
    }

    // Two-pass measurement of the bar's items, returning both the bar's logical size and the
    // count of items that fit on the target monitor at the target orientation.
    //
    // Why two passes: a naive single-pass measurement with availableSize = (infinity, infinity)
    // assumes items don't wrap. But once the bar's actual thickness is determined (from the
    // widest item), any item whose text exceeds that thickness wraps to multiple lines and
    // becomes TALLER than the single-pass measurement reported. Trimming based on the underestimate
    // lets too many items in, and the rendered bar overflows.
    //
    // Pass 1: measure every item with availableSize = (infinity, infinity) -> get the natural
    //         maximum item thickness. Combine with chrome to determine the bar's effective
    //         thickness, capped to the working area.
    // Pass 2: re-measure every item with availableSize constrained on the thickness axis (the
    //         bar's effective thickness minus margins). Items wrap as they would in the live bar.
    //         Sum the resulting per-item lengths and compute how many fit.
    //
    // The orientation argument may differ from this.Orientation (e.g. during drag-preview), in
    // which case items are measured via IBarItemControl.MeasureForOrientation, which composes
    // for the requested orientation without mutating the live items.
    //
    // If items aren't all Loaded yet (their templates may not have applied, so Measure would
    // return zero/incomplete values), returns the current cached size fields and a "no trim"
    // fitting count -- the eventual post-Loaded MeasureAndResize will run the real measurement.
    internal (uint logicalLength, uint logicalThickness, int fittingCount) MeasureBarForOrientation(
        Windows.Win32.Graphics.Gdi.HMONITOR hMonitor,
        Microsoft.UI.Xaml.Controls.Orientation orientation)
    {
        if (this.AllBarItemsLoaded() == false)
        {
            // measurements would be unreliable; preserve current size and tell caller everything fits
            return (_logicalLength, _logicalThickness, _allBarItemControls.Count);
        }

        // ----- monitor working area in logical pixels (the per-axis caps) -----
        var monitorInfo = new Windows.Win32.Graphics.Gdi.MONITORINFO();
        monitorInfo.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Windows.Win32.Graphics.Gdi.MONITORINFO>();
        var getMonitorInfoResult = Windows.Win32.PInvoke.GetMonitorInfo(hMonitor, ref monitorInfo);
        double rasterizationScale = 1.0;
        double logicalAvailableWidth = double.PositiveInfinity;
        double logicalAvailableHeight = double.PositiveInfinity;
        if (getMonitorInfoResult != 0)
        {
            var getRasterizationScaleResult = LayoutUtils.GetRasterizationScaleForMonitor(hMonitor);
            if (getRasterizationScaleResult.IsSuccess) { rasterizationScale = getRasterizationScaleResult.Value; }
            int keepaway = LayoutUtils.WINDOW_CORNER_DOCKING_DISTANCE_FROM_SCREEN_EDGE_IN_DEVICE_UNITS;
            logicalAvailableWidth = System.Math.Max(0, (monitorInfo.rcWork.Width / rasterizationScale) - (2 * keepaway));
            logicalAvailableHeight = System.Math.Max(0, (monitorInfo.rcWork.Height / rasterizationScale) - (2 * keepaway));
        }
        else
        {
            Debug.Assert(false, "Could not get current monitor (to measure working area); fell back to 'infinite' screen size and 100% rasterization scale");
        }
		
        double logicalAvailableLength = (orientation == Orientation.Horizontal) ? logicalAvailableWidth : logicalAvailableHeight;
        double logicalAvailableThickness = (orientation == Orientation.Horizontal) ? logicalAvailableHeight : logicalAvailableWidth;
        var fullAvailableSize = new Windows.Foundation.Size(logicalAvailableWidth, logicalAvailableHeight);

        // ----- chrome measurement (logo + close button); same approach as before -----
        this.MorphicMenuButton.Measure(fullAvailableSize);
        var liveLogoMargin = this.MorphicMenuButton.Margin;
        double logoNaturalWidth = System.Math.Max(0, this.MorphicMenuButton.DesiredSize.Width - (liveLogoMargin.Left + liveLogoMargin.Right));
        double logoNaturalHeight = System.Math.Max(0, this.MorphicMenuButton.DesiredSize.Height - (liveLogoMargin.Top + liveLogoMargin.Bottom));
        var requestedLogoMargin = GetMorphicMenuButtonMargin(orientation);
        double logoWidthWithMargin = logoNaturalWidth + requestedLogoMargin.Left + requestedLogoMargin.Right;
        double logoHeightWithMargin = logoNaturalHeight + requestedLogoMargin.Top + requestedLogoMargin.Bottom;

        this.CloseButton.Measure(fullAvailableSize);
        double closeWidth = this.CloseButton.DesiredSize.Width;
        double closeHeight = this.CloseButton.DesiredSize.Height;

        // ----- items panel margin (added once to bar's length and thickness) -----
        var itemsPanelMargin = GetBarItemsPanelMargin(orientation);
        double itemsPanelMarginAlongLength = (orientation == Orientation.Horizontal)
            ? itemsPanelMargin.Left + itemsPanelMargin.Right
            : itemsPanelMargin.Top + itemsPanelMargin.Bottom;
        double itemsPanelMarginAlongThickness = (orientation == Orientation.Horizontal)
            ? itemsPanelMargin.Top + itemsPanelMargin.Bottom
            : itemsPanelMargin.Left + itemsPanelMargin.Right;

        // ----- Pass 1: measure each item at full available size to find max natural thickness -----
        // NOTE: Visibility=Collapsed elements return DesiredSize=(0,0) from Measure (the framework
        // short-circuits MeasureCore for them). To get accurate measurements for currently-Collapsed
        // (trimmed-out) items, we temporarily flip them to Visible, measure, then restore. The
        // toggles happen synchronously within this method so no layout pass runs in between,
        // and the user-visible Visibility state ends up identical to what it was on entry.
        double maxItemNaturalThickness = 0;
        foreach (var control in _allBarItemControls)
        {
            if (control is IBarItemControl item)
            {
                var savedVisibility = control.Visibility;
                if (savedVisibility != Visibility.Visible) { control.Visibility = Visibility.Visible; }

                var natural = item.MeasureForOrientation(fullAvailableSize, orientation);
                double thicknessContribution = (orientation == Orientation.Horizontal) ? natural.Height : natural.Width;
                if (thicknessContribution > maxItemNaturalThickness) { maxItemNaturalThickness = thicknessContribution; }

                if (control.Visibility != savedVisibility) { control.Visibility = savedVisibility; }
            }
        }

        // ----- bar's outer Border eats inner space on each axis (BorderThickness=1 logical, so
        //       2 total per axis). Items, chrome, and margins all live INSIDE that border, so
        //       both Pass 2's thickness constraint and the trim's length cap have to subtract it.
        //       The returned outer dimensions (barLength, barThickness) add it back so AppWindow
        //       is sized to include the border. -----
        double barBorderThicknessPerSide = MorphicBarWindowMetrics.BarBorderThicknessPerSide;
        double barBorderThicknessBothSides = barBorderThicknessPerSide * 2;

        // ----- compose the bar's effective INNER thickness (capped to working area minus border) -----
        // thickness axis composition: max(items + items panel margin, logo with margin, close)
        double logoThickness = (orientation == Orientation.Horizontal) ? logoHeightWithMargin : logoWidthWithMargin;
        double closeThickness = (orientation == Orientation.Horizontal) ? closeHeight : closeWidth;
        double itemsPanelNaturalThickness = maxItemNaturalThickness + itemsPanelMarginAlongThickness;
        double innerBarThickness = System.Math.Max(itemsPanelNaturalThickness, System.Math.Max(logoThickness, closeThickness));
        double innerThicknessCap = System.Math.Max(0, logicalAvailableThickness - barBorderThicknessBothSides);
        if (innerBarThickness > innerThicknessCap) { innerBarThickness = innerThicknessCap; }

        // ----- Pass 2: re-measure each item with the bar's effective thickness as cross-axis cap -----
        // items get availableSize.thickness = bar's INNER thickness minus items-panel margin
        double itemsAvailableThickness = System.Math.Max(0, innerBarThickness - itemsPanelMarginAlongThickness);
        var constrainedAvailableSize = (orientation == Orientation.Horizontal)
            ? new Windows.Foundation.Size(double.PositiveInfinity, itemsAvailableThickness)
            : new Windows.Foundation.Size(itemsAvailableThickness, double.PositiveInfinity);

        // ----- chrome length (for the bar's length-axis composition + the trim cap) -----
        double chromeLength = (orientation == Orientation.Horizontal)
            ? logoWidthWithMargin + closeWidth   // horizontal: logo and close both add to length
            : logoHeightWithMargin;              // vertical: close overlaps items area via top margin
        // items get inner length = working-area-cap - border - chrome - margin
        double availableForItemsContent = System.Math.Max(0, logicalAvailableLength - barBorderThicknessBothSides - chromeLength - itemsPanelMarginAlongLength);

        // walk items in order, accumulating length and counting how many fit.
        // NOTE: same Visibility-toggle pattern as Pass 1: Collapsed elements return DesiredSize=(0,0)
        // from Measure, so trim would silently let everything through if we measured them collapsed.
        // Toggle synchronously so no layout pass runs with the intermediate state visible.
        double spacing = this.BarItemsPanel.Spacing;
        double cumulativeItemsLength = 0;
        int fittingCount = 0;
        for (int i = 0; i < _allBarItemControls.Count; i++)
        {
            var control = _allBarItemControls[i];
            if (control is not IBarItemControl item) { continue; }

            var savedVisibility = control.Visibility;
            if (savedVisibility != Visibility.Visible) { control.Visibility = Visibility.Visible; }

            var constrained = item.MeasureForOrientation(constrainedAvailableSize, orientation);

            if (control.Visibility != savedVisibility) { control.Visibility = savedVisibility; }

            // Math.Ceiling per item: WinUI's LayoutRounding rounds each item's painted position to
            // an integer pixel at the current rasterization scale. With fractional per-item widths
            // (e.g. an AutoSize multi-button group whose buttons measure to non-integer DesiredSize
            // values), the cumulative offset of items can drift up to ~0.5 logical px per item.
            // Across several items that drift accumulates enough to push the rightmost item's
            // painted right edge past the bar's reserved length, clipping the last item's right-side
            // rounded corner. Ceiling here absorbs that drift -- each item gets up to 1 logical px
            // of slack in the bar's overall budget, so the bar reserves a touch more than the raw
            // DesiredSize sum and the last item paints fully.
            double rawItemLength = (orientation == Orientation.Horizontal) ? constrained.Width : constrained.Height;
            double itemLength = System.Math.Ceiling(rawItemLength);
            double additional = itemLength + (i > 0 ? spacing : 0);
            if (cumulativeItemsLength + additional <= availableForItemsContent)
            {
                cumulativeItemsLength += additional;
                fittingCount = i + 1;
            }
            else
            {
                break;
            }
        }

        // bar's OUTER logical length (= AppWindow size): inner content + border
        double innerBarLength = cumulativeItemsLength + chromeLength + itemsPanelMarginAlongLength;
        double outerBarLength = innerBarLength + barBorderThicknessBothSides;
        double outerBarThickness = innerBarThickness + barBorderThicknessBothSides;

        return ((uint)System.Math.Ceiling(outerBarLength), (uint)System.Math.Ceiling(outerBarThickness), fittingCount);
    }

    // Returns the cached current-monitor handle if it still points at a live monitor (probed via
    // GetMonitorInfo, which returns zero when the monitor is no longer attached). If the cache is
    // stale (e.g. the display was disconnected), falls back to whatever monitor the window is
    // currently on, refreshes the cache, and returns the new handle.
    private Windows.Win32.Graphics.Gdi.HMONITOR GetVerifiedCurrentMonitorHandle()
    {
        var monitorInfo = new Windows.Win32.Graphics.Gdi.MONITORINFO();
        monitorInfo.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Windows.Win32.Graphics.Gdi.MONITORINFO>();
        if (Windows.Win32.PInvoke.GetMonitorInfo(_currentMonitorHandle, ref monitorInfo) != 0)
        {
            return _currentMonitorHandle;
        }

        var hwnd = (Windows.Win32.Foundation.HWND)WinRT.Interop.WindowNative.GetWindowHandle(this);
        _currentMonitorHandle = Windows.Win32.PInvoke.MonitorFromWindow(
            hwnd,
            Windows.Win32.Graphics.Gdi.MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        return _currentMonitorHandle;
    }

    private void InitializeBorderlessWindowProperties(Windows.Win32.Foundation.HWND hwnd)
    {
        // remove window chrome (minimize/maximize/close buttons); set the window to be 'always on top'
        var presenter = this.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
        if (presenter is not null)
        {
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
            presenter.IsResizable = false;
            presenter.IsAlwaysOnTop = true;
        }

        // update window style (i.e. remove the dialog frame); with our styling, this is required to prevent the window from growing beyond our target size
        var style = (Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE)Windows.Win32.PInvoke.GetWindowLongPtr(hwnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        style &= ~Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_DLGFRAME;
        //
        // NOTE: SetWindowLongPtr can return 0 even if there is no error; see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowlongptrw
        System.Runtime.InteropServices.Marshal.SetLastPInvokeError(0);
        var setWindowLongPtrResult = Windows.Win32.PInvoke.SetWindowLongPtr(hwnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_STYLE, (nint)style);
        if (setWindowLongPtrResult == 0)
        {
            var win32ErrorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            if (win32ErrorCode != 0)
            {
                System.Diagnostics.Debug.Assert(false);
            }
        }

        // notify Windows that the frame has changed
        var setWindowPosResult = Windows.Win32.PInvoke.SetWindowPos(hwnd, Windows.Win32.Foundation.HWND.Null, 0, 0, 0, 0,
            Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED | Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOMOVE |
            Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOSIZE | Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOZORDER);
        System.Diagnostics.Debug.Assert(setWindowPosResult != 0);

        // turn off DWM corner rounding
        int cornerPreference = (int)Windows.Win32.Graphics.Dwm.DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND;
        Span<byte> cornerPreferenceAsSpan = System.Runtime.InteropServices.MemoryMarshal.AsBytes(new Span<int>(ref cornerPreference));
        var setAttributeResult = Windows.Win32.PInvoke.DwmSetWindowAttribute(hwnd, Windows.Win32.Graphics.Dwm.DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, cornerPreferenceAsSpan);
        System.Diagnostics.Debug.Assert(setAttributeResult == Windows.Win32.Foundation.HRESULT.S_OK);
    }

    private void InitializePointerPressAndDrag(UIElement rootDragElement)
    {
        rootDragElement.PointerPressed += (s, e) =>
        {
            var isLeftButtonPressed = e.GetCurrentPoint(null).Properties.IsLeftButtonPressed;
            if (isLeftButtonPressed)
            {
                // Suppress focus upgrades for 500ms covering the post-WA_ACTIVE deferred update.
                // First-click-on-bar fires WA_ACTIVE (not WA_CLICKACTIVE), and our deferred update
                // would otherwise put a keyboard ring on the first button (either by upgrading an
                // existing Pointer-state focus, or by taking the initial-focus path with Keyboard
                // when the focused element is in a popup-style subtree IsBarOwnedElement misses).
                this.SuppressFocusUpgradeFor(TimeSpan.FromMilliseconds(500));

                // Cancel any in-flight move animation from a PREVIOUS drag's release. Without this,
                // starting a new drag while the previous drag's settle-animation would cause a visual
                // oscillation between the user's drag position and the animation's current position.
                this.AnimateStop();

                _isDraggingWindow = true;

                // capture the window's current position (i.e. at the time that we start the drag)
                _dragStartWindowPosition = this.AppWindow.Position;
                //
                // capture the mouse cursor's position in virtual screen coordinate space
                var getCursorPosResult = Windows.Win32.PInvoke.GetCursorPos(out var startPointerPosition);
                System.Diagnostics.Debug.Assert(getCursorPosResult != 0);
                _dragStartPointerPosition = new Windows.Foundation.Point(startPointerPosition.X, startPointerPosition.Y);
                //
                // capture (corner, orientation) at drag start so the accidental-drag gate can
                // detect small same-corner orientation flips; reset the sticky gate flag so the
                // first PointerMoved tick of this drag re-evaluates IsAccidentalDragOperation.
                _dragStartDockedState = (_dockingLocation, _orientation);
                _dragStopwatch = System.Diagnostics.Stopwatch.StartNew();
                _dragHasPassedAccidentalGate = false;

                // Stop any prior bypass timer defensively in case a previous PointerReleased was
                // missed (e.g., capture loss without release).
                _accidentalDragBypassTimer?.Stop();
				//
                // Start (or restart) the bypass timer. Fires once at the hold threshold so the
                // preview appears at the bypass point even if the mouse stops moving.
                _accidentalDragBypassTimer = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().CreateTimer();
                _accidentalDragBypassTimer.Interval = ACCIDENTAL_DRAG_HOLD_THRESHOLD;
                _accidentalDragBypassTimer.IsRepeating = false;
                _accidentalDragBypassTimer.Tick += this.OnAccidentalDragBypassTimerTick;
                _accidentalDragBypassTimer.Start();
                //
                // capture the pointer with WinUI (so that we can capture PointerMoved and PointerReleased events
                rootDragElement.CapturePointer(e.Pointer);
            }
        };
        rootDragElement.PointerMoved += (s, e) =>
        {
            if (_isDraggingWindow)
            {
                // get the updated cursor position
                var getCursorPosResult = Windows.Win32.PInvoke.GetCursorPos(out var currentPointerPosition);

                var deltaX = currentPointerPosition.X - _dragStartPointerPosition.X;
                var deltaY = currentPointerPosition.Y - _dragStartPointerPosition.Y;
                var newLeft = (int)(_dragStartWindowPosition.X + deltaX);
                var newTop = (int)(_dragStartWindowPosition.Y + deltaY);
                this.AppWindow.Move(new Windows.Graphics.PointInt32(
                    newLeft,
                    newTop
                ));

                // determine if/where we should show the layout preview window
                var newCenterX = newLeft + (this.AppWindow.Size.Width / 2);
                var newCenterY = newTop + (this.AppWindow.Size.Height / 2);
                this.UpdateLayoutPreviewState(currentPointerPosition, new System.Drawing.Point(newCenterX, newCenterY), _orientation);
            }
        };
        rootDragElement.PointerReleased += (s, e) =>
        {
            _isDraggingWindow = false;
            rootDragElement.ReleasePointerCapture(e.Pointer);

            if (_layoutPreviewWindow.Visible == true)
            {
                _layoutPreviewWindow.AppWindow.Hide();
            }

            // capture the mouse cursor's position in virtual screen coordinate space
            _ = Windows.Win32.PInvoke.GetCursorPos(out var currentPointerPosition);
            var hMonitor = Windows.Win32.PInvoke.MonitorFromPoint(currentPointerPosition, Windows.Win32.Graphics.Gdi.MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
            if (hMonitor.IsNull)
            {
                Debug.Assert(false, "No monitor handle; this might be a headless system; aborting.");
                return;
            }
            // pick the target orientation and docking location: prefer whatever the layout preview
            // had locked in during the drag; fall back to the current values if the user didn't drag
            // far enough to trigger a preview change
            var targetOrientation = _layoutPreviewWindowOrientation ?? _orientation;
            var targetDockingLocation = _layoutPreviewDockingLocation ?? _dockingLocation;

            // If orientation is flipping, ask AnimateMoveTo to snap-resize at the cursor BEFORE
            // animating. AnimateMoveTo's snap uses the actual MEASURED target dimensions (not just
            // naively swapped width/height), so the subsequent animation only interpolates position
            // and the bar never appears to "grow" mid-flight to its final size.
            System.Drawing.Point? snapResizeAtPoint = (_orientation != targetOrientation)
                ? currentPointerPosition
                : (System.Drawing.Point?)null;

            // AnimateMoveTo handles the orientation change (via the property setter), updates the
            // docking location, measures for the new state, optionally snap-resizes at the cursor,
            // and animates position + size to the docking location.
            this.AnimateMoveTo(hMonitor, targetOrientation, targetDockingLocation, new TimeSpan(0, 0, 1), snapResizeAtPoint);

            _layoutPreviewWindowOrientation = null;
            _layoutPreviewDockingLocation = null;

            // clear the drag-only state back to "not in use" so a stray read between drags
            // surfaces immediately (NRE on .Value) instead of silently picking up stale data
            _dragStartDockedState = null;
            _dragStopwatch = null;

            // stop the bypass timer (safe if already fired -- single-shot Stop() is idempotent)
            _accidentalDragBypassTimer?.Stop();
            _accidentalDragBypassTimer = null;
        };
    }


    // Returns true when the in-progress drag is a likely accident the user did not intend to
    // commit AND the drag distance is below a small threshold. Two distinct accidental-flip
    // scenarios are recognized, both characterized by an orientation flip relative to the bar's
    // start state:
    //   A) SAME docking location with OPPOSITE orientation -- the geometric-adjacency flip
    //      within a corner (e.g., a horizontal bar in the bottom-right corner gets nudged into
    //      the "vertical in bottom-right" drop zone, because those zones are adjacent on the
    //      screen).
    //   B) DIFFERENT docking location with OPPOSITE orientation -- the cross-docking flip
    //      (e.g., a full-width horizontal bar docked along the bottom edge gets nudged into the 
    //      "vertical docked along the left edge" drop zone, flipping both the docking location 
    //      and the orientation).
    // Returns false in any of these cases:
    //   * no drag is in progress (defensive; caller should not be invoking us then);
    //   * the proposed orientation matches the start orientation (no flip means not the
    //     accidental case -- same-orientation drags are intentional gestures, and the existing
    //     code's same-corner same-orientation path is a no-op anyway);
    //   * the drag diagonal distance has exceeded the threshold (intentional gesture).
    private bool IsAccidentalDragOperation(
        System.Drawing.Point currentPointerPosition,
        Morphic.MorphicBar.DockingLocation proposedDockingLocation,
        Microsoft.UI.Xaml.Controls.Orientation proposedOrientation,
        double rasterizationScale,
        Windows.Win32.Foundation.RECT monitorFullRect)
    {
        if (_dragStartDockedState is not { } dragStart)
        {
            // defensive: no drag in progress, so nothing to compare against
            return false;
        }

        // Time-based bypass: after the hold threshold elapses, the gate is disabled entirely
        // (any flip is treated as intentional). Lets users who deliberately hold-and-drag
        // commit a flip even within the small-distance range.
        if (_dragStopwatch is { } dragStopwatch)
        {
            TimeSpan elapsed = dragStopwatch.Elapsed;
            if (elapsed > ACCIDENTAL_DRAG_HOLD_THRESHOLD)
            {
                return false;
            }
        }

        // Both scenarios require an orientation flip; keeping the two conditions explicit
        // documents the user-facing intent even though their union collapses to "orientation
        // has flipped" mathematically.
        bool isSameDockingLocationFlip = (proposedDockingLocation == dragStart.DockingLocation)
                              && (proposedOrientation != dragStart.Orientation);
        // Cross-docking-location accidental flips only matter between fixed-margin docks --
        // only those four positions are geometrically adjacent enough that a small drag can
        // accidentally cross from one to another with an orientation flip. Floating docks
        // don't share an edge with each other, so a cross-dock change involving them implies
        // a sizable, intentional drag. This calculation assumes a four-sided rectangle
        // (i.e. a display).
        bool isCrossDockingLocationFlip = false;
        if (dragStart.DockingLocation.IsFixedDockingLocation() && proposedDockingLocation.IsFixedDockingLocation())
        {
            isCrossDockingLocationFlip = (proposedDockingLocation != dragStart.DockingLocation)
                                      && (proposedOrientation != dragStart.Orientation);
        }
        if (isSameDockingLocationFlip == false && isCrossDockingLocationFlip == false)
        {
            return false;
        }

        // Threshold: Max(ACCIDENTAL_DRAG_THRESHOLD_DIPS * rasterizationScale,
        //                ACCIDENTAL_DRAG_THRESHOLD_MONITOR_DIAGONAL_FRACTION * monitorDiagonalPhysical).
        // The DIPs term gives an absolute "small gesture" floor that scales naturally with DPI; the
        // monitor-fraction term gives screen-size scaling so tiny screens don't get tighter and huge
        // screens don't get looser than feels right. Max ensures the threshold is at least the
        // larger of the two.
        double monitorWidthPhysical = monitorFullRect.right - monitorFullRect.left;
        double monitorHeightPhysical = monitorFullRect.bottom - monitorFullRect.top;
        double monitorDiagonalPhysical = System.Math.Sqrt(
            (monitorWidthPhysical * monitorWidthPhysical) + (monitorHeightPhysical * monitorHeightPhysical));
        double dipsBasedThreshold = ACCIDENTAL_DRAG_THRESHOLD_DIPS * rasterizationScale;
        double monitorBasedThreshold = ACCIDENTAL_DRAG_THRESHOLD_MONITOR_DIAGONAL_FRACTION * monitorDiagonalPhysical;
        double thresholdPhysical = System.Math.Max(dipsBasedThreshold, monitorBasedThreshold);

        double deltaX = currentPointerPosition.X - _dragStartPointerPosition.X;
        double deltaY = currentPointerPosition.Y - _dragStartPointerPosition.Y;
        double dragDiagonal = System.Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));

        return dragDiagonal < thresholdPhysical;
    }

    // Fires once when ACCIDENTAL_DRAG_HOLD_THRESHOLD elapses after PointerPressed. Re-evaluates
    // UpdateLayoutPreviewState with the CURRENT cursor + window position so the layout preview
    // appears at the bypass mark without requiring a mouse movement to trigger it. See
    // _accidentalDragBypassTimer field comment for the why.
    private void OnAccidentalDragBypassTimerTick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
    {
        if (_isDraggingWindow == false)
        {
            // defensive: drag ended between Start() and Tick (e.g., PointerReleased fired before Stop()
            // could prevent the Tick from queuing); ignore the stale tick
            return;
        }

        // re-evaluate the layout preview using fresh cursor + current window position. The
        // time-bypass inside IsAccidentalDragOperation will short-circuit the gate now that the
        // threshold has been exceeded, so any flip-zone cursor position will produce a preview.
        var getCursorPosResult = Windows.Win32.PInvoke.GetCursorPos(out var currentPointerPosition);
        if (getCursorPosResult == 0)
        {
            return;
        }
        var windowPosition = this.AppWindow.Position;
        var windowSize = this.AppWindow.Size;
        var windowCenterX = windowPosition.X + (windowSize.Width / 2);
        var windowCenterY = windowPosition.Y + (windowSize.Height / 2);
        this.UpdateLayoutPreviewState(currentPointerPosition, new System.Drawing.Point(windowCenterX, windowCenterY), _orientation);
    }

    private void UpdateLayoutPreviewState(System.Drawing.Point currentPointerPosition, System.Drawing.Point windowCenterPoint, Microsoft.UI.Xaml.Controls.Orientation orientation)
    {
        var ANIMATION_DURATION = new TimeSpan(0, 0, 0, 0, 500);

        // get the current monitor, based on the current mouse cursor relative position
        var hMonitor = Windows.Win32.PInvoke.MonitorFromPoint(currentPointerPosition, Windows.Win32.Graphics.Gdi.MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        if (hMonitor.IsNull)
        {
            Debug.Assert(false, "No monitor handle; this might be a headless system; aborting.");
            return;
        }

        // get the monitor's info (including dimensions)
        var monitorInfo = new Windows.Win32.Graphics.Gdi.MONITORINFO();
        monitorInfo.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Windows.Win32.Graphics.Gdi.MONITORINFO>();
        var getMonitorInfoResult = Windows.Win32.PInvoke.GetMonitorInfo(hMonitor, ref monitorInfo);
        if (getMonitorInfoResult == 0)
        {
            Debug.Assert(false);
            return;
        }

        // get the absolute monitor size (including taskbar, etc.)
        var monitorFullRect = monitorInfo.rcMonitor;
        // and get the working area (excluding taskbar, etc.)
        var monitorWorkingArea = monitorInfo.rcWork;

        // get the RasterizationScale for this monitor
        var getRasterizationScaleResult = LayoutUtils.GetRasterizationScaleForMonitor(hMonitor);
        if (getRasterizationScaleResult.IsError)
        {
            return;
        }
        double rasterizationScale = getRasterizationScaleResult!.Value;

        /* determine target area on monitor (in coords) where the bar will go when released */
        
        // select which 'center' point will be used to determine whether a MorphicBar will be docked left/right or top/bottom depends on the current MorphicBar orientation
        System.Drawing.Point horizontalDockingCenterPoint;
        System.Drawing.Point verticalDockingCenterPoint;
        if (orientation == Orientation.Horizontal)
        {
            // if the orientation is horiontal:
            // - windowCenterPoint determines HORIZONTAL DOCKING
            // - currentPointerPos determines VERTICAL DOCKING
            horizontalDockingCenterPoint = windowCenterPoint;
            verticalDockingCenterPoint = currentPointerPosition;
        }
        else
        {
            // if the orientation is vertical:
            // - windowCenterPoint determines VERTICAL DOCKING
            // - currentPointerPos determines HORIZONTAL DOCKING
            horizontalDockingCenterPoint = currentPointerPosition;
            verticalDockingCenterPoint = windowCenterPoint;
        }

        // calculate the preview window rect using the already-gathered data points
        var verticalBarDockingHitAreaWidth = _logicalThickness;
        var calculatePreviewWindowRectResult = LayoutUtils.CalculatePreviewDockingLocation(monitorFullRect, monitorWorkingArea, currentPointerPosition, horizontalDockingCenterPoint, verticalDockingCenterPoint, rasterizationScale, verticalBarDockingHitAreaWidth);
        if (calculatePreviewWindowRectResult.IsError)
        {
            return;
        }
        var newPreviewDockingLocation = calculatePreviewWindowRectResult.Value!.DockingLocation;
        var newPreviewOrientation = calculatePreviewWindowRectResult.Value!.Orientation;

        // Accidental-drag suppression: if the drag is a small motion that would land in an
        // "accidental flip" zone (same-corner-with-opposite-orientation, or cross-edge-fixed-dock
        // with opposite-orientation), suppress the preview entirely and clear the preview state.
        // PointerReleased's `?? _dockingLocation` / `?? _orientation` fallbacks then naturally
        // land on the bar's starting state, so the bar snaps back without rotating.
        //
        // Sticky ratchet: once IsAccidentalDragOperation returns false even once during this
        // drag, set _dragHasPassedAccidentalGate so subsequent ticks skip the check entirely.
        // Prevents preview flicker if the user drags back into the short-range zone during a
        // clearly intentional drag. The ratchet resets to false on the next PointerPressed.
        if (!_dragHasPassedAccidentalGate)
        {
            if (this.IsAccidentalDragOperation(currentPointerPosition, newPreviewDockingLocation, newPreviewOrientation, rasterizationScale, monitorFullRect) == true)
            {
                // No need to null _layoutPreviewDockingLocation / _layoutPreviewWindowOrientation
                // here: while the ratchet is unlatched (this branch), they're guaranteed to still
                // be null from PointerReleased's cleanup, because non-null assignment to them only
                // happens further down this method AFTER the ratchet latches.
                return;
            }

            // IsAccidentalDragOperation returned false. That could mean either:
            //   (a) proposed (corner, orientation) is IDENTICAL to start -- cursor hasn't moved
            //       enough to land in any flip zone yet. Don't latch -- a later tick might still
            //       propose an accidental flip, and we want the gate to evaluate it.
            //   (b) proposed (corner, orientation) DIFFERS from start AND isn't the small-flip
            //       case -- drag has genuinely committed to a change. Latch.
            // The latch signal is "proposed differs from start," NOT "helper returned false,"
            // because the helper conflates both meanings.
            var dragStart = _dragStartDockedState!.Value;
            bool proposedDiffersFromStart = newPreviewDockingLocation != dragStart.DockingLocation
                                         || newPreviewOrientation != dragStart.Orientation;
            if (proposedDiffersFromStart)
            {
                _dragHasPassedAccidentalGate = true;
            }
        }

        // measure the bar's desired size for the TARGET (monitor, orientation), not the bar's
        // current state -- the preview must show what the bar will look like after release:
        //   - if orientation differs, IBarItemControl.MeasureForOrientation gives the cross-orientation
        //     answer without mutating the live items
        //   - if the target monitor's working area is tighter than the current monitor's, the
        //     working-area cap inside MeasureBarForOrientation keeps the preview within bounds
        var (previewLogicalLength, previewLogicalThickness, _) = this.MeasureBarForOrientation(hMonitor, newPreviewOrientation);
        var getRectForDockingLocationResult = LayoutUtils.GetRectForDockingLocation(newPreviewDockingLocation, newPreviewOrientation, previewLogicalLength, previewLogicalThickness, hMonitor);
        if (getRectForDockingLocationResult.IsError)
        {
            return;
        }
        var newPreviewRect = getRectForDockingLocationResult!.Value;

		// NOTE: we should only show the preview window once the MorphicBarWindow window has moved far enough to dock in a different location; therefore, don't
		//       show the layout preview window until the MorphicBar has been moved far enough to warrant it (i.e. docking location or orientation changes)
        if (_layoutPreviewWindow.Visible == false && _layoutPreviewDockingLocation == null && (newPreviewDockingLocation == _dockingLocation && newPreviewOrientation == _orientation))
        {
            // if the window hasn't moved far enough to have a new docking location, don't show the layout window yet
            return;
        }

        _layoutPreviewDockingLocation = newPreviewDockingLocation;
        _layoutPreviewWindowOrientation = newPreviewOrientation;

        // if the layout preview is not already visible, create a small preview window (which will "expand out") and set its initial orientation
        if (_layoutPreviewWindow.Visible == false)
        {
            // calculate a preview "10%-sized" window that can grow into the animated full-size preview window
            var initialSizePercent = 0.10;
            var smallPreviewWidth = (int)(newPreviewRect.Width * initialSizePercent);
            var smallPreviewHeight = (int)(newPreviewRect.Height * initialSizePercent);
            var smallPreviewLeft = newPreviewRect.left + ((newPreviewRect.Width  - smallPreviewWidth) / 2);
            var smallPreviewTop = newPreviewRect.top + ((newPreviewRect.Height - smallPreviewHeight) / 2);

            _lastLayoutPreviewTargetPosition = null;

            _layoutPreviewWindow.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(smallPreviewLeft, smallPreviewTop, smallPreviewWidth, smallPreviewHeight));
            _layoutPreviewWindow.AppWindow.Show();
            Debug.Assert(_layoutPreviewWindow.AppWindow.Position.Y == smallPreviewTop);
        }

        // resize/move the window to its new position (with animation)
        if (_lastLayoutPreviewTargetPosition is null || Windows.Win32.PInvoke.EqualRect(newPreviewRect, _lastLayoutPreviewTargetPosition!.Value) == false)
        {
            _layoutPreviewWindow.AnimateMoveTo(new Windows.Graphics.PointInt32(newPreviewRect.left, newPreviewRect.top), new Windows.Graphics.SizeInt32(newPreviewRect.Width, newPreviewRect.Height), ANIMATION_DURATION);
            // update the layout position
            _lastLayoutPreviewTargetPosition = newPreviewRect;
        }
    }
}
