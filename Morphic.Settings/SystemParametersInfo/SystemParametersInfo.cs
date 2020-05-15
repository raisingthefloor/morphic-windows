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
using System.Runtime.InteropServices;

namespace Morphic.Settings
{

    /// <summary>
    /// An SPI implementation that calls the SystemParametersInfo function in windows
    /// </summary>
    public class SystemParametersInfo: ISystemParametersInfo
    {

        public bool Call(Action action, int parameter1, object? parameter2, bool updateUserProfile = false, bool sendChange = false)
        {
            var param2Handle = GCHandle.Alloc(parameter2);
            int param3 = 0;
            if (updateUserProfile)
            {
                param3 |= 0x1;
            }
            if (sendChange)
            {
                param3 |= 0x2;
            }
            var result = SystemParametersInfoW((int)action, parameter1, GCHandle.ToIntPtr(param2Handle), param3);
            param2Handle.Free();
            return result;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SystemParametersInfoW(int uiAction, int uiParam, IntPtr pvParam, int fWinIni);

        public enum Action
        {
            SetCursors = 0x57
        }
    }
}
