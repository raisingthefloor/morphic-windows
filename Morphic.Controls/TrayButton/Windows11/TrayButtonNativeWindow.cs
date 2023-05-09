﻿// Copyright 2020-2023 Raising the Floor - US, Inc.
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

using Morphic.Controls.TrayButton.Windows10;
using Morphic.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;

namespace Morphic.Controls.TrayButton.Windows11;

internal class TrayButtonNativeWindow : NativeWindow, IDisposable
{
     private bool disposedValue;

     private static ushort? s_morphicTrayButtonClassInfoExAtom = null;

     private System.Windows.Visibility _visibility;
     private bool _taskbarIsTopmost;

     private ArgbImageNativeWindow? _argbImageNativeWindow = null;
     
     private IntPtr _tooltipWindowHandle;
     private bool _tooltipInfoAdded = false;
     private string? _tooltipText;

     private IntPtr _locationChangeWindowEventHook = IntPtr.Zero;
     private WindowsApi.WinEventProc? _locationChangeWindowEventProc = null;

     private IntPtr _objectReorderWindowEventHook = IntPtr.Zero;
     private WindowsApi.WinEventProc? _objectReorderWindowEventProc = null;

     [Flags]
     private enum TrayButtonVisualStateFlags
     {
          None = 0, // normal visual state
          Hover = 1,
          LeftButtonPressed = 2,
          RightButtonPressed = 4
     }
     private TrayButtonVisualStateFlags _visualState = TrayButtonVisualStateFlags.None;

     private const byte ALPHA_VALUE_FOR_TRANSPARENT_BUT_HIT_TESTABLE = 1;

     public event MouseEventHandler? MouseUp;

     private TrayButtonNativeWindow()
     {
     }

     public static MorphicResult<TrayButtonNativeWindow, Morphic.Controls.TrayButton.Windows11.CreateNewError> CreateNew()
     {
          var result = new TrayButtonNativeWindow();

          /* register a custom native window class for our Morphic Tray Button (or refer to the already-registered class, if we captured it earlier in the application's execution) */
          const string nativeWindowClassName = "Morphic-TrayButton";
          //
          if (s_morphicTrayButtonClassInfoExAtom is null)
          {
               // register our control's custom native window class
               var pointerToWndProcCallback = Marshal.GetFunctionPointerForDelegate(new WindowsApi.WndProc(result.WndProcCallback));
               var lpWndClassEx = new WindowsApi.WNDCLASSEX
               {
                    cbSize = (uint)Marshal.SizeOf(typeof(WindowsApi.WNDCLASSEX)),
                    lpfnWndProc = pointerToWndProcCallback,
                    lpszClassName = nativeWindowClassName,
                    hCursor = PInvoke.User32.LoadCursor(IntPtr.Zero, (IntPtr)PInvoke.User32.Cursors.IDC_ARROW).DangerousGetHandle()
               };

               // NOTE: RegisterClassEx returns an ATOM (or 0 if the call failed)
               var registerClassResult = WindowsApi.RegisterClassEx(ref lpWndClassEx);
               if (registerClassResult == 0) // failure
               {
                    var win32Exception = new PInvoke.Win32Exception(Marshal.GetLastWin32Error());
                    if (win32Exception.NativeErrorCode == PInvoke.Win32ErrorCode.ERROR_CLASS_ALREADY_EXISTS)
                    {
                         Debug.Assert(false, "Class was already registered; we should have recorded this ATOM, and we cannot proceed");
                    }
                    return MorphicResult.ErrorResult(Morphic.Controls.TrayButton.Windows11.CreateNewError.Win32Exception(win32Exception));
               }
               s_morphicTrayButtonClassInfoExAtom = registerClassResult;
          }

          /* calculate the initial position of the tray button */
          var calculatePositionResult = TrayButtonNativeWindow.CalculatePositionAndSizeForTrayButton(null);
          if (calculatePositionResult.IsError)
          {
               Debug.Assert(false, "Cannot calculate position for tray button");
               return MorphicResult.ErrorResult(Morphic.Controls.TrayButton.Windows11.CreateNewError.CouldNotCalculateWindowPosition);
          }
          var trayButtonPositionAndSize = calculatePositionResult.Value!;

          /* get the handle for the taskbar; it will be the owner of our native window (so that our window sits above it in the zorder) */
          // NOTE: we will still need to push our window to the front of its owner's zorder stack in some circumstances, as certain actions (such as popping up the task list balloons above the task bar) will reorder the taskbar's zorder and push us behind the taskbar
          // NOTE: making the taskbar our owner has the side-effect of putting our window above full-screen applications (even though our window is not itself "always on top"); we will need to hide our window whenever a window goes full-screen on the same monitor (and re-show our window whenever the window exits full-screen mode)
          var taskbarHandle = TrayButtonNativeWindow.GetWindowsTaskbarHandle();

          // capture the current state of the taskbar; this is combined with the visibility value to determine whether or not the window is actually visible to the user
          result._taskbarIsTopmost = TrayButtonNativeWindow.IsTaskbarTopmost();


          /* create an instance of our native window */

          CreateParams windowParams = new CreateParams()
          {
               ClassName = s_morphicTrayButtonClassInfoExAtom.ToString(), // for simplicity, we pass the value of the custom class as its integer self but in string form; our CreateWindow function will parse this and convert it to an int
               Caption = nativeWindowClassName,
               Style = unchecked((int)(/*PInvoke.User32.WindowStyles.WS_CLIPSIBLINGS | */PInvoke.User32.WindowStyles.WS_POPUP /*| PInvoke.User32.WindowStyles.WS_TABSTOP*/ | PInvoke.User32.WindowStyles.WS_VISIBLE)),
               ExStyle = (int)(PInvoke.User32.WindowStylesEx.WS_EX_LAYERED/* | PInvoke.User32.WindowStylesEx.WS_EX_TOOLWINDOW*//* | PInvoke.User32.WindowStylesEx.WS_EX_TOPMOST*/),
               //ClassStyle = ?,
               X = trayButtonPositionAndSize.left,
               Y = trayButtonPositionAndSize.top,
               Width = trayButtonPositionAndSize.right - trayButtonPositionAndSize.left,
               Height = trayButtonPositionAndSize.bottom - trayButtonPositionAndSize.top,
               Parent = taskbarHandle,
               //Param = ?,
          };

          // NOTE: CreateHandle can throw InvalidOperationException, OutOfMemoryException or Win32Exception
          try
          {
               result.CreateHandle(windowParams);
          }
          catch (PInvoke.Win32Exception ex)
          {
               return MorphicResult.ErrorResult(Morphic.Controls.TrayButton.Windows11.CreateNewError.Win32Exception(ex));
          }
          catch (Exception ex)
          {
               return MorphicResult.ErrorResult(Morphic.Controls.TrayButton.Windows11.CreateNewError.OtherException(ex));
          }

          // set the window's background transparency to 0% (in the range of a 0 to 255 alpha channel, with 255 being 100%)
          // NOTE: an alpha value of 0 (0%) makes our window complete see-through but it has the side-effect of not capturing any mouse events; to counteract this,
          //       we set our "tranparent" alpha value to 1 instead.  We will only use an alpha value of 0 when we want our window to be invisible and also not capture mouse events
          var setBackgroundAlphaResult = TrayButtonNativeWindow.SetBackgroundAlpha(result.Handle, ALPHA_VALUE_FOR_TRANSPARENT_BUT_HIT_TESTABLE);
          if (setBackgroundAlphaResult.IsError)
          {
               switch (setBackgroundAlphaResult.Error!.Value)
               {
                    case WindowsNative.Win32ApiError.Values.Win32Error:
                         var win32Error = setBackgroundAlphaResult.Error!.Win32ErrorCode!.Value;
                         return MorphicResult.ErrorResult(Morphic.Controls.TrayButton.Windows11.CreateNewError.Win32Exception(new PInvoke.Win32Exception((PInvoke.Win32ErrorCode)win32Error)));
                    default:
                         throw new Exception("invalid code path");
               }
          }

          // since we are making the control visible by default, set its _visibility state
          result._visibility = Visibility.Visible;

          // create an instance of the ArgbImageNativeWindow to hold our icon; we cannot draw the bitmap directly on this window as the bitmap would then be alphablended the same % as our background (instead of being independently blended over our window)
          var argbImageNativeWindowResult = ArgbImageNativeWindow.CreateNew(result.Handle, windowParams.X, windowParams.Y, windowParams.Width, windowParams.Height);
          if (argbImageNativeWindowResult.IsError == true)
          {
               result.Dispose();
               return MorphicResult.ErrorResult(argbImageNativeWindowResult.Error!);
          }
          result._argbImageNativeWindow = argbImageNativeWindowResult.Value!;

          /* wire up windows event hook listeners, to watch for events which require adjusting the zorder of our window */

          // NOTE: we could provide the process handle and thread of processes/threads which we were interested in specifically, but for now we're interested in more than one window so we filter broadly
          var locationChangeWindowEventProc = new WindowsApi.WinEventProc(result.LocationChangeWindowEventProc);
          var locationChangeWindowEventHook = WindowsApi.SetWinEventHook(
               WindowsApi.WinEventHookType.EVENT_OBJECT_LOCATIONCHANGE, // start index
               WindowsApi.WinEventHookType.EVENT_OBJECT_LOCATIONCHANGE, // end index
               IntPtr.Zero,
               locationChangeWindowEventProc,
               0, // process handle (0 = all processes on current desktop)
               0, // thread (0 = all existing threads on current desktop)
               WindowsApi.WinEventHookFlags.WINEVENT_OUTOFCONTEXT | WindowsApi.WinEventHookFlags.WINEVENT_SKIPOWNPROCESS
          );
          Debug.Assert(locationChangeWindowEventHook != IntPtr.Zero, "Could not wire up location change window event listener for tray button");
          //
          result._locationChangeWindowEventHook = locationChangeWindowEventHook;
          // NOTE: we must capture the delegate so that it is not garbage collected; otherwise the native callbacks can crash the .NET execution engine
          result._locationChangeWindowEventProc = locationChangeWindowEventProc;
          //
          //
          //
          var objectReorderWindowEventProc = new WindowsApi.WinEventProc(result.ObjectReorderWindowEventProc);
          var objectReorderWindowEventHook = WindowsApi.SetWinEventHook(
               WindowsApi.WinEventHookType.EVENT_OBJECT_REORDER, // start index
               WindowsApi.WinEventHookType.EVENT_OBJECT_REORDER, // end index
               IntPtr.Zero,
               objectReorderWindowEventProc,
               0, // process handle (0 = all processes on current desktop)
               0, // thread (0 = all existing threads on current desktop)
               WindowsApi.WinEventHookFlags.WINEVENT_OUTOFCONTEXT | WindowsApi.WinEventHookFlags.WINEVENT_SKIPOWNPROCESS
          );
          Debug.Assert(objectReorderWindowEventHook != IntPtr.Zero, "Could not wire up object reorder window event listener for tray button");
          //
          result._objectReorderWindowEventHook = objectReorderWindowEventHook;
          // NOTE: we must capture the delegate so that it is not garbage collected; otherwise the native callbacks can crash the .NET execution engine
          result._objectReorderWindowEventProc = objectReorderWindowEventProc;

          // create the tooltip window (although we won't provide it with any actual text until/unless the text is set
          result._tooltipWindowHandle = result.CreateTooltipWindow();
          result._tooltipText = null;

          return MorphicResult.OkResult(result);
     }

