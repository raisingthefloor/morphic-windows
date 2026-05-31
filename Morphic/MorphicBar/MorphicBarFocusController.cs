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

    public readonly record struct FocusSnapshot(
        Microsoft.UI.Xaml.Controls.Control? Control,
        Microsoft.UI.Xaml.FocusState State);

    public enum SnapshotScope
    {
        BarOwnedOnly,
        AnyInBarXamlRoot,
    }

    #endregion Public types


    #region Upgrade suppression

    public void BeginSuppressUpgrade()
    {
        _suppressUpgradeDepth++;
    }

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

    public bool IsUpgradeSuppressed => _suppressUpgradeDepth > 0;

    #endregion Upgrade suppression


    #region Initial focus placement

    public void SetInitialFocus(Microsoft.UI.Xaml.FocusState focusState = Microsoft.UI.Xaml.FocusState.Programmatic)
    {
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

        bool mousePressed = (Windows.Win32.PInvoke.GetAsyncKeyState(0x01) & 0x8000) != 0
            || (Windows.Win32.PInvoke.GetAsyncKeyState(0x02) & 0x8000) != 0
            || (Windows.Win32.PInvoke.GetAsyncKeyState(0x04) & 0x8000) != 0;
        System.Diagnostics.Debug.WriteLine($"[Focus] RunDeferredFocusUpdate({activationState}) mousePressed={mousePressed}");
        if (mousePressed)
        {
            activationState = Microsoft.UI.Xaml.WindowActivationState.PointerActivated;
            this.DowngradeKeyboardFocusInBar();
        }

        var focused = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(xamlRoot) as Microsoft.UI.Xaml.DependencyObject;
        if (focused is not null && this.IsBarOwnedElement(focused))
        {
            if (focused is Microsoft.UI.Xaml.Controls.Control focusedControl)
            {
                if (activationState != Microsoft.UI.Xaml.WindowActivationState.PointerActivated
                    && focusedControl.FocusState != Microsoft.UI.Xaml.FocusState.Keyboard
                    && this.IsUpgradeSuppressed == false
                    && this.IsBarForegroundWindow())
                {
                    _ = focusedControl.Focus(Microsoft.UI.Xaml.FocusState.Keyboard);
                }
                else if (activationState == Microsoft.UI.Xaml.WindowActivationState.PointerActivated
                    && focusedControl.FocusState == Microsoft.UI.Xaml.FocusState.Keyboard)
                {
                    _ = focusedControl.Focus(Microsoft.UI.Xaml.FocusState.Pointer);
                }
            }
            return;
        }
        if (this.IsUpgradeSuppressed == true)
        {
            return;
        }
        Microsoft.UI.Xaml.FocusState focusState;
        if (activationState == Microsoft.UI.Xaml.WindowActivationState.PointerActivated)
        {
            focusState = Microsoft.UI.Xaml.FocusState.Pointer;
        }
        else
        {
            focusState = this.IsBarForegroundWindow()
                ? Microsoft.UI.Xaml.FocusState.Keyboard
                : Microsoft.UI.Xaml.FocusState.Programmatic;
        }
        this.SetInitialFocus(focusState);
    }

    private bool IsBarForegroundWindow()
    {
        var barHwnd = (Windows.Win32.Foundation.HWND)WinRT.Interop.WindowNative.GetWindowHandle(_barWindow);
        return Windows.Win32.PInvoke.GetForegroundWindow() == barHwnd;
    }

    #endregion Activation drive


    #region Snapshot capture / restore

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

    public void PrepareForShow()
    {
        this.DowngradeKeyboardFocusInBar();
        this.BeginSuppressUpgrade();
    }

    #endregion Show-time bundle
}
