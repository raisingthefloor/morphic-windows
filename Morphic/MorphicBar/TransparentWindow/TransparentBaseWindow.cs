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

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Morphic.MorphicBar.TransparentWindow;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Morphic.MorphicBar.TransparentWindow;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public class TransparentBaseWindow : Window
{
    // a generated dispatch queue controller (required to use our custom transparent SystemBackdrop
    private static Windows.System.DispatcherQueueController? _dispatcherQueueController;

    // GC-protected reference to our static window subclass (required to prevent the delegate from being GC'd while the subclass is still active)
    private static Windows.Win32.UI.Shell.SUBCLASSPROC? _subclassProc;

    public TransparentBaseWindow()
    {
        // TransparentBackdrop requires that a dispatch queue is initialized on this thread before being created/connected
        // since TransparentBackdrop requires a Microsoft.UI.Composition.Compositor--but the window needs a Windows.UI.Composition.Compositor--we need to create a Windows.System.DispatchQueue manually (one is fine)
        TransparentBaseWindow.EnsureSystemDispatcherQueue();

        // remove the WinUI 3 window chrome (title bar and border)
        var overlappedPresenter = this.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
        if (overlappedPresenter != null)
        {
            overlappedPresenter.SetBorderAndTitleBar(false, false);
        }

        var hwnd = (HWND)WinRT.Interop.WindowNative.GetWindowHandle(this);

        // subclass the window so that we can handle non-client repaint messages and can eliminate the client-vs-nonclient calculations
        _subclassProc = TransparentBaseWindow.SubclassWndProc;
        Windows.Win32.PInvoke.SetWindowSubclass(hwnd, _subclassProc, 0, 0);

        // tell Windows to send WM_NCCALCSIZE immediately; our SubclassWndProc will handle that, to make the client area fill the entire window
        _ = Windows.Win32.PInvoke.SetWindowPos(hwnd, new HWND(new IntPtr(-1)), 0, 0, 0, 0, SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED | SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);

        // do not draw the standard rounded corners; this will make the border square, but we'll remove that border in a moment
        int cornerPreference = (int)Windows.Win32.Graphics.Dwm.DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND;
        Span<byte> cornerPreferenceAsSpan = System.Runtime.InteropServices.MemoryMarshal.AsBytes(new Span<int>(ref cornerPreference));
        var setAttributeResult = Windows.Win32.PInvoke.DwmSetWindowAttribute(hwnd, Windows.Win32.Graphics.Dwm.DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, cornerPreferenceAsSpan);
        System.Diagnostics.Debug.Assert(setAttributeResult == HRESULT.S_OK);

        // set the DWM border color to "none"
        uint colorNone = 0xFFFFFFFE; // DWMWA_COLOR_NONE
        Span<byte> colorNoneAsSpan = System.Runtime.InteropServices.MemoryMarshal.AsBytes(new Span<uint>(ref colorNone));
        setAttributeResult = Windows.Win32.PInvoke.DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_BORDER_COLOR, colorNoneAsSpan);
        System.Diagnostics.Debug.Assert(setAttributeResult == HRESULT.S_OK);

        // NOTE: even though we've set a fully-transparent system backdrop via WinUI, DWM will still fill the composition
        //       background with an opaque brush
        var dummyRegion = Windows.Win32.PInvoke.CreateRectRgn(-2, -2, -1, -1);
        try
        {
            var dwmBlurBehind = new Windows.Win32.Graphics.Dwm.DWM_BLURBEHIND
            {
                dwFlags = Windows.Win32.PInvoke.DWM_BB_ENABLE | Windows.Win32.PInvoke.DWM_BB_BLURREGION,
                fEnable = true,
                hRgnBlur = dummyRegion
            };
            // enable DWN blur-behind (to composite the window with per-pixel alpha)
            Windows.Win32.PInvoke.DwmEnableBlurBehindWindow(hwnd, in dwmBlurBehind);
        }
        finally
        {
            _ = Windows.Win32.PInvoke.DeleteObject(dummyRegion);
        }

        // set our system backdrop to a fully-transparent backdrop
        this.SystemBackdrop = new Morphic.MorphicBar.TransparentWindow.TransparentBackdrop();
    }

    private static void EnsureSystemDispatcherQueue()
    {
        if (Windows.System.DispatcherQueue.GetForCurrentThread() != null)
        {
            return;
        }

        _ = Windows.Win32.PInvoke.CreateDispatcherQueueController(
            new Windows.Win32.System.WinRT.DispatcherQueueOptions
            {
                dwSize = (uint)Marshal.SizeOf<Windows.Win32.System.WinRT.DispatcherQueueOptions>(),
                threadType = Windows.Win32.System.WinRT.DISPATCHERQUEUE_THREAD_TYPE.DQTYPE_THREAD_CURRENT,
                apartmentType = Windows.Win32.System.WinRT.DISPATCHERQUEUE_THREAD_APARTMENTTYPE.DQTAT_COM_STA
            },
            out var controller);
        _dispatcherQueueController = controller;
    }

    private static LRESULT SubclassWndProc(Windows.Win32.Foundation.HWND hwnd, uint msg, Windows.Win32.Foundation.WPARAM wParam, Windows.Win32.Foundation.LPARAM lParam, nuint uIdSubclass, nuint dwRefData)
    {
        if (msg == Windows.Win32.PInvoke.WM_NCCALCSIZE && wParam != 0)
        {
            // return 0, indicating that the client area equals the full client window rect; this eliminates the non-client border/frame space that SetBorderAndTitleBar(false, false) leaves behind.
            return new LRESULT(0);
        }

        if (msg == Windows.Win32.PInvoke.WM_NCACTIVATE)
        {
            // return a "handled" result; this will suppress the default non-client paint
            return new LRESULT(1);
        }

        return Windows.Win32.PInvoke.DefSubclassProc(hwnd, msg, wParam, lParam);
    }
}
