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

using Morphic.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Morphic.MorphicBar;

internal class LayoutUtils
{
    private const int WINDOW_CORNER_DOCKING_DISTANCE_FROM_SCREEN_EDGE_IN_DEVICE_UNITS = 5;

    internal static MorphicResult<double, MorphicUnit> GetRasterizationScaleForMonitor(Windows.Win32.Graphics.Gdi.HMONITOR hMonitor)
    {
        var getDpiForMonitorResult = Windows.Win32.PInvoke.GetDpiForMonitor(hMonitor, Windows.Win32.UI.HiDpi.MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out uint dpiX, out uint dpiY);
        if (getDpiForMonitorResult != Windows.Win32.Foundation.HRESULT.S_OK)
        {
            Debug.Assert(false);
            return MorphicResult.ErrorResult();
        }

        double rasterizationScale = dpiX / 96.0; // 96 DPI = 1.0x scale
        return MorphicResult.OkResult(rasterizationScale);
    }

    //

    internal static MorphicResult<Windows.Win32.Foundation.RECT, MorphicUnit> GetRectForDockingLocation(Morphic.MorphicBar.DockingLocation dockingLocation, Microsoft.UI.Xaml.Controls.Orientation orientation, uint unscaledRequestedLength, uint unscaledRequestedThickness, Windows.Win32.Graphics.Gdi.HMONITOR hMonitor)
    {
        /* STEP 1: get the monitor resolution, coord space metrics (full screen and working area) and rasterization scale ('zoom' level) */

        // get the monitor's info (including dimensions)
        var monitorInfo = new Windows.Win32.Graphics.Gdi.MONITORINFO();
        monitorInfo.cbSize = (uint)Marshal.SizeOf<Windows.Win32.Graphics.Gdi.MONITORINFO>();
        var getMonitorInfoResult = Windows.Win32.PInvoke.GetMonitorInfo(hMonitor, ref monitorInfo);
        if (getMonitorInfoResult == 0)
        {
            Debug.Assert(false);
            return MorphicResult.ErrorResult();
        }

        // get the full monitor area in universal coords (i.e. resolution including taskbar, etc.)
        var monitorFullRect = monitorInfo.rcMonitor;
        //
        // and get the working area in universal coords (i.e. full rect minus taskbar, etc.)
        var monitorWorkingAreaRect = monitorInfo.rcWork;

        // get the RasterizationScale for this monitor
        var getRasterizationScaleResult = LayoutUtils.GetRasterizationScaleForMonitor(hMonitor);
        if (getRasterizationScaleResult.IsError)
        {
            return MorphicResult.ErrorResult();
        }
        double rasterizationScale = getRasterizationScaleResult!.Value;

        /* STEP 2: calculate target RECT for MorphicBar */
        var targetRect = LayoutUtils.GetRectForDockingLocation(dockingLocation, orientation, unscaledRequestedLength, unscaledRequestedThickness, monitorFullRect, monitorWorkingAreaRect, rasterizationScale);
        return MorphicResult.OkResult(targetRect);
    }

    private static Windows.Win32.Foundation.RECT GetRectForDockingLocation(Morphic.MorphicBar.DockingLocation dockingLocation, Microsoft.UI.Xaml.Controls.Orientation orientation, uint unscaledRequestedLength, uint unscaledRequestedThickness, Windows.Win32.Foundation.RECT fullRect, Windows.Win32.Foundation.RECT workingAreaRect, double rasterizationScale)
    {
        const string ERROR_INVALID_DOCKING_LOCATION_AND_ORIENTATION_COMBINATION = "The provided 'dockingLocation' argument is invalid in combination with the provided 'orientation' argument.";

        int SCALED_LENGTH = (int)(unscaledRequestedLength * rasterizationScale);
        int SCALED_THICKNESS = (int)(unscaledRequestedThickness * rasterizationScale);
        int SCALED_CORNER_KEEPAWAY_PADDING = (int)(WINDOW_CORNER_DOCKING_DISTANCE_FROM_SCREEN_EDGE_IN_DEVICE_UNITS * rasterizationScale);

        /* STEP 1: caculate ideal coordinates (which might overlap the edge of the working area) */

        int targetLeft;
        int targetTop;
        int targetRight;
        int targetBottom;
        //
        switch (orientation)
        {
            case Microsoft.UI.Xaml.Controls.Orientation.Horizontal:
                {
                    // first half of HORIZONTAL MORPHICBAR calculation: calculate the left and right (X) coordinates
                    switch (dockingLocation)
                    {
                        case DockingLocation.FloatingTopLeft:
                        case DockingLocation.FloatingBottomLeft:
                            targetLeft = workingAreaRect.left + SCALED_CORNER_KEEPAWAY_PADDING;
                            targetRight = targetLeft + SCALED_LENGTH;
                            break;
                        case DockingLocation.FloatingTopRight:
                        case DockingLocation.FloatingBottomRight:
                            targetRight = workingAreaRect.right - SCALED_CORNER_KEEPAWAY_PADDING;
                            targetLeft = targetRight - SCALED_LENGTH;
                            break;
                        case DockingLocation.FixedTopMargin:
                        case DockingLocation.FixedBottomMargin:
                            targetLeft = workingAreaRect.left;
                            targetRight = workingAreaRect.right;
                            break;
                        case DockingLocation.FixedLeftMargin:
                        case DockingLocation.FixedRightMargin:
                            throw new ArgumentException(ERROR_INVALID_DOCKING_LOCATION_AND_ORIENTATION_COMBINATION);
                        default:
                            throw new MorphicUnhandledCaseException(dockingLocation);
                    }

                    // second half of HORIZONTAL MORPHICBAR calculation: calculate the top and bottom (Y) coordinates
                    switch (dockingLocation)
                    {
                        case DockingLocation.FloatingTopLeft:
                        case DockingLocation.FloatingTopRight:
                            targetTop = workingAreaRect.top + SCALED_CORNER_KEEPAWAY_PADDING;
                            targetBottom = targetTop + SCALED_THICKNESS;
                            break;
                        case DockingLocation.FloatingBottomLeft:
                        case DockingLocation.FloatingBottomRight:
                            targetBottom = workingAreaRect.bottom - SCALED_CORNER_KEEPAWAY_PADDING;
                            targetTop = targetBottom - SCALED_THICKNESS;
                            break;
                        case DockingLocation.FixedTopMargin:
                            targetTop = workingAreaRect.top;
                            targetBottom = targetTop + SCALED_THICKNESS;
                            break;
                        case DockingLocation.FixedBottomMargin:
                            targetBottom = workingAreaRect.bottom;
                            targetTop = targetBottom - SCALED_THICKNESS;
                            break;
                        case DockingLocation.FixedLeftMargin:
                        case DockingLocation.FixedRightMargin:
                            throw new ArgumentException(ERROR_INVALID_DOCKING_LOCATION_AND_ORIENTATION_COMBINATION);
                        default:
                            throw new MorphicUnhandledCaseException(dockingLocation);
                    }
                }
                break;
            case Microsoft.UI.Xaml.Controls.Orientation.Vertical:
                {
                    // first half of VERTICAL MORPHICBAR calculation: calculate the left and right (X) coordinates
                    switch (dockingLocation)
                    {
                        case DockingLocation.FloatingTopLeft:
                        case DockingLocation.FloatingBottomLeft:
                            targetLeft = workingAreaRect.left + SCALED_CORNER_KEEPAWAY_PADDING;
                            targetRight = targetLeft + SCALED_THICKNESS;
                            break;
                        case DockingLocation.FloatingTopRight:
                        case DockingLocation.FloatingBottomRight:
                            targetRight = workingAreaRect.right - SCALED_CORNER_KEEPAWAY_PADDING;
                            targetLeft = targetRight - SCALED_THICKNESS;
                            break;
                        case DockingLocation.FixedLeftMargin:
                            targetLeft = workingAreaRect.left;
                            targetRight = targetLeft + SCALED_THICKNESS;
                            break;
                        case DockingLocation.FixedRightMargin:
                            targetRight = workingAreaRect.right;
                            targetLeft = targetRight - SCALED_THICKNESS;
                            break;
                        case DockingLocation.FixedTopMargin:
                        case DockingLocation.FixedBottomMargin:
                            throw new ArgumentException(ERROR_INVALID_DOCKING_LOCATION_AND_ORIENTATION_COMBINATION);
                        default:
                            throw new MorphicUnhandledCaseException(dockingLocation);
                    }

                    // second half of VERTICAL MORPHICBAR calculation: calculate the top and bottom (Y) coordinates
                    switch (dockingLocation)
                    {
                        case DockingLocation.FloatingTopLeft:
                        case DockingLocation.FloatingTopRight:
                            targetTop = workingAreaRect.top + SCALED_CORNER_KEEPAWAY_PADDING;
                            targetBottom = targetTop + SCALED_LENGTH;
                            break;
                        case DockingLocation.FloatingBottomLeft:
                        case DockingLocation.FloatingBottomRight:
                            targetBottom = workingAreaRect.bottom - SCALED_CORNER_KEEPAWAY_PADDING;
                            targetTop = targetBottom - SCALED_LENGTH;
                            break;
                        case DockingLocation.FixedLeftMargin:
                        case DockingLocation.FixedRightMargin:
                            targetTop = workingAreaRect.top;
                            targetBottom = workingAreaRect.bottom;
                            break;
                        case DockingLocation.FixedTopMargin:
                        case DockingLocation.FixedBottomMargin:
                            throw new ArgumentException(ERROR_INVALID_DOCKING_LOCATION_AND_ORIENTATION_COMBINATION);
                        default:
                            throw new MorphicUnhandledCaseException(dockingLocation);
                    }
                }
                break;
            default:
                throw new MorphicUnhandledCaseException(dockingLocation);
        }

        /* STEP 2: clamp the RECT if necessary (i.e. prevent overflow from the working area) */
        switch (dockingLocation)
        {
            case DockingLocation.FloatingTopLeft:
            case DockingLocation.FloatingTopRight:
            case DockingLocation.FloatingBottomLeft:
            case DockingLocation.FloatingBottomRight:
                targetLeft = Math.Max(targetLeft, workingAreaRect.left + SCALED_CORNER_KEEPAWAY_PADDING);
                targetRight = Math.Min(targetRight, workingAreaRect.right - SCALED_CORNER_KEEPAWAY_PADDING);
                targetTop = Math.Max(targetTop, workingAreaRect.top + SCALED_CORNER_KEEPAWAY_PADDING);
                targetBottom = Math.Min(targetBottom, workingAreaRect.bottom - SCALED_CORNER_KEEPAWAY_PADDING);
                break;
            case DockingLocation.FixedLeftMargin:
            case DockingLocation.FixedRightMargin:
            case DockingLocation.FixedTopMargin:
            case DockingLocation.FixedBottomMargin:
                targetLeft = Math.Max(targetLeft, workingAreaRect.left);
                targetRight = Math.Min(targetRight, workingAreaRect.right);
                targetTop = Math.Max(targetTop, workingAreaRect.top);
                targetBottom = Math.Min(targetBottom, workingAreaRect.bottom);
                break;
            default:
                throw new MorphicUnhandledCaseException(dockingLocation);
        }

        /* STEP 3: if adding keepaway padding has created a "negative space" area (i.e. fewer than SCALED_CORNER_KEEPAWAY_PADDING * 2 pixels were available), make that size dimension 0 pixels */
        targetRight = Math.Max(targetLeft, targetRight);
        targetBottom = Math.Max(targetTop, targetBottom);

        // return the target rect points as a RECT
        return new Windows.Win32.Foundation.RECT(targetLeft, targetTop, targetRight, targetBottom);
    }

