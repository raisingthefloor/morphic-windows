//
// Mouse.cs
// Morphic support library for Windows
//
// Copyright © 2020 Raising the Floor -- US Inc. All rights reserved.
//
// The R&D leading to these results received funding from the
// Department of Education - Grant H421A150005 (GPII-APCP). However,
// these results do not necessarily represent the policy of the
// Department of Education, and you should not assume endorsement by the
// Federal Government.

using System;

namespace Morphic.Windows.Native
{
    public class Mouse
    {
        public static Boolean? GetMouseButtonsAreSwapped()
        {
            // first, make sure that a mouse is present
            if (Mouse.GetMouseIsPresent() == false)
            {
                return null;
            }

            var mouseButtonsAreSwapped = WindowsApi.GetSystemMetrics(WindowsApi.SystemMetricIndex.SM_SWAPBUTTON);
            return mouseButtonsAreSwapped != 0 ? true : false;
        }

        // NOTE: historically, virtually all Windows machines will believe that a mouse is present: even if no mouse is plugged in, the presence of a "mouse port" can also register as presence of a mouse
        public static Boolean GetMouseIsPresent()
        {
            var mouseIsPresent = WindowsApi.GetSystemMetrics(WindowsApi.SystemMetricIndex.SM_MOUSEPRESENT);
            return mouseIsPresent != 0 ? true : false;
        }

        //public static Int32? GetMousePointerSpeed()
        //{
        //    // first, make sure that a mouse is present
        //    if (MorphicMouse.GetMouseIsPresent() == false)
        //    {
        //        return null;
        //    }

        //    Int32[] mouseInfo = new Int32[3];
        //    var getMouseInfoResult = SystemParametersInfo(SPI_GETMOUSE, 0, &mouseInfo, 0);
        //    if (getMouseInfoResult == 0)
        //    {
        //        return null;
        //    }

        //    // mouse pointer speed is stored in mouseInfo at element index 2
        //    return mouseInfo[2];
        //}

    }
}
