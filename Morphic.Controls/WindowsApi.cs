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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static Morphic.Controls.TrayButton.Windows10.LegacyWindowsApi;
using static PInvoke.User32;

namespace Morphic.Controls;

internal class WindowsApi
{
     #region common/well-known declarations

     /* well-known HRESULT values */

     internal static readonly int S_OK = 0x00000000;

     #endregion common/well-known declarations


     #region commctrl

     internal const string TOOLTIPS_CLASS = "tooltips_class32";

     internal const ushort TTM_ADDTOOL = WM_USER + 50;
     internal const byte TTS_ALWAYSTIP = 0x01;
     internal const ushort TTM_DELTOOL = WM_USER + 51;

     // https://learn.microsoft.com/en-us/windows/win32/api/commctrl/ns-commctrl-tttoolinfow
     internal struct TOOLINFO
     {
          public uint cbSize;
          public uint uFlags;
          public IntPtr hwnd;
          public UIntPtr uId;
          public PInvoke.RECT rect;
          public IntPtr hinst;
          [MarshalAs(UnmanagedType.LPTStr)]
          public string? lpszText;
          public IntPtr lParam;
          //public IntPtr reserved; // NOTE: this exists in the official declaration as a void pointer but adding it causes SendMessage to fail; pinvoke.net leaves it out and so do we
     }

     #endregion commctrl


     #region uxtheme

     // https://learn.microsoft.com/en-us/windows/win32/api/uxtheme/ne-uxtheme-bp_bufferformat
     internal enum BP_BUFFERFORMAT : int
     {
          BPBF_COMPATIBLEBITMAP,
          BPBF_DIB,
          BPBF_TOPDOWNDIB,
          BPBF_TOPDOWNMONODIB
     }

     // https://docs.microsoft.com/en-us/windows/win32/api/uxtheme/nf-uxtheme-beginbufferedpaint
     [DllImport("uxtheme.dll")]
     internal static extern IntPtr BeginBufferedPaint(IntPtr hdcTarget, [In] ref PInvoke.RECT prcTarget, BP_BUFFERFORMAT dwFormat, IntPtr /* [In] BP_PAINTPARAMS */ pPaintParams, out IntPtr phdc);

     // https://docs.microsoft.com/en-us/windows/win32/api/uxtheme/nf-uxtheme-bufferedpaintclear
     [DllImport("uxtheme.dll")]
     internal static extern uint BufferedPaintClear(IntPtr hBufferedPaint, ref PInvoke.RECT prc);
     // // alternative implementation (i.e. to pass in null for RECT)
     //[DllImport("uxtheme.dll")]
     //internal static extern uint BufferedPaintClear(IntPtr hBufferedPaint, IntPtr prc);

     // https://learn.microsoft.com/en-us/windows/win32/api/uxtheme/nf-uxtheme-bufferedpaintinit
     [DllImport("uxtheme.dll")]
     internal static extern int BufferedPaintInit();

     // https://learn.microsoft.com/en-us/windows/win32/api/uxtheme/nf-uxtheme-bufferedpaintuninit
     [DllImport("uxtheme.dll")]
     internal static extern int BufferedPaintUnInit();


     // https://docs.microsoft.com/en-us/windows/win32/api/uxtheme/nf-uxtheme-endbufferedpaint
     [DllImport("uxtheme.dll")]
     internal static extern uint EndBufferedPaint(IntPtr hBufferedPaint, bool fUpdateTarget);

     #endregion uxtheme


     #region windgi

     // currently-defined blend functions
     internal const byte AC_SRC_OVER = 0x00;

     // alpha format flags
     internal const byte AC_SRC_ALPHA = 0x01;

     // https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-blendfunction
     [StructLayout(LayoutKind.Sequential)]
     internal struct BLENDFUNCTION
     {
          public byte BlendOp;
          public byte BlendFlags;
          public byte SourceConstantAlpha;
          public byte AlphaFormat;
     }

     // https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-createsolidbrush
     [DllImport("gdi32.dll")]
     internal static extern IntPtr CreateSolidBrush(uint crColor);

     #endregion wingdi


     #region winuser

     internal const int CW_USEDEFAULT = unchecked((int)0x80000000);

     internal const nint HWND_TOPMOST = -1;

     internal const uint MK_LBUTTON = 0x0001;
     internal const uint MK_RBUTTON = 0x0002;

     internal const ushort WM_USER = 0x0400;


