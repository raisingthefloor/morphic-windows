﻿// Copyright 2022-2024 Raising the Floor - US, Inc.
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
using Morphic.WindowsNative;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Morphic.WindowsNative.Mouse;

public class Mouse
{
    // NOTE: the GetCursorPos API returns the mouse position in logical pixels; however if the app is DPI-aware then testing has shown that GetCursorPos returns the position in physical pixels (just like GetPhysicalCursorPos)
    public static MorphicResult<Point, IWin32ApiError> GetCurrentPosition()
    {
        // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getcursorpos
        // NOTE: the input desktop must be the current desktop when this function is called; that should not be an issue, but if it is then we need to call OpenInputDesktop (and maybe SetThreadDesktop, using the HDESK returned by OpenInputDesktop) to switch to the proper desktop.
        //       see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getcursorpos#remarks
        System.Drawing.Point point;
        var getCursorPosResult = Windows.Win32.PInvoke.GetCursorPos(out point);
        if (getCursorPosResult == 0)
        {
            var win32ErrorCode = (Windows.Win32.Foundation.WIN32_ERROR)System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            if (win32ErrorCode != Windows.Win32.Foundation.WIN32_ERROR.ERROR_SUCCESS)
            {
                return MorphicResult.ErrorResult<IWin32ApiError>(new IWin32ApiError.Win32Error((uint)win32ErrorCode));
            }
        }

        return MorphicResult.OkResult(point);
    }

   public static MorphicResult<uint?, IWin32ApiError> GetCursorSize()
   {
       var openCursorsSubKeyResult = Morphic.WindowsNative.Registry.CurrentUser.OpenSubKey(@"Control Panel\Cursors");
       if (openCursorsSubKeyResult.IsError == true)
       {
           var win32ApiError = openCursorsSubKeyResult.Error!;
           return MorphicResult.ErrorResult(win32ApiError);
       }
       var cursorsSubKey = openCursorsSubKeyResult.Value!;

       var getValueDataResult = cursorsSubKey.GetValueDataOrNull<uint>("CursorBaseSize");
       if (getValueDataResult.IsError == true)
       {
           switch (getValueDataResult.Error!)
           {
                case Registry.RegistryKey.IRegistryGetValueError.ValueDoesNotExist:
                   return MorphicResult.OkResult<uint?>(null);
                case Registry.RegistryKey.IRegistryGetValueError.Win32Error(Win32ErrorCode: var win32ErrorCode):
                   {
                        var win32ApiError = new IWin32ApiError.Win32Error(unchecked((uint)win32ErrorCode));
                        return MorphicResult.ErrorResult<IWin32ApiError>(win32ApiError);
                   }
               case Registry.RegistryKey.IRegistryGetValueError.TypeMismatch:
               case Registry.RegistryKey.IRegistryGetValueError.UnsupportedType:
               default:
                   throw new MorphicUnhandledErrorException();
           }
       }
       var cursorBaseSize = getValueDataResult.Value!;

       return MorphicResult.OkResult(cursorBaseSize);
   }

   // NOTE: in the future, we may want to consider offering an animation of our cursor's move to the center of the display, via a second (options) parameter
   public static MorphicResult<MorphicUnit, Win32ApiError> MoveCursorToCenterOfDisplay(Morphic.WindowsNative.Display.Display display)
   {
       // get the current display's bounds
       // for PerMonitorV2 DPI-aware clients, this function will return the display rectangle in PHYSICAL pixels
       // for non-DPI-aware clients, this function will return the display rectangle in VIRTUAL pixels
       var getDisplayRectangleInPixelsResult = display.GetDisplayRectangleInPixels();
       if (getDisplayRectangleInPixelsResult.IsError == true)
       {
           var win32ErrorCode = PInvoke.Kernel32.GetLastError();
           if (win32ErrorCode != PInvoke.Win32ErrorCode.ERROR_SUCCESS)
           {
               return MorphicResult.ErrorResult(Win32ApiError.Win32Error((uint)win32ErrorCode));
           }
       }
       var currentDisplayPhysicalBounds = getDisplayRectangleInPixelsResult.Value!;

       // calculate the center point on the display
       var centerX = currentDisplayPhysicalBounds.Left + (currentDisplayPhysicalBounds.Width / 2);
       var centerY = currentDisplayPhysicalBounds.Top + (currentDisplayPhysicalBounds.Height / 2);

       // move the mouse cursor to the center point on the display
       var setCursorPosResult = PInvoke.User32.SetCursorPos(centerX, centerY);
       if (setCursorPosResult == false)
       {
           var win32ErrorCode = PInvoke.Kernel32.GetLastError();
           if (win32ErrorCode != PInvoke.Win32ErrorCode.ERROR_SUCCESS)
           {
               return MorphicResult.ErrorResult(Win32ApiError.Win32Error((uint)win32ErrorCode));
           }
       }

       // return success
       return MorphicResult.OkResult();
   }
}