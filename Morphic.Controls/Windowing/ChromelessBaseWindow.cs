// Copyright 2026 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-controls-lib-cs/blob/main/LICENSE.txt
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
using System;
using System.Runtime.InteropServices;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Morphic.Controls.Windowing;

// Base Window that strips all WinUI / DWM chrome and enables per-pixel alpha, leaving the
// subclass free to draw the visible shape (and pick a SystemBackdrop) however it likes.
//
// Setup performed:
//   * Ensures a Windows.System.DispatcherQueue exists on this thread (required for any
//     SystemBackdrop a subclass might later attach)
//   * SetBorderAndTitleBar(false, false) on the OverlappedPresenter (no chrome)
//   * Subclasses the window with a WndProc that:
//       - returns 0 for WM_NCCALCSIZE (client area equals full window rect, no non-client)
//       - returns 1 for WM_NCACTIVATE (suppress default non-client paint)
//       - honors per-instance min-track-size set via SetMinimumTrackSize
//   * SetWindowPos(SWP_FRAMECHANGED) to apply the WM_NCCALCSIZE stripout immediately
//   * DWMWA_WINDOW_CORNER_PREFERENCE = DWMWCP_DONOTROUND (no DWM rounding)
//   * DWMWA_BORDER_COLOR = DWMWA_COLOR_NONE (no DWM-drawn border)
//   * DwmEnableBlurBehindWindow with a dummy region (enables per-pixel alpha compositing
//     so anything that the subclass leaves transparent shows through to whatever is behind)
public class ChromelessBaseWindow : Window
{
    public static Action<string>? OnDiagnostic;
    // a generated dispatch queue controller (required for any custom SystemBackdrop the
    // subclass might attach later)
    private static Windows.System.DispatcherQueueController? _dispatcherQueueController;

    // GC-protected reference to our static window subclass (required to prevent the
    // delegate from being collected while the subclass is still installed)
    private static Windows.Win32.UI.Shell.SUBCLASSPROC? _subclassProc;

