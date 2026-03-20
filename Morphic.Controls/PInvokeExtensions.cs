// Copyright 2020-2026 Raising the Floor - US, Inc.
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

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Morphic.Controls;

internal class PInvokeExtensions
{
    #region wingdi 

    public static readonly IntPtr HGDI_ERROR = new IntPtr(-1);

    #endregion wingdi

    #region winuser

    internal static readonly uint HOVER_DEFAULT = 0xFFFFFFFF;

    internal const ushort MK_LBUTTON = 0x0001;
    internal const ushort MK_RBUTTON = 0x0002;

    // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowlongptrw
    internal static IntPtr GetWindowLongPtr_IntPtr(Windows.Win32.Foundation.HWND hWnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX nIndex)
    {
        if (IntPtr.Size == 4)
        {
            return (nint)Windows.Win32.PInvoke.GetWindowLong(hWnd, nIndex);
        }
        else
        {
            return PInvokeExtensions.GetWindowLongPtr(hWnd, nIndex);
        }
    }
    //
    // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowlongptrw
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX nIndex);

    // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowlongptrw
    internal static IntPtr SetWindowLongPtr_IntPtr(Windows.Win32.Foundation.HWND hWnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX nIndex, IntPtr dwNewLong)
    {
#if PLATFORM_X86
        return (nint)Windows.Win32.PInvoke.SetWindowLong(hWnd, nIndex, (int)dwNewLong);
#else
        if (IntPtr.Size == 4)
        {
            return (nint)Windows.Win32.PInvoke.SetWindowLong(hWnd, nIndex, (int)dwNewLong);
        }
        else
        {
            return Windows.Win32.PInvoke.SetWindowLongPtr(hWnd, nIndex, dwNewLong);
        }
#endif
    }

    #endregion winuser
}
