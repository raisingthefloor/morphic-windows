// Copyright 2022 Raising the Floor - US, Inc.
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

namespace Morphic.WindowsNative.Windowing.Utils;

using Morphic.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

public class AcrylicWindowUtils
{
    public static MorphicResult<MorphicUnit, Win32ApiError> EnableAcrylicBackground(IntPtr hwnd, uint argbBackgroundColor)
    {
        // NOTE: the composition API appears to use ABGR color rather than ARGB, so we convert it here before assigning it to "GradientColor"
        uint gradientColor = AcrylicWindowUtils.ConvertArgbToAbgr(argbBackgroundColor);

        var accentPolicy = new ExtendedPInvoke.ACCENT_POLICY()
        {
            AccentState = ExtendedPInvoke.ACCENT_STATE.ACCENT_ENABLE_ACRYLICBLURBEHIND,
            AccentFlags = 0, // no flags
            GradientColor = gradientColor,
            AnimationId = 0, // no animation id
        };
        var sizeOfAccentPolicy = Marshal.SizeOf(accentPolicy);

        IntPtr pointerToAccentPolicy = Marshal.AllocHGlobal(sizeOfAccentPolicy);
        try
        {
            Marshal.StructureToPtr(accentPolicy, pointerToAccentPolicy, false);

            var attributeData = new ExtendedPInvoke.WINDOWCOMPOSITIONATTRIBDATA();
            attributeData.attribute = ExtendedPInvoke.WINDOWCOMPOSITIONATTRIB.WCA_ACCENT_POLICY;
            attributeData.pvData = pointerToAccentPolicy;
            attributeData.cbData = (uint)sizeOfAccentPolicy;

            var result = ExtendedPInvoke.SetWindowCompositionAttribute(hwnd, ref attributeData);
            if (result == false)
            {
                var win32ErrorCode = Marshal.GetLastWin32Error();
                return MorphicResult.ErrorResult(Win32ApiError.Win32Error((uint)win32ErrorCode));
            }
        }
        finally
        {
            Marshal.FreeHGlobal(pointerToAccentPolicy);
        }

        return MorphicResult.OkResult();
    }

    private static uint ConvertArgbToAbgr(uint argb)
    {
        var result = (argb & 0xFF000000) |
                     ((argb & 0x00FF0000) >> 8) |
                     ((argb & 0x0000FF00) << 8) |
                     ((argb & 0x000000FF));

        return result;
    }
}
