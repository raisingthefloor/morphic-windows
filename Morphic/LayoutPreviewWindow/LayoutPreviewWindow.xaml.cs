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

namespace Morphic.MorphicBar;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class LayoutPreviewWindow : Window
{
    Window _dummyParentWindow;

    public LayoutPreviewWindow()
    {
        InitializeComponent();

        var hwnd = (Windows.Win32.Foundation.HWND)WinRT.Interop.WindowNative.GetWindowHandle(this);

        // create a dummy "parent window" for the layout preview window (so that this window doesn't show up in the taskbar)
        _dummyParentWindow = new Window();
        IntPtr dummyParentHwndAsIntPtr = WindowNative.GetWindowHandle(_dummyParentWindow);
        Windows.Win32.PInvoke.SetWindowLongPtr(hwnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWLP_HWNDPARENT, dummyParentHwndAsIntPtr);

        // remove title bar and extend content to fill the entire window
        this.ExtendsContentIntoTitleBar = true;

        // remove window chrome (minimize/maximize/close buttons, borders)
        var presenter = this.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
        if (presenter is not null)
        {
            presenter.IsResizable = false;
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
            presenter.SetBorderAndTitleBar(true, false);
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

        // set the DWM border color
        uint borderColor = 0x00707070;
        Span<byte> borderColorAsSpan = MemoryMarshal.AsBytes(new Span<uint>(ref borderColor));
        var setAttributeResult = Windows.Win32.PInvoke.DwmSetWindowAttribute(hwnd, Windows.Win32.Graphics.Dwm.DWMWINDOWATTRIBUTE.DWMWA_BORDER_COLOR, borderColorAsSpan);
        System.Diagnostics.Debug.Assert(setAttributeResult == HRESULT.S_OK);

        // use a custom translucent acrylic backdrop for the stained glass effect
        this.SystemBackdrop = new Morphic.LayoutPreviewWindow.AcrylicGrayBackdrop();

        this.Activated += LayoutPreviewWindow_Activated;
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
}
