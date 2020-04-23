// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt
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
