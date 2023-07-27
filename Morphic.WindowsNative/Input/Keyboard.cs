// Copyright 2020-2023 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-windowsnative-lib-cs/blob/main/LICENSE
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

namespace Morphic.WindowsNative.Input;

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

       if (newvalue is not null)
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
