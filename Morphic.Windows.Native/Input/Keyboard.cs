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
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

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
    }
}
