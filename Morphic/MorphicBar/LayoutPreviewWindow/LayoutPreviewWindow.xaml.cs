// Copyright 2026 Raising the Floor - US, Inc.
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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Win32.Foundation;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Morphic.MorphicBar.LayoutPreviewWindow;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class LayoutPreviewWindow : Morphic.Controls.Windowing.ChromelessBaseWindow
{
    DummyWindow _dummyParentWindow;

    private Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue;

    // animation timer for moving (and rotating-via-resizing) the window
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _moveAnimationTimer;

    public LayoutPreviewWindow()
    {
        // NOTE: ChromelessBaseWindow's constructor strips all WinUI / DWM chrome and 
        // enables per-pixel alpha, so this constructor only has to do LayoutPreviewWindow's
        // specific setup: dummy parent (keep out of the taskbar), tool window + no-activate
        // styles (keep out of ALT-TAB and don't steal focus), and HC-tracking that drives
        // the visible appearance via UpdateAppearanceForCurrentHighContrastState.
        InitializeComponent();

        _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        var hwnd = (Windows.Win32.Foundation.HWND)WinRT.Interop.WindowNative.GetWindowHandle(this);

        // create a dummy "parent window" for the layout preview window (so that this window doesn't show up in the taskbar)
        _dummyParentWindow = new DummyWindow();
        _ = _dummyParentWindow.SetAsParentHwnd(hwnd);

        // remove title bar and extend content to fill the entire window
//        this.ExtendsContentIntoTitleBar = true;

        // make the WinUI presenter non-resizable/min/max (chrome is already stripped by the base class)
        var presenter = this.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
        if (presenter is not null)
        {
            presenter.IsResizable = false;
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
        }

        // remove the window from the Alt+Tab task switcher (by making it a 'tool window') and also make it unactivate-able (so that it can't steal focus)
        var exStyle = (Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE)Windows.Win32.PInvoke.GetWindowLongPtr(hwnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        exStyle |= Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_TOOLWINDOW;
        exStyle |= Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_NOACTIVATE;
        //
        // NOTE: SetWindowLongPtr can return 0 even if there is no error; see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowlongptrw
        System.Runtime.InteropServices.Marshal.SetLastPInvokeError(0);
        var setWindowLongPtrResult = Windows.Win32.PInvoke.SetWindowLongPtr(hwnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (nint)exStyle);
        if (setWindowLongPtrResult == 0)
        {
            var win32ErrorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            if (win32ErrorCode != 0)
            {
                System.Diagnostics.Debug.Assert(false);
            }
        }

        // Pick the right backdrop + border appearance for the current HC state, and keep
        // it in sync as the user toggles HC or swaps HC variants. SystemSettingsListener's
        // HighContrastChanged fires for every HC setting change (on/off AND variant swap),
        // which is what we want here.
        this.UpdateAppearanceForCurrentHighContrastState();
        Morphic.WindowsNative.SystemSettings.SystemSettingsListener.Shared.HighContrastChanged += this.OnHighContrastSettingChanged;
        this.Closed += this.LayoutPreviewWindow_Closed;

        this.Activated += LayoutPreviewWindow_Activated;
    }

    private void LayoutPreviewWindow_Closed(object sender, WindowEventArgs args)
    {
        Morphic.WindowsNative.SystemSettings.SystemSettingsListener.Shared.HighContrastChanged -= this.OnHighContrastSettingChanged;
        this.Closed -= this.LayoutPreviewWindow_Closed;
    }

    //

    private void OnHighContrastSettingChanged(object? sender, EventArgs e)
    {
        _dispatcherQueue.TryEnqueue(this.UpdateAppearanceForCurrentHighContrastState);
    }

    // Re-reads the HC state and applies the matching appearance. ChromelessBaseWindow
    // strips chrome + DWM border + DWM rounding by default; in non-HC we re-enable DWM
    // rounding so the window's outer shape is rounded (clipping the AcrylicGrayBackdrop to
    // a rounded silhouette without us having to clip the acrylic ourselves). In HC we
    // leave the DWM rounding off and let the XAML Border draw the rounded shape + outline.
    private void UpdateAppearanceForCurrentHighContrastState()
    {
        bool isHighContrast = false;
        var getResult = Morphic.WindowsNative.Theme.HighContrast.GetIsOn();
        if (getResult.IsSuccess)
        {
            isHighContrast = getResult.Value!;
        }

        var hwnd = (Windows.Win32.Foundation.HWND)WinRT.Interop.WindowNative.GetWindowHandle(this);

        if (isHighContrast)
        {
            // HC: transparent backdrop + opaque rounded XAML Border with visible HC outline.
            // DWM rounding stays OFF (ChromelessBaseWindow's default) so the only rounded
            // shape is the XAML Border; corners outside it are truly invisible.
            int cornerPreferenceHC = (int)Windows.Win32.Graphics.Dwm.DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND;
            Span<byte> cornerPreferenceHCSpan = MemoryMarshal.AsBytes(new Span<int>(ref cornerPreferenceHC));
            _ = Windows.Win32.PInvoke.DwmSetWindowAttribute(hwnd, Windows.Win32.Graphics.Dwm.DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, cornerPreferenceHCSpan);

            this.SystemBackdrop = new Morphic.Controls.Windowing.TransparentBackdrop();

            var bg = GetSysColorAsWinUIColor(Windows.Win32.Graphics.Gdi.SYS_COLOR_INDEX.COLOR_WINDOW);
            var border = GetSysColorAsWinUIColor(Windows.Win32.Graphics.Gdi.SYS_COLOR_INDEX.COLOR_WINDOWTEXT);
            this.RootBorder.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(bg);
            this.RootBorder.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(border);
            this.RootBorder.BorderThickness = new Microsoft.UI.Xaml.Thickness(1.5);
            this.RootBorder.CornerRadius = new Microsoft.UI.Xaml.CornerRadius(8);
        }
        else
        {
            // non-HC: original frosted-glass look via AcrylicGrayBackdrop. Re-enable DWM
            // rounding so the window's outer shape is rounded (DWM clips the rectangular
            // acrylic fill to a rounded outer silhouette). The XAML Border is rectangular
            // here and just overlays the dark-overlay tint that gives the acrylic the
            // dark-frosted appearance.
            int cornerPreferenceNonHC = (int)Windows.Win32.Graphics.Dwm.DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND;
            Span<byte> cornerPreferenceNonHCSpan = MemoryMarshal.AsBytes(new Span<int>(ref cornerPreferenceNonHC));
            _ = Windows.Win32.PInvoke.DwmSetWindowAttribute(hwnd, Windows.Win32.Graphics.Dwm.DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, cornerPreferenceNonHCSpan);

            this.SystemBackdrop = new Morphic.MorphicBar.LayoutPreviewWindow.AcrylicGrayBackdrop();

            this.RootBorder.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0x40, 0x00, 0x00, 0x00));
            this.RootBorder.BorderBrush = null;
            this.RootBorder.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
            this.RootBorder.CornerRadius = new Microsoft.UI.Xaml.CornerRadius(0);
        }

        // SWP_FRAMECHANGED nudges DWM to re-apply the corner-preference change to the
        // already-shown window (without it, DWM keeps the previous rounding decision).
        _ = Windows.Win32.PInvoke.SetWindowPos(hwnd, Windows.Win32.Foundation.HWND.Null, 0, 0, 0, 0,
            Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED |
            Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOMOVE |
            Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
            Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOZORDER |
            Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
    }

    // GetSysColor returns COLORREF (0x00BBGGRR). Convert to a WinUI ARGB color with full alpha.
    private static Windows.UI.Color GetSysColorAsWinUIColor(Windows.Win32.Graphics.Gdi.SYS_COLOR_INDEX index)
    {
        uint colorRef = Windows.Win32.PInvoke.GetSysColor(index);
        byte r = (byte)(colorRef & 0xFF);
        byte g = (byte)((colorRef >> 8) & 0xFF);
        byte b = (byte)((colorRef >> 16) & 0xFF);
        return Windows.UI.Color.FromArgb(0xFF, r, g, b);
    }

    private void LayoutPreviewWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        var hwnd = (Windows.Win32.Foundation.HWND)WinRT.Interop.WindowNative.GetWindowHandle(this);

        // set the window position to topmost (to push it to the top of the zorder)
        Windows.Win32.PInvoke.SetWindowPos(hwnd, Windows.Win32.Foundation.HWND.HWND_TOPMOST, 0, 0, 0, 0,
            Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOMOVE |
            Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
            Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);

        // set the window position to 'no'-topmost (so that it doesn't try to stay on top of all other windows)
        Windows.Win32.PInvoke.SetWindowPos(hwnd, Windows.Win32.Foundation.HWND.HWND_NOTOPMOST, 0, 0, 0, 0,
            Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOMOVE |
            Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
            Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
    }

    /* public methods */

    /// <summary>
    /// Smoothly animates the window to the target position and optionally to a target size,
    /// using ease-out cubic interpolation. If called while a previous animation is in progress,
    /// the current animation is cancelled and a new one starts from the window's current state.
    /// </summary>
    public void AnimateMoveTo(Windows.Graphics.PointInt32 targetPosition, Windows.Graphics.SizeInt32 targetSize, TimeSpan duration)
    {
        // stop any existing timer
        _moveAnimationTimer?.Stop();
        _moveAnimationTimer = null;

        // start the new animation
        _moveAnimationTimer = AnimationUtils.AnimateMoveTo(_dispatcherQueue, this.AppWindow, targetPosition, targetSize, duration);
    }

    public void AnimateStop()
    {
        _moveAnimationTimer?.Stop();
        _moveAnimationTimer = null;
    }
}
