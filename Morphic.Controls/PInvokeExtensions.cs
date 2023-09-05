// Copyright 2020-2023 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-controls-lib-cs/blob/master/LICENSE.txt
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

using System;
using System.Runtime.InteropServices;

namespace Morphic.Controls;

internal class PInvokeExtensions
{
     #region winuser

     internal static IntPtr GetWindowLongPtr_IntPtr(Windows.Win32.Foundation.HWND hWnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX nIndex)
     {
          if (IntPtr.Size == 4)
          {
               return (nint)Windows.Win32.PInvoke.GetWindowLong(hWnd, nIndex);
          }
          else
          {
               return PInvokeExtensions.GetWindowLongPtr(hWnd.Value, nIndex);
          }
     }

     // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowlongptrw
     [DllImport("user32.dll", SetLastError = true)]
     private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX nIndex);

     // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-redrawwindow
     [DllImport("user32.dll")]
     internal static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, Windows.Win32.Graphics.Gdi.REDRAW_WINDOW_FLAGS flags);

     internal static IntPtr SetWindowLongPtr_IntPtr(Windows.Win32.Foundation.HWND hWnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX nIndex, IntPtr dwNewLong)
     {
          if (IntPtr.Size == 4)
          {
               return (nint)Windows.Win32.PInvoke.SetWindowLong(hWnd, nIndex, (int)dwNewLong);
          }
          else
          {
               return PInvokeExtensions.SetWindowLongPtr(hWnd, nIndex, dwNewLong);
          }
     }

     [DllImport("user32.dll", SetLastError = true)]
     private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX nIndex, IntPtr dwNewLong);

     #endregion winuser
}
