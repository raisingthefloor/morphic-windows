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

namespace Morphic.MorphicBar;

// Owns the focus policy for a single MorphicBarWindow. The bar window holds an instance of
// this controller via the FocusController property and forwards focus-related signals
// (WM_ACTIVATE arrivals, pointer presses, item-list rebuilds, orientation changes) into
// the corresponding methods here. Other components (MorphicBarManager etc.) interact with
// this controller through the bar window's FocusController property; they never see the
// bar's internal focus state directly.
//
// Responsibilities consolidated here:
//   * Suppression scope that gates the Programmatic/Pointer -> Keyboard focus upgrade
//     (BeginSuppressUpgrade / EndSuppressUpgradeAfterPendingActivations + IsUpgradeSuppressed).
//   * The "initial focus" placement (first focusable bar item, falling back to the
//     Morphic logo button) -- SetInitialFocus.
//   * The WM_ACTIVATE-driven deferred-focus decision tree -- OnWindowActivated.
//   * Brute-force "downgrade any stale Keyboard focus state in the bar" tree walk --
//     DowngradeKeyboardFocusInBar.
//   * Capture / restore of focused-control snapshots across an operation that drops
//     focus (orientation change OR hide/show cycle) -- Capture / Restore.
//   * Predicates the policy needs (IsBarForegroundWindow, IsBarOwnedElement) -- internal
//     to this class.
//
// PrepareForShow bundles the two operations callers like MorphicBarManager.ShowBar need to
// run BEFORE an AppWindow.Show: defuse any stale Keyboard ring AND open a suppression scope
// so the post-Show WM_ACTIVATE doesn't re-acquire one (the caller closes the scope after the
// Show). Callers should prefer the bundle over the individual methods unless they have a
// reason to deviate.
internal sealed class MorphicBarFocusController
{
    private readonly MorphicBarWindow _barWindow;

    // Suppression depth (>0 == suppressed). While suppressed, the RunDeferredFocusUpdate
    // decision tree must NOT upgrade the existing focus state (Programmatic/Pointer) to
    // Keyboard, and must NOT take the "no element focused, place initial focus with Keyboard
    // ring" path. A depth counter (not a wall-clock window) because the deferred focus update
    // is enqueued on the DispatcherQueue: callers bracket the focus-disturbing operation with
    // BeginSuppressUpgrade() ... EndSuppressUpgradeAfterPendingActivations(), and FIFO ordering
    // guarantees the update runs (sees suppression) before the enqueued decrement clears it.
    // UI-thread-confined, so no synchronization is needed.
    private int _suppressUpgradeDepth;

    public MorphicBarFocusController(MorphicBarWindow barWindow)
    {
        _barWindow = barWindow;
    }


    #region Public types

    // Snapshot of the focused-control + FocusState pair, returned by Capture and consumed
    // by Restore so a caller can round-trip focus across an operation that disturbs it.
    // Control == null means "nothing eligible was focused at capture time" -- Restore is a
    // no-op in that case.
    public readonly record struct FocusSnapshot(
        Microsoft.UI.Xaml.Controls.Control? Control,
        Microsoft.UI.Xaml.FocusState State);

    // Capture predicate selector. The orientation-change path uses BarOwnedOnly: it should
    // ignore focus that lives in a popup-style subtree (which won't survive the relayout
    // anyway). The hide/show-cycle path uses AnyInBarXamlRoot: it captures whatever is
    // focused, since after a Show() the popup is gone and the no-op fallback is fine.
    public enum SnapshotScope
    {
        BarOwnedOnly,
        AnyInBarXamlRoot,
    }

    #endregion Public types


    #region Upgrade suppression

    // Enters a suppression scope: while the depth is >0, the deferred WM_ACTIVATE focus update
    // neither promotes an existing focus to Keyboard nor seeds a fresh Keyboard ring. Call
    // immediately BEFORE a focus-disturbing operation (AppWindow.Show, a first-click
    // activation, the hide/await/re-show cycle) and pair with
    // EndSuppressUpgradeAfterPendingActivations once the operation has run.
    public void BeginSuppressUpgrade()
    {
        _suppressUpgradeDepth++;
    }