    public ChromelessBaseWindow() : base()
    {
        // A SystemBackdrop attached by a subclass needs a Windows.System.DispatcherQueue
        // on this thread: create one preemptively. (Even subclasses that don't use a custom
        // backdrop are unaffected by an unused queue controller.)
        ChromelessBaseWindow.EnsureSystemDispatcherQueue();

        // remove the WinUI 3 window chrome (title bar and border)
        var overlappedPresenter = this.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
        if (overlappedPresenter != null)
        {
            overlappedPresenter.SetBorderAndTitleBar(false, false);
        }

        var hwnd = (HWND)WinRT.Interop.WindowNative.GetWindowHandle(this);

        // subclass the window so that we can handle non-client repaint messages and can eliminate the client-vs-nonclient calculations
        _subclassProc = ChromelessBaseWindow.SubclassWndProc;
        var setSubclassResult = Windows.Win32.PInvoke.SetWindowSubclass(hwnd, _subclassProc, 0, 0);
        System.Diagnostics.Debug.Assert(setSubclassResult);
        if (setSubclassResult == false)
        {
            ChromelessBaseWindow.OnDiagnostic?.Invoke("ChromelessBaseWindow: SetWindowSubclass returned FALSE");
        }

        // tell Windows to send WM_NCCALCSIZE immediately; our SubclassWndProc will handle that, to make the client area fill the entire window
        var setWindowPosResult = Windows.Win32.PInvoke.SetWindowPos(hwnd, new HWND(new IntPtr(-1)), 0, 0, 0, 0, SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED | SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
        System.Diagnostics.Debug.Assert(setWindowPosResult);
        if (setWindowPosResult == false)
        {
            ChromelessBaseWindow.OnDiagnostic?.Invoke("ChromelessBaseWindow: SetWindowPos(SWP_FRAMECHANGED) returned FALSE");
        }

        // DWMWA_WINDOW_CORNER_PREFERENCE and DWMWA_BORDER_COLOR are Win11-only DWM attributes
        // (introduced in build 22000). On Win10 the system returns E_INVALIDARG /
        // ERROR_INVALID_PARAMETER (0x80070057) for either attribute. Skip both calls on Win10:
        // Win10 doesn't draw rounded top-level window corners or a DWM-managed border in the
        // first place, so the "don't round" and "no border color" requests are no-ops there
        // anyway.
        if (Morphic.WindowsNative.OsVersion.OsVersion.IsWindows11OrLater() == true)
        {
            // do not draw the standard rounded corners; this will make the border square, but we'll remove that border in a moment
            int cornerPreference = (int)Windows.Win32.Graphics.Dwm.DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND;
            Span<byte> cornerPreferenceAsSpan = System.Runtime.InteropServices.MemoryMarshal.AsBytes(new Span<int>(ref cornerPreference));
            var setCornerAttributeResult = Windows.Win32.PInvoke.DwmSetWindowAttribute(hwnd, Windows.Win32.Graphics.Dwm.DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, cornerPreferenceAsSpan);
            System.Diagnostics.Debug.Assert(setCornerAttributeResult == HRESULT.S_OK);
            if (setCornerAttributeResult != HRESULT.S_OK)
            {
                ChromelessBaseWindow.OnDiagnostic?.Invoke($"ChromelessBaseWindow: DwmSetWindowAttribute(DWMWA_WINDOW_CORNER_PREFERENCE) returned HRESULT 0x{(uint)setCornerAttributeResult.Value:X8}");
            }

            // set the DWM border color to "none"
            uint colorNone = 0xFFFFFFFE; // DWMWA_COLOR_NONE
            Span<byte> colorNoneAsSpan = System.Runtime.InteropServices.MemoryMarshal.AsBytes(new Span<uint>(ref colorNone));
            var setBorderAttributeResult = Windows.Win32.PInvoke.DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_BORDER_COLOR, colorNoneAsSpan);
            System.Diagnostics.Debug.Assert(setBorderAttributeResult == HRESULT.S_OK);
            if (setBorderAttributeResult != HRESULT.S_OK)
            {
                ChromelessBaseWindow.OnDiagnostic?.Invoke($"ChromelessBaseWindow: DwmSetWindowAttribute(DWMWA_BORDER_COLOR) returned HRESULT 0x{(uint)setBorderAttributeResult.Value:X8}");
            }
        }

        // Extend the DWM frame into the entire client area using the "sheet of glass" pattern
        // (negative margins). This is DWM's modern, documented mechanism for per-pixel-alpha
        // composition: it tells the compositor that every pixel in the client area is part of
        // the alpha-blended frame, so wherever a subclass leaves pixels transparent, whatever
        // is behind the window shows through. Replaces the previous DwmEnableBlurBehindWindow
        // dummy-region pattern, which worked on most configurations but rendered an opaque
        // rectangle behind the rounded shape on some VMs and in some legacy screen-capture
        // tools (those tools read the window's redirection bitmap rather than the DComp
        // composed output).
        var sheetOfGlassMargins = new Windows.Win32.UI.Controls.MARGINS
        {
            cxLeftWidth = -1,
            cxRightWidth = -1,
            cyTopHeight = -1,
            cyBottomHeight = -1
        };
        var extendFrameResult = Windows.Win32.PInvoke.DwmExtendFrameIntoClientArea(hwnd, in sheetOfGlassMargins);
        System.Diagnostics.Debug.Assert(extendFrameResult == HRESULT.S_OK);
        if (extendFrameResult != HRESULT.S_OK)
        {
            ChromelessBaseWindow.OnDiagnostic?.Invoke($"ChromelessBaseWindow: DwmExtendFrameIntoClientArea returned HRESULT 0x{(uint)extendFrameResult.Value:X8}");
        }
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

        if (msg == Windows.Win32.PInvoke.WM_ERASEBKGND)
        {
            return new LRESULT(1);
        }

        // see: https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-getminmaxinfo
        if ((msg == Windows.Win32.PInvoke.WM_GETMINMAXINFO) && (dwRefData != 0))
        {
            // dwRefData carries the override min-track-size set via SetMinimumTrackSize:
            // low 32 bits = width in physical pixels, high 32 bits = height in physical
            // pixels. If the subclass never called SetMinimumTrackSize, dwRefData is 0 and
            // this branch is skipped -- the OS default minimum applies.
			// [however, if the subclass calls SetMinimumTrackSize(...), they can set the minimum size]
            int minWidth = unchecked((int)(uint)(dwRefData & 0xFFFFFFFF));
            int minHeight = unchecked((int)(uint)(dwRefData >> 32));
            if (minWidth > 0 && minHeight > 0)
            {
                // populate lParam (casted to MINMAXINFO); this will be passed along in the DefSubclassProc(...) call below
                unsafe
                {
                    var mmi = (Windows.Win32.UI.WindowsAndMessaging.MINMAXINFO*)(nint)lParam.Value;
                    mmi->ptMinTrackSize.X = minWidth;
                    mmi->ptMinTrackSize.Y = minHeight;
                }
                return new LRESULT(0);
            }
        }

        return Windows.Win32.PInvoke.DefSubclassProc(hwnd, msg, wParam, lParam);
    }

    // Overrides the OS-enforced minimum window track size for THIS window. Subclasses call
    // this when they need to shrink below the OverlappedPresenter default minimum (which
    // is approximately 196x156 DIPs and would otherwise clamp AppWindow.MoveAndResize calls
    // -- a problem for tiny popup-style windows like tooltips).
    //
    // Default behavior (subclass never calls this): the OS minimum stays in place, so any
    // existing ChromelessBaseWindow subclass keeps the standard size floor.
    //
    // The values are packed into the subclass dwRefData so the static SubclassWndProc can
    // read them without needing per-instance state.  Packing is defined by the operating system.
    protected void SetMinimumTrackSize(int widthPhysicalPixels, int heightPhysicalPixels)
    {
        var hwnd = (HWND)WinRT.Interop.WindowNative.GetWindowHandle(this);
        nuint packed = ((nuint)(uint)widthPhysicalPixels) | (((nuint)(uint)heightPhysicalPixels) << 32);
        // Calling SetWindowSubclass again with the same proc + uIdSubclass updates dwRefData in place.
		// see: https://learn.microsoft.com/en-us/windows/win32/api/commctrl/nf-commctrl-setwindowsubclass
        var updateSubclassResult = Windows.Win32.PInvoke.SetWindowSubclass(hwnd, _subclassProc!, 0, packed);
        System.Diagnostics.Debug.Assert(updateSubclassResult);
        if (updateSubclassResult == false)
        {
            ChromelessBaseWindow.OnDiagnostic?.Invoke("ChromelessBaseWindow.SetMinimumTrackSize: SetWindowSubclass returned FALSE");
        }
    }
}
