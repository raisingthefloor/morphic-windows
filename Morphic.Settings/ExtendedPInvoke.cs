﻿// Copyright 2021 Raising the Floor - International
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Morphic.Settings
{
    internal struct ExtendedPInvoke
    {
        #region WinUser.h

        internal const int COLOR_BACKGROUND = 1;
        internal const int COLOR_DESKTOP = COLOR_BACKGROUND;

        [DllImport("user32.dll")]
        internal static extern uint GetDoubleClickTime();

        [DllImport("user32.dll")]
        internal static extern uint GetSysColor(int nIndex);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetSysColorBrush(int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool SetDoubleClickTime(uint unnamedParam1);

        [DllImport("user32.dll")]
        internal static extern bool SetSysColors(int cElements, int[] lpaElements, uint[] lpaRgbValues);

        #endregion WinUser.h

    }
}