     public enum WinEventHookType : uint
     {
          EVENT_AIA_START = 0xA000,
          EVENT_AIA_END = 0xAFFF,
          EVENT_MIN = 0x00000001,
          EVENT_MAX = 0x7FFFFFFF,
          EVENT_OBJECT_ACCELERATORCHANGE = 0x8012,
          EVENT_OBJECT_CLOAKED = 0x8017,
          EVENT_OBJECT_CONTENTSCROLLED = 0x8015,
          EVENT_OBJECT_CREATE = 0x8000,
          EVENT_OBJECT_DEFACTIONCHANGE = 0x8011,
          EVENT_OBJECT_DESCRIPTIONCHANGE = 0x800D,
          EVENT_OBJECT_DESTROY = 0x8001,
          EVENT_OBJECT_DRAGSTART = 0x8021,
          EVENT_OBJECT_DRAGCANCEL = 0x8022,
          EVENT_OBJECT_DRAGCOMPLETE = 0x8023,
          EVENT_OBJECT_DRAGENTER = 0x8024,
          EVENT_OBJECT_DRAGLEAVE = 0x8025,
          EVENT_OBJECT_DRAGDROPPED = 0x8026,
          EVENT_OBJECT_END = 0x80FF,
          EVENT_OBJECT_FOCUS = 0x8005,
          EVENT_OBJECT_HELPCHANGE = 0x8010,
          EVENT_OBJECT_HIDE = 0x8003,
          EVENT_OBJECT_HOSTEDOBJECTSINVALIDATED = 0x8020,
          EVENT_OBJECT_IME_HIDE = 0x8028,
          EVENT_OBJECT_IME_SHOW = 0x8027,
          EVENT_OBJECT_IME_CHANGE = 0x8029,
          EVENT_OBJECT_INVOKED = 0x8013,
          EVENT_OBJECT_LIVEREGIONCHANGED = 0x8019,
          EVENT_OBJECT_LOCATIONCHANGE = 0x800B,
          EVENT_OBJECT_NAMECHANGE = 0x800C,
          EVENT_OBJECT_PARENTCHANGE = 0x800F,
          EVENT_OBJECT_REORDER = 0x8004,
          EVENT_OBJECT_SELECTION = 0x8006,
          EVENT_OBJECT_SELECTIONADD = 0x8007,
          EVENT_OBJECT_SELECTIONREMOVE = 0x8008,
          EVENT_OBJECT_SELECTIONWITHIN = 0x8009,
          EVENT_OBJECT_SHOW = 0x8002,
          EVENT_OBJECT_STATECHANGE = 0x800A,
          EVENT_OBJECT_TEXTEDIT_CONVERSIONTARGETCHANGED = 0x8030,
          EVENT_OBJECT_TEXTSELECTIONCHANGED = 0x8014,
          EVENT_OBJECT_UNCLOAKED = 0x8018,
          EVENT_OBJECT_VALUECHANGE = 0x800E,
          EVENT_OEM_DEFINED_START = 0x0101,
          EVENT_OEM_DEFINED_END = 0x01FF,
          EVENT_SYSTEM_ALERT = 0x0002,
          EVENT_SYSTEM_ARRANGMENTPREVIEW = 0x8016,
          EVENT_SYSTEM_CAPTUREEND = 0x0009,
          EVENT_SYSTEM_CAPTURESTART = 0x0008,
          EVENT_SYSTEM_CONTEXTHELPEND = 0x000D,
          EVENT_SYSTEM_CONTEXTHELPSTART = 0x000C,
          EVENT_SYSTEM_DESKTOPSWITCH = 0x0020,
          EVENT_SYSTEM_DIALOGEND = 0x0011,
          EVENT_SYSTEM_DIALOGSTART = 0x0010,
          EVENT_SYSTEM_DRAGDROPEND = 0x000F,
          EVENT_SYSTEM_DRAGDROPSTART = 0x000E,
          EVENT_SYSTEM_END = 0x00FF,
          EVENT_SYSTEM_FOREGROUND = 0x0003,
          EVENT_SYSTEM_MENUPOPUPEND = 0x0007,
          EVENT_SYSTEM_MENUPOPUPSTART = 0x0006,
          EVENT_SYSTEM_MENUEND = 0x0005,
          EVENT_SYSTEM_MENUSTART = 0x0004,
          EVENT_SYSTEM_MINIMIZEEND = 0x0017,
          EVENT_SYSTEM_MINIMIZESTART = 0x0016,
          EVENT_SYSTEM_MOVESIZEEND = 0x000B,
          EVENT_SYSTEM_MOVESIZESTART = 0x000A,
          EVENT_SYSTEM_SCROLLINGEND = 0x0013,
          EVENT_SYSTEM_SCROLLINGSTART = 0x0012,
          EVENT_SYSTEM_SOUND = 0x0001,
          EVENT_SYSTEM_SWITCHEND = 0x0015,
          EVENT_SYSTEM_SWITCHSTART = 0x0014,
          EVENT_UIA_EVENTID_START = 0x4E00,
          EVENT_UIA_EVENTID_END = 0x4EFF,
          EVENT_UIA_PROPID_START = 0x7500,
          EVENT_UIA_PROPID_END = 0x75FF
     }

     // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook
     [Flags]
     internal enum WinEventHookFlags : uint
     {
          WINEVENT_OUTOFCONTEXT = 0x0000, // Events are ASYNC
          WINEVENT_SKIPOWNTHREAD = 0x0001, // Don't call back for events on installer's thread
          WINEVENT_SKIPOWNPROCESS = 0x0002, // Don't call back for events on installer's process
          WINEVENT_INCONTEXT = 0x0004, // Events are SYNC, this causes your dll to be injected into every process
     }

     // https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-paintstruct
     [StructLayout(LayoutKind.Sequential)]
     internal struct PAINTSTRUCT
     {
          public IntPtr hdc;
          public bool fErase;
          public PInvoke.RECT rcPaint;
          public bool fRestore;
          public bool fIncUpdate;
          [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
          public byte[] rgbReserved;
     }

     // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-redrawwindow
     [Flags]
     internal enum RedrawWindowFlags : uint
     {
          Invalidate = 0x0001,
          Internalpaint = 0x0002,
          Erase = 0x0004,
          //
          Validate = 0x0008,
          Nointernalpaint = 0x0010,
          Noerase = 0x0020,
          //
          Nochildren = 0x0040,
          Allchildren = 0x0080,
          //
          Updatenow = 0x0100,
          Erasenow = 0x0200,
          //
          Frame = 0x0400,
          Noframe = 0x0800,
     }

     // https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-trackmouseevent
     [Flags]
     internal enum TRACKMOUSEEVENTFlags : uint
     {
          TME_CANCEL     = 0x80000000,
          TME_HOVER      = 0x00000001,
          TME_LEAVE      = 0x00000002,
          TME_NONCLIENT  = 0x00000010,
          TME_QUERY      = 0x40000000
     }

     internal static readonly uint HOVER_DEFAULT = 0xFFFFFFFF;

     // https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-trackmouseevent
     [StructLayout(LayoutKind.Sequential)]
     internal struct TRACKMOUSEEVENT
     {
          public uint cbSize;
          public TRACKMOUSEEVENTFlags dwFlags;
          public IntPtr hWnd;
          public uint dwHoverTime;

          public static TRACKMOUSEEVENT CreateNew(TRACKMOUSEEVENTFlags dwFlags, IntPtr hWnd, uint dwHoverTime)
          {
               var result = new TRACKMOUSEEVENT()
               {
                    cbSize = (uint)Marshal.SizeOf(typeof(TRACKMOUSEEVENT)),
                    dwFlags = dwFlags,
                    hWnd = hWnd,
                    dwHoverTime = dwHoverTime
               };
               return result;
          }
     }

     // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-updatelayeredwindow
     internal enum UpdateLayeredWindowFlags : uint
     {
          ULW_COLORKEY = 0x00000001,
          ULW_ALPHA = 0x00000002,
          ULW_OPAQUE = 0x00000004,
          ULW_EX_NORESIZE = 0x00000008,
     }