     // NOTE: the built-in CreateHandle function couldn't accept our custom class (an ATOM rather than a string) as input, so we have overridden CreateHandle and are calling CreateWindowEx manually
     // NOTE: in some circumstances, it is possible that we are unable to create our window; our caller may want to consider retrying mechanism
     public override void CreateHandle(CreateParams cp)
     {
          // NOTE: if cp.ClassName is a string parseable as a short unsigned integer, parse it into an unsigned short; otherwise use the string as the classname
          IntPtr classNameAsIntPtr;
          bool classNameAsIntPtrRequiresFree = false;
          if (cp.ClassName is not null && ushort.TryParse(cp.ClassName, out var classNameAsUshort) == true)
          {
               classNameAsIntPtr = (IntPtr)classNameAsUshort;
          }
          else
          {
               if (cp.ClassName is not null)
               {
                    classNameAsIntPtr = Marshal.StringToHGlobalUni(cp.ClassName);
                    classNameAsIntPtrRequiresFree = true;
               }
               else
               {
                    classNameAsIntPtr = IntPtr.Zero;
               }
          }
          //
          try
          {
               // NOTE: CreateWindowEx will return IntPtr.Zero ("NULL") if it fails
               var handle = WindowsApi.CreateWindowEx(
                    (PInvoke.User32.WindowStylesEx)cp.ExStyle,
                    classNameAsIntPtr,
                    cp.Caption,
                    (PInvoke.User32.WindowStyles)cp.Style,
                    cp.X,
                    cp.Y,
                    cp.Width,
                    cp.Height,
                    cp.Parent,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero
               );
               if (handle == IntPtr.Zero)
               {
                    var lastError = Marshal.GetLastWin32Error();
                    throw new PInvoke.Win32Exception(lastError);
               }

               this.AssignHandle(handle);
          }
          finally
          {
               if (classNameAsIntPtrRequiresFree == true)
               {
                    Marshal.FreeHGlobal(classNameAsIntPtr);
               }
          }
     }


     // Listen to when the handle changes to keep the argb image native window synced
     protected override void OnHandleChange()
     {
          base.OnHandleChange();

          // NOTE: if we ever need to update our children (or other owned windows) to let them know that our handle had changed, this is where we would add that code
     }

     //

     protected virtual void Dispose(bool disposing)
     {
          if (!disposedValue)
          {
               if (disposing)
               {
                    // dispose managed state (managed objects)

                    if (_objectReorderWindowEventHook != IntPtr.Zero)
                    {
                         WindowsApi.UnhookWinEvent(_objectReorderWindowEventHook);
                    }
                    if (_locationChangeWindowEventHook != IntPtr.Zero)
                    {
                         WindowsApi.UnhookWinEvent(_locationChangeWindowEventHook);
                    }

                    _argbImageNativeWindow?.Dispose();
               }

               // TODO: free unmanaged resources (unmanaged objects) and override finalizer
               this.DestroyHandle();
               _ = this.DestroyTooltipWindow();

               // TODO: set large fields to null
               disposedValue = true;
          }
     }

     ~TrayButtonNativeWindow()
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