    //

    internal static MorphicResult<(Morphic.MorphicBar.DockingLocation DockingLocation, Microsoft.UI.Xaml.Controls.Orientation Orientation), MorphicUnit> CalculatePreviewDockingLocation(Windows.Win32.Foundation.RECT monitorFullRect, Windows.Win32.Foundation.RECT monitorWorkingRect, System.Drawing.Point currentPointerPosition, System.Drawing.Point horizontalDockingCenterPoint, System.Drawing.Point verticalDockingCenterPoint, double rasterizationScale, uint verticalBarDockingHitAreaWidth)
    {
        // TODO: pass in length/thickness for "vertical dock hit area"
        int SCALED_VERTICAL_BAR_DOCKING_HIT_AREA_WIDTH = (int)(verticalBarDockingHitAreaWidth * rasterizationScale);

        Morphic.MorphicBar.DockingLocation dockingLocation;
        Microsoft.UI.Xaml.Controls.Orientation orientation;
        if (currentPointerPosition.X >= monitorFullRect.left && currentPointerPosition.X < monitorWorkingRect.left + SCALED_VERTICAL_BAR_DOCKING_HIT_AREA_WIDTH)
        {
            // left 'vertical' working area
            if (verticalDockingCenterPoint.Y >= monitorFullRect.Y && verticalDockingCenterPoint.Y < monitorWorkingRect.top + (monitorWorkingRect.Height / 2))
            {
                // top half of 'vertical' working area (top-left vertical bar)
                dockingLocation = DockingLocation.FloatingTopLeft;
            }
            else
            {
                // bottom half of 'vertical' working area (bottom-left vertical bar)
                dockingLocation = DockingLocation.FloatingBottomLeft;
            }
            orientation = Microsoft.UI.Xaml.Controls.Orientation.Vertical;
        }
        else if (currentPointerPosition.X >= monitorWorkingRect.right - SCALED_VERTICAL_BAR_DOCKING_HIT_AREA_WIDTH && currentPointerPosition.X < monitorFullRect.right)
        {
            // right 'vertical' working area
            if (verticalDockingCenterPoint.Y >= monitorFullRect.Y && verticalDockingCenterPoint.Y < monitorWorkingRect.Y + (monitorWorkingRect.Height / 2))
            {
                // top half of 'vertical' area (top-right vertical bar)
                dockingLocation = DockingLocation.FloatingTopRight;
            }
            else
            {
                // bottom half of 'vertical' area (bottom-right vertical bar)
                dockingLocation = DockingLocation.FloatingBottomRight;
            }
            orientation = Microsoft.UI.Xaml.Controls.Orientation.Vertical;
        }
        else if (horizontalDockingCenterPoint.X >= monitorFullRect.left && horizontalDockingCenterPoint.X < monitorWorkingRect.left + (monitorWorkingRect.Width / 2))
        {
            // left half of working area
            if (currentPointerPosition.Y <= monitorWorkingRect.top + (monitorWorkingRect.Height / 2))
            {
                // top-left quarter of working area (top-left horizontal bar)
                dockingLocation = DockingLocation.FloatingTopLeft;
            }
            else
            {
                // bottom-left quarter of working area (bottom-left horizontal bar)
                dockingLocation = DockingLocation.FloatingBottomLeft;
            }
            orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal;
        }
        else if (horizontalDockingCenterPoint.X < monitorFullRect.right && horizontalDockingCenterPoint.X >= monitorWorkingRect.left + (monitorWorkingRect.Width / 2))
        {
            // right half of working area
            //
            if (currentPointerPosition.Y <= monitorWorkingRect.top + (monitorWorkingRect.Height / 2))
            {
                // top-right quarter of working area (top-right horizontal bar)
                dockingLocation = DockingLocation.FloatingTopRight;
            }
            else
            {
                // bottom-right quarter of working area (bottom-right horizontal bar)
                dockingLocation = DockingLocation.FloatingBottomRight;
            }
            orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal;
        }
        else
        {
            // center point is off-screen, somehow
            return MorphicResult.ErrorResult();
        }

        return MorphicResult.OkResult((dockingLocation, orientation));
    }
}
