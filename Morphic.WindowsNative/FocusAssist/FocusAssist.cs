// Copyright 2021-2022 Raising the Floor - US, Inc.
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

// TODO: this code is neither 100% robust nor 100% tested; it is _very_ reverse-engineered and further research to determine the failure of some "SetState: Off" commands is still needed

namespace Morphic.WindowsNative.FocusAssist
{
    using Morphic.Core;
    using System;
    using System.Runtime.InteropServices;

    public class FocusAssist
    {
        // NOTE from ContentDeliveryManager.Utilities.dll: "This event signals when Quiet Hours mode has changed state in shell"
        // data0/data1 captured from multiple Internet sources and also documented here: https://github.com/googleprojectzero/sandbox-attacksurface-analysis-tools/blob/80d7fcc8df9c3160c814c60f5121ae46c560a1b5/NtApiDotNet/NtWnfWellKnownNames.cs
        private static ExtendedPInvoke.WNF_STATE_NAME WNF_SHEL_QUIET_MOMENT_SHELL_MODE_CHANGED = new ExtendedPInvoke.WNF_STATE_NAME() { data0 = 0xa3bf5075, data1 = 0xd83063e };
        private static ExtendedPInvoke.WNF_STATE_NAME WNF_SHEL_QUIETHOURS_ACTIVE_PROFILE_CHANGED = new ExtendedPInvoke.WNF_STATE_NAME() { data0 = 0xa3Bf1c75, data1 = 0xd83063e };

        public enum FocusAssistState
        {
            Off = 0,
            PriorityOnly = 1,
            AlarmsOnly = 2,
        }

        public static MorphicResult<FocusAssistState, MorphicUnit> GetState()
        {
            var queryWnfStateDataResult = FocusAssist.QueryWnfStateData(FocusAssist.WNF_SHEL_QUIETHOURS_ACTIVE_PROFILE_CHANGED);
            if (queryWnfStateDataResult.IsError == true)
            {
                return MorphicResult.ErrorResult();
            }
            var stateData = queryWnfStateDataResult.Value!;

            // parse the result
            if (stateData.Length != 4)
            {
                return MorphicResult.ErrorResult();
            }

            if (stateData[0] == 0x00 && stateData[1] == 0x00 && stateData[2] == 0x00 && stateData[3] == 0x00)
            {
                // focus assist off
                return MorphicResult.OkResult(FocusAssistState.Off);
            }
            else if (stateData[0] == 0x01 && stateData[1] == 0x00 && stateData[2] == 0x00 && stateData[3] == 0x00)
            {
                // focus assist on, priority only
                return MorphicResult.OkResult(FocusAssistState.PriorityOnly);
            }
            else if (stateData[0] == 0x02 && stateData[1] == 0x00 && stateData[2] == 0x00 && stateData[3] == 0x00)
            {
                // focus assist on, alarms only
                return MorphicResult.OkResult(FocusAssistState.AlarmsOnly);
            }
            else
            {
                // unknown state
                return MorphicResult.ErrorResult();
            }
        }

        public static MorphicResult<MorphicUnit, MorphicUnit> SetState(FocusAssistState value)
        {
            byte[] buffer;
            switch (value)
            {
                case FocusAssistState.AlarmsOnly:
                    buffer = new byte[] { 0x02, 0x00, 0x00, 0x00 };
                    break;
                case FocusAssistState.PriorityOnly:
                    buffer = new byte[] { 0x01, 0x00, 0x00, 0x00 };
                    break;
                case FocusAssistState.Off:
                    buffer = new byte[] { 0x00, 0x00, 0x00, 0x00 };
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value));
            }

            // update Focus Assist state (e.g. profile)
            var updateWnfStateDataResult = FocusAssist.UpdateWnfStateData(FocusAssist.WNF_SHEL_QUIETHOURS_ACTIVE_PROFILE_CHANGED, buffer);
            if (updateWnfStateDataResult.IsError == true)
            {
                return MorphicResult.ErrorResult();
            }

            // update UI to match mode change
            updateWnfStateDataResult = FocusAssist.UpdateWnfStateData(FocusAssist.WNF_SHEL_QUIET_MOMENT_SHELL_MODE_CHANGED, buffer);
            if (updateWnfStateDataResult.IsError == true)
            {
                return MorphicResult.ErrorResult();
            }

            return MorphicResult.OkResult();
        }

        /* helper functions */

        // some more details: https://blog.quarkslab.com/playing-with-the-windows-notification-facility-wnf.html
        private static MorphicResult<byte[], MorphicUnit> QueryWnfStateData(ExtendedPInvoke.WNF_STATE_NAME stateName)
        {
            const uint MAX_BUFFER_LENGTH = 4096;
            uint bufferSize = MAX_BUFFER_LENGTH;

            var pointerToBuffer = Marshal.AllocHGlobal((int)bufferSize);
            try
            {
                uint changeStamp;
                var queryWnfStateDataResult = ExtendedPInvoke.NtQueryWnfStateData(ref stateName, IntPtr.Zero, IntPtr.Zero, out changeStamp, pointerToBuffer, ref bufferSize);
                if (queryWnfStateDataResult != 0)
                {
                    return MorphicResult.ErrorResult();
                }

                var bufferSizeAsInt = (int)bufferSize;

                var result = new byte[bufferSizeAsInt];
                Marshal.Copy(pointerToBuffer, result, 0, bufferSizeAsInt);

                return MorphicResult.OkResult(result);
            }
            finally
            {
                Marshal.FreeHGlobal(pointerToBuffer);
            }
        }

        private static MorphicResult<MorphicUnit, MorphicUnit> UpdateWnfStateData(ExtendedPInvoke.WNF_STATE_NAME stateName, byte[] buffer)
        {
            var bufferLength = buffer.Length;

            var pointerToBuffer = Marshal.AllocHGlobal(bufferLength);
            try
            {
                Marshal.Copy(buffer, 0, pointerToBuffer, bufferLength);

                var updateWnfStateDataResult = ExtendedPInvoke.NtUpdateWnfStateData(ref stateName, pointerToBuffer, (uint)bufferLength, IntPtr.Zero /* null */, IntPtr.Zero, 0, 0);
                if (updateWnfStateDataResult != 0)
                {
                    return MorphicResult.ErrorResult();
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pointerToBuffer);
            }

            return MorphicResult.OkResult();
        }
    }
}
