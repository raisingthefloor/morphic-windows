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
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Morphic.Windows.Native.Windowing.Utils
{
    public class WindowUtils
    {
        public static MorphicResult<MorphicUnit, Win32ApiError> SetShowInTaskbar(IntPtr hWnd, bool value)
        {
            IntPtr oldParentHWndAsIntPtr;
            IntPtr newParentHWndAsIntPtr;

            oldParentHWndAsIntPtr = IntPtr.Zero;

            if (value == true)
            {
                // determine if the window already has a "hidden window" parent; if it does, then capture that window's handle so we can destroy it AFTER disconnecting it as our parent; note that if we tried to destroy it before disconnecting it, we could inadvertently destroy its child--our window--as well

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
                // NOTE: technically we should be destroying the window using the thread it was created on; also...ideally we would dispose of its SafeHandle instead of disposing its hWnd directly
                _ = PInvoke.User32.DestroyWindow(oldParentHWndAsIntPtr);
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
    }
}