    // Leaves the suppression scope, but DEFERRED: the decrement is enqueued on the bar's
    // DispatcherQueue so it runs AFTER any focus-update callback that the just-completed
    // operation synchronously enqueued (a local WM_ACTIVATE is delivered inline during the
    // activating call, and OnWindowActivated enqueues the update at that instant). FIFO
    // ordering therefore drains the suppressed focus update first, then clears suppression --
    // no wall-clock guess about "how long until the activation settles."
    public void EndSuppressUpgradeAfterPendingActivations()
    {
        _ = _barWindow.DispatcherQueue.TryEnqueue(() =>
        {
            if (_suppressUpgradeDepth > 0)
            {
                _suppressUpgradeDepth--;
            }
        });
    }

    // True while inside one or more suppression scopes. UI-thread-confined (the writers and
    // this reader all run on the bar's UI thread), so no synchronization is needed.
    public bool IsUpgradeSuppressed => _suppressUpgradeDepth > 0;

    #endregion Upgrade suppression


    #region Initial focus placement

    // Places keyboard focus on a sensible "first interactable" element inside the bar.
    // Order of preference:
    //   1. First focusable descendant of BarItemsPanel (the first inner button of the first
    //      bar item; FocusManager walks the visual tree, so we don't need per-item knowledge
    //      of which inner control to focus).
    //   2. Morphic logo button (always present, used as the fallback when the bar has no
    //      items or none with focusable content).
    //
    // focusState determines whether the focus ring shows:
    //   * FocusState.Keyboard     -> ring shown (only appropriate when the bar genuinely
    //                                has keyboard focus, e.g., user Alt+Tab'd in)
    //   * FocusState.Programmatic -> silent focus, no ring (right for startup-time calls
    //                                where we're seeding focus but the bar isn't necessarily
    //                                foreground)
    //   * FocusState.Pointer      -> silent focus, semantically for mouse activation
    //
    // The default (Programmatic) is the safe choice; the WM_ACTIVATE deferred update passes
    // Keyboard when it has confirmed the bar is the OS-level foreground window.
    public void SetInitialFocus(Microsoft.UI.Xaml.FocusState focusState = Microsoft.UI.Xaml.FocusState.Programmatic)
    {
        // The focus-setting block needs a live visual tree connected to a XamlRoot.
        // FindFirstFocusableElement and Focus() both throw if called before that connection
        // (e.g., from InitializeBarItems during initial setup, before RootGrid_Loaded
        // fires). Gate the work behind XamlRoot availability rather than early-returning so
        // any future non-focus-setting code added to this method would still run.
        if (_barWindow.Content?.XamlRoot is null)
        {
            return;
        }
        var firstFocusable = Microsoft.UI.Xaml.Input.FocusManager.FindFirstFocusableElement(_barWindow.BarItemsPanel);
        if (firstFocusable is Microsoft.UI.Xaml.Controls.Control firstControl)
        {
            _ = firstControl.Focus(focusState);
        }
        else
        {
            // no bar items (or none with focusable content) -> fall back to the Morphic logo button
            _ = _barWindow.MorphicMenuButton.Focus(focusState);
        }
    }

    #endregion Initial focus placement


    #region Activation drive

    // Called from MorphicBarWindow.SubclassWndProc on every WM_ACTIVATE arrival. Always
    // DEFERRED via DispatcherQueue.TryEnqueue because at WM_ACTIVATE time:
    //   * XamlRoot can be null (initial activation arrives before the XAML tree is connected).
    //   * GetForegroundWindow does NOT yet return our HWND (OS hasn't finalized the transition).
    // Both settle by the next dispatcher cycle; re-evaluating from a clean slate handles both.
    //
    // activationState carries the semantic distinction: PointerActivated -> mouse (no ring),
    // CodeActivated -> keyboard/programmatic (ring if foreground). Caller is responsible for
    // translating WM_ACTIVATE's wParam (WA_ACTIVE / WA_CLICKACTIVE) into the right value.
    public void OnWindowActivated(Microsoft.UI.Xaml.WindowActivationState activationState)
    {
        _ = _barWindow.DispatcherQueue.TryEnqueue(() =>
        {
            // Window may have been torn down between schedule and fire. Wrap the whole body --
            // even `_barWindow.Content` getter can throw COMException ("WinUI Desktop Window
            // object has already been closed") if the bar Close()'d after enqueue. Swallow to
            // keep shutdown quiet; nothing here is correctness-critical past teardown.
            try
            {
                this.RunDeferredFocusUpdate(activationState);
            }
            catch (System.Runtime.InteropServices.COMException)
            {
            }
        });
    }