     // https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-wndclassexw
     [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
     public struct WNDCLASSEX
     {
          public uint cbSize;
          public uint style;
          public IntPtr lpfnWndProc;
          public int cbClsExtra;
          public int cbWndExtra;
          public IntPtr hInstance;
          public IntPtr hIcon;
          public IntPtr hCursor;
          public IntPtr hbrBackground;
          public string lpszMenuName;
          public string lpszClassName;
          public IntPtr hIconSm;

          public static WNDCLASSEX CreateNew()
          {
               var result = new WNDCLASSEX()
               {
                    cbSize = (uint)Marshal.SizeOf(typeof(WNDCLASSEX))
               };
               return result;
          }
     }

     // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-beginpaint
     [DllImport("user32.dll")]
     internal static extern IntPtr BeginPaint(IntPtr hwnd, out PAINTSTRUCT lpPaint);

     // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-createwindowexw
     [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
     internal static extern IntPtr CreateWindowEx(
          PInvoke.User32.WindowStylesEx dwExStyle,
          IntPtr lpClassName,
          string? lpWindowName,
          PInvoke.User32.WindowStyles dwStyle,
          int x,
          int y,
          int nWidth,
          int nHeight,
          IntPtr hWndParent,
          IntPtr hMenu,
          IntPtr hInstance,
          IntPtr lpParam
     );

     // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-endpaint
     [DllImport("user32.dll")]
     internal static extern bool EndPaint(IntPtr hWnd, [In] ref PAINTSTRUCT lpPaint);

     [DllImport("user32.dll", SetLastError = true)]
     internal static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

     internal static IntPtr GetWindowLongPtr_IntPtr(IntPtr hWnd, PInvoke.User32.WindowLongIndexFlags nIndex)
     {
          if (IntPtr.Size == 4)
          {
               return (nint)WindowsApi.GetWindowLong(hWnd, nIndex);
          } 
          else
          {
               return WindowsApi.GetWindowLongPtr(hWnd, nIndex);
          }
     }

     // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowlongw
     [DllImport("user32.dll", SetLastError = true)]
     private static extern int GetWindowLong(IntPtr hWnd, PInvoke.User32.WindowLongIndexFlags nIndex);

     // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowlongptrw
     [DllImport("user32.dll", SetLastError = true)]
     private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, PInvoke.User32.WindowLongIndexFlags nIndex);

     // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-fillrect
     [DllImport("user32.dll")]
     internal static extern int FillRect(IntPtr hDC, [In] ref PInvoke.RECT lprc, IntPtr hbr);

     // see: https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-mapwindowpoints
     // NOTE: this signature is the POINT option (in which cPoints must always be set to 1).
     [DllImport("user32.dll", SetLastError = true)]
     internal static extern int MapWindowPoints(IntPtr hWndFrom, IntPtr hWndTo, ref PInvoke.POINT lpPoints, uint cPoints);

     // see: https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-mapwindowpoints
     // NOTE: this signature is the RECT option (in which cPoints must always be set to 2).
     [DllImport("user32.dll", SetLastError = true)]
     internal static extern int MapWindowPoints(IntPtr hWndFrom, IntPtr hWndTo, ref PInvoke.RECT lpPoints, uint cPoints);

     // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-redrawwindow
     [DllImport("user32.dll")]
     internal static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, RedrawWindowFlags flags);

     // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerclassexw
     [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
     internal static extern ushort RegisterClassEx([In] ref WNDCLASSEX lpWndClass);

     internal enum SetLayeredWindowAttributesFlags : uint
     {
          LWA_COLORKEY = 0x00000001,
          LWA_ALPHA = 0x00000002
     }
     //
     // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setlayeredwindowattributes
     [DllImport("user32.dll", SetLastError = true)]
     internal static extern bool SetLayeredWindowAttributes(IntPtr hwnd, /* COLORREF */uint crKey, byte bAlpha, uint dwFlags);

     // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook
     [DllImport("user32.dll")]
     internal static extern IntPtr SetWinEventHook(WinEventHookType eventMin, WinEventHookType eventMax, IntPtr hmodWinEventProc, WinEventProc lpfnWinEventProc, uint idProcess, uint idThread, WinEventHookFlags dwFlags);
     internal static IntPtr SetWindowLongPtr_IntPtr(IntPtr hWnd, PInvoke.User32.WindowLongIndexFlags nIndex, IntPtr dwNewLong)
     {
          if (IntPtr.Size == 4)
          {
               return (nint)WindowsApi.SetWindowLong(hWnd, nIndex, (int)dwNewLong);
          }
          else
          {
               return WindowsApi.SetWindowLongPtr(hWnd, nIndex, dwNewLong);
          }
     }

     [DllImport("user32.dll", SetLastError = true)]
     private static extern int SetWindowLong(IntPtr hWnd, PInvoke.User32.WindowLongIndexFlags nIndex, int dwNewLong);

     [DllImport("user32.dll", SetLastError = true)]
     private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, PInvoke.User32.WindowLongIndexFlags nIndex, IntPtr dwNewLong);

     // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-trackmouseevent
     [DllImport("user32.dll", SetLastError = true)]
     internal static extern bool TrackMouseEvent(ref TRACKMOUSEEVENT lpEventTrack);

     // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-unhookwinevent
     [DllImport("user32.dll")]
     internal static extern bool UnhookWinEvent(IntPtr hWinEventHook);

     // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-updatelayeredwindow
     //[DllImport("user32.dll", SetLastError = true)]
     //internal static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, [In] ref PInvoke.POINT pptDst, [In] ref System.Drawing.Size psize, IntPtr hdcSrc, [In] ref System.Drawing.Point pptSrc, /*COLORREF*/uint crKey, [In] ref BLENDFUNCTION pblend, UpdateLayeredWindowFlags dwFlags);
     [DllImport("user32.dll", SetLastError = true)]
     internal static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, IntPtr pptDst, [In] ref System.Drawing.Size psize, IntPtr hdcSrc, [In] ref System.Drawing.Point pptSrc, /*COLORREF*/uint crKey, [In] ref BLENDFUNCTION pblend, UpdateLayeredWindowFlags dwFlags);

     // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nc-winuser-wineventproc
     internal delegate void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint idEventThread, uint dwmsEventTime);

     // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nc-winuser-wndproc
     internal delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

     #endregion winuser
}
