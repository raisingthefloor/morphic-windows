// Copyright 2021-2023 Raising the Floor - US, Inc.
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

using Morphic.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace Morphic.WindowsNative.Windowing;

//public enum WindowMessage: uint
//{
//    WM_ACTIVATE = 0x0006
//}

public class Window
{
   public IntPtr hWnd { get; private set; }

   public Window(IntPtr hWnd)
   {
       this.hWnd = hWnd;
   }

   public MorphicResult<MorphicUnit, MorphicUnit> Activate(IntPtr? hWndBeingDeactivated = null, bool emulateClickActivation = false)
   {
       var sendMessageResult = PInvoke.User32.SendMessage(
           this.hWnd,  
           PInvoke.User32.WindowMessage.WM_ACTIVATE,
           emulateClickActivation ? ExtendedPInvoke.WA_CLICKACTIVE : ExtendedPInvoke.WA_ACTIVE, 
           hWndBeingDeactivated ?? IntPtr.Zero
           );

       return (sendMessageResult == IntPtr.Zero) ? MorphicResult.OkResult() : MorphicResult.ErrorResult();
   }

   public MorphicResult<MorphicUnit, MorphicUnit> Inactivate(IntPtr? hWndBeingActivated = null)
   {
       var sendMessageResult = PInvoke.User32.SendMessage(this.hWnd, PInvoke.User32.WindowMessage.WM_ACTIVATE, ExtendedPInvoke.WA_INACTIVE, hWndBeingActivated ?? IntPtr.Zero);

       return (sendMessageResult == IntPtr.Zero) ? MorphicResult.OkResult() : MorphicResult.ErrorResult();
   }

   //// NOTE: ideally we would not directly expose a Win32 API like this; consider wrapping it inside other (more appropriate, higher-level) functions instead
   //public IntPtr SendMessage(Morphic.WindowsNative.Windowing.WindowMessage wMsg, IntPtr wParam, IntPtr lParam)
   //{
   //    return PInvoke.User32.SendMessage(this.hWnd, (PInvoke.User32.WindowMessage)wMsg, wParam, lParam);
   //}
}