    private void RunDeferredFocusUpdate(Microsoft.UI.Xaml.WindowActivationState activationState)
    {
        if (_barWindow.Content?.XamlRoot is not Microsoft.UI.Xaml.XamlRoot xamlRoot)
        {
            return;
        }

        // Live mouse-button check: if ANY mouse button is currently pressed, the user is mid-
        // click on the bar -- treat as mouse-driven regardless of what WM_ACTIVATE reported.
        // The bar's first-click activation fires WA_ACTIVE (not WA_CLICKACTIVE), so we can't
        // rely on wParam alone. 0x01=LBUTTON, 0x02=RBUTTON, 0x04=MBUTTON; high bit (0x8000)
        // set when down.
        bool mousePressed = (Windows.Win32.PInvoke.GetAsyncKeyState(0x01) & 0x8000) != 0
            || (Windows.Win32.PInvoke.GetAsyncKeyState(0x02) & 0x8000) != 0
            || (Windows.Win32.PInvoke.GetAsyncKeyState(0x04) & 0x8000) != 0;
        System.Diagnostics.Debug.WriteLine($"[Focus] RunDeferredFocusUpdate({activationState}) mousePressed={mousePressed}");
        if (mousePressed)
        {
            activationState = Microsoft.UI.Xaml.WindowActivationState.PointerActivated;
            // Aggressively clear any Keyboard FocusState in the bar tree. WinUI's default-
            // activation focus may have placed Keyboard focus on a button before our deferred
            // update runs, and FocusManager.GetFocusedElement may not return that button
            // (it can return a parent ScrollViewer instead), so we walk the whole tree.
            this.DowngradeKeyboardFocusInBar();
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
        var focused = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(xamlRoot) as Microsoft.UI.Xaml.DependencyObject;
        if (focused is not null && this.IsBarOwnedElement(focused))
        {
            if (focused is Microsoft.UI.Xaml.Controls.Control focusedControl)
            {
                // Promote to Keyboard for keyboard-style activation (CodeActivated) when the bar
                // is foreground and we're not in a suppress window (set by RunWithBarHiddenAsync
                // to cover its own re-Show).
                if (activationState != Microsoft.UI.Xaml.WindowActivationState.PointerActivated
                    && focusedControl.FocusState != Microsoft.UI.Xaml.FocusState.Keyboard
                    && this.IsUpgradeSuppressed == false
                    && this.IsBarForegroundWindow())
                {
                    _ = focusedControl.Focus(Microsoft.UI.Xaml.FocusState.Keyboard);
                }
                // Mirror: DEMOTE to Pointer for mouse-driven activation when the focused control
                // currently has Keyboard FocusState. WinUI's default activation focus places
                // Keyboard focus on the first focusable button as a side effect of WA_CLICKACTIVE,
                // which lights up the keyboard ring on a mouse-driven activation -- exactly wrong.
                // Downgrading to Pointer suppresses the ring (Pointer state renders no ring).
                else if (activationState == Microsoft.UI.Xaml.WindowActivationState.PointerActivated
                    && focusedControl.FocusState == Microsoft.UI.Xaml.FocusState.Keyboard)
                {
                    _ = focusedControl.Focus(Microsoft.UI.Xaml.FocusState.Pointer);
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
        if (this.IsUpgradeSuppressed == true)
        {
            return;
        }
        Microsoft.UI.Xaml.FocusState focusState;
        if (activationState == Microsoft.UI.Xaml.WindowActivationState.PointerActivated)
        {
            // Mouse activation: no ring -- WinUI convention is mouse activation doesn't show
            // the keyboard focus ring.
            focusState = Microsoft.UI.Xaml.FocusState.Pointer;
        }
        else
        {
            // CodeActivated path: show the ring ONLY if this bar window is now the OS-level
            // foreground window. Distinguishes "user Alt+Tab'd to us" (foreground == ourHwnd,
            // ring) from spurious activations where focus-stealing was denied (silent).
            focusState = this.IsBarForegroundWindow()
                ? Microsoft.UI.Xaml.FocusState.Keyboard
                : Microsoft.UI.Xaml.FocusState.Programmatic;
        }
        this.SetInitialFocus(focusState);
    }

    // True if this bar's HWND is the OS-level foreground window right now. Used by
    // RunDeferredFocusUpdate to decide whether to show the keyboard focus ring (only
    // appropriate when WE actually have keyboard focus -- if some other window is
    // foreground, the ring would be a lie).
    private bool IsBarForegroundWindow()
    {
        var barHwnd = (Windows.Win32.Foundation.HWND)WinRT.Interop.WindowNative.GetWindowHandle(_barWindow);
        return Windows.Win32.PInvoke.GetForegroundWindow() == barHwnd;
    }

    #endregion Activation drive


    #region Snapshot capture / restore

    // Captures the currently-focused control + its FocusState according to the supplied
    // scope, so a caller can round-trip focus across an operation that would drop it
    // (orientation change, AppWindow.Hide/Show cycle, DPI-driven relayout). Returns
    // (null, Unfocused) when nothing matches; Restore is a no-op for that snapshot.
    //
    // FocusManager.GetFocusedElement is normally accurate at the leaf level (Button etc.)
    // so it's used directly here -- unlike the brute-force walk in DowngradeKeyboardFocusInBar
    // which has to cope with cases where the framework reports a parent element
    // (ScrollViewer etc.). For capture it's OK to occasionally miss a deep leaf -- the
    // (null, Unfocused) snapshot is the documented fallback.
    public FocusSnapshot Capture(SnapshotScope scope)
    {
        if (_barWindow.Content?.XamlRoot is not Microsoft.UI.Xaml.XamlRoot xamlRoot)
        {
            return new FocusSnapshot(null, Microsoft.UI.Xaml.FocusState.Unfocused);
        }
        if (Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(xamlRoot) is not Microsoft.UI.Xaml.Controls.Control focused)
        {
            return new FocusSnapshot(null, Microsoft.UI.Xaml.FocusState.Unfocused);
        }
        if (scope == SnapshotScope.BarOwnedOnly && this.IsBarOwnedElement(focused) == false)
        {
            // Focused element lives in a popup-style subtree (Menu, Flyout, etc.) that
            // won't survive the operation the caller is about to perform. Returning an
            // empty snapshot keeps Restore from trying to refocus an element whose parent
            // tree is about to be torn down.
            return new FocusSnapshot(null, Microsoft.UI.Xaml.FocusState.Unfocused);
        }
        return new FocusSnapshot(focused, focused.FocusState);
    }

    // Restores focus from a snapshot taken by Capture. No-op when snapshot.Control is null.
    //
    // If the captured state was Unfocused (defensive -- Capture wouldn't normally produce
    // that for a populated snapshot), the restore uses Keyboard so the ring is visible:
    // the typical caller is keyboard-driven (user pressed Text-Size +/- and we want focus
    // to remain visibly on that button across the brief AppWindow.Hide / relayout).
    //
    // The Focus call is DEFERRED via DispatcherQueue.TryEnqueue: callers like
    // RunWithBarHiddenAsync and the Text Size handler run their restore right after a
    // Show()/relayout, and additional layout work may still be queued behind us; running
    // Focus() synchronously gets clobbered by that late settling. Enqueuing puts the
    // Focus() call after the queued layout work.
    public void Restore(FocusSnapshot snapshot)
    {
        if (snapshot.Control is null)
        {
            return;
        }
        var state = (snapshot.State == Microsoft.UI.Xaml.FocusState.Unfocused)
            ? Microsoft.UI.Xaml.FocusState.Keyboard
            : snapshot.State;
        _ = _barWindow.DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                var barHwnd = (Windows.Win32.Foundation.HWND)WinRT.Interop.WindowNative.GetWindowHandle(_barWindow);
                var foreground = Windows.Win32.PInvoke.GetForegroundWindow();
                var isForeground = foreground == barHwnd;
                var result = snapshot.Control.Focus(state);
                System.Diagnostics.Debug.WriteLine($"[Restore] Focus({state}) returned {result}, barIsForeground={isForeground}");
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Restore] COMException: {ex.Message}");
            }
        });
    }

