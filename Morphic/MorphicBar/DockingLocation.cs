// Copyright 2026 Raising the Floor - US, Inc.
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
using System.Text;

namespace Morphic.MorphicBar;

public enum DockingLocation
{
    // floating docked corner
    FloatingTopLeft,
    FloatingTopRight,
    FloatingBottomLeft,
    FloatingBottomRight,
    //
    // traditional "docked" locations (like the taskbar, taking up screen real estate)
    FixedLeftMargin,
    FixedRightMargin,
    FixedTopMargin,
    FixedBottomMargin,
}

public static class DockingLocationExtensions
{
    // True for the four Floating* corner-anchored docks.
    public static bool IsFloatingDockingLocation(this DockingLocation location) =>
        location switch
        {
            /* Floating* (corner-anchored) */
            DockingLocation.FloatingTopLeft
                or DockingLocation.FloatingTopRight
                or DockingLocation.FloatingBottomLeft
                or DockingLocation.FloatingBottomRight => true,
            //
            /* Fixed*Margin (taskbar-style) */
            DockingLocation.FixedLeftMargin
                or DockingLocation.FixedRightMargin
                or DockingLocation.FixedTopMargin
                or DockingLocation.FixedBottomMargin => false,
            //
            _ => throw new System.ComponentModel.InvalidEnumArgumentException(
                nameof(location), (int)location, location.GetType()),
        };

    // True for the four Fixed*Margin edge-docked positions (taskbar-style).
    public static bool IsFixedDockingLocation(this DockingLocation location) =>
        location switch
        {
            /* Fixed*Margin (taskbar-style) */
            DockingLocation.FixedLeftMargin
                or DockingLocation.FixedRightMargin
                or DockingLocation.FixedTopMargin
                or DockingLocation.FixedBottomMargin => true,
            //
            /* Floating* (corner-anchored) */
            DockingLocation.FloatingTopLeft
                or DockingLocation.FloatingTopRight
                or DockingLocation.FloatingBottomLeft
                or DockingLocation.FloatingBottomRight => false,
            //
            _ => throw new System.ComponentModel.InvalidEnumArgumentException(
                nameof(location), (int)location, location.GetType()),
        };
}