     // NOTE: during initial creation of the window, callbacks are sent to this delegated event; after creation, messages are captured by the WndProc function instead
     private IntPtr WndProcCallback(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
     {
          switch ((PInvoke.User32.WindowMessage)msg)
          {
               case PInvoke.User32.WindowMessage.WM_CREATE:
                    // NOTE: it may not technically be necessary for us to use buffered painting for this control since we're effectively just painting it with a single fill color--but 
                    //       we do so to maintain consistency with the ArgbImageNativeWindow class and other user-painted forms
                    // see: https://learn.microsoft.com/en-us/windows/win32/api/uxtheme/nf-uxtheme-bufferedpaintinit
                    if (WindowsApi.BufferedPaintInit() != WindowsApi.S_OK)
                    {
                         // failed; abort
                         Debug.Assert(false, "Could not initialize buffered paint");
                         return new IntPtr(-1); // abort window creation process
                    }
                    break;
               default:
                    break;
          }

          // pass all non-handled messages through to DefWindowProc
          return PInvoke.User32.DefWindowProc(hWnd, (PInvoke.User32.WindowMessage)msg, wParam, lParam);
     }

     // NOTE: this WndProc method processes all messages after the initial creation of the window
     protected override void WndProc(ref Message m)
     {
          IntPtr? result = null;

          switch ((PInvoke.User32.WindowMessage)m.Msg)
          {
               case PInvoke.User32.WindowMessage.WM_LBUTTONDOWN:
                    {
                         _visualState |= TrayButtonVisualStateFlags.LeftButtonPressed;
                         this.UpdateVisualStateAlpha();

                         result = IntPtr.Zero;
                    }
                    break;
               case PInvoke.User32.WindowMessage.WM_LBUTTONUP:
                    {
                         _visualState &= ~TrayButtonVisualStateFlags.LeftButtonPressed;
                         this.UpdateVisualStateAlpha();

                         var convertLParamResult = this.ConvertMouseMessageLParamToScreenPoint(m.LParam);
                         if (convertLParamResult.IsSuccess)
                         {
                              var hitPoint = convertLParamResult.Value!;

                              var mouseArgs = new MouseEventArgs(MouseButtons.Left, 1, hitPoint.X, hitPoint.Y, 0);
                              Task.Run(() => this.MouseUp?.Invoke(this, mouseArgs));
                         }
                         else
                         {
                              Debug.Assert(false, "Could not map tray button hit point to screen coordinates");
                         }

                         result = IntPtr.Zero;
                    }
                    break;
               case PInvoke.User32.WindowMessage.WM_MOUSELEAVE:
                    {
                         // the cursor has left our tray button's window area; remove the hover state from our visual state
                         _visualState &= ~TrayButtonVisualStateFlags.Hover;

                         // NOTE: as we aren't able to track mouseup when the cursor is outside of the button, we also remove the left/right button pressed states here
                         //       (and then we check them again when the mouse moves back over the button)
                         _visualState &= ~TrayButtonVisualStateFlags.LeftButtonPressed;
                         _visualState &= ~TrayButtonVisualStateFlags.RightButtonPressed;
                         this.UpdateVisualStateAlpha();

                         result = IntPtr.Zero;
                    }
                    break;
               case PInvoke.User32.WindowMessage.WM_MOUSEMOVE:
                    {
                         // NOTE: this message is raised while we are tracking (whereas the SETCURSOR WM_MOUSEMOVE is captured when the mouse cursor first enters the window)
                         //
                         // NOTE: if the cursor moves off of the tray button while the button is pressed, we would have removed the "pressed" focus as well as the "hover" focus
                         //       because we can't track mouseup when the cursor is outside of the button; consequently we also need to check the mouse pressed state during
                         //       mousemove so that we can re-visualize (re-set flags for) the pressed state as appropriate.
                         if (((_visualState & TrayButtonVisualStateFlags.LeftButtonPressed) == 0) && ((m.WParam.ToInt64() & WindowsApi.MK_LBUTTON) != 0))
                         {
                              _visualState |= TrayButtonVisualStateFlags.LeftButtonPressed;
                              this.UpdateVisualStateAlpha();
                         }
                         if (((_visualState & TrayButtonVisualStateFlags.RightButtonPressed) == 0) && ((m.WParam.ToInt64() & WindowsApi.MK_RBUTTON) != 0))
                         {
                              _visualState |= TrayButtonVisualStateFlags.RightButtonPressed;
                              this.UpdateVisualStateAlpha();
                         }

                         result = IntPtr.Zero;
                    }
                    break;
               case PInvoke.User32.WindowMessage.WM_NCDESTROY:
                    {
                         // NOTE: we are calling this in response to WM_NCDESTROY (instead of WM_DESTROY)
                         //       see: https://learn.microsoft.com/en-us/windows/win32/api/uxtheme/nf-uxtheme-bufferedpaintinit
                         _ = WindowsApi.BufferedPaintUnInit();

                         // NOTE: we pass along this message (i.e. we don't return a "handled" result)
                    }
                    break;
               case PInvoke.User32.WindowMessage.WM_NCPAINT:
                    {
                         // we suppress all painting of the non-client areas (so that we can have a transparent window)
                         // return zero, indicating that we processed the message
                         result = IntPtr.Zero;
                    }
                    break;
               case PInvoke.User32.WindowMessage.WM_PAINT:
                    {
                         // NOTE: we override the built-in paint functionality with our own Paint function
                         this.OnPaintWindowsMessage(m.HWnd);
                         //
                         // return zero, indicating that we processed the message
                         result = IntPtr.Zero;
                    }
                    break;
               case PInvoke.User32.WindowMessage.WM_RBUTTONDOWN:
                    {
                         _visualState |= TrayButtonVisualStateFlags.RightButtonPressed;
                         this.UpdateVisualStateAlpha();

                         result = IntPtr.Zero;
                    }
                    break;
               case PInvoke.User32.WindowMessage.WM_RBUTTONUP:
                    {
                         _visualState &= ~TrayButtonVisualStateFlags.RightButtonPressed;
                         this.UpdateVisualStateAlpha();

                         var convertLParamResult = this.ConvertMouseMessageLParamToScreenPoint(m.LParam);
                         if (convertLParamResult.IsSuccess)
                         {
                              var hitPoint = convertLParamResult.Value!;

                              var mouseArgs = new MouseEventArgs(MouseButtons.Right, 1, hitPoint.X, hitPoint.Y, 0);
                              Task.Run(() => this.MouseUp?.Invoke(this, mouseArgs));
                         }
                         else
                         {
                              Debug.Assert(false, "Could not map tray button hit point to screen coordinates");
                         }

                         result = IntPtr.Zero;
                    }
                    break;
               case PInvoke.User32.WindowMessage.WM_SETCURSOR:
                    {
                         // wParam: window handle
                         // lParam: low-order word is the high-test result for the cursor position; high-order word specifies the mouse message that triggered this event

                         var hitTestResult = (uint)((m.LParam.ToInt64() >> 0) & 0xFFFF);
                         var mouseMsg = (uint)((m.LParam.ToInt64() >> 16) & 0xFFFF);

                         // NOTE: for messages which we handle, we return "TRUE" (1) to halt further message processing; this may not technically be necessary
                         //       see: https://learn.microsoft.com/en-us/windows/win32/menurc/wm-setcursor
                         switch ((PInvoke.User32.WindowMessage)mouseMsg)
                         {
                              case PInvoke.User32.WindowMessage.WM_LBUTTONDOWN:
                                   {
                                        _visualState |= TrayButtonVisualStateFlags.LeftButtonPressed;
                                        this.UpdateVisualStateAlpha();

                                        result = new IntPtr(1);
                                   }
                                   break;
                              case PInvoke.User32.WindowMessage.WM_LBUTTONUP:
                                   {
                                        _visualState &= ~TrayButtonVisualStateFlags.LeftButtonPressed;
                                        this.UpdateVisualStateAlpha();

                                        result = new IntPtr(1);
                                   }
                                   break;
                              case PInvoke.User32.WindowMessage.WM_MOUSEMOVE:
                                   {
                                        // if we are not yet tracking the mouse position (i.e. this is effectively "mouse enter"), then start tracking it now (so that we can capture its move out of our box)
                                        // NOTE: we track whether or not we are tracking the mouse by analyzing the hover state of our visual state flags
                                        if ((_visualState & TrayButtonVisualStateFlags.Hover) == 0)
                                        {
                                             // track mousehover (for tooltips) and mouseleave (to remove hover effect)
                                             var eventTrack = WindowsApi.TRACKMOUSEEVENT.CreateNew(WindowsApi.TRACKMOUSEEVENTFlags.TME_LEAVE, this.Handle, WindowsApi.HOVER_DEFAULT);
                                             var trackMouseEventSuccess = WindowsApi.TrackMouseEvent(ref eventTrack);
                                             if (trackMouseEventSuccess == false)
                                             {
                                                  // failed; we could capture the win32 error code via GetLastWin32Error
                                                  Debug.Assert(false, "Could not set up tracking of tray button window area");
                                             }

                                             _visualState |= TrayButtonVisualStateFlags.Hover;
                                             this.UpdateVisualStateAlpha();
                                        }
                                        result = new IntPtr(1);
                                   }
                                   break;
                              case PInvoke.User32.WindowMessage.WM_RBUTTONDOWN:
                                   {
                                        _visualState |= TrayButtonVisualStateFlags.RightButtonPressed;
                                        this.UpdateVisualStateAlpha();

                                        result = new IntPtr(1);
                                   }
                                   break;
                              case PInvoke.User32.WindowMessage.WM_RBUTTONUP:
                                   {
                                        _visualState &= ~TrayButtonVisualStateFlags.RightButtonPressed;
                                        this.UpdateVisualStateAlpha();

                                        result = new IntPtr(1);
                                   }
                                   break;
                              default:
                                   // unhandled setcurosr mouse message
                                   break;
                         }
                    }
                    break;
               default:
                    break;
          }

          // if we handled the message, return 'result'; otherwise, if we did not handle the message, call through to DefWindowProc to handle the message
          if (result is not null)
          {
               m.Result = result.Value!;
          }
          else
          {
               // NOTE: per the Microsoft .NET documentation, we should call base.WndProc to process any events which we have not handled; however,
               //       in our testing, this led to frequent crashes.  So instead, we follow the traditional pattern and call DefWindowProc to handle any events which we have not handled
               //       see: https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.nativewindow.wndproc?view=windowsdesktop-6.0
               m.Result = PInvoke.User32.DefWindowProc(m.HWnd, (PInvoke.User32.WindowMessage)m.Msg, m.WParam, m.LParam);
               //base.WndProc(ref m); // causes crashes (when other native windows are capturing/processing/passing along messages)
          }
     }

     // NOTE: this function may ONLY be called when responding to a WM_PAINT message
     private void OnPaintWindowsMessage(IntPtr hWnd)
     {
          // create a device context for drawing; we must destroy this automatically in a finally block.  We are effectively replicating the functionality of C++'s CPaintDC.
          WindowsApi.PAINTSTRUCT paintStruct;
          // NOTE: we experienced significant issues using PInvoke.User32.BeginPaint (possibly due to its IntPtr result wrapper), so we have redeclared the BeginPaint function ourselves
          var deviceContext = WindowsApi.BeginPaint(hWnd, out paintStruct)!;
          if (deviceContext == IntPtr.Zero)
          {
               // no display context is available
               Debug.Assert(false, "Cannot paint TrayButton in response to WM_PAINT message; no display context is available.");
               return;
          }
          try
          {
               // NOTE: to avoid flickering, we use buffered painting to erase the background, fill the background with a single (white) brush, and then apply the painted area to the window in a single paint operation
               IntPtr bufferedPaintDc;
               var paintBufferHandle = WindowsApi.BeginBufferedPaint(paintStruct.hdc, ref paintStruct.rcPaint, WindowsApi.BP_BUFFERFORMAT.BPBF_TOPDOWNDIB, IntPtr.Zero, out bufferedPaintDc);
               if (paintBufferHandle == IntPtr.Zero)
               {
                    Debug.Assert(false, "Cannot begin a buffered paint operation for TrayButton (when responding to a WM_PAINT message).");
                    return;
               }
               try
               {
                    // NOTE: this is the section where we call all of our actual (buffered) paint operations

                    // clear our window's background (i.e. the buffer background)
                    var bufferedPaintClearHresult = WindowsApi.BufferedPaintClear(paintBufferHandle, ref paintStruct.rcPaint);
                    if (bufferedPaintClearHresult != WindowsApi.S_OK)
                    {
                         Debug.Assert(false, "Could not clear background of TrayButton window--using buffered clearing (when responding to a WM_Paint message).");
                         return;
                    }

                    // create a solid white brush
                    var createSolidBrushResult = WindowsApi.CreateSolidBrush(0x00FFFFFF);
                    if (createSolidBrushResult == IntPtr.Zero)
                    {
                         Debug.Assert(false, "Could not create white brush to paint the background of the TrayButton window (when responding to a WM_Paint message).");
                         return;
                    }
                    var whiteBrush = createSolidBrushResult;
                    try
                    {
                         var fillRectResult = WindowsApi.FillRect(bufferedPaintDc, ref paintStruct.rcPaint, whiteBrush);
                         Debug.Assert(fillRectResult != 0, "Could not fill highlight background of Tray icon with white brush");
                    }
                    finally
                    {
                         // clean up the white solid brush we created for the fill operation
                         var deleteObjectSuccess = PInvoke.Gdi32.DeleteObject(whiteBrush);
                         Debug.Assert(deleteObjectSuccess == true, "Could not delete white brush object used to highlight Tray icon");
                    }
               }
               finally
               {
                    // complete the buffered paint operation and free the buffered paint handle
                    var endBufferedPaintHresult = WindowsApi.EndBufferedPaint(paintBufferHandle, true /* copy buffer to DC, completing hte paint operation */);
                    Debug.Assert(endBufferedPaintHresult == WindowsApi.S_OK, "Error while attempting to end buffered paint operation for TrayButton; hresult: " + endBufferedPaintHresult);
               }
          }
          finally
          {
               // mark the end of painting; this function must always be called when BeginPaint was called (and succeeded), and only after drawing is complete
               // NOTE: per the MSDN docs, this function never returns zero (so there is no result to check)
               _ = WindowsApi.EndPaint(hWnd, ref paintStruct);
          }
     }

     public System.Windows.Visibility Visibility
     {
          get
          {
               return _visibility;
          }
          set
          {
               if (_visibility != value)
               {
                    _visibility = value;
                    this.UpdateVisibility();
               }
          }
     }

     private void UpdateVisibility()
     {
          _argbImageNativeWindow?.SetVisbile(this.ShouldWindowBeVisible());
          this.UpdateVisualStateAlpha();
     }

     private bool ShouldWindowBeVisible()
     {
          return (_visibility == Visibility.Visible) && (_taskbarIsTopmost == true);
     }

     private void UpdateVisualStateAlpha()
     {
          // default to "Normal" visual state
          Double highlightOpacity = 0.0;

          if (this.ShouldWindowBeVisible())
          {
               if (((_visualState & TrayButtonVisualStateFlags.LeftButtonPressed) != 0) ||
                       ((_visualState & TrayButtonVisualStateFlags.RightButtonPressed) != 0))
               {
                    highlightOpacity = 0.25;
               }
               else if ((_visualState & TrayButtonVisualStateFlags.Hover) != 0)
               {
                    highlightOpacity = 0.1;
               }

               var alpha = (byte)((double)255 * highlightOpacity);
               TrayButtonNativeWindow.SetBackgroundAlpha(this.Handle, Math.Max(alpha, ALPHA_VALUE_FOR_TRANSPARENT_BUT_HIT_TESTABLE));
          }
          else
          {
               // collapsed or hidden controls should be invisible
               TrayButtonNativeWindow.SetBackgroundAlpha(this.Handle, 0);
          }
     }

     private static MorphicResult<MorphicUnit, Morphic.WindowsNative.Win32ApiError> SetBackgroundAlpha(IntPtr handle, byte alpha)
     {
          // set the window's background transparency to 0% (in the range of a 0 to 255 alpha channel, with 255 being 100%)
          var setLayeredWindowAttributesSuccess = WindowsApi.SetLayeredWindowAttributes(handle, 0, alpha, (uint)WindowsApi.SetLayeredWindowAttributesFlags.LWA_ALPHA);
          if (setLayeredWindowAttributesSuccess == false)
          {
               var win32Error = Marshal.GetLastWin32Error();
               return MorphicResult.ErrorResult(Morphic.WindowsNative.Win32ApiError.Win32Error((uint)win32Error));
          }

          return MorphicResult.OkResult();
     }

     //

     private void LocationChangeWindowEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint idEventThread, uint dwmsEventTime)
     {
          // we cannot process a location change message if the hwnd is zero
          if (hwnd == IntPtr.Zero)
          {
               return;
          }

          // attempt to capture the class name for the window; if the window has already been destroyed, this will fail
          string? className = null;
          var getWindowClassNameResult = TrayButtonNativeWindow.GetWindowClassName(hwnd);
          if (getWindowClassNameResult.IsSuccess)
          {
               className = getWindowClassNameResult.Value!;
          }

          if (className == "TaskListThumbnailWnd" || className == "TaskListOverlayWnd")
          {
               // if the window being moved was one of the task list windows (i.e. the windows that pop up above the taskbar), then our zorder has probably been pushed down.  To counteract this, we make sure our window is "TOPMOST"
               // NOTE: in initial testing, we set the window to TOPMOST in the ExStyles during handle construction.  This was not always successful in keeping the window topmost, however, possibly because the taskbar becomes "more" topmost sometimes.  So we re-set the window zorder here instead (without activating the window).
               PInvoke.User32.SetWindowPos(this.Handle, WindowsApi.HWND_TOPMOST, 0, 0, 0, 0, PInvoke.User32.SetWindowPosFlags.SWP_NOMOVE | PInvoke.User32.SetWindowPosFlags.SWP_NOSIZE | PInvoke.User32.SetWindowPosFlags.SWP_NOACTIVATE);
          }
          else if (className == "Shell_TrayWnd"/* || className == "ReBarWindow32"*/ || className == "TrayNotifyWnd")
          {
               // if the window being moved was the taskbar or the taskbar's notification tray, recalculate and update our position
               // NOTE: we might also consider watching for location changes of the task button container, but as we don't use it for position/size calculations at the present time we do not watch accordingly
               var repositionResult = this.RecalculatePositionAndRepositionWindow();
               Debug.Assert(repositionResult.IsSuccess, "Could not reposition Tray Button window");
          }
     }