    // Walks the visual-parent chain of `element` to determine whether it lives inside this
    // bar window's content tree. Used by Capture(BarOwnedOnly) to skip focus that lives in
    // a popup-style subtree (Menu/Flyout) that won't survive the operation the caller is
    // about to perform.
    private bool IsBarOwnedElement(Microsoft.UI.Xaml.DependencyObject element)
    {
        var current = element;
        while (current is not null)
        {
            if (current == _barWindow.Content)
            {
                return true;
            }
            current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    #endregion Snapshot capture / restore


    #region Stale-ring cleanup

    // Walks the bar's visual tree and downgrades any Control with FocusState=Keyboard to
    // FocusState=Pointer. Used before showing the bar (to wipe a stale ring from a prior
    // keyboard session) and from the WM_ACTIVATE deferred-update path when we detect a
    // mouse-driven activation. A brute-force tree walk is necessary because WinUI's
    // FocusManager.GetFocusedElement may return a parent element (ScrollViewer, etc.)
    // instead of the actual Keyboard-focused button, so we can't simply downgrade
    // whatever GetFocusedElement returns.
    public void DowngradeKeyboardFocusInBar()
    {
        if (_barWindow.Content is not Microsoft.UI.Xaml.DependencyObject root)
        {
            return;
        }
        var queue = new System.Collections.Generic.Queue<Microsoft.UI.Xaml.DependencyObject>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current is Microsoft.UI.Xaml.Controls.Control control
                && control.FocusState == Microsoft.UI.Xaml.FocusState.Keyboard)
            {
                System.Diagnostics.Debug.WriteLine($"[Focus] Downgrading {control.GetType().Name} Keyboard -> Pointer");
                _ = control.Focus(Microsoft.UI.Xaml.FocusState.Pointer);
            }
            int childCount = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(current);
            for (int i = 0; i < childCount; i++)
            {
                queue.Enqueue(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(current, i));
            }
        }
    }

    #endregion Stale-ring cleanup


    #region Show-time bundle

    // Called from MorphicBarManager.ShowBar BEFORE the AppWindow.Show. Wraps the two
    // operations the manager used to do directly: defuse any stale Keyboard ring left from
    // a prior session, and OPEN a suppression scope so the post-Show WM_ACTIVATE focus upgrade
    // doesn't re-acquire an unwanted ring just from appearing. The caller MUST close the scope
    // via EndSuppressUpgradeAfterPendingActivations() right after the Show (ShowBar does).
    // Consolidating the open-half here is the whole point of the controller: the manager only
    // needs to say "prepare to show", not know what that entails.
    public void PrepareForShow()
    {
        this.DowngradeKeyboardFocusInBar();
        this.BeginSuppressUpgrade();
    }

    #endregion Show-time bundle
}
