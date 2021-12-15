// Copyright 2020-2021 Raising the Floor - US, Inc.
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

using Morphic.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Morphic.Windows.Native.Windowing.Utils
{
    public class WindowUtils
    {
        public enum ResizeMode
        {
            CanMinimize,
            CanResize,
            // CanResizeWithGrip, // not currently implemented; if we need this, see what WPF does (via the style on a WPF Windows with this ResizeMode; use Spy++)
            NoResize
        }
        public static MorphicResult<MorphicUnit, Win32ApiError> SetResizable(IntPtr hWnd, ResizeMode resizeMode)
        {
            IntPtr getStyleResult = ExtendedPInvoke.GetWindowLongPtr(hWnd, (int)PInvoke.User32.WindowLongIndexFlags.GWL_STYLE);
            if (getStyleResult == IntPtr.Zero)
            {
                // NOTE: if SetWindowLong or SetWindowLongPtr has not been called on this window, it will still return IntPtr.Zero
                var win32ErrorCode = PInvoke.Kernel32.GetLastError();
                if (win32ErrorCode != PInvoke.Win32ErrorCode.ERROR_SUCCESS)
                {
                    return MorphicResult.ErrorResult(Win32ApiError.Win32Error((int)win32ErrorCode));
                }
            }
            nint windowStyle = (nint)getStyleResult;

            switch (resizeMode)
            {
                case ResizeMode.CanMinimize:
                    {
                        windowStyle &= ~(nint)PInvoke.User32.WindowStyles.WS_MAXIMIZEBOX;
                        windowStyle |= (nint)PInvoke.User32.WindowStyles.WS_MINIMIZEBOX;
                        windowStyle &= ~(nint)PInvoke.User32.WindowStyles.WS_SIZEFRAME;
                    }
                    break;
                case ResizeMode.CanResize:
                    {
                        windowStyle |= (nint)PInvoke.User32.WindowStyles.WS_MAXIMIZEBOX;
                        windowStyle |= (nint)PInvoke.User32.WindowStyles.WS_MINIMIZEBOX;
                        windowStyle |= (nint)PInvoke.User32.WindowStyles.WS_SIZEFRAME;
                    }
                    break;
                case ResizeMode.NoResize:
                    {
                        windowStyle &= ~(nint)PInvoke.User32.WindowStyles.WS_MAXIMIZEBOX;
                        windowStyle &= ~(nint)PInvoke.User32.WindowStyles.WS_MINIMIZEBOX;
                        windowStyle &= ~(nint)PInvoke.User32.WindowStyles.WS_SIZEFRAME;
                    }
                    break;
                default:
                    throw new ArgumentException("Invalid argument", nameof(resizeMode));
            }

            // NOTE: per the Microsoft docs, we need to set last error to zero before calling this function...or else our result will probably be zero and the error code may be from a different Win32 API call (even if we were successful)
            // see: https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowlongptrw
            PInvoke.Kernel32.SetLastError(0);
            var setStyleResult = PInvoke.User32.SetWindowLongPtr(hWnd, PInvoke.User32.WindowLongIndexFlags.GWL_STYLE, windowStyle);
            if (setStyleResult == IntPtr.Zero)
            {
                // NOTE: this function call may still return zero upon success as well as failure (but with an error result of ERROR_SUCCESS)
                var win32ErrorCode = PInvoke.Kernel32.GetLastError();
                if (win32ErrorCode != PInvoke.Win32ErrorCode.ERROR_SUCCESS)
                {
                    return MorphicResult.ErrorResult(Win32ApiError.Win32Error((int)win32ErrorCode));
                }
            }

            return MorphicResult.OkResult();
        }

        public static MorphicResult<MorphicUnit, Win32ApiError> SetShowInTaskbar(IntPtr hWnd, bool value)
        {
            IntPtr oldParentHWndAsIntPtr;
            IntPtr newParentHWndAsIntPtr;

            oldParentHWndAsIntPtr = IntPtr.Zero;

            if (value == true)
            {
                // determine if the window already has a "hidden window" parent; if it does, then capture that window's handle so we can destroy it AFTER disconnecting it as our parent; note that if we tried to destroy it before disconnecting it, we could inadvertently destroy its child--our window--as well

				// NOTE: another implementation option here would be to check NativeWindow.RemoveHiddenWindowSafeHandle (and capture the window's safe handle at the same time)
				//       [if we ever move this logic to the Window class (or an extension to it), we should just store the "ShowInTaskbar=false owner"'s safehandle in that class]

                oldParentHWndAsIntPtr = PInvoke.User32.GetWindow(hWnd, PInvoke.User32.GetWindowCommands.GW_OWNER);
				// NOTE: we are looking for the owner (above), not the parent (below); this is a bit confusing because of the flag we use to set the owner (which is a "PARENT" flag)
                //oldParentHWndAsIntPtr = PInvoke.User32.GetAncestor(hWnd, PInvoke.User32.GetAncestorFlags.GA_PARENT);
                if (oldParentHWndAsIntPtr != IntPtr.Zero)
                {
                    var oldParentClassNameAsCharSpan = new Span<char>(new char[256]);
                    var getClassNameResult = PInvoke.User32.GetClassName(oldParentHWndAsIntPtr, oldParentClassNameAsCharSpan);
                    if (getClassNameResult == 0)
                    {
                        var win32ErrorCode = PInvoke.Kernel32.GetLastError();
                        return MorphicResult.ErrorResult(Win32ApiError.Win32Error((int)win32ErrorCode));
                    }
                    oldParentClassNameAsCharSpan = oldParentClassNameAsCharSpan.Slice(0, getClassNameResult);

                    // check to see if the parent is our "hidden window" class
                    var oldParentClassName = new String(oldParentClassNameAsCharSpan);
                    if (oldParentClassName != NativeWindow.HiddenWindowClassName)
                    {
                        // if the window has a parent, it doesn't show in the taskbar; return success; note that we may want to expand our error result type to offer this as an Error condition as well
                        return MorphicResult.OkResult();
                    }
                }

                newParentHWndAsIntPtr = IntPtr.Zero;
            }
            else
            {
                var createNewHiddenWindowResult = Morphic.Windows.Native.Windowing.NativeWindow.CreateNewHiddenWindow();
                if (createNewHiddenWindowResult.IsError == true)
                {
                    var win32ErrorCode = createNewHiddenWindowResult.Error!.Win32ErrorCode!.Value;
                    return MorphicResult.ErrorResult(Win32ApiError.Win32Error(win32ErrorCode));
                }
                var newParentHwnd = createNewHiddenWindowResult.Value!;

                newParentHWndAsIntPtr = newParentHwnd.DangerousGetHandle();
            }

            // NOTE: per the Microsoft docs, we need to set last error to zero before calling this function...or else our result will probably be zero and the error code may be from a different Win32 API call (even if we were successful)
            // see: https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowlongptrw
            PInvoke.Kernel32.SetLastError(0);
            var setOwnerResult = PInvoke.User32.SetWindowLongPtr(hWnd, PInvoke.User32.WindowLongIndexFlags.GWLP_HWNDPARENT, newParentHWndAsIntPtr);
            if (setOwnerResult == IntPtr.Zero)
            {
                // NOTE: this function call may still return zero upon success as well as failure (but with an error result of ERROR_SUCCESS)
                var win32ErrorCode = PInvoke.Kernel32.GetLastError();
                if (win32ErrorCode != PInvoke.Win32ErrorCode.ERROR_SUCCESS)
                {
                    return MorphicResult.ErrorResult(Win32ApiError.Win32Error((int)win32ErrorCode));
                }
            }

            if (oldParentHWndAsIntPtr != IntPtr.Zero)
            {
                // if we need to clean up the old parent window, destroy it now

                // obtain the safe handle we created when we originally created this window owner to make a "do not show in taskbar" window; remove it from the collection since we're going to destroy it
                var oldParentHwndAsSafeHandle = NativeWindow.RemoveHiddenWindowSafeHandle(oldParentHWndAsIntPtr);
                if (oldParentHwndAsSafeHandle is not null)
                {
                    // if we could find the safe handle, dispose of it now; dispose should take care of destroying the window on the original creation thread
                    oldParentHwndAsSafeHandle.Dispose();
                }
                else
                {
                    // NOTE: this shouldn't really happen in practice, so trap it for the debugger if we're actively debugging the app; otherwise, we'll attempt to degrade gracefully
                    Debug.Assert(false, "Could not find native window's safe handle to dispose of; this probably indicates a programming bug.");

                    // if we could not find the safe handle, destroy the parent as a precaution
                    // NOTE: technically we should be destroying the window using the thread it was created on (which is why we use a SafeNativeWindowHandle, above, which takes of that for us)
                    _ = PInvoke.User32.DestroyWindow(oldParentHWndAsIntPtr);
                }
            }

            return MorphicResult.OkResult();
        }

        public static MorphicResult<MorphicUnit, Win32ApiError> SetTopmost(IntPtr hWnd, bool value)
        {
            var hWndInsertAfter = value ? PInvoke.User32.SpecialWindowHandles.HWND_TOPMOST : PInvoke.User32.SpecialWindowHandles.HWND_NOTOPMOST;
            var flags = PInvoke.User32.SetWindowPosFlags.SWP_ASYNCWINDOWPOS | 
                PInvoke.User32.SetWindowPosFlags.SWP_NOACTIVATE | 
                PInvoke.User32.SetWindowPosFlags.SWP_NOMOVE | 
                PInvoke.User32.SetWindowPosFlags.SWP_NOOWNERZORDER | /* TODO: this prevents the window's OWNER'S z-order from changing; I'm not sure if we need this or not; it's probably safe to omit? */
                PInvoke.User32.SetWindowPosFlags.SWP_NOSIZE;
            //
            var success = PInvoke.User32.SetWindowPos(hWnd, hWndInsertAfter, 0, 0, 0, 0, flags);
            if (success == false) {
                var win32ErrorCode = PInvoke.Kernel32.GetLastError();
                return MorphicResult.ErrorResult(Win32ApiError.Win32Error((int)win32ErrorCode));
            }

            return MorphicResult.OkResult();
        }

        public static void SetVisible(IntPtr hWnd, bool value)
        {
            var nCmdShow = value ? PInvoke.User32.WindowShowStyle.SW_SHOW : PInvoke.User32.WindowShowStyle.SW_HIDE;

            // NOTE: this Win32 API doesn't return a success/failure result, so we just call the function and assume it works
            PInvoke.User32.ShowWindow(hWnd, nCmdShow);
        }

        public static MorphicResult<MorphicUnit, Win32ApiError> SetWindowSize(IntPtr hWnd, int width, int height)
        {
            // get the DPI for the current window (or rather the DPI of the monitor where this window is located)
            // NOTE: our application must have its DPI_AWARENESS set to DPI_AWARENESS_PER_MONITOR_AWARE for this to work in multi-monitor scenarios
            var dpiForWindow = PInvoke.User32.GetDpiForWindow(hWnd);
            if (dpiForWindow == 0)
            {
                // an invalid hWnd will cause the GetDpiForWindow to return a value of zero
                // TODO: test this to make sure it returns an actual error!!!
                var win32ErrorCode = PInvoke.Kernel32.GetLastError();
                return MorphicResult.ErrorResult(Win32ApiError.Win32Error((int)win32ErrorCode));
            }

            // calculate a scaled width and height, based on the DPI
            // NOTE: we scale using double-precision floating point value here for efficiency
            var scaleFactor = ((double)dpiForWindow) / 96.0;
            var scaledWidth = (int)((double)width * scaleFactor);
            var scaledHeight = (int)((double)height * scaleFactor);

            var flags = PInvoke.User32.SetWindowPosFlags.SWP_ASYNCWINDOWPOS |
                PInvoke.User32.SetWindowPosFlags.SWP_NOACTIVATE |
                PInvoke.User32.SetWindowPosFlags.SWP_NOMOVE |
                PInvoke.User32.SetWindowPosFlags.SWP_NOOWNERZORDER | /* TODO: this prevents the window's OWNER'S z-order from changing; I'm not sure if we need this or not; it's probably safe to omit? */
                PInvoke.User32.SetWindowPosFlags.SWP_NOZORDER;
            //
            var success = PInvoke.User32.SetWindowPos(hWnd, IntPtr.Zero, 0, 0, scaledWidth, scaledHeight, flags);
            if (success == false)
            {
                var win32ErrorCode = PInvoke.Kernel32.GetLastError();
                return MorphicResult.ErrorResult(Win32ApiError.Win32Error((int)win32ErrorCode));
            }

            return MorphicResult.OkResult();
        }

        public enum WindowStartupLocation
        {
            //CenterOwner, // not currently implemented; there are a lot of scenarios to consider (child window with visible parent vs. child window with hidden/minimized parent vs. top level window, etc.)
            CenterScreen,
            //Manual, // not currently implemented, as this should be the default Windows window condition (and we don't have any logic to determine Left or Top boundaries to use in this scenario)
        }
        public static MorphicResult<MorphicUnit, Win32ApiError> SetWindowStartupLocation(IntPtr hWnd, WindowStartupLocation windowStartupLocation)
        {
            PInvoke.RECT rectToCenterWithin;
            switch (windowStartupLocation)
            {
                case WindowStartupLocation.CenterScreen:
                    {
                        // NOTE: this code finds the monitor which contains the window (or contains more of the window than any other monitor); this appears to be what we want to happen.
                        var monitorHandle = PInvoke.User32.MonitorFromWindow(hWnd, PInvoke.User32.MonitorOptions.MONITOR_DEFAULTTONEAREST);
                        var monitorInfo = new PInvoke.User32.MONITORINFO();
                        monitorInfo.cbSize = Marshal.SizeOf<PInvoke.User32.MONITORINFO>();
                        var getMonitorInfoSuccess = PInvoke.User32.GetMonitorInfo(monitorHandle, ref monitorInfo);
                        if (getMonitorInfoSuccess == false)
                        {
                            var win32ErrorCode = PInvoke.Kernel32.GetLastError();
                            return MorphicResult.ErrorResult(Win32ApiError.Win32Error((int)win32ErrorCode));
                        }

                        // get the working area of the monitor (i.e. desktop, minus the taskbar or any docked windows)
                        rectToCenterWithin = monitorInfo.rcWork;
                    }
                    break;
                default:
                    throw new ArgumentException("Invalid argument", nameof(windowStartupLocation));
            }

            // get the rect of our target window
            PInvoke.RECT windowRect;
            var getWindowRectSuccess = PInvoke.User32.GetWindowRect(hWnd, out windowRect);
            if (getWindowRectSuccess == false)
            {
                var win32ErrorCode = PInvoke.Kernel32.GetLastError();
                return MorphicResult.ErrorResult(Win32ApiError.Win32Error((int)win32ErrorCode));
            }
            var windowCurrentWidth = windowRect.right - windowRect.left;
            var windowCurrentHeight = windowRect.bottom - windowRect.top;

            // calculate the target window's new center based on the current center of the owner/screen
            var centerX = rectToCenterWithin.left + ((rectToCenterWithin.right - rectToCenterWithin.left) / 2);
            var centerY = rectToCenterWithin.top + ((rectToCenterWithin.bottom - rectToCenterWithin.top) / 2);
            //
            var windowNewLeft = centerX - (windowCurrentWidth / 2);
            var windowNewTop = centerY - (windowCurrentHeight / 2);

            // NOTE: there may be scenarios where centering the window will leave part of it off-screen (possibly even off of the active displays' workspace); we may want to test how WPF handles those scenarios, to determine if we should be forcing the window onto the visible screen (and what to do if it's too big for that)

            var flags = PInvoke.User32.SetWindowPosFlags.SWP_ASYNCWINDOWPOS |
                PInvoke.User32.SetWindowPosFlags.SWP_NOACTIVATE |
                PInvoke.User32.SetWindowPosFlags.SWP_NOOWNERZORDER | /* TODO: this prevents the window's OWNER'S z-order from changing; I'm not sure if we need this or not; it's probably safe to omit? */
                PInvoke.User32.SetWindowPosFlags.SWP_NOSIZE | // NOTE: we set the flag to not resize out of an abundance of caution (even though we pass in the existing window size); if we find it necessary to set the width/height as well, we can remove this flag
                PInvoke.User32.SetWindowPosFlags.SWP_NOZORDER;
            //
            var success = PInvoke.User32.SetWindowPos(hWnd, IntPtr.Zero, windowNewLeft, windowNewTop, windowCurrentWidth, windowCurrentHeight, flags);
            if (success == false)
            {
                var win32ErrorCode = PInvoke.Kernel32.GetLastError();
                return MorphicResult.ErrorResult(Win32ApiError.Win32Error((int)win32ErrorCode));
            }

            return MorphicResult.OkResult();
        }
    }
}
