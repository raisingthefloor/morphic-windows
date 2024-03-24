// Copyright 2020-2024 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-controls-lib-cs/blob/main/LICENSE.txt
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
using PInvoke;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Morphic.Controls.TrayButton.Windows11;

internal class ArgbImageNativeWindow : System.Windows.Forms.NativeWindow, IDisposable
{
     private bool disposedValue;

     private System.Drawing.Bitmap? _sourceBitmap = null;
     private System.Drawing.Bitmap? _sizedBitmap = null;

     private bool _visible;

     private static ushort? s_morphicArgbImageClassInfoExAtom = null;

     private ArgbImageNativeWindow()
     {
     }

     public static MorphicResult<ArgbImageNativeWindow, Morphic.Controls.TrayButton.Windows11.CreateNewError> CreateNew(IntPtr parentHWnd, int x, int y, int width, int height)
     {
          var result = new ArgbImageNativeWindow();

          /* register a custom native window class for our ARGB Image window (or refer to the already-registered class, if we captured it earlier in the application's execution) */
          const string nativeWindowClassName = "Morphic-ArgbImage";
          //
          if (s_morphicArgbImageClassInfoExAtom is null)
          {
               // register our control's custom native window class
               var pointerToWndProcCallback = Marshal.GetFunctionPointerForDelegate(new PInvokeExtensions.WndProc(result.WndProcCallback));
               var lpWndClassEx = new PInvokeExtensions.WNDCLASSEX
               {
                    cbSize = (uint)Marshal.SizeOf(typeof(PInvokeExtensions.WNDCLASSEX)),
                    lpfnWndProc = pointerToWndProcCallback,
                    lpszClassName = nativeWindowClassName,
                    hCursor = PInvoke.User32.LoadCursor(IntPtr.Zero, (IntPtr)PInvoke.User32.Cursors.IDC_ARROW).DangerousGetHandle()
               };

               // NOTE: RegisterClassEx returns an ATOM (or 0 if the call failed)
               var registerClassResult = PInvokeExtensions.RegisterClassEx(ref lpWndClassEx);
               if (registerClassResult == 0) // failure
               {
                    var win32Exception = new PInvoke.Win32Exception(Marshal.GetLastWin32Error());
                    if (win32Exception.NativeErrorCode == PInvoke.Win32ErrorCode.ERROR_CLASS_ALREADY_EXISTS)
                    {
                         Debug.Assert(false, "Class was already registered; we should have recorded this ATOM, and we cannot proceed");
                    }
                    return MorphicResult.ErrorResult(Morphic.Controls.TrayButton.Windows11.CreateNewError.Win32Error((uint)win32Exception.ErrorCode));
               }
               s_morphicArgbImageClassInfoExAtom = registerClassResult;
          }

          /* create an instance of our native window */

          var windowParams = new System.Windows.Forms.CreateParams()
          {
               ClassName = s_morphicArgbImageClassInfoExAtom.ToString(), // for simplicity, we pass the value of the custom class as its integer self but in string form; our CreateWindow function will parse this and convert it to an int
               Caption = nativeWindowClassName,
               Style = unchecked((int)(/*PInvoke.User32.WindowStyles.WS_CLIPSIBLINGS | */PInvoke.User32.WindowStyles.WS_POPUP | PInvoke.User32.WindowStyles.WS_VISIBLE)),
               ExStyle = (int)(PInvoke.User32.WindowStylesEx.WS_EX_LAYERED/* | PInvoke.User32.WindowStylesEx.WS_EX_TOOLWINDOW*/ | PInvoke.User32.WindowStylesEx.WS_EX_TRANSPARENT),
               //ClassStyle = ?,
               X = x,
               Y = y,
               Width = width,
               Height = height,
               Parent = parentHWnd,
               //Param = ?,
          };

          // NOTE: CreateHandle can throw InvalidOperationException, OutOfMemoryException or Win32Exception
          try
          {
               result.CreateHandle(windowParams);
          }
          catch (PInvoke.Win32Exception ex)
          {
               return MorphicResult.ErrorResult(Morphic.Controls.TrayButton.Windows11.CreateNewError.Win32Error((uint)ex.ErrorCode));
          }
          catch (Exception ex)
          {
               return MorphicResult.ErrorResult(Morphic.Controls.TrayButton.Windows11.CreateNewError.OtherException(ex));
          }

          // since we are making the image visible by default, set its visible state
          result._visible = true;

          return MorphicResult.OkResult(result);
     }

