// Copyright 2020-2022 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-windowsnative-lib-cs/blob/main/LICENSE
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

namespace Morphic.WindowsNative.Windowing;

using Morphic.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

public class NativeWindow
{
	// NOTE: in the future, we may want to consider just using the class name in all scenarios (and not worrying about the atom UInt16 value)
    internal static string? HiddenWindowClassName { get; private set; } = null;
    private static ushort? HiddenWindowClassAtom = null;

    // NOTE: we currently retain a list of all hidden windows that we've created (indexed by their IntPtr handle, and holding their SafeHandle which itself holds the appropriate thread to destroy the window); in an ideal scenario,
    //       each window class would retain its own SafeHandle so that we didn't need to do this centrally
    private static Dictionary<IntPtr, SafeNativeWindowHandle> HiddenWindowSafeHandles = new Dictionary<IntPtr, SafeNativeWindowHandle>();
    internal static SafeNativeWindowHandle? RemoveHiddenWindowSafeHandle(IntPtr hWnd)
    {
        SafeNativeWindowHandle? safeHandle;
        var keyExists = NativeWindow.HiddenWindowSafeHandles.TryGetValue(hWnd, out safeHandle);
        if (keyExists == true)
        {
            NativeWindow.HiddenWindowSafeHandles.Remove(hWnd);
            return safeHandle;
        }
        else
        {
            return null;
        }
    }
    public static MorphicResult<SafeNativeWindowHandle, Win32ApiError> CreateNewHiddenWindow()
    {
        // register the "hidden window" class
        ushort hiddenWindowClassAtom;
        if (NativeWindow.HiddenWindowClassAtom.HasValue == true)
        {
            hiddenWindowClassAtom = NativeWindow.HiddenWindowClassAtom.Value;
        }
        else
        {
            var cursorHandle = ExtendedPInvoke.LoadCursor(IntPtr.Zero, (int)ExtendedPInvoke.Cursors.IDC_ARROW);
            if (cursorHandle == IntPtr.Zero)
            {
                var win32ErrorCode = Marshal.GetLastWin32Error();
                return MorphicResult.ErrorResult(Win32ApiError.Win32Error((uint)win32ErrorCode));
            }

            // NOTE: we have modeled this class name after our Spy++ evaluation of WPF's ShowInTray "hidden window"; it used a class name of "HwndWrapper[Morphic;;725f87d8-4897-4321-b720-e2fcd113885b]" in our initial observation
            var className = "HwndWrapper[Morphic;;" + Guid.NewGuid().ToString() + "]";
            var wndClassEx = new ExtendedPInvoke.WNDCLASSEX()
            {
                style = 0,
                lpfnWndProc = NativeWindow.PassThroughWndProcCallback,
                cbClsExtra = 0,
                cbWndExtra = 0,
                hInstance = IntPtr.Zero, // System.Diagnostics.Process.GetCurrentProcess().Handle,
                hIcon = IntPtr.Zero,
                hCursor = cursorHandle,
                hbrBackground = IntPtr.Zero,
                lpszMenuName = null!,
                lpszClassName = className,
                hIconSm = IntPtr.Zero
            };
            wndClassEx.cbSize = (uint)Marshal.SizeOf(wndClassEx);

            var registerWindowClassResult = NativeWindow.RegisterWindowClass(wndClassEx);
            if (registerWindowClassResult.IsError == true)
            {
                var win32ErrorCode = Marshal.GetLastWin32Error();
                return MorphicResult.ErrorResult(Win32ApiError.Win32Error((uint)win32ErrorCode));
            }
            hiddenWindowClassAtom = registerWindowClassResult.Value;
                
            // cache the hidden window class atom (for later reuse)
            NativeWindow.HiddenWindowClassAtom = hiddenWindowClassAtom;
            NativeWindow.HiddenWindowClassName = className;
        }

        // NOTE: these are styles observed in our Spy++ evaluation of WPF's ShowInTray "hidden window"
        ExtendedPInvoke.WindowStylesEx exStyle = 
            ExtendedPInvoke.WindowStylesEx.WS_EX_LEFT |
            ExtendedPInvoke.WindowStylesEx.WS_EX_LTRREADING |
            ExtendedPInvoke.WindowStylesEx.WS_EX_RIGHTSCROLLBAR |
            ExtendedPInvoke.WindowStylesEx.WS_EX_WINDOWEDGE;
        //
        ushort classNameAsUInt16 = hiddenWindowClassAtom;
        //
        // NOTE: this is the caption (window name) observed in our Spy++ evaluation of WPF's ShowInTray "hidden window"
        string? windowName = "Hidden Window";
        //
        // NOTE: these are exstyles observed in our Spy++ evaluation of WPF's ShowInTray "hidden window"
        ExtendedPInvoke.WindowStyles style = 
            ExtendedPInvoke.WindowStyles.WS_CAPTION |
            ExtendedPInvoke.WindowStyles.WS_CLIPSIBLINGS |
            ExtendedPInvoke.WindowStyles.WS_SYSMENU |
            ExtendedPInvoke.WindowStyles.WS_THICKFRAME |
            ExtendedPInvoke.WindowStyles.WS_OVERLAPPED |
            ExtendedPInvoke.WindowStyles.WS_MINIMIZEBOX |
            ExtendedPInvoke.WindowStyles.WS_MAXIMIZEBOX;
        //
        // NOTE: in our Spy++ evaluation on a 4K display, WPF's ShowInTray "hidden window" used x=480, y=480, width=2880, height=1550; if using these simpler (and constant) values causes problems for us (although I'm not sure what those would be), re-evaluate the x/y/width/height parameters
        int x = 0;
        int y = 0;
        int width = 200;
        int height = 200;
        IntPtr hWndParent = IntPtr.Zero;
        IntPtr hMenu = IntPtr.Zero;
        // NOTE: in our Spy++ evaluation, WPF's ShowInTray "hidden window" used hInstance = IntPtr.Zero; if using the process handle causes problems for us (i.e. blocks the process from closing properly), set this to IntPtr.Zero instead
        IntPtr hInstance = IntPtr.Zero; //  System.Diagnostics.Process.GetCurrentProcess().Handle;
        IntPtr param = IntPtr.Zero;

        var hWnd = ExtendedPInvoke.CreateWindowEx(exStyle, classNameAsUInt16, windowName, style, x, y, width, height, hWndParent, hMenu, hInstance, param);
        if (hWnd == IntPtr.Zero)
        {
            var win32Error = Marshal.GetLastWin32Error();
            return MorphicResult.ErrorResult(Win32ApiError.Win32Error((uint)win32Error));
        }
        var hWndAsSafeHandle = new SafeNativeWindowHandle(hWnd, true);

        // NOTE: we currently retain a list of all hidden windows that we've created (indexed by their IntPtr handle, and holding their SafeHandle which itself holds the appropriate thread to destroy the window); in an ideal scenario,
        //       each window class would retain its own SafeHandle so that we didn't need to do this centrally
        NativeWindow.HiddenWindowSafeHandles[hWnd] = hWndAsSafeHandle;

        return MorphicResult.OkResult(hWndAsSafeHandle);
    }

    private static MorphicResult<ushort, Win32ApiError> RegisterWindowClass(ExtendedPInvoke.WNDCLASSEX wndClassEx)
    {
        // sanity check: ensure that wndClassEx.cbSize is set properly
        wndClassEx.cbSize = (uint)Marshal.SizeOf(wndClassEx);

        var registerClassResult = ExtendedPInvoke.RegisterClassEx(ref wndClassEx);
        if (registerClassResult == 0)
        {
            var win32ErrorCode = Marshal.GetLastWin32Error();
            return MorphicResult.ErrorResult(Win32ApiError.Win32Error((uint)win32ErrorCode));
        }

        return MorphicResult.OkResult(registerClassResult);
    }

    private static IntPtr PassThroughWndProcCallback(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        // pass all messages through to DefWindowProc
        return ExtendedPInvoke.DefWindowProc(hWnd, msg, wParam, lParam);
    }

}