     private void ObjectReorderWindowEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint idEventThread, uint dwmsEventTime)
     {
          // we cannot process an object reorder message if the hwnd is zero
          if (hwnd == IntPtr.Zero)
          {
               return;
          }

          // attempt to capture the class name for the window; if the window has already been destroyed, this will fail
          string? className = null;
          var getWindowClassNameResult = TrayButtonNativeWindow.GetWindowClassName(hwnd);
          if (getWindowClassNameResult.IsSuccess)
          {
               className = getWindowClassNameResult.Value!;
          }

          if (className == "Shell_TrayWnd")
          {
               // determine if the taskbar is topmost; the taskbar's topmost flag is removed when an app goes full-screen and should cover the taskbar (e.g. a full-screen video)
               _taskbarIsTopmost = TrayButtonNativeWindow.IsTaskbarTopmost(hwnd);
               //
               // NOTE: UpdateVisibility takes both the .Visibility property and the topmost state of the taskbar into consideration to determine whether or not to show the control
               this.UpdateVisibility();
          }
     }

     private static bool IsTaskbarTopmost(IntPtr? taskbarHWnd = null)
     {
          var taskbarHandle = taskbarHWnd ?? TrayButtonNativeWindow.GetWindowsTaskbarHandle();

          var taskbarWindowExStyle = WindowsApi.GetWindowLongPtr_IntPtr(taskbarHandle, PInvoke.User32.WindowLongIndexFlags.GWL_EXSTYLE);
          var taskbarIsTopmost = ((nint)taskbarWindowExStyle & (nint)PInvoke.User32.WindowStylesEx.WS_EX_TOPMOST) != 0;

          return taskbarIsTopmost;
     }

     private MorphicResult<MorphicUnit, MorphicUnit> RecalculatePositionAndRepositionWindow()
     {
          // first, reposition our control (NOTE: this will be required to subsequently determine the position of our bitmap)
          var calculatePositionResult = TrayButtonNativeWindow.CalculatePositionAndSizeForTrayButton(this.Handle);
          if (calculatePositionResult.IsError)
          {
               Debug.Assert(false, "Cannot calculate position for tray button");
               return MorphicResult.ErrorResult();
          }
          var trayButtonPositionAndSize = calculatePositionResult.Value!;
          //
          var size = new System.Drawing.Size(trayButtonPositionAndSize.right - trayButtonPositionAndSize.left, trayButtonPositionAndSize.bottom - trayButtonPositionAndSize.top);
          PInvoke.User32.SetWindowPos(this.Handle, IntPtr.Zero, trayButtonPositionAndSize.left, trayButtonPositionAndSize.top, size.Width, size.Height, PInvoke.User32.SetWindowPosFlags.SWP_NOZORDER | PInvoke.User32.SetWindowPosFlags.SWP_NOACTIVATE);

          // once the control is repositioned, reposition the bitmap
          var bitmap = _argbImageNativeWindow?.GetBitmap();
          if (bitmap is not null)
          {
               this.PositionAndResizeBitmap(bitmap);
          }

          // also reposition the tooltip's tracking rectangle
          if (_tooltipText is not null)
          {
               this.UpdateTooltipTextAndTracking();
          }


          PInvoke.User32.BringWindowToTop(this.Handle);

          return MorphicResult.OkResult();
     }

     private static MorphicResult<string, Morphic.WindowsNative.Win32ApiError> GetWindowClassName(IntPtr hWnd)
     {
          StringBuilder classNameBuilder = new(256);
          var getClassNameResult = WindowsApi.GetClassName(hWnd, classNameBuilder, classNameBuilder.Capacity);
          if (getClassNameResult == 0)
          {
               var win32Error = Marshal.GetLastWin32Error();
               return MorphicResult.ErrorResult(Morphic.WindowsNative.Win32ApiError.Win32Error((uint)win32Error));
          }

          var classNameAsString = classNameBuilder.ToString();
          return MorphicResult.OkResult(classNameAsString);
     }

     //

     public void SetBitmap(Bitmap? bitmap)
     {
          if (bitmap is not null)
          {
               this.PositionAndResizeBitmap(bitmap);
          }
          _argbImageNativeWindow?.SetBitmap(bitmap);
     }

     private void PositionAndResizeBitmap(Bitmap bitmap)
     {
          // then, reposition the bitmap
          PInvoke.User32.GetWindowRect(this.Handle, out var positionAndSize);
          var bitmapSize = bitmap.Size;

          var argbImageNativeWindowSize = TrayButtonNativeWindow.CalculateWidthAndHeightForBitmap(positionAndSize, bitmapSize);
          var bitmapRect = TrayButtonNativeWindow.CalculateCenterRectInsideRect(positionAndSize, bitmapSize);

          _argbImageNativeWindow?.SetPositionAndSize(bitmapRect);
     }

     public void SetText(string? text)
     {
          _tooltipText = text;

          this.UpdateTooltipTextAndTracking();
     }

     //

     private IntPtr CreateTooltipWindow()
     {
          if (_tooltipWindowHandle != IntPtr.Zero)
          {
               // tooltip window already exists
               return _tooltipWindowHandle;
          }

          var tooltipWindowHandle = PInvoke.User32.CreateWindowEx(
               0 /* no styles */,
               WindowsApi.TOOLTIPS_CLASS,
               null,
               PInvoke.User32.WindowStyles.WS_POPUP | (PInvoke.User32.WindowStyles)WindowsApi.TTS_ALWAYSTIP,
               WindowsApi.CW_USEDEFAULT,
               WindowsApi.CW_USEDEFAULT,
               WindowsApi.CW_USEDEFAULT,
               WindowsApi.CW_USEDEFAULT,
               this.Handle,
               IntPtr.Zero,
               IntPtr.Zero,
               IntPtr.Zero);

          // NOTE: Microsoft's documentation seems to indicate that we should set the tooltip as topmost, but in our testing this was unnecessary.  It's possible that using SendMessage to add/remove tooltip text automatically handles this when the system handles showing the tooltip
          //       see: https://learn.microsoft.com/en-us/windows/win32/controls/tooltip-controls
          //PInvoke.User32.SetWindowPos(tooltipWindowHandle, WindowsApi.HWND_TOPMOST, 0, 0, 0, 0, PInvoke.User32.SetWindowPosFlags.SWP_NOMOVE | PInvoke.User32.SetWindowPosFlags.SWP_NOSIZE | PInvoke.User32.SetWindowPosFlags.SWP_NOACTIVATE);

          Debug.Assert(tooltipWindowHandle != IntPtr.Zero, "Could not create tooltip window.");

          return tooltipWindowHandle;
     }

     private bool DestroyTooltipWindow()
     {
          if (_tooltipWindowHandle == IntPtr.Zero)
          {
               return true;
          }

          // set the tooltip text to empty (so that UpdateTooltipText will clear out the tooltip), then update the tooltip text.
          _tooltipText = null;
          this.UpdateTooltipTextAndTracking();

          var result = PInvoke.User32.DestroyWindow(_tooltipWindowHandle);
          _tooltipWindowHandle = IntPtr.Zero;

          return result;
     }

     private void UpdateTooltipTextAndTracking()
     {
          if (_tooltipWindowHandle == IntPtr.Zero)
          {
               // tooltip window does not exist; failed; abort
               Debug.Assert(false, "Tooptip window does not exist; if this is an expected failure, remove this assert.");
               return;
          }

          var trayButtonNativeWindowHandle = this.Handle;
          if (trayButtonNativeWindowHandle == IntPtr.Zero)
          {
               // tray button window does not exist; there is no tool window to update
               return;
          }

          var getClientRectSuccess = PInvoke.User32.GetClientRect(this.Handle, out var trayButtonClientRect);
          if (getClientRectSuccess == false)
          {
               // failed; abort
               Debug.Assert(false, "Could not get client rect for tray button; could not set up tooltip");
               return;
          }

          var toolinfo = new WindowsApi.TOOLINFO();
          toolinfo.cbSize = (uint)Marshal.SizeOf(toolinfo);
          toolinfo.hwnd = this.Handle;
          toolinfo.uFlags = LegacyWindowsApi.TTF_SUBCLASS;
          toolinfo.lpszText = _tooltipText;
          toolinfo.uId = unchecked((nuint)(nint)this.Handle); // unique identifier (for adding/deleting the tooltip)
          toolinfo.rect = trayButtonClientRect;
          //
          var pointerToToolinfo = Marshal.AllocHGlobal(Marshal.SizeOf(toolinfo));
          try
          {
               Marshal.StructureToPtr(toolinfo, pointerToToolinfo, false);
               if (toolinfo.lpszText is not null)
               {
                    if (_tooltipInfoAdded == false)
                    {
                         _ = PInvoke.User32.SendMessage(_tooltipWindowHandle, (PInvoke.User32.WindowMessage)WindowsApi.TTM_ADDTOOL, IntPtr.Zero, pointerToToolinfo);
                         _tooltipInfoAdded = true;
                    }
                    else
                    {
                         // delete and re-add the tooltipinfo; this will update all the info (including the text and tracking rect)
                         _ = PInvoke.User32.SendMessage(_tooltipWindowHandle, (PInvoke.User32.WindowMessage)WindowsApi.TTM_DELTOOL, IntPtr.Zero, pointerToToolinfo);
                         _ = PInvoke.User32.SendMessage(_tooltipWindowHandle, (PInvoke.User32.WindowMessage)WindowsApi.TTM_ADDTOOL, IntPtr.Zero, pointerToToolinfo);
                    }
               }
               else
               {
                    // NOTE: we might technically call "deltool" even when a tooltipinfo was already removed
                    _ = PInvoke.User32.SendMessage(_tooltipWindowHandle, (PInvoke.User32.WindowMessage)WindowsApi.TTM_DELTOOL, IntPtr.Zero, pointerToToolinfo);
                    _tooltipInfoAdded = false;
               }
          }
          finally
          {
               Marshal.FreeHGlobal(pointerToToolinfo);
          }
     }

     //

     /* helper functions */

     internal static PInvoke.RECT CalculateCenterRectInsideRect(PInvoke.RECT outerRect, System.Drawing.Size innerSize)
     {
          var outerWidth = outerRect.right - outerRect.left;
          var outerHeight = outerRect.bottom - outerRect.top;

          var innerWidth = innerSize.Width;
          var innerHeight = innerSize.Height;

          var left = outerRect.left + ((outerWidth - innerWidth) / 2);
          var top = outerRect.top + ((outerHeight - innerHeight) / 2);
          var right = left + innerWidth;
          var bottom = top + innerHeight;


          return new PInvoke.RECT()
          {
               left = left,
               top = top,
               right = right,
               bottom = bottom,
          };
     }

     internal static MorphicResult<PInvoke.RECT, MorphicUnit> CalculatePositionAndSizeForTrayButton(IntPtr? trayButtonHandle)
     {
          // NOTE: in this implementation, we simply place the tray button over the taskbar, directly to the left of the system tray
          //       in the future, we may want to consider searching for any children which might occupy the area--and any system windows which are owned by the taskbar or any of its children--and then try to find a place to the "left" of those

          // get the handles for the taskbar, task button container, and the notify tray
          //
          var taskbarHandle = TrayButtonNativeWindow.GetWindowsTaskbarHandle();
          if (taskbarHandle == IntPtr.Zero) { return MorphicResult.ErrorResult(); }
          //
          var taskButtonContainerHandle = TrayButtonNativeWindow.GetWindowsTaskbarTaskButtonContainerHandle(taskbarHandle);
          if (taskButtonContainerHandle == IntPtr.Zero) { return MorphicResult.ErrorResult(); }
          //
          var notifyTrayHandle = TrayButtonNativeWindow.GetWindowsTaskbarNotificationTrayHandle(taskbarHandle);
          if (notifyTrayHandle == IntPtr.Zero) { return MorphicResult.ErrorResult(); }

          // get the RECTs for the taskbar, task button container and the notify tray
          //
          var getTaskbarRectSuccess = PInvoke.User32.GetWindowRect(taskbarHandle, out var taskbarRect);
          if (getTaskbarRectSuccess == false) { return MorphicResult.ErrorResult(); }
          //
          var getTaskButtonContainerRectSuccess = PInvoke.User32.GetWindowRect(taskButtonContainerHandle, out var taskButtonContainerRect);
          if (getTaskButtonContainerRectSuccess == false) { return MorphicResult.ErrorResult(); }
          //
          var getNotifyTrayRectSuccess = PInvoke.User32.GetWindowRect(notifyTrayHandle, out var notifyTrayRect);
          if (getNotifyTrayRectSuccess == false) { return MorphicResult.ErrorResult(); }

          // determine the taskbar's orientation
          //
          System.Windows.Forms.Orientation taskbarOrientation;
          if ((taskbarRect.right - taskbarRect.left) > (taskbarRect.bottom - taskbarRect.top))
          {
               taskbarOrientation = Orientation.Horizontal;
          }
          else
          {
               taskbarOrientation = Orientation.Vertical;
          }

          // establish the appropriate size for our tray button (i.e. same height/width as taskbar, and with an aspect ratio of 8:10)
          int trayButtonHeight;
          int trayButtonWidth;
          // NOTE: on some computers, the taskbar and notify tray return an inaccurate size, but the task button container appears to always return the correct size; therefore we match our primary dimension to the taskbutton container's same dimension
          // NOTE: the inaccurate size returned by GetWindowRect may be due to our moving this class from the main application to a helper library (i.e. perhaps the pixel scaling isn't applying correctly), or it could just be a weird quirk on some computers.
          //       [The GetWindowRect issue hapepns with both our own homebuilt PINVOKE methods as well as with PInvoke.User32.GetWindowRect; the function is returning the correct left, bottom and right positions of the taskbar and notify tray--but is
          //       sometimes misrepresenting the top (i.e. height) value of both the taskbar and notify tray rects]
          if (taskbarOrientation == Orientation.Horizontal)
          {
               // option 1: base our primary dimension off of the taskbutton container's same dimension
               trayButtonHeight = taskButtonContainerRect.bottom - taskButtonContainerRect.top;
               //
               // option 2: base our primary dimension off of the taskbar's same dimension
               //trayButtonHeight = taskbarRect.bottom - taskbarRect.top;
               //
               // [and then scale the secondary dimension to 80% of the size of the primary dimension]
               trayButtonWidth = (int)((Double)trayButtonHeight * 0.8);
          }
          else
          {
               // option 1: base our primary dimension off of the taskbutton container's same dimension
               trayButtonWidth = taskButtonContainerRect.right - taskButtonContainerRect.left;
               //
               // option 2: base our primary dimension off of the taskbar's same dimension
               //trayButtonWidth = taskbarRect.right - taskbarRect.left;
               //
               // [and then scale the secondary dimension to 80% of the size of the primary dimension]
               trayButtonHeight = (int)((Double)trayButtonWidth * 0.8);
          }

          // choose a space in the rightmost/bottommost position of the taskbar
          int trayButtonX;
          int trayButtonY;
          if (taskbarOrientation == Orientation.Horizontal)
          {
               trayButtonX = notifyTrayRect.left - trayButtonWidth;
               // NOTE: if we have any issues with positioning, try to replace taskbarRect.bottom with taskButtoncontainerRect.bottom (if we chose option #1 for our size calculations above)
               trayButtonY = taskbarRect.bottom - trayButtonHeight;
          }
          else
          {
               // NOTE: if we have any issues with positioning, try to replace taskbarRect.bottom with taskButtoncontainerRect.right (if we chose option #1 for our size calculations above)
               trayButtonX = taskbarRect.right - trayButtonWidth;
               trayButtonY = notifyTrayRect.top - trayButtonHeight;
          }

          // TEMPORARY: WRITE OUT LOG CALCULATIONS 

          var legacyCalculateResult = TrayButtonNativeWindow.CalculateCurrentAndTargetRectOfTrayButton(trayButtonHandle);
          var logBuilder = new StringBuilder();
          logBuilder.AppendLine("*** CalculatePositionAndSizeForTrayButton BEGIN ***");
          logBuilder.AppendLine("***");
          if (legacyCalculateResult is not null)
          {
               logBuilder.Append(legacyCalculateResult.Value.log);
          }
          else
          {
               logBuilder.Append("ERROR: CalculateCurrentAndTargetRectOfTrayButton RETURNED NULL...ERROR!");
          }
          logBuilder.AppendLine("***");

          // END TEMPORARY

          var result = new PInvoke.RECT() { left = trayButtonX, top = trayButtonY, right = trayButtonX + trayButtonWidth, bottom = trayButtonY + trayButtonHeight };

          // TEMPORARY: WRITE OUT LOG CALCULATIONS 
          logBuilder.AppendLine("modern API result: new PInvoke.RECT() { left = " + trayButtonX + ", top = " + trayButtonY + ", right = " + trayButtonX + " + " + trayButtonWidth + ", bottom = " + trayButtonY + " + " + trayButtonHeight + " } :: " + TrayButtonNativeWindow.RectToString(new LegacyWindowsApi.RECT() { Left = result.left, Top = result.top, Right = result.right, Bottom = result.bottom}));

          logBuilder.AppendLine("*** CalculatePositionAndSizeForTrayButton END ***");
          logBuilder.AppendLine("***");
          logBuilder.AppendLine("***");
          logBuilder.AppendLine("***");

          var getKnownFolderPathResult = TrayButtonNativeWindow.GetKnownFolderPath(KNOWNFOLDERID.FOLDERID_Downloads);
          string pathToDownloadsFolder;
          if (getKnownFolderPathResult.IsError == true)
          {
               Debug.Assert(false, "Could not get path to downloads folder");
               return new();
          }
          pathToDownloadsFolder = getKnownFolderPathResult.Value!;
          var pathToLogFile = System.IO.Path.Combine(pathToDownloadsFolder, "morphic_button_position_log.txt");
          System.IO.File.AppendAllText(pathToLogFile, logBuilder.ToString());

          // END TEMPORARY

          return MorphicResult.OkResult(result);
     }

     // NOTE: when using this function, ppszPath must be freed manually using FreeCoTaskMem
     [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
     private static extern int SHGetKnownFolderPath(
         [MarshalAs(UnmanagedType.LPStruct)] Guid rfid,
         uint dwFlags,
         IntPtr hToken,
         out IntPtr ppszPath);

     private const int S_OK = 0;

     // knownfolders.h
     internal struct KNOWNFOLDERID
     {
          public static Guid FOLDERID_Downloads = new Guid(0x374de290, 0x123f, 0x4565, 0x91, 0x64, 0x39, 0xc4, 0x92, 0x5e, 0x46, 0x7b); // {374DE290-123F-4565-9164-39C4925E467B}
     }

     internal static MorphicResult<string, MorphicUnit> GetKnownFolderPath(Guid knownFolderId)
     {
          IntPtr pointerToPath;
          var getKnownFolderPathResult = TrayButtonNativeWindow.SHGetKnownFolderPath(knownFolderId, 0, IntPtr.Zero, out pointerToPath);
          try
          {
               if (getKnownFolderPathResult == S_OK)
               {
                    var path = Marshal.PtrToStringUni(pointerToPath);
                    return MorphicResult.OkResult(path!);
               }
               else
               {
                    return MorphicResult.ErrorResult();
               }
          }
          finally
          {
               Marshal.FreeCoTaskMem(pointerToPath);
          }
     }

     // //

     private static string RectToString(LegacyWindowsApi.RECT rect)
     {
          return "RECT(left: " + rect.Left + "; top: " + rect.Top + "; right: " + rect.Right + "; bottom: " + rect.Bottom + ")";
     }

     // NOTE: this function is temporary, used only for calculating the position and size for layout of the tray button using our old calculations (to validate our new calculations)
     private static (LegacyWindowsApi.RECT? rect, Orientation orientation, string log)? CalculateCurrentAndTargetRectOfTrayButton(IntPtr? trayButtonHandle)
     {
          StringBuilder logBuilder = new();
          logBuilder.Append("CalculateCurrentAndTargetRectOfTrayButton() BEGIN\n");

          // NOTE: there are scenarios we must deal with where there may be multiple potential "taskbar button" icons to the left of the notification tray; in those scenarios, we must:
          // 1. Position ourself to the left of the other icon-button(s) (or in an empty space in between them)
          // 2. Reposition our icon when the other icon-button(s) are removed from the taskbar (e.g. when their host applications close them)
          // 3. If we detect that we and another application are writing on top of each other (or repositioning the taskbar button container on top of our icon), then we must fail
          //    gracefully and let our host application know so it can warn the user, place the icon in the notification tray instead, etc.

          // To position the tray button, we need to find three windows:
          // 1. the taskbar itself
          // 2. the section of the taskbar which holds the taskbar buttons (i.e. to the right of the start button and find/cortana/taskview buttons, but to the left of the notification tray) */
          // 3. the notification tray
          //
          // We will then resize the section of the taskbar that holds the taskbar buttons so that we can place our tray button to its right (i.e. to the left of the notification tray).

          var taskbarTripletHandles = TrayButtonNativeWindow.GetTaskbarTripletHandles();
          var taskbarHandle = taskbarTripletHandles.TaskbarHandle;
          logBuilder.Append("taskbarHandle: " + taskbarTripletHandles.TaskbarHandle.ToString() + "\n");
          logBuilder.Append("taskButtonContainerHandle: " + taskbarTripletHandles.TaskButtonContainerHandle.ToString() + "\n");
          logBuilder.Append("notifyTrayHandle: " + taskbarTripletHandles.NotifyTrayHandle.ToString() + "\n");
          logBuilder.Append("...\n");

          var taskbarRects = TrayButtonNativeWindow.GetTaskbarTripletRects(taskbarTripletHandles.TaskbarHandle, taskbarTripletHandles.TaskButtonContainerHandle, taskbarTripletHandles.NotifyTrayHandle);
          if (taskbarRects is null)
          {
               return null;
          }
          var taskbarRect = taskbarRects.Value.TaskbarRect;
          var taskButtonContainerRect = taskbarRects.Value.TaskButtonContainerRect;
          var notifyTrayRect = taskbarRects.Value.NotifyTrayRect;
          logBuilder.Append("taskbarRect: " + TrayButtonNativeWindow.RectToString(taskbarRects.Value.TaskbarRect) + "\n");
          logBuilder.Append("taskButtonContainerRect: " + TrayButtonNativeWindow.RectToString(taskbarRects.Value.TaskButtonContainerRect) + "\n");
          logBuilder.Append("notifyTrayRect: " + TrayButtonNativeWindow.RectToString(taskbarRects.Value.NotifyTrayRect) + "\n");
          logBuilder.Append("...\n");

          // determine the taskbar's orientation
          System.Windows.Forms.Orientation taskbarOrientation;
          if ((taskbarRect.Right - taskbarRect.Left) > (taskbarRect.Bottom - taskbarRect.Top))
          {
               taskbarOrientation = Orientation.Horizontal;
               logBuilder.Append("taskbarOrientation: Horizontal\n");
          }
          else
          {
               taskbarOrientation = Orientation.Vertical;
               logBuilder.Append("taskbarOrientation: Vertical\n");
          }
          logBuilder.Append("...\n");

          // calculate all of the free rects between the task button container and notify tray
          var calculateEmptyRectsResult = TrayButtonNativeWindow.CalculateEmptyRectsBetweenTaskButtonContainerAndNotifyTray(trayButtonHandle, taskbarHandle, taskbarOrientation, taskbarRect, taskButtonContainerRect, notifyTrayRect);
          logBuilder.AppendLine("***");
          logBuilder.Append(calculateEmptyRectsResult.log);
          logBuilder.AppendLine("***");
          var freeAreaChildRects = calculateEmptyRectsResult.childRects;
          var freeAreaAvailableRect = calculateEmptyRectsResult.availableAreaRect;
          foreach (var freeAreaChildRect in freeAreaChildRects)
          {
               logBuilder.Append("freeAreaChildRect: " + TrayButtonNativeWindow.RectToString(freeAreaChildRect) + "\n");

          }
          logBuilder.Append("freeAreaAvailableRect: " + TrayButtonNativeWindow.RectToString(freeAreaAvailableRect) + "\n");
          logBuilder.Append("...\n");

          /* determine the rect for our tray button; based on our current positioning strategy, this will either be its existing position or the leftmost/topmost "next to tray" position.  
               * If we are determining the leftmost/topmost "next to tray" position, we will find the available space between the task button container and the notification tray (or any 
               * already-present controls that are already left/top of the notification tray); if there is not enough free space available in that area then we will shrink the task button
               * container to make room. */
          //
          /* NOTE: there are some deficiencies to our current positioning strategy.  Of note...
               * 1. In some circumstances, it might be possible that we are leaving "holes" of available space between the task button container and the notification tray; but if that
               *    happens, it might be something beyond our control (as other apps may have created that space).  One concern is if we shrink our icon (in which case we should in theory
               *    shrink the space to our top/left)
               * 2. If other apps draw their next-to-tray buttons after us and are not watching for conflicts then they could draw over us; a mitigation measure in that instance might be to
               *    use a timer to check that our tray button is not obscured and then remedy the situation; if we got into a "fight" over real estate that appeared to never terminate then
               *    we could destroy our icon and raise an event letting the application know it should choose an alternate strategy (such as a notification tray icon) instead.
               * 3. If a more-rightmost/bottommost icon's application is closed while we are running, the taskbar could be resized to obscure us; we might need a timer (or we might need to
               *    capture the appropriate window message) to discover this scenario.
               * In summary there is no standardized system (other than perhaps the "(dock) toolbar in taskbar" mechanism); if we find that we encounter problems in the field with our current
               * strategy, we may want to consider rebuilding this functionality via the "toolbar in taskbar" mechanism.  See HP Support Assistant for an example of another application
               * which is doing what we are trying to do with the next-to-tray button strategy */

          // establish the appropriate size for our tray button (i.e. same height/width as taskbar, and with an aspect ratio of 8:10)
          int trayButtonHeight;
          int trayButtonWidth;
          if (taskbarOrientation == Orientation.Horizontal)
          {
               trayButtonHeight = taskbarRect.Bottom - taskbarRect.Top;
               trayButtonWidth = (int)((Double)trayButtonHeight * 0.8);
          }
          else
          {
               trayButtonWidth = taskbarRect.Right - taskbarRect.Left;
               trayButtonHeight = (int)((Double)trayButtonWidth * 0.8);
          }
          logBuilder.Append("trayButtonWidth: " + trayButtonWidth.ToString() + "\n");
          logBuilder.Append("trayButtonHeight: " + trayButtonHeight.ToString() + "\n");
          logBuilder.Append("...\n");

          // get our current rect (in case we can just reuse the current position...and also to make sure it doesn't need to be resized)
          LegacyWindowsApi.RECT currentRectAsNonNullable;
          LegacyWindowsApi.RECT? currentRect = null;
          LegacyWindowsApi.RECT? currentRectForResult = null;
          if (LegacyWindowsApi.GetWindowRect(trayButtonHandle is not null ? trayButtonHandle.Value : IntPtr.Zero, out currentRectAsNonNullable) == true)
          {
               currentRect = currentRectAsNonNullable;
               currentRectForResult = currentRectAsNonNullable;
               logBuilder.Append("currentRect: " + TrayButtonNativeWindow.RectToString(currentRect.Value) + "\n");
          }
          else
          {
               logBuilder.Append("currentRect: [NULL]\n");
          }

          // if the current position of our window isn't the right size for our icon, then set it to NULL so we don't try to reuse it.
          if ((currentRect is not null) &&
               ((currentRect.Value.Right - currentRect.Value.Left != trayButtonWidth) || (currentRect.Value.Bottom - currentRect.Value.Top != trayButtonHeight)))
          {
               currentRect = null;
               logBuilder.Append("current position of window is acceptable for reuse.\n");
          }
          else
          {
               logBuilder.Append("current position of window is NOT ACCEPTABLE for reuse; discarding.\n");
          }
          logBuilder.Append("...\n");

          // calculate the new rect for our tray button's window
          LegacyWindowsApi.RECT? newRect = null;

          // if the space occupied by our already-existing rect is not overlapped by anyone else and is in the free area, keep using the same space
          if (currentRect is not null)
          {
               logBuilder.Append("currentRect.Value.Intersects(freeAreaAvailableRect): " + currentRect.Value.Intersects(freeAreaAvailableRect).ToString() + "\n");
          }
          else
          {
               logBuilder.AppendLine("currentRect IS NULL");
          }
          if ((currentRect is not null) && (currentRect.Value.Intersects(freeAreaAvailableRect) == true))
          {
               // by default, assume that our currentRect is still available (i.e. not overlapped)
               bool currentRectIsNotOverlapped = true;

               // make sure we do not overlap another control in the free area
               foreach (var freeAreaChildRect in freeAreaChildRects)
               {
                    logBuilder.Append("currentRect.Value.Intersects(freeAreaChildRect: " + TrayButtonNativeWindow.RectToString(freeAreaChildRect) + "): " + currentRect.Value.Intersects(freeAreaChildRect).ToString() + "\n");
                    if (currentRect.Value.Intersects(freeAreaChildRect) == true)
                    {
                         // overlap conflict
                         currentRectIsNotOverlapped = false;
                         break;
                    }
               }

               logBuilder.Append("currentRectIsNotOverlapped: " + currentRectIsNotOverlapped.ToString() + "\n");
               if (currentRectIsNotOverlapped == true)
               {
                    // set "newRect" (the variable for where we will now place our tray button) to the same position we were already at
                    newRect = currentRect;
               }
          }

          logBuilder.Append("...\n");
          // if our current (already-used-by-us) rect was not available, choose the leftmost/topmost space available
          if (newRect is null)
          {
               if (taskbarOrientation == Orientation.Horizontal)
               {
                    logBuilder.AppendLine("taskbarOrientation == Orientation.Horizontal");
                    // horizontal taskbar: find the leftmost rect in the available space (which we'll then carve the "rightmost" section out of)
                    LegacyWindowsApi.RECT leftmostRect = freeAreaAvailableRect;

                    foreach (var freeAreaChildRect in freeAreaChildRects)
                    {
                         logBuilder.Append("freeAreaChildRect.Left < leftmostRect.Right [" + freeAreaChildRect.Left + " < " + leftmostRect.Right + "]: " + (freeAreaChildRect.Left < leftmostRect.Right).ToString() + "\n");
                         if (freeAreaChildRect.Left < leftmostRect.Right)
                         {
                              leftmostRect.Right = freeAreaChildRect.Left;
                         }
                    }

                    // choose the rightmost space in the leftmostRect area; expand our tray button towards the left if/as necessary
                    newRect = new LegacyWindowsApi.RECT(new System.Windows.Rect(leftmostRect.Right - trayButtonWidth, leftmostRect.Bottom - trayButtonHeight, trayButtonWidth, trayButtonHeight));
                    logBuilder.Append("SETTING newRect to: {" + leftmostRect.Right + " - " + trayButtonWidth + ", " + leftmostRect.Bottom + " - " + trayButtonHeight + ", " + trayButtonWidth + ", " + trayButtonHeight + "}\n");
                    logBuilder.AppendLine("newRect: " + TrayButtonNativeWindow.RectToString(newRect.Value));
                    logBuilder.Append("...\n");
               }
               else
               {
                    logBuilder.AppendLine("taskbarOrientation != Orientation.Horizontal");
                    // vertical taskbar: find the topmost rect in the available space (which we'll then carve the "bottommost" section out of)
                    LegacyWindowsApi.RECT topmostRect = freeAreaAvailableRect;

                    foreach (var freeAreaChildRect in freeAreaChildRects)
                    {
                         logBuilder.Append("freeAreaChildRect.Top < topmostRect.Bottom [" + freeAreaChildRect.Top + " < " + topmostRect.Bottom + "]: " + (freeAreaChildRect.Top < topmostRect.Bottom).ToString() + "\n");
                         if (freeAreaChildRect.Top < topmostRect.Bottom)
                         {
                              topmostRect.Bottom = freeAreaChildRect.Top;
                         }
                    }

                    // choose the bottommost space in the topmostRect area; expand our tray button towards the top if/as necessary
                    newRect = new LegacyWindowsApi.RECT(new System.Windows.Rect(topmostRect.Right - trayButtonWidth, topmostRect.Bottom - trayButtonHeight, trayButtonWidth, trayButtonHeight));
                    logBuilder.Append("SETTING newRect to: {" + topmostRect.Right + " - " + trayButtonWidth + ", " + topmostRect.Bottom + " - " + trayButtonHeight + ", " + trayButtonWidth + ", " + trayButtonHeight + "}\n");
                    logBuilder.AppendLine("newRect: " + TrayButtonNativeWindow.RectToString(newRect.Value));
                    logBuilder.Append("...\n");
               }
          }

          logBuilder.Append("CalculateCurrentAndTargetRectOfTrayButton() END\n");

          return (newRect, taskbarOrientation, logBuilder.ToString());
     }

     private static (IntPtr TaskbarHandle, IntPtr TaskButtonContainerHandle, IntPtr NotifyTrayHandle) GetTaskbarTripletHandles()
     {
          var taskbarHandle = TrayButtonNativeWindow.GetWindowsTaskbarHandle();
          var taskButtonContainerHandle = TrayButtonNativeWindow.GetWindowsTaskbarTaskButtonContainerHandle();
          var notifyTrayHandle = TrayButtonNativeWindow.GetWindowsTaskbarNotificationTrayHandle();

          return (taskbarHandle, taskButtonContainerHandle, notifyTrayHandle);
     }

     private static IntPtr GetWindowsTaskbarTaskButtonContainerHandle()
     {
          var taskbarHandle = TrayButtonNativeWindow.GetWindowsTaskbarHandle();
          if (taskbarHandle == IntPtr.Zero)
          {
               return IntPtr.Zero;
          }
          return LegacyWindowsApi.FindWindowEx(taskbarHandle, IntPtr.Zero, "ReBarWindow32", null);
     }

     private static IntPtr GetWindowsTaskbarNotificationTrayHandle()
     {
          var taskbarHandle = TrayButtonNativeWindow.GetWindowsTaskbarHandle();
          if (taskbarHandle == IntPtr.Zero)
          {
               return IntPtr.Zero;
          }
          return LegacyWindowsApi.FindWindowEx(taskbarHandle, IntPtr.Zero, "TrayNotifyWnd", null);
     }

     private static (LegacyWindowsApi.RECT TaskbarRect, LegacyWindowsApi.RECT TaskButtonContainerRect, LegacyWindowsApi.RECT NotifyTrayRect)? GetTaskbarTripletRects(IntPtr taskbarHandle, IntPtr taskButtonContainerHandle, IntPtr notifyTrayHandle)
     {
          // find the taskbar and its rect
          LegacyWindowsApi.RECT taskbarRect = new LegacyWindowsApi.RECT();
          if (LegacyWindowsApi.GetWindowRect(taskbarHandle, out taskbarRect) == false)
          {
               // failed; abort
               Debug.Assert(false, "Could not obtain window handle to taskbar.");
               return null;
          }

          // find the window handles and rects of the task button container and the notify tray (which are children inside of the taskbar)
          //
          LegacyWindowsApi.RECT taskButtonContainerRect = new LegacyWindowsApi.RECT();
          if (LegacyWindowsApi.GetWindowRect(taskButtonContainerHandle, out taskButtonContainerRect) == false)
          {
               // failed; abort
               Debug.Assert(false, "Could not obtain window handle to taskbar's task button list container.");
               return null;
          }
          //
          LegacyWindowsApi.RECT notifyTrayRect = new LegacyWindowsApi.RECT();
          if (LegacyWindowsApi.GetWindowRect(notifyTrayHandle, out notifyTrayRect) == false)
          {
               // failed; abort
               Debug.Assert(false, "Could not obtain window handle to taskbar's notify tray.");
               return null;
          }

          return (taskbarRect, taskButtonContainerRect, notifyTrayRect);
     }

     private static (LegacyWindowsApi.RECT availableAreaRect, List<LegacyWindowsApi.RECT> childRects, string log) CalculateEmptyRectsBetweenTaskButtonContainerAndNotifyTray(IntPtr? trayButtonHandle, IntPtr taskbarHandle, Orientation taskbarOrientation, LegacyWindowsApi.RECT taskbarRect, LegacyWindowsApi.RECT taskButtonContainerRect, LegacyWindowsApi.RECT notifyTrayRect)
     {
          StringBuilder logBuilder = new();
          logBuilder.Append("CalculateEmptyRectsBetweenTaskButtonContainerAndNotifyTray() BEGIN\n");

          // calculate the total "free area" rectangle (the area between the task button container and the notify tray where we want to place our tray button)
          LegacyWindowsApi.RECT freeAreaAvailableRect;
          if (taskbarOrientation == Orientation.Horizontal)
          {
               logBuilder.AppendLine("taskbarOrientation == Orientation.Horizontal");
               freeAreaAvailableRect = new LegacyWindowsApi.RECT(new System.Windows.Rect(taskButtonContainerRect.Right, taskbarRect.Top, Math.Max(notifyTrayRect.Left - taskButtonContainerRect.Right, 0), Math.Max(taskbarRect.Bottom - taskbarRect.Top, 0)));
               logBuilder.AppendLine("freeAreaAvailableRect = RECT(" + taskButtonContainerRect.Right + ", " + taskbarRect.Top + ", " + Math.Max(notifyTrayRect.Left - taskButtonContainerRect.Right, 0) + ", " + Math.Max(taskbarRect.Bottom - taskbarRect.Top, 0) + ") :: " + TrayButtonNativeWindow.RectToString(freeAreaAvailableRect));
          }
          else
          {
               logBuilder.AppendLine("taskbarOrientation != Orientation.Horizontal");
               freeAreaAvailableRect = new LegacyWindowsApi.RECT(new System.Windows.Rect(taskbarRect.Left, taskButtonContainerRect.Bottom, Math.Max(taskbarRect.Right - taskbarRect.Left, 0), Math.Max(notifyTrayRect.Top - taskButtonContainerRect.Bottom, 0)));
               logBuilder.AppendLine("freeAreaAvailableRect = RECT(" + taskbarRect.Left + ", " + taskButtonContainerRect.Bottom + ", " + Math.Max(taskbarRect.Right - taskbarRect.Left, 0) + ", " + Math.Max(notifyTrayRect.Top - taskButtonContainerRect.Bottom, 0) + ") :: " + TrayButtonNativeWindow.RectToString(freeAreaAvailableRect));
          }
          logBuilder.AppendLine("........");

          // capture a list of all child windows within the taskbar; we'll use this list to enumerate the rects of all the taskbar's children
          var taskbarChildHandles = TrayButtonNativeWindow.EnumerateChildWindows(taskbarHandle);
          //
          // find the rects of all windows within the taskbar; we need this information so that we do not overlap any other accessory windows which are trying to sit in the same area as us
          var taskbarChildHandlesWithRects = new Dictionary<IntPtr, LegacyWindowsApi.RECT>();
          foreach (var taskbarChildHandle in taskbarChildHandles)
          {
               logBuilder.AppendLine("taskbarChildHandle: " + taskbarChildHandle.ToString());
               LegacyWindowsApi.RECT taskbarChildRect = new LegacyWindowsApi.RECT();
               if (LegacyWindowsApi.GetWindowRect(taskbarChildHandle, out taskbarChildRect) == true)
               {
                    logBuilder.AppendLine("taskbarChildRect: " + TrayButtonNativeWindow.RectToString(taskbarChildRect));
                    taskbarChildHandlesWithRects.Add(taskbarChildHandle, taskbarChildRect);
               }
               else
               {
                    logBuilder.AppendLine("taskbarChildRect: ERR COULD NOT CAPTURE");
                    Debug.Assert(false, "Could not capture RECTs of all taskbar child windows");
               }
          }
          logBuilder.AppendLine("........");

          // remove any child rects which are contained inside the task button container (so that we eliminate any subchildren from our calculations)
          foreach (var taskbarChildHandle in taskbarChildHandles)
          {
               if (taskbarChildHandlesWithRects.ContainsKey(taskbarChildHandle) == true)
               {
                    var taskbarChildRect = taskbarChildHandlesWithRects[taskbarChildHandle];
                    if (taskbarChildRect.IsInside(taskButtonContainerRect))
                    {
                         logBuilder.AppendLine("taskbarChildRect " + TrayButtonNativeWindow.RectToString(taskbarChildRect) + " IsInside " + TrayButtonNativeWindow.RectToString(taskButtonContainerRect) + "taskButtonContainerRect");
                         taskbarChildHandlesWithRects.Remove(taskbarChildHandle);
                    }
                    else
                    {
                         logBuilder.AppendLine("taskbarChildRect " + TrayButtonNativeWindow.RectToString(taskbarChildRect) + " IS NOT INSIDE" + TrayButtonNativeWindow.RectToString(taskButtonContainerRect) + "taskButtonContainerRect");
                    }
               }
          }
          logBuilder.AppendLine("........");

          // remove our own (tray button) window handle from the list (so that we don't see our current screen rect as "taken" in the list of occupied RECTs)
          if (trayButtonHandle is not null)
          {
               taskbarChildHandlesWithRects.Remove(trayButtonHandle.Value);
          }

          // create a list of children which are located between the task button container and the notify tray (i.e. windows which are occupying the same region we want to
          // occupy...so we can try to avoid overlapping)
          List<LegacyWindowsApi.RECT> freeAreaChildRects = new List<LegacyWindowsApi.RECT>();
          foreach (var taskbarChildHandle in taskbarChildHandles)
          {
               if (taskbarChildHandlesWithRects.ContainsKey(taskbarChildHandle) == true)
               {
                    var taskbarChildRect = taskbarChildHandlesWithRects[taskbarChildHandle];
                    if ((taskbarChildRect.IsInside(freeAreaAvailableRect) == true) &&
                    (taskbarChildRect.HasNonZeroWidthOrHeight() == false))
                    {
                         logBuilder.AppendLine("freeAreaChildRects.Add(" + TrayButtonNativeWindow.RectToString(taskbarChildRect) + ")");
                         freeAreaChildRects.Add(taskbarChildRect);
                    }
               }
          }

          logBuilder.Append("CalculateEmptyRectsBetweenTaskButtonContainerAndNotifyTray() END\n");

          return (freeAreaAvailableRect, freeAreaChildRects, logBuilder.ToString());
     }

     internal static List<IntPtr> EnumerateChildWindows(IntPtr parentHwnd)
     {
          var result = new List<IntPtr>();

          // create an unmanaged pointer to our list (using a GC-managed handle)
          GCHandle resultGCHandle = GCHandle.Alloc(result, GCHandleType.Normal);
          // convert our GCHandle into an IntPtr (which we will unconvert back to a GCHandler in the EnumChildWindows callback) 
          IntPtr resultGCHandleAsIntPtr = GCHandle.ToIntPtr(resultGCHandle);

          try
          {
               var enumFunction = new LegacyWindowsApi.EnumWindowsProc(TrayButtonNativeWindow.EnumerateChildWindowsCallback);
               LegacyWindowsApi.EnumChildWindows(parentHwnd, enumFunction, resultGCHandleAsIntPtr);

          }
          finally
          {
               if (resultGCHandle.IsAllocated)
               {
                    resultGCHandle.Free();
               }
          }

          return result;
     }
     internal static bool EnumerateChildWindowsCallback(IntPtr hwnd, IntPtr lParam)
     {
          // convert lParam back into the result list object
          var resultGCHandle = GCHandle.FromIntPtr(lParam);
          List<IntPtr>? result = resultGCHandle.Target as List<IntPtr>;

          if (result is not null)
          {
               result.Add(hwnd);
          }
          else
          {
               Debug.Assert(false, "Could not enumerate child windows");
          }

          return true;
     }

     // //

     //

     private MorphicResult<System.Drawing.Point, MorphicUnit> ConvertMouseMessageLParamToScreenPoint(IntPtr lParam)
     {
          var x = (ushort)((lParam.ToInt64() >> 0) & 0xFFFF);
          var y = (ushort)((lParam.ToInt64() >> 16) & 0xFFFF);
          // convert x and y to screen coordinates
          var hitPoint = new PInvoke.POINT { x = x, y = y };

          // NOTE: the instructions for MapWindowPoints instruct us to call SetLastError before calling MapWindowPoints to ensure that we can distinguish a result of 0 from an error if the last win32 error wasn't set (because it wasn't an error)
          Marshal.SetLastPInvokeError(0);
          //
          // NOTE: the PInvoke implementation of MapWindowPoints did not support passing in a POINT struct, so we manually declared the function
          var mapWindowPointsResult = WindowsApi.MapWindowPoints(this.Handle, IntPtr.Zero, ref hitPoint, 1);
          if (mapWindowPointsResult == 0 && Marshal.GetLastWin32Error() != 0)
          {
               // failed; abort
               Debug.Assert(false, "Could not map tray button hit point to screen coordinates");
               return MorphicResult.ErrorResult();
          }

          var result = new System.Drawing.Point(hitPoint.x, hitPoint.y);
          return MorphicResult.OkResult(result);
     }

     //

     // NOTE: this function takes the window size as input and calculates the size of the icon to display, centered, within the window.
     private static System.Drawing.Size CalculateWidthAndHeightForBitmap(PInvoke.RECT availableRect, System.Drawing.Size bitmapSize)
     {
          var availableSize = new System.Drawing.Size(availableRect.right - availableRect.left, availableRect.bottom - availableRect.top);

          /* determine the larger dimension (width or height) */
          //int largerDimensionSize;
          //int smallerDimensionSize;
          System.Drawing.Size insideMarginsSize;
          if (availableSize.Height > availableSize.Width)
          {
               //largerDimensionSize = availableSize.Height;
               //smallerDimensionSize = availableSize.Width;
               insideMarginsSize = new((int)((double)availableSize.Width * 0.9), (int)((double)availableSize.Height * (2.0/3.0)));
          }
          else
          {
               //largerDimensionSize = availableSize.Width;
               //smallerDimensionSize = availableSize.Height;
               insideMarginsSize = new((int)((double)availableSize.Width * (2.0/3.0)), (int)((double)availableSize.Height * 0.9));
          }

          /* shrink the bitmap size down so that it fits inside the available rect */

          // by default, assume the bitmap will be the size of the source image
          int bitmapWidth = bitmapSize.Width;
          int bitmapHeight = bitmapSize.Height;
          //
          // if bitmap is wider than the available rect, shrink it equally in both directions
          if (bitmapWidth > insideMarginsSize.Width)
          {
               double scaleFactor = (double)insideMarginsSize.Width / (double)bitmapWidth;
               bitmapWidth = insideMarginsSize.Width;
               bitmapHeight = (int)((double)bitmapHeight * scaleFactor);
          }
          //
          // if bitmap is taller than the available rect, shrink it further (and equally in both directions)
          if (bitmapHeight > insideMarginsSize.Height)
          {
               double scaleFactor = (double)insideMarginsSize.Height / (double)bitmapHeight;
               bitmapWidth = (int)((double)bitmapWidth * scaleFactor);
               bitmapHeight = insideMarginsSize.Height;
          }

          // if bitmap does not touch either of the two margins (i.e. is too small), enlarge it now.
          if (bitmapWidth != insideMarginsSize.Width && bitmapHeight != insideMarginsSize.Height)
          {
               // if bitmap is not as wide as the insideMarginsWidth, enlarge it now (equally in both directions)
               if (bitmapWidth < insideMarginsSize.Width) {
                    double scaleFactor = (double)insideMarginsSize.Width / (double)bitmapWidth;
                    bitmapWidth = insideMarginsSize.Width;
                    bitmapHeight = (int)((double)bitmapHeight * scaleFactor);
               }
               //
               // if bitmap is now too tall, shrink it back down (equally in both directions)
               if (bitmapHeight > insideMarginsSize.Height)
               {
                    double scaleFactor = (double)insideMarginsSize.Height / (double)bitmapHeight;
                    bitmapWidth = (int)((double)bitmapWidth * scaleFactor);
                    bitmapHeight = insideMarginsSize.Height;
               }
          }



          return new System.Drawing.Size(bitmapWidth, bitmapHeight);
     }

     //

     private static IntPtr GetWindowsTaskbarHandle()
     {
          return PInvoke.User32.FindWindow("Shell_TrayWnd", null);
     }
     //
     private static IntPtr GetWindowsTaskbarTaskButtonContainerHandle(IntPtr taskbarHandle)
     {
          if (taskbarHandle == IntPtr.Zero)
          {
               return IntPtr.Zero;
          }
          return PInvoke.User32.FindWindowEx(taskbarHandle, IntPtr.Zero, "ReBarWindow32", null);
     }
     //
     private static IntPtr GetWindowsTaskbarNotificationTrayHandle(IntPtr taskbarHandle)
     {
          if (taskbarHandle == IntPtr.Zero)
          {
               return IntPtr.Zero;
          }
          return PInvoke.User32.FindWindowEx(taskbarHandle, IntPtr.Zero, "TrayNotifyWnd", null);
     }
}