     // NOTE: the built-in CreateHandle function couldn't accept our custom class (an ATOM rather than a string) as input, so we have overridden CreateHandle and are calling CreateWindowEx manually
     // NOTE: in some circumstances, it is possible that we are unable to create our window; our caller may want to consider retrying mechanism
     public override void CreateHandle(System.Windows.Forms.CreateParams cp)
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
               var handle = PInvokeExtensions.CreateWindowEx(
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

     //

     protected virtual void Dispose(bool disposing)
     {
          if (!disposedValue)
          {
               if (disposing)
               {
                    // TODO: dispose managed state (managed objects)
               }

               // TODO: free unmanaged resources (unmanaged objects) and override finalizer
               this.DestroyHandle();

               // TODO: set large fields to null
               disposedValue = true;
          }
     }

     ~ArgbImageNativeWindow()
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
                    // see: https://learn.microsoft.com/en-us/windows/win32/api/uxtheme/nf-uxtheme-bufferedpaintinit
                    if (Windows.Win32.PInvoke.BufferedPaintInit() != Windows.Win32.Foundation.HRESULT.S_OK)
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
     protected override void WndProc(ref System.Windows.Forms.Message m)
     {
          IntPtr? result = null;

          switch ((PInvoke.User32.WindowMessage)m.Msg)
          {
               case PInvoke.User32.WindowMessage.WM_NCDESTROY:
                    {
                         // NOTE: we are calling this in response to WM_NCDESTROY (instead of WM_DESTROY)
                         //       see: https://learn.microsoft.com/en-us/windows/win32/api/uxtheme/nf-uxtheme-bufferedpaintinit
                         _ = Windows.Win32.PInvoke.BufferedPaintUnInit();

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

     //

     public System.Drawing.Bitmap? GetBitmap()
     {
          return _sourceBitmap;
     }

     public void SetBitmap(System.Drawing.Bitmap? bitmap)
     {
          _sourceBitmap = bitmap;
          this.RecreateSizedBitmap(bitmap);

          this.RequestRedraw();
     }

     public void SetPositionAndSize(Windows.Win32.Foundation.RECT rect)
     {
          // set the new window position (including size); we must resize the window before recreating the sized bitmap (which will be sized to the updated size)
          Windows.Win32.PInvoke.SetWindowPos((Windows.Win32.Foundation.HWND)this.Handle, (Windows.Win32.Foundation.HWND)IntPtr.Zero, rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top, Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOZORDER | Windows.Win32.UI.WindowsAndMessaging.SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);

          this.RecreateSizedBitmap(_sourceBitmap);
          this.RequestRedraw();
     }

     public void SetVisbile(bool value)
     {
          if (_visible != value)
          {
               _visible = value;

               var windowStyle = PInvokeExtensions.GetWindowLongPtr_IntPtr((Windows.Win32.Foundation.HWND)this.Handle, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_STYLE);
               nint newWindowStyle;
               if (_visible == true)
               {
                    newWindowStyle = (nint)windowStyle | (nint)Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_VISIBLE;
               }
               else
               {
                    newWindowStyle = (nint)windowStyle & ~(nint)Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_VISIBLE;
               }
               PInvokeExtensions.SetWindowLongPtr_IntPtr((Windows.Win32.Foundation.HWND)this.Handle, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_STYLE, newWindowStyle);
          }
     }

     private void RecreateSizedBitmap(System.Drawing.Bitmap? bitmap)
     {
          if (bitmap != null)
          {
               _sizedBitmap = new System.Drawing.Bitmap(bitmap, this.GetCurrentSize());
          }
          else
          {
               _sizedBitmap = null;
          }
     }

     private System.Drawing.Size GetCurrentSize()
     {
          Windows.Win32.PInvoke.GetWindowRect((Windows.Win32.Foundation.HWND)this.Handle, out var rect);
          return new System.Drawing.Size(rect.right - rect.left, rect.bottom - rect.top);
     }

     //

     private void RequestRedraw()
     {
          // update our layered bitmap
          this.UpdateLayeredPainting();

          // invalidate the window
          PInvokeExtensions.RedrawWindow(this.Handle, IntPtr.Zero, IntPtr.Zero, /*Windows.Win32.Graphics.Gdi.REDRAW_WINDOW_FLAGS.RDW_ERASE | */Windows.Win32.Graphics.Gdi.REDRAW_WINDOW_FLAGS.RDW_INVALIDATE/* | Windows.Win32.Graphics.Gdi.REDRAW_WINDOW_FLAGS.RDW_ALLCHILDREN*/);
     }

     private MorphicResult<MorphicUnit, MorphicUnit> UpdateLayeredPainting()
     {
          var ownerHWnd = Windows.Win32.PInvoke.GetWindow((Windows.Win32.Foundation.HWND)this.Handle, Windows.Win32.UI.WindowsAndMessaging.GET_WINDOW_CMD.GW_OWNER);
          //
          var size = this.GetCurrentSize();
          //
          var sizedBitmap = _sizedBitmap;

          if (sizedBitmap is not null)
          {
               // create a GDI bitmap from the Bitmap (using (0, 0, 0, 0) as the color of the ARGB background i.e. transparent)
               Windows.Win32.Graphics.Gdi.HGDIOBJ sizedBitmapPointer;
               try
               {
                    sizedBitmapPointer = (Windows.Win32.Graphics.Gdi.HGDIOBJ)sizedBitmap.GetHbitmap(System.Drawing.Color.FromArgb(0));
               }
               catch
               {
                    Debug.Assert(false, "Could not create GDI bitmap object from the sized bitmap.");
                    return MorphicResult.ErrorResult();
               }
               try
               {
                    var ownerDC = Windows.Win32.PInvoke.GetDC(ownerHWnd);
                    if (ownerDC.Value == IntPtr.Zero)
                    {
                         Debug.Assert(false, "Could not get owner DC so that we can draw the icon bitmap.");
                         return MorphicResult.ErrorResult();
                    }
                    try
                    {
                         var sourceDC = Windows.Win32.PInvoke.CreateCompatibleDC(ownerDC);
                         if (sourceDC.Value == IntPtr.Zero)
                         {
                              Debug.Assert(false, "Could not get create compatible DC for screen DC so that we can draw the icon bitmap.");
                              return MorphicResult.ErrorResult();
                         }
                         try
                         {
                              // select our bitmap in the source DC
                              var oldSourceDCObject = Windows.Win32.PInvoke.SelectObject(sourceDC, sizedBitmapPointer);
                              if (oldSourceDCObject.Value == new IntPtr(-1) /*HGDI_ERROR*/)
                              {
                                   Debug.Assert(false, "Could not select the icon bitmap GDI object to update the layered window with the alpha-blended bitmap.");
                                   return MorphicResult.ErrorResult();
                              }
                              try
                              {
                                   // configure our blend function to blend the bitmap into its background
                                   var blendfunction = new Windows.Win32.Graphics.Gdi.BLENDFUNCTION()
                                   {
                                        BlendOp = (byte)Windows.Win32.PInvoke.AC_SRC_OVER, /* the only available blend op, this will place the source bitmap over the destination bitmap based on the alpha values of the source pixels */
                                        BlendFlags = 0, /* must be zero */
                                        SourceConstantAlpha = 255, /* use per-pixel alpha values */
                                        AlphaFormat = (byte)Windows.Win32.PInvoke.AC_SRC_ALPHA, /* the bitmap has an alpha channel; it MUST be a 32bpp bitmap */
                                   };
                                   var sourcePoint = new System.Drawing.Point(0, 0);
                                   var flags = Windows.Win32.UI.WindowsAndMessaging.UPDATE_LAYERED_WINDOW_FLAGS.ULW_ALPHA; // this flag indicates the blendfunction should be used as the blend function
                                   //var updateLayeredWindowSuccess = Windows.Win32.PInvoke.UpdateLayeredWindow((Windows.Win32.Foundation.HWND)this.Handle, ownerDC, position/* captured position of our window */, size, sourceDC, sourcePoint, (Windows.Win32.Foundation.COLORREF)0/* unused COLORREF*/, blendfunction, flags);
                                   var updateLayeredWindowSuccess = Windows.Win32.PInvoke.UpdateLayeredWindow((Windows.Win32.Foundation.HWND)this.Handle, ownerDC, null/* current position is not changing */, size, sourceDC, sourcePoint, (Windows.Win32.Foundation.COLORREF)0/* unused COLORREF*/, blendfunction, flags);
                                   if (updateLayeredWindowSuccess == false)
                                   {
                                        var win32Error = Marshal.GetLastWin32Error();
                                        Debug.Assert(false, "Could not update the layered window with the alpha-blended bitmap; win32 error code: " + win32Error.ToString());
                                        return MorphicResult.ErrorResult();
                                   }
                              }
                              finally
                              {
                                   // restore the old source object for the source DC
                                   var selectObjectResult = Windows.Win32.PInvoke.SelectObject(sourceDC, oldSourceDCObject);
                                   if (selectObjectResult == new IntPtr(-1) /*HGDI_ERROR*/)
                                   {
                                        Debug.Assert(false, "Could not restore the screen's compatible DC to its previous object after attempting to update the layered window.");
                                   }
                              }
                         }
                         finally
                         {
                              var deleteDCSuccess = Windows.Win32.PInvoke.DeleteDC(sourceDC);
                              Debug.Assert(deleteDCSuccess == true, "Could not delete the compatible DC for the owner DC.");
                         }

                    }
                    finally
                    {
                         var releaseDcResult = Windows.Win32.PInvoke.ReleaseDC(ownerHWnd, ownerDC);
                         Debug.Assert(releaseDcResult == 1, "Could not release owner DC.");
                    }
               }
               finally
               {
                    var deleteObjectSuccess = Windows.Win32.PInvoke.DeleteObject(sizedBitmapPointer);
                    Debug.Assert(deleteObjectSuccess == true, "Could not delete the GDI bitmap object which was created from the icon bitmap");
               }
          }
          else
          {
               // NOTE: we do not support erasing the bitmap once it's created, so there is nothing to do here; the caller may hide the image by setting its visible state to true
          }

          // if we reach here, the operation was successful          
          return MorphicResult.OkResult();
     }
}
