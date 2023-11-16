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

using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Morphic.WindowsNative.WindowMessageHooks;

public class MouseWindowMessageHook : IDisposable
{
   PInvoke.User32.WindowsHookDelegate _filterFunction;
   PInvoke.User32.SafeHookHandle _hookHandle;
   private bool _isDisposed;

   PInvoke.RECT? _trackingRect = null;

   public struct WndProcEventArgs
   {
       public uint Message;
       public int X;
       public int Y;
   }
   public event EventHandler<WndProcEventArgs> WndProcEvent;

   public MouseWindowMessageHook()
   {
       // NOTE: we are using a low-level hook in this implementation, and we are monitoring mouse events globally (and then filtering by RECT below)
       _filterFunction = new PInvoke.User32.WindowsHookDelegate(this.MessageFilterProc);
       _hookHandle = PInvoke.User32.SetWindowsHookEx(PInvoke.User32.WindowsHookType.WH_MOUSE_LL, _filterFunction, IntPtr.Zero, 0 /* global hook */);
   }

   public void UpdateTrackingRegion(PInvoke.RECT rect)
   {
       _trackingRect = rect;
   }

   bool _lastMessageWasInTrackingRect = false;
   // NOTE: ideally, we would create a queue of messages and then use Task.Run to run code which dequeued the latest messages sequentially
   private int MessageFilterProc(int nCode, IntPtr wParam, IntPtr lParam)
   {
       if (nCode < 0)
       {
           // per Microsoft's docs: if the code is less than zero, we must pass the message along with _no_ intermediate processing 
           // see: https://docs.microsoft.com/en-us/previous-versions/windows/desktop/legacy/ms644986(v=vs.85)

           // call the next hook in the chain and return its result
           return PInvoke.User32.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
       }

       // NOTE: as this is a low-level hook, we must process the message in less than the LowLevelHooksTimeout value (in ms) specified at:
       //       HKEY_CURRENT_USER\Control Panel\Desktop
       //       [for this reason and others, we simply capture the events and add them to a thread-safe queue...and then dispatch them to the UI thread's event loop]

       switch (nCode)
       {
           case 0 /* HC_ACTION */:
               // wParam and lParam contain information about a mouse message
               {
                   // NOTE: wParam is one of: { WM_LBUTTONDOWN, WM_LBUTTONUP, WM_MOUSEMOVE, WM_MOUSEWHEEL, WM_MOUSEHWHEEL, WM_RBUTTONDOWN, WM_RBUTTONUP }
                   // NOTE: lParam is a MSLLHOOKSTRUCT structure instance; see: https://docs.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-msllhookstruct

                   var mouseEventInfo = Marshal.PtrToStructure<ExtendedPInvoke.MSLLHOOKSTRUCT>(lParam);

                   var eventArgs = new WndProcEventArgs()
                   {
                       Message = (uint)wParam.ToInt64(),
                       X = mouseEventInfo.pt.x,
                       Y = mouseEventInfo.pt.y
                   };

                   if (_trackingRect is not null)
                   {
                       if ((mouseEventInfo.pt.x >= _trackingRect.Value.left) &&
                           (mouseEventInfo.pt.x <= _trackingRect.Value.right) &&
                           (mouseEventInfo.pt.y >= _trackingRect.Value.top) &&
                           (mouseEventInfo.pt.y <= _trackingRect.Value.bottom))
                       {
                           // NOTE: this may not be guaranteed to execute in sequence
                           Task.Run(() => { WndProcEvent(this, eventArgs); });
                           _lastMessageWasInTrackingRect = true;
                       }
                       else
                       {
                           if (_lastMessageWasInTrackingRect == true)
                           {
                               // send a WM_MOUSELEAVE event when the mouse leaves the tracking rect
                               eventArgs.Message = 0x02A3 /* WM_MOUSELEAVE */;
                               //
                               // NOTE: this may not be guaranteed to execute in sequence
                               Task.Run(() => { WndProcEvent(this, eventArgs); });
                           }

                           _lastMessageWasInTrackingRect = false;
                       }
                   }
                   else
                   {
                       // there is no tracking RECT, so track globally
                       //
                       // NOTE: this may not be guaranteed to execute in sequence
                       Task.Run(() => { WndProcEvent(this, eventArgs); });
                       _lastMessageWasInTrackingRect = true;
                   }
               }
               // NOTE: we are not "processing" the message, so we will always fall-through and let the next hook in the chain process the message
               break;
           default:
               // unsupported code
               break;
       }

       // call the next hook in the chain and return its result
       return PInvoke.User32.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
   }

   #region IDisposable
   protected virtual void Dispose(bool disposing)
   {
       if (!_isDisposed)
       {
           if (disposing)
           {
               // dispose any managed objects here
           }

           // free unmanaged resources

           // NOTE: this function will return false if it fails
           // NOTE: in theory the system should clean up after this hook handle automatically (so we could probably comment out the following two lines of code)
           _ = ExtendedPInvoke.UnhookWindowsHookEx(_hookHandle.DangerousGetHandle());
           _hookHandle.SetHandleAsInvalid();

           // set any large fields to null

           _isDisposed = true;
       }
   }

   ~MouseWindowMessageHook()
   {
       // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
       Dispose(disposing: false);
   }

   public void Dispose()
   {
       // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
       Dispose(disposing: true);
       GC.SuppressFinalize(this);
   }

   #endregion IDisposable
}
