// Copyright 2021-2025 Raising the Floor - US, Inc.
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

public enum WindowStyles: uint
{
    Overlapped = Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_OVERLAPPED,
    Popup = Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_POPUP,
    Child = Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_CHILD,
    Minimize = Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_MINIMIZE,
    Visible = Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_VISIBLE,
    Disabled = Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_DISABLED,
    Clipsiblings = Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_CLIPSIBLINGS,
    Clipchildren = Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_CLIPCHILDREN,
    Maximize = Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_MAXIMIZE,
    Caption = Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_CAPTION,
    Border = Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_BORDER,
    Dlgframe = Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_DLGFRAME,
    Vscroll = Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_VSCROLL,
    Hscroll = Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_HSCROLL,
    Sysmenu = Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_SYSMENU,
    Thickframe = Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_THICKFRAME,
    Group = Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_GROUP,
    Tabstop = Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_TABSTOP,
    Minimizebox = Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_MINIMIZEBOX,
    Maximizebox = Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_MAXIMIZEBOX,
    Tiled = Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_TILED,
    Iconic = Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_ICONIC,
    Sizebox = Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_SIZEBOX,
    Tiledwindow = Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_TILEDWINDOW,
    OverlappedWindow = Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_OVERLAPPEDWINDOW,
    Popupwindow = Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_POPUPWINDOW,
    Childwindow = Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_CHILDWINDOW,
    ActiveCaption = Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_ACTIVECAPTION,
}

public enum WindowExStyles: uint
{
    Dlgmodalframe = Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_DLGMODALFRAME,
    Noparentnotify = Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_NOPARENTNOTIFY,
    Topmost = Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_TOPMOST,
    Acceptfiles = Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_ACCEPTFILES,
    Transparent = Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_TRANSPARENT,
    Mdichild = Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_MDICHILD,
    Toolwindow = Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_TOOLWINDOW,
    Windowedge = Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_WINDOWEDGE,
    Clientedge = Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_CLIENTEDGE,
    Contexthelp = Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_CONTEXTHELP,
    Right = Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_RIGHT,
    Left = Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_LEFT,
    Rtlreading = Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_RTLREADING,
    Ltrreading = Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_LTRREADING,
    Leftscrollbar = Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_LEFTSCROLLBAR,
    Rightscrollbar = Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_RIGHTSCROLLBAR,
    Controlparent = Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_CONTROLPARENT,
    Staticedge = Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_STATICEDGE,
    Appwindow = Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_APPWINDOW,
    Overlappedwindow = Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_OVERLAPPEDWINDOW,
    Palettewindow = Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_PALETTEWINDOW,
    Layered = Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_LAYERED,
    Noinheritlayout = Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_NOINHERITLAYOUT,
    Noredirectionbitmap = Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_NOREDIRECTIONBITMAP,
    Layoutrtl = Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_LAYOUTRTL,
    Composited = Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_COMPOSITED,
    Noactivate = Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_NOACTIVATE,
}

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
	
    //

    public MorphicResult<nint, IWin32ApiError> GetStyle()
    {
        var getWindowLongPtrResult = PInvokeExtensions.GetWindowLongPtr_IntPtr((Windows.Win32.Foundation.HWND)this.hWnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        if (getWindowLongPtrResult == IntPtr.Zero)
        {
            var win32Error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            return MorphicResult.ErrorResult<IWin32ApiError>(new IWin32ApiError.Win32Error((uint)win32Error));
        }

        return MorphicResult.OkResult(getWindowLongPtrResult);
    }

    public MorphicResult<nint, IWin32ApiError> GetExStyle()
    {
        var getWindowLongPtrResult = PInvokeExtensions.GetWindowLongPtr_IntPtr((Windows.Win32.Foundation.HWND)this.hWnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        if (getWindowLongPtrResult == IntPtr.Zero)
        {
            var win32Error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            return MorphicResult.ErrorResult<IWin32ApiError>(new IWin32ApiError.Win32Error((uint)win32Error));
        }

        return MorphicResult.OkResult(getWindowLongPtrResult);
    }

    public struct GetLayeredWindowAttributesResult
    {
        public uint pcrKey;
        public byte pbAlpha;
        public uint pdwFlags;
    }
    public MorphicResult<GetLayeredWindowAttributesResult, IWin32ApiError> GetLayeredWindowAttributes()
    {
        Windows.Win32.Foundation.COLORREF pcrKey;// = new(0);
        byte pbAlpha;// = 0;
        Windows.Win32.UI.WindowsAndMessaging.LAYERED_WINDOW_ATTRIBUTES_FLAGS pdwFlags;// = (Windows.Win32.UI.WindowsAndMessaging.LAYERED_WINDOW_ATTRIBUTES_FLAGS)0;
        Windows.Win32.Foundation.BOOL getLayeredWindowsAttributesResult;
        unsafe
        {
            getLayeredWindowsAttributesResult = Windows.Win32.PInvoke.GetLayeredWindowAttributes((Windows.Win32.Foundation.HWND)this.hWnd, &pcrKey, &pbAlpha, &pdwFlags);
        }
        if (getLayeredWindowsAttributesResult.Value == 0)
        {
            var win32Error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            return MorphicResult.ErrorResult<IWin32ApiError>(new IWin32ApiError.Win32Error((uint)win32Error));
        }

        var result = new GetLayeredWindowAttributesResult()
        {
            pcrKey = pcrKey.Value,
            pbAlpha = pbAlpha,
            pdwFlags = (uint)pdwFlags,
        };
        return MorphicResult.OkResult(result);
    }
}
