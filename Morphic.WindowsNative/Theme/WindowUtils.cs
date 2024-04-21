// Copyright 2020-2024 Raising the Floor - US, Inc.
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
using System.Runtime.InteropServices;

namespace Morphic.WindowsNative.Theme;

public static class WindowUtils
{
    public static MorphicResult<MorphicUnit, MorphicUnit> SetNonClientUIDarkModeAttribute(nint hWnd, bool value)
    {
        var hwnd = new Windows.Win32.Foundation.HWND(hWnd);

        var sizeOfValue = (uint)Marshal.SizeOf(value);

        Windows.Win32.Foundation.HRESULT setWindowAttributeResult;
        unsafe
        {
            setWindowAttributeResult = Windows.Win32.PInvoke.DwmSetWindowAttribute(hwnd, Windows.Win32.Graphics.Dwm.DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, &value, sizeOfValue);
        }
        if (setWindowAttributeResult != Windows.Win32.Foundation.HRESULT.S_OK)
        {
            return MorphicResult.ErrorResult();
        }

        return MorphicResult.OkResult();
    }
}
