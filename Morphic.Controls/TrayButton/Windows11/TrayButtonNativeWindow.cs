// Copyright 2020-2023 Raising the Floor - US, Inc.
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

     private PInvoke.RECT _positionAndSize;

     private ArgbImageNativeWindow? _argbImageNativeWindow = null;

     private IntPtr _locationChangeWindowEventHook = IntPtr.Zero;
     private WindowsApi.WinEventProc? _locationChangeWindowEventProc = null;

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
          result._positionAndSize = trayButtonPositionAndSize;

          /* get the handle for the taskbar; it will be the owner of our native window (so that our window sits above it in the zorder) */
          // NOTE: we will still need to push our window to the front of its owner's zorder stack in some circumstances, as certain actions (such as popping up the task list balloons above the task bar) will reorder the taskbar's zorder and push us behind the taskbar
          // NOTE: making the taskbar our owner has the side-effect of putting our window above full-screen applications (even though our window is not itself "always on top"); we will need to hide our window whenever a window goes full-screen on the same monitor (and re-show our window whenever the window exits full-screen mode)
          var taskbarHandle = TrayButtonNativeWindow.GetWindowsTaskbarHandle();


          /* create an instance of our native window */

          CreateParams windowParams = new CreateParams()
          {
               ClassName = s_morphicTrayButtonClassInfoExAtom.ToString(), // for simplicity, we pass the value of the custom class as its integer self but in string form; our CreateWindow function will parse this and convert it to an int
               Caption = nativeWindowClassName,
               Style = unchecked((int)(/*PInvoke.User32.WindowStyles.WS_CLIPSIBLINGS | */PInvoke.User32.WindowStyles.WS_POPUP /*| PInvoke.User32.WindowStyles.WS_TABSTOP*/ | PInvoke.User32.WindowStyles.WS_VISIBLE)),
               ExStyle = (int)(PInvoke.User32.WindowStylesEx.WS_EX_LAYERED/* | PInvoke.User32.WindowStylesEx.WS_EX_TOOLWINDOW*/),
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
          _argbImageNativeWindow?.UpdateOwnerHWnd(this.Handle);
     }

     //

     protected virtual void Dispose(bool disposing)
     {
          if (!disposedValue)
          {
               if (disposing)
               {
                    // dispose managed state (managed objects)

                    if (_locationChangeWindowEventHook != IntPtr.Zero)
                    {
                         WindowsApi.UnhookWinEvent(_locationChangeWindowEventHook);
                    }
               }

               // TODO: free unmanaged resources (unmanaged objects) and override finalizer
               this.DestroyHandle();

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

     private void UpdateVisualStateAlpha()
     {
          // default to "Normal" visual state
          Double highlightOpacity = 0.0;

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

          // if the window being moved was one of the task list windows (i.e. the windows that pop up above the taskbar), then our zorder has probably been pushed down: bring our window to the top of its owner
          if (className == "TaskListThumbnailWnd" || className == "TaskListOverlayWnd")
          {
               Debug.WriteLine("className: " + className);
               var bringWindowToTopSuccess = PInvoke.User32.BringWindowToTop(this.Handle);
          }
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
               var bitmapSize = bitmap.Size;
               var argbImageNativeWindowSize = TrayButtonNativeWindow.CalculateWidthAndHeightForBitmap(_positionAndSize, bitmapSize);

               var bitmapRect = TrayButtonNativeWindow.CalculateCenterRectInsideRect(_positionAndSize, bitmapSize);

               _argbImageNativeWindow?.SetPositionAndSize(bitmapRect);
          }
          _argbImageNativeWindow?.SetBitmap(bitmap);
     }

     public void SetText(string? text)
     {
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
          //var taskButtonContainerHandle = TrayButtonNativeWindow.GetWindowsTaskbarTaskButtonContainerHandle(taskbarHandle);
          //if (taskButtonContainerHandle == IntPtr.Zero) { return MorphicResult.ErrorResult(); }
          //
          var notifyTrayHandle = TrayButtonNativeWindow.GetWindowsTaskbarNotificationTrayHandle(taskbarHandle);
          if (notifyTrayHandle == IntPtr.Zero) { return MorphicResult.ErrorResult(); }

          // get the RECTs for the taskbar, task button container and the notify tray
          //
          var getTaskbarRectSuccess = PInvoke.User32.GetWindowRect(taskbarHandle, out var taskbarRect);
          if (getTaskbarRectSuccess == false) { return MorphicResult.ErrorResult(); }
          //
          //var getTaskButtonContainerRectSuccess = PInvoke.User32.GetWindowRect(taskButtonContainerHandle, out var taskButtonContainerRect);
          //if (getTaskButtonContainerRectSuccess == false) { return MorphicResult.ErrorResult(); }
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
          if (taskbarOrientation == Orientation.Horizontal)
          {
               trayButtonHeight = taskbarRect.bottom - taskbarRect.top;
               trayButtonWidth = (int)((Double)trayButtonHeight * 0.8);
          }
          else
          {
               trayButtonWidth = taskbarRect.right - taskbarRect.left;
               trayButtonHeight = (int)((Double)trayButtonWidth * 0.8);
          }

          // choose a space in the rightmost/bottommost position of the taskbar
          int trayButtonX;
          int trayButtonY;
          if (taskbarOrientation == Orientation.Horizontal)
          {
               trayButtonX = notifyTrayRect.left - trayButtonWidth;
               trayButtonY = taskbarRect.top;
          }
          else
          {
               trayButtonX = taskbarRect.left;
               trayButtonY = notifyTrayRect.top - trayButtonHeight;
          }

          var result = new PInvoke.RECT() { left = trayButtonX, top = trayButtonY, right = trayButtonX + trayButtonWidth, bottom = trayButtonY + trayButtonHeight };
          return MorphicResult.OkResult(result);
     }

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
