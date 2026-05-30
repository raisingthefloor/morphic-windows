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

namespace Morphic.MorphicBar;

public enum DockingLocation
{
    // floating corner docks, logical (flow-relative); resolve to Left/Right per FlowDirection
    FloatingTopLeading = 0,
    FloatingTopTrailing = 1,
    FloatingBottomLeading = 2,
    FloatingBottomTrailing = 3,
    // floating corner docks, physical (absolute screen corner)
    FloatingTopLeft = 4,
    FloatingTopRight = 5,
    FloatingBottomLeft = 6,
    FloatingBottomRight = 7,

    // taskbar-style edge docks (reserve screen space), logical (flow-relative)
    FixedLeadingMargin = 8,
    FixedTrailingMargin = 9,
    // taskbar-style edge docks (reserve screen space), physical (absolute screen edge)
    FixedLeftMargin = 10,
    FixedRightMargin = 11,
    FixedTopMargin = 12,
    FixedBottomMargin = 13,
}

public static class DockingLocationExtensions
{
    // True for the eight Floating* corner-anchored docks (logical and physical).
    public static bool IsFloatingDockingLocation(this DockingLocation location) =>
        location switch
        {
            /* Floating* (corner-anchored) */
            DockingLocation.FloatingTopLeading
                or DockingLocation.FloatingTopTrailing
                or DockingLocation.FloatingBottomLeading
                or DockingLocation.FloatingBottomTrailing
                //
                or DockingLocation.FloatingTopLeft
                or DockingLocation.FloatingTopRight
                or DockingLocation.FloatingBottomLeft
                or DockingLocation.FloatingBottomRight => true,
            //
            /* Fixed*Margin (taskbar-style) */
            DockingLocation.FixedLeadingMargin
                or DockingLocation.FixedTrailingMargin
                //
                or DockingLocation.FixedLeftMargin
                or DockingLocation.FixedRightMargin
                or DockingLocation.FixedTopMargin
                or DockingLocation.FixedBottomMargin => false,
            //
            _ => throw new System.ComponentModel.InvalidEnumArgumentException(
                nameof(location), (int)location, location.GetType()),
        };

    // True for the six Fixed*Margin edge-docked positions (taskbar-style); the exact complement of IsFloatingDockingLocation.
    public static bool IsFixedDockingLocation(this DockingLocation location) =>
        !location.IsFloatingDockingLocation();

    // True for the eight physical (absolute) docks; False for the six logical (flow-relative) docks.
    public static bool IsPhysicalDockingLocation(this DockingLocation location) =>
        location switch
        {
            DockingLocation.FloatingTopLeading
                or DockingLocation.FloatingTopTrailing
                or DockingLocation.FloatingBottomLeading
                or DockingLocation.FloatingBottomTrailing
                //
                or DockingLocation.FixedLeadingMargin
                or DockingLocation.FixedTrailingMargin => false,
            //
            DockingLocation.FloatingTopLeft
                or DockingLocation.FloatingTopRight
                or DockingLocation.FloatingBottomLeft
                or DockingLocation.FloatingBottomRight
                or DockingLocation.FixedLeftMargin
                or DockingLocation.FixedRightMargin
                or DockingLocation.FixedTopMargin
                or DockingLocation.FixedBottomMargin => true,
            //
            _ => throw new System.ComponentModel.InvalidEnumArgumentException(
                nameof(location), (int)location, location.GetType()),
        };

    // True for the six logical (flow-relative) docks; the exact complement of IsPhysicalDockingLocation.
    public static bool IsLogicalDockingLocation(this DockingLocation location) =>
        !location.IsPhysicalDockingLocation();

    // Resolve a (possibly logical) dock to its physical (absolute) equivalent for the given flow direction; physical inputs pass through unchanged.
    public static DockingLocation ToPhysicalDockingLocation(this DockingLocation location, bool isRightToLeft) =>
        location switch
        {
            DockingLocation.FloatingTopLeading => isRightToLeft ? DockingLocation.FloatingTopRight : DockingLocation.FloatingTopLeft,
            DockingLocation.FloatingTopTrailing => isRightToLeft ? DockingLocation.FloatingTopLeft : DockingLocation.FloatingTopRight,
            DockingLocation.FloatingBottomLeading => isRightToLeft ? DockingLocation.FloatingBottomRight : DockingLocation.FloatingBottomLeft,
            DockingLocation.FloatingBottomTrailing => isRightToLeft ? DockingLocation.FloatingBottomLeft : DockingLocation.FloatingBottomRight,
            //
            DockingLocation.FloatingTopLeft
                or DockingLocation.FloatingTopRight
                or DockingLocation.FloatingBottomLeft
                or DockingLocation.FloatingBottomRight => location,
            //
            DockingLocation.FixedLeadingMargin => isRightToLeft ? DockingLocation.FixedRightMargin : DockingLocation.FixedLeftMargin,
            DockingLocation.FixedTrailingMargin => isRightToLeft ? DockingLocation.FixedLeftMargin : DockingLocation.FixedRightMargin,
            //
            DockingLocation.FixedLeftMargin
                or DockingLocation.FixedRightMargin
                or DockingLocation.FixedTopMargin
                or DockingLocation.FixedBottomMargin => location,
            //
            _ => throw new System.ComponentModel.InvalidEnumArgumentException(
                nameof(location), (int)location, location.GetType()),
        };

    // Convert a physical dock to its logical (flow-relative) equivalent for the given flow direction; logical inputs and the non-mirrorable Top/Bottom fixed margins pass through unchanged.
    public static DockingLocation ToLogicalDockingLocation(this DockingLocation location, bool isRightToLeft) =>
        location switch
        {
            DockingLocation.FloatingTopLeft => isRightToLeft ? DockingLocation.FloatingTopTrailing : DockingLocation.FloatingTopLeading,
            DockingLocation.FloatingTopRight => isRightToLeft ? DockingLocation.FloatingTopLeading : DockingLocation.FloatingTopTrailing,
            DockingLocation.FloatingBottomLeft => isRightToLeft ? DockingLocation.FloatingBottomTrailing : DockingLocation.FloatingBottomLeading,
            DockingLocation.FloatingBottomRight => isRightToLeft ? DockingLocation.FloatingBottomLeading : DockingLocation.FloatingBottomTrailing,
            //
            DockingLocation.FixedLeftMargin => isRightToLeft ? DockingLocation.FixedTrailingMargin : DockingLocation.FixedLeadingMargin,
            DockingLocation.FixedRightMargin => isRightToLeft ? DockingLocation.FixedLeadingMargin : DockingLocation.FixedTrailingMargin,
            //
            DockingLocation.FloatingTopLeading
                or DockingLocation.FloatingTopTrailing
                or DockingLocation.FloatingBottomLeading
                or DockingLocation.FloatingBottomTrailing
                or DockingLocation.FixedLeadingMargin
                or DockingLocation.FixedTrailingMargin
                or DockingLocation.FixedTopMargin
                or DockingLocation.FixedBottomMargin => location,
            //
            _ => throw new System.ComponentModel.InvalidEnumArgumentException(
                nameof(location), (int)location, location.GetType()),
        };
}
