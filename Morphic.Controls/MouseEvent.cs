// Copyright 2026 Raising the Floor - US, Inc.
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

// modeled after System.Windows.Forms.MouseEventArgs
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Morphic.Controls;

[Flags]
public enum MouseButtons
{
    Left     = 0x00100000,
    None     = 0x00000000,
    Right    = 0x00200000,
    Middle   = 0x00400000,
    XButton1 = 0x00800000,
    XButton2 = 0x01000000,
}

public class MouseEventArgs : EventArgs
{
    public MouseButtons Button { get; }
    public int Clicks { get; }
    public int X { get; }
    public int Y { get; }

    public MouseEventArgs(MouseButtons button, int clicks, int x, int y)
    {
        this.Button = button;
        this.Clicks = clicks;
        this.X = x;
        this.Y = y;
    }
}
