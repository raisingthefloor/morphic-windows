// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt


namespace Morphic.Windows.Native
{
    using System;
    using System.Runtime.InteropServices;

    public class Keyboard
    {
        /// <summary>
        /// Press or depress a key.
        /// </summary>
        /// <param name="virtualKey">The virtual key code.</param>
        /// <param name="pressed">true to press the key, false to release.</param>
        public static void PressKey(uint virtualKey, bool pressed)
        {
            const uint KEYEVENTF_KEYUP = 0x2;
            WindowsApi.keybd_event((byte)virtualKey, 0, pressed ? 0 : KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        /// <summary>
        /// Gets or sets the current value of filter keys setting.
        /// </summary>
        /// <param name="newvalue"></param>
        /// <returns></returns>
        public static bool KeyRepeat(bool? newvalue = null)
        {
            WindowsApi.FILTERKEYS filterKeys = new WindowsApi.FILTERKEYS
            {
                cbSize = Marshal.SizeOf<WindowsApi.FILTERKEYS>()
            };

            WindowsApi.SystemParametersInfoFilterKeys(
                WindowsApi.SPI_GETFILTERKEYS, filterKeys.cbSize, ref filterKeys, 0);

            if (newvalue != null)
            {
                if (newvalue == true)
                {
                    filterKeys.dwFlags |= WindowsApi.FILTERKEYS.FKF_FILTERKEYSON;
                }
                else
                {
                    filterKeys.dwFlags &= ~WindowsApi.FILTERKEYS.FKF_FILTERKEYSON;
                }

                WindowsApi.SystemParametersInfoFilterKeys(
                    WindowsApi.SPI_SETFILTERKEYS, filterKeys.cbSize, ref filterKeys, 3);
            }

            return (filterKeys.dwFlags & WindowsApi.FILTERKEYS.FKF_FILTERKEYSON)
                == WindowsApi.FILTERKEYS.FKF_FILTERKEYSON;
        }

    }
}
