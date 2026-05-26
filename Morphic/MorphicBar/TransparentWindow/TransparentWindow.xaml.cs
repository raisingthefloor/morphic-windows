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

using System.Diagnostics;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Morphic.MorphicBar.TransparentWindow;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class TransparentWindow : Morphic.Controls.Windowing.TransparentBaseWindow
{
    // Optional WM_CLOSE intercept. NOT armed by default
    // uIdSubclass=1 because the base ChromelessBaseWindow already installs a static subclass
    // at uIdSubclass=0.
    private Windows.Win32.UI.Shell.SUBCLASSPROC? _instanceSubclassProc;
    private bool _userCloseEnabled = true;

    public TransparentWindow()
    {
        InitializeComponent();

        var hwnd = (Windows.Win32.Foundation.HWND)WinRT.Interop.WindowNative.GetWindowHandle(this);
        _instanceSubclassProc = this.InstanceSubclassWndProc;
        var setSubclassResult = Windows.Win32.PInvoke.SetWindowSubclass(hwnd, _instanceSubclassProc, uIdSubclass: 1, dwRefData: 0);
        Debug.Assert(setSubclassResult);

        this.Closed += TransparentWindow_Closed;
    }

    // Toggles whether Alt+F4 / shell close from the user destroys the window. Default is true
    // (normal close behavior). Pass false to arm the WM_CLOSE intercept; for shutdown, set back
    // to true before calling Close() so the programmatic close goes through.
    public void SetUserCloseEnabled(bool enabled)
    {
        _userCloseEnabled = enabled;
    }

    private void TransparentWindow_Closed(object sender, Microsoft.UI.Xaml.WindowEventArgs args)
    {
        if (_instanceSubclassProc is not null)
        {
            var hwnd = (Windows.Win32.Foundation.HWND)WinRT.Interop.WindowNative.GetWindowHandle(this);
            _ = Windows.Win32.PInvoke.RemoveWindowSubclass(hwnd, _instanceSubclassProc, uIdSubclass: 1);
            _instanceSubclassProc = null;
        }
    }

    private Windows.Win32.Foundation.LRESULT InstanceSubclassWndProc(
        Windows.Win32.Foundation.HWND hwnd,
        uint msg,
        Windows.Win32.Foundation.WPARAM wParam,
        Windows.Win32.Foundation.LPARAM lParam,
        nuint uIdSubclass,
        nuint dwRefData)
    {
        if (msg == Windows.Win32.PInvoke.WM_CLOSE && _userCloseEnabled == false)
        {
            // Swallow Alt+F4 / shell close while the user-close intercept is armed.
            return new Windows.Win32.Foundation.LRESULT(0);
        }
        return Windows.Win32.PInvoke.DefSubclassProc(hwnd, msg, wParam, lParam);
    }

    public void DisableAcceptsFocus()
    {
        var hwnd = (Windows.Win32.Foundation.HWND)WinRT.Interop.WindowNative.GetWindowHandle(this);

        var exStyle = (Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE)Windows.Win32.PInvoke.GetWindowLongPtr(hwnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        Debug.Assert(exStyle != 0);
        exStyle |= Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_NOACTIVATE;
        _ = Windows.Win32.PInvoke.SetWindowLongPtr(hwnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (nint)(exStyle));
    }

    public void EnablePointerEventsPassthrough()
    {
        var hwnd = (Windows.Win32.Foundation.HWND)WinRT.Interop.WindowNative.GetWindowHandle(this);

        var exStyle = (Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE)Windows.Win32.PInvoke.GetWindowLongPtr(hwnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        Debug.Assert(exStyle != 0);
        exStyle |= Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_TRANSPARENT | Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_LAYERED;
        _ = Windows.Win32.PInvoke.SetWindowLongPtr(hwnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (nint)(exStyle));
    }
}
