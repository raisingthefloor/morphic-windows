// Copyright 2020-2026 Raising the Floor - US, Inc.
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

using Morphic.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace Morphic.WindowsNative.Mouse;

public class Mouse
{
    // NOTE: the GetCursorPos API returns the mouse position in logical pixels; however if the app is DPI-aware then testing has shown that GetCursorPos returns the position in physical pixels (just like GetPhysicalCursorPos)
    //
    // NOT IMPLEMENTED: per MSDN, GetCursorPos requires the calling thread's desktop to be the
    // input desktop, and the recommended remedy is to call OpenInputDesktop (and SetThreadDesktop
    // with the returned HDESK) before calling GetCursorPos. We deliberately do NOT do that here.
    //
    // When would the dance actually be needed?
    //   * The calling code is running on a thread whose desktop is not the input desktop. Realistic
    //     cases: a service in session 0; a worker that was deliberately bound to an alternate
    //     desktop; a custom sandbox host. For an interactive UI app running in the user's session
    //     on the default desktop, the calling thread IS already on the input desktop during normal
    //     operation. (When the input desktop is temporarily Winlogon/Secure Desktop -- screen
    //     locked, UAC prompt, Ctrl+Alt+Del -- the user can't reach our buttons anyway, so the
    //     question is moot for our current callers.)
    //
    // Why we have not implemented it:
    //   * SetThreadDesktop fails on any thread that owns windows or hooks, which a UI thread
    //     always does. To make the dance work in practice, we'd need a dedicated worker thread
    //     with no UI, then marshal the call onto it -- significant plumbing for a path none of
    //     the current callers exercise.
    //   * We don't have a target app or test harness that runs in a context where the dance
    //     would matter. Without something to validate it against, adding the code blind would
    //     just be untested complexity.
    //
    // If/when a caller appears that needs this (a service, session-0 host, etc.), revisit:
    // an opt-in `bool ensureOnInputDesktop = false` parameter is the natural shape, with the
    // caller responsible for being on a thread that owns no UI when they pass `true`.
    //
    // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getcursorpos#remarks
    public static MorphicResult<Point, IWin32ApiError> GetCurrentPosition()
    {
        // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getcursorpos
        System.Drawing.Point point;
        var getCursorPosResult = Windows.Win32.PInvoke.GetCursorPos(out point);
        if (getCursorPosResult == 0)
        {
            var win32ErrorCode = (Windows.Win32.Foundation.WIN32_ERROR)System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            return MorphicResult.ErrorResult<IWin32ApiError>(new IWin32ApiError.Win32Error((uint)win32ErrorCode));
        }

        return MorphicResult.OkResult(point);
    }

    // NOTE: in the future, we may want to consider offering an animation of our cursor's move to the center of the display, via a second (options) parameter
    public static MorphicResult<MorphicUnit, IWin32ApiError> MoveCursorToCenterOfDisplay(Morphic.WindowsNative.Display.Display display)
    {
        // get the current display's bounds
        // for PerMonitorV2 DPI-aware clients, this function will return the display rectangle in PHYSICAL pixels
        // for non-DPI-aware clients, this function will return the display rectangle in VIRTUAL pixels
        var getDisplayRectangleInPixelsResult = display.GetDisplayRectangleInPixels();
        if (getDisplayRectangleInPixelsResult.IsError == true)
        {
            var win32ErrorCode = getDisplayRectangleInPixelsResult.Error switch
            {
                IWin32ApiError.Win32Error(var win32Error) => win32Error,
                _ => throw new MorphicUnhandledErrorException(),
            };
            return MorphicResult.ErrorResult<IWin32ApiError>(new IWin32ApiError.Win32Error((uint)win32ErrorCode));
        }
        var currentDisplayPhysicalBounds = getDisplayRectangleInPixelsResult.Value!;

        // calculate the center point on the display
        var centerX = currentDisplayPhysicalBounds.Left + (currentDisplayPhysicalBounds.Width / 2);
        var centerY = currentDisplayPhysicalBounds.Top + (currentDisplayPhysicalBounds.Height / 2);

        // move the mouse cursor to the center point on the display
        var setCursorPosResult = Windows.Win32.PInvoke.SetCursorPos(centerX, centerY);
        if (setCursorPosResult == false)
        {
            var win32ErrorCode = (Windows.Win32.Foundation.WIN32_ERROR)System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            return MorphicResult.ErrorResult<IWin32ApiError>(new IWin32ApiError.Win32Error((uint)win32ErrorCode));
        }

        // return success
        return MorphicResult.OkResult();
    }

    // Moves the mouse cursor to the specified position (physical virtual-screen pixels for a
    // PerMonitorV2 DPI-aware client, matching GetCurrentPosition's coordinate space).
    public static MorphicResult<MorphicUnit, IWin32ApiError> MoveCursorToPosition(System.Drawing.Point position)
    {
        var setCursorPosResult = Windows.Win32.PInvoke.SetCursorPos(position.X, position.Y);
        if (setCursorPosResult == false)
        {
            var win32ErrorCode = (Windows.Win32.Foundation.WIN32_ERROR)System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            return MorphicResult.ErrorResult<IWin32ApiError>(new IWin32ApiError.Win32Error((uint)win32ErrorCode));
        }

        return MorphicResult.OkResult();
    }
}
