// Copyright 2020-2023 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-controls-lib-cs/blob/master/LICENSE.txt
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

namespace Morphic.Controls.TrayButton.Windows10;

internal class LegacyWindowsApi
{
     #region Win32 error codes
     public const uint ERROR_SUCCESS = 0;
     #endregion

     #region Window Positioning

     [StructLayout(LayoutKind.Sequential)]
     public struct RECT
     {
          public int Left;
          public int Top;
          public int Right;
          public int Bottom;

          public PInvoke.RECT ToPInvokeRect()
          {
               return new PInvoke.RECT() { left = this.Left, top = this.Top, right = this.Right, bottom = this.Bottom };
          }

          /// <summary>
          /// Creates a win32 RECT from a .NET Rect.
          /// </summary>
          /// <param name="rect">The rectangle.</param>
          public RECT(System.Windows.Rect rect)
          {
               this.Left = (int)rect.Left;
               this.Top = (int)rect.Top;
               this.Right = (int)rect.Right;
               this.Bottom = (int)rect.Bottom;
          }

          public static RECT Empty
          {
               get
               {
                    return new RECT(new System.Windows.Rect(0, 0, 0, 0));
               }
          }

          public bool HasNonZeroWidthOrHeight()
          {
               return ((this.Left == this.Right) || (this.Top == this.Bottom));
          }

          public bool IsInside(RECT rect)
          {
               return ((this.Left >= rect.Left) && (this.Right <= rect.Right) && (this.Top >= rect.Top) && (this.Bottom <= rect.Bottom));
          }

          public bool Intersects(RECT rect)
          {
               bool overlapsHorizontally = false;
               bool overlapsVertically = false;

               // horizontal check
               if ((this.Right > rect.Left) && (this.Left < rect.Right))
               {
                    // partially or fully overlaps horizontally
                    overlapsHorizontally = true;
               }

               // vertical check
               if ((this.Bottom > rect.Top) && (this.Top < rect.Bottom))
               {
                    // partially or fully overlaps vertically
                    overlapsVertically = true;
               }

               if ((overlapsHorizontally == true) && (overlapsVertically == true))
               {
                    return true;
               }

               // if we could not find overlap, then return false
               return false;
          }

          public static bool operator ==(RECT lhs, RECT rhs)
          {
               if ((lhs.Left == rhs.Left) &&
                   (lhs.Top == rhs.Top) &&
                   (lhs.Right == rhs.Right) &&
                   (lhs.Bottom == rhs.Bottom))
               {
                    return true;
               }
               else
               {
                    return false;
               }
          }

          public static bool operator !=(RECT lhs, RECT rhs)
          {
               if ((lhs.Left != rhs.Left) ||
                   (lhs.Top != rhs.Top) ||
                   (lhs.Right != rhs.Right) ||
                   (lhs.Bottom != rhs.Bottom))
               {
                    return true;
               }
               else
               {
                    return false;
               }
          }
     }

     [StructLayout(LayoutKind.Sequential)]
     public struct POINT
     {
          public int X;
          public int Y;

          public POINT(int x, int y)
          {
               this.X = x;
               this.Y = y;
          }

          public POINT(System.Windows.Point pt) : this((int)pt.X, (int)pt.Y)
          {
          }

          public System.Windows.Point ToPoint()
          {
               return new System.Windows.Point(this.X, this.Y);
          }
     }

     [DllImport("user32.dll")]
     public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, IntPtr lParam);

     [DllImport("user32.dll")]
     internal static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

     #endregion

     #region Window Creation and Management

     [DllImport("gdi32.dll")]
     internal static extern IntPtr CreateCompatibleDC(IntPtr hdc);

     [DllImport("gdi32.dll")]
     internal static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO pbmi, uint usage, out IntPtr ppvBits, IntPtr hSection, uint offset);

     //[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
     //internal static extern IntPtr CreateWindowEx(
     //    WindowStylesEx dwExStyle,
     //    IntPtr lpClassName,
     //    string? lpWindowName,
     //    WindowStyles dwStyle,
     //    int x,
     //    int y,
     //    int nWidth,
     //    int nHeight,
     //    IntPtr hWndParent,
     //    IntPtr hMenu,
     //    IntPtr hInstance,
     //    IntPtr lpParam);

     [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
     internal static extern IntPtr CreateWindowEx(
         WindowStylesEx dwExStyle,
         string lpClassName,
         string? lpWindowName,
         WindowStyles dwStyle,
         int x,
         int y,
         int nWidth,
         int nHeight,
         IntPtr hWndParent,
         IntPtr hMenu,
         IntPtr hInstance,
         IntPtr lpParam);

     [DllImport("user32.dll", CharSet = CharSet.Unicode)]
     internal static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

     [DllImport("gdi32.dll")]
     internal static extern bool DeleteDC(IntPtr hdc);

     [DllImport("gdi32.dll")]
     internal static extern bool DeleteObject(IntPtr ho);

     [DllImport("user32.dll")]
     internal static extern bool DestroyWindow(IntPtr hWnd);

     internal delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

     [DllImport("user32.dll")]
     [return: MarshalAs(UnmanagedType.Bool)]
     internal static extern bool EnumChildWindows(IntPtr hwndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

     [DllImport("user32.dll", CharSet = CharSet.Unicode)]
     internal static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

     [DllImport("user32.dll", CharSet = CharSet.Unicode)]
     internal static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string lpszClass, string? lpszWindow);

     [DllImport("user32.dll")]
     internal static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

     // https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-loadcursorw
     [DllImport("user32.dll", CharSet = CharSet.Unicode)]
     internal static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);
     //
     internal enum Cursors
     {
          IDC_APPSTARTING = 32650,
          IDC_ARROW = 32512,
          IDC_CROSS = 32515,
          IDC_HAND = 32649,
          IDC_HELP = 32651,
          IDC_IBEAM = 32513,
          IDC_ICON = 32641,
          IDC_NO = 32648,
          IDC_SIZE = 32640,
          IDC_SIZEALL = 32646,
          IDC_SIZENESW = 32643,
          IDC_SIZENS = 32645,
          IDC_SIZENWSE = 32642,
          IDC_SIZEWE = 32644,
          IDC_UPARROW = 32516,
          IDC_WAIT = 32514,
     }

     [DllImport("user32.dll")]
     internal static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, RedrawWindowFlags flags);

     // source for values: http://www.pinvoke.net/default.aspx/Enums/RedrawWindowFlags.html
     internal enum RedrawWindowFlags : uint
     {
          // invalidation flags
          RDW_ERASE = 0x4,
          RDW_FRAME = 0x400,
          RDW_INTERNALPAINT = 0x2,
          RDW_INVALIDATE = 0x1,
          // validation flags
          RDW_NOERASE = 0x20,
          RDW_NOFRAME = 0x800,
          RDW_NOINTERNALPAINT = 0x10,
          RDW_VALIDATE = 0x8,
          // repainting flags
          RDW_ERASENOW = 0x200,
          RDW_UPDATENOW = 0x100,
          // misc. control flags
          RDW_ALLCHILDREN = 0x80,
          RDW_NOCHILDREN = 0x40
     }

     [DllImport("gdi32.dll")]
     internal static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

     [DllImport("user32.dll")]
     internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, SetWindowPosFlags uFlags);

     internal enum SetWindowPosFlags : uint
     {
          SWP_ASYNCWINDOWPOS = 0x4000,
          SWP_DEFERERASE = 0x2000,
          SWP_DRAWFRAME = 0x0020,
          SWP_FRAMECHANGED = 0x0020,
          SWP_HIDEWINDOW = 0x0080,
          SWP_NOACTIVATE = 0x0010,
          SWP_NOCOPYBITS = 0x0100,
          SWP_NOMOVE = 0x0002,
          SWP_NOOWNERZORDER = 0x0200,
          SWP_NOREDRAW = 0x0008,
          SWP_NOREPOSITION = 0x0200,
          SWP_NOSENDCHANGING = 0x0400,
          SWP_NOSIZE = 0x0001,
          SWP_NOZORDER = 0x0004,
          SWP_SHOWWINDOW = 0x0040
     }

     [DllImport("user32.dll")]
     internal static extern bool TrackMouseEvent(ref TRACKMOUSEEVENT lpEventTrack);

     [StructLayout(LayoutKind.Sequential)]
     internal struct TRACKMOUSEEVENT
     {
          public uint cbSize;
          public TMEFlags dwFlags;
          public IntPtr hWnd;
          public uint dwHoverTime;

          public TRACKMOUSEEVENT(TMEFlags dwFlags, IntPtr hWnd, uint dwHoverTime)
          {
               this.cbSize = (uint)Marshal.SizeOf(typeof(TRACKMOUSEEVENT));
               this.dwFlags = dwFlags;
               this.hWnd = hWnd;
               this.dwHoverTime = dwHoverTime;
          }
     }

     // WinUser.h (Windows 10 1809 SDK)
     internal static readonly uint HOVER_DEFAULT = 0xFFFFFFFF;

     [Flags]
     internal enum TMEFlags : uint
     {
          TME_CANCEL = 0x80000000,
          TME_HOVER = 0x00000001,
          TME_LEAVE = 0x00000002,
          TME_NONCLIENT = 0x00000010,
          TME_QUERY = 0x40000000
     }

     [StructLayout(LayoutKind.Sequential)]
     internal struct BITMAPINFO
     {
          public BITMAPINFOHEADER bmiHeader;
          [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)] // NOTE: in other implementations, this was represented as a uint instead (with 256 elements instead of 1 element)
          public RGBQUAD[] bmiColors;
     }

     [StructLayout(LayoutKind.Sequential)]
     public struct BITMAPINFOHEADER
     {
          public uint biSize;
          public int biWidth;
          public int biHeight;
          public ushort biPlanes;
          public ushort biBitCount;
          public BitmapCompressionType biCompression;
          public uint biSizeImage;
          public int biXPelsPerMeter;
          public int biYPelsPerMeter;
          public uint biClrUsed;
          public uint biClrImportant;
     }

     [StructLayout(LayoutKind.Sequential)]
     internal struct RGBQUAD
     {
          byte rgbBlue;
          byte rgbGreen;
          byte rgbRed;
          byte rgbReserved;
     }

     // wingdi.h (Windows 10 1809 SDK)
     internal enum BitmapCompressionType : uint
     {
          BI_RGB = 0,
          BI_RLE8 = 1,
          BI_RLE4 = 2,
          BI_BITFIELDS = 3,
          BI_JPEG = 4,
          BI_PNG = 5
     }

     internal static readonly IntPtr HWND_TOP = new IntPtr(0);
     //internal static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
     //internal static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
     //internal static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

     internal const int MA_NOACTIVATEANDEAT = 4;

     //

     internal const uint MK_LBUTTON = 0x0001;
     internal const uint MK_RBUTTON = 0x0002;

     internal const uint S_OK = 0;

     // WinUser.h (Windows 10 1809 SDK)
     public enum WindowMessage : uint
     {
          // https://docs.microsoft.com/en-us/windows/win32/winmsg/wm-create
          WM_CREATE = 0x0001,

          // https://docs.microsoft.com/en-us/windows/win32/winmsg/wm-destroy
          WM_DESTROY = 0x0002,

          // https://docs.microsoft.com/en-us/windows/win32/gdi/wm-displaychange
          WM_DISPLAYCHANGE = 0x007E,

          // https://docs.microsoft.com/en-us/windows/win32/winmsg/wm-erasebkgnd
          WM_ERASEBKGND = 0x0014,

          // https://docs.microsoft.com/en-us/windows/win32/inputdev/wm-lbuttondown
          WM_LBUTTONDOWN = 0x0201,

          // https://docs.microsoft.com/en-us/windows/win32/inputdev/wm-lbuttonup
          WM_LBUTTONUP = 0x0202,

          // https://docs.microsoft.com/en-us/windows/win32/inputdev/wm-mouseactivate
          WM_MOUSEACTIVATE = 0x0021,

          // https://docs.microsoft.com/en-us/windows/win32/inputdev/wm-mouseleave
          WM_MOUSELEAVE = 0x02A3,

          // https://docs.microsoft.com/en-us/windows/win32/inputdev/wm-mousemove
          WM_MOUSEMOVE = 0x0200,

          // https://docs.microsoft.com/en-us/windows/win32/inputdev/wm-nchittest
          WM_NCHITTEST = 0x0084,

          // https://docs.microsoft.com/en-us/windows/win32/gdi/wm-ncpaint
          WM_NCPAINT = 0x0085,

          // https://docs.microsoft.com/en-us/windows/win32/gdi/wm-paint
          WM_PAINT = 0x000F,

          // https://docs.microsoft.com/en-us/windows/win32/winmsg/wm-windowposchanged
          WM_WINDOWPOSCHANGED = 0x0047,

          // https://docs.microsoft.com/en-us/windows/win32/winmsg/wm-windowposchanging
          WM_WINDOWPOSCHANGING = 0x0046,

          // https://docs.microsoft.com/en-us/windows/win32/inputdev/wm-rbuttondown
          WM_RBUTTONDOWN = 0x0204,

          // https://docs.microsoft.com/en-us/windows/win32/inputdev/wm-rbuttonup
          WM_RBUTTONUP = 0x0205,

          // https://docs.microsoft.com/en-us/windows/win32/menurc/wm-setcursor
          WM_SETCURSOR = 0x0020,

          // https://docs.microsoft.com/en-us/windows/win32/winmsg/wm-size
          WM_SIZE = 0x0005,
     }

     [Flags]
     internal enum WindowStyles : uint
     {
          WS_BORDER = 0x00800000,
          WS_CAPTION = 0x00C00000,
          WS_CHILD = 0x40000000,
          WS_CHILDWINDOW = 0x40000000,
          WS_CLIPCHILDREN = 0x02000000,
          WS_CLIPSIBLINGS = 0x04000000,
          WS_DISABLED = 0x08000000,
          WS_DLGFRAME = 0x00400000,
          WS_GROUP = 0x00020000,
          WS_HSCROLL = 0x00100000,
          WS_ICONIC = 0x20000000,
          WS_MAXIMIZE = 0x01000000,
          WS_MAXIMIZEBOX = 0x00010000,
          WS_MINIMIZE = 0x20000000,
          WS_MINIMIZEBOX = 0x00020000,
          WS_OVERLAPPED = 0x00000000,
          WS_OVERLAPPEDWINDOW = WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX,
          WS_POPUP = 0x80000000,
          WS_POPUPWINDOW = WS_POPUP | WS_BORDER | WS_SYSMENU,
          WS_SIZEBOX = 0x00040000,
          WS_SYSMENU = 0x00080000,
          WS_TABSTOP = 0x00010000,
          WS_THICKFRAME = 0x00040000,
          WS_TILED = 0x00000000,
          WS_TILEDWINDOW = WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX,
          WS_VISIBLE = 0x10000000,
          WS_VSCROLL = 0x00200000
     }

     [Flags]
     internal enum WindowStylesEx : uint
     {
          WS_EX_ACCEPTFILES = 0x00000010,
          WS_EX_APPWINDOW = 0x00040000,
          WS_EX_CLIENTEDGE = 0x00000200,
          WS_EX_COMPOSITED = 0x02000000,
          WS_EX_CONTEXTHELP = 0x00000400,
          WS_EX_CONTROLPARENT = 0x00010000,
          WS_EX_DLGMODALFRAME = 0x00000001,
          WS_EX_LAYERED = 0x00080000,
          WS_EX_LAYOUTRTL = 0x00400000,
          WS_EX_LEFT = 0x00000000,
          WS_EX_LEFTSCROLLBAR = 0x00004000,
          WS_EX_LTRREADING = 0x00000000,
          WS_EX_MDICHILD = 0x00000040,
          WS_EX_NOACTIVATE = 0x08000000,
          WS_EX_NOINHERITLAYOUT = 0x00100000,
          WS_EX_NOPARENTNOTIFY = 0x00000004,
          WS_EX_NOREDIRECTIONBITMAP = 0x00200000,
          WS_EX_OVERLAPPEDWINDOW = WS_EX_WINDOWEDGE | WS_EX_CLIENTEDGE,
          WS_EX_PALETTEWINDOW = WS_EX_WINDOWEDGE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST,
          WS_EX_RIGHT = 0x00001000,
          WS_EX_RIGHTSCROLLBAR = 0x00000000,
          WS_EX_RTLREADING = 0x00002000,
          WS_EX_STATICEDGE = 0x00020000,
          WS_EX_TOOLWINDOW = 0x00000080,
          WS_EX_TOPMOST = 0x00000008,
          WS_EX_TRANSPARENT = 0x00000020,
          WS_EX_WINDOWEDGE = 0x00000100
     }

     internal const uint TTF_SUBCLASS = 0x0010;

     internal const uint TTS_ALWAYSTIP = 0x01;
     //internal const uint TTS_NOPREFIX = 0x02;
     //internal const uint TTS_BALLOON = 0x40;

     #endregion

     #region Window Painting

     // NOTE: per pinvoke.net, this function is called "GdiAlphaBlend" even though the Microsoft documentation calls it AlphaBlend
     [DllImport("gdi32.dll", EntryPoint = "GdiAlphaBlend")]
     internal static extern bool AlphaBlend(IntPtr hdcDest, int xOriginDest, int yOriginDest, int wDest, int hDest, IntPtr hdcSrc, int xOriginSrc, int yOriginSrc, int wSrc, int hSrc, BLENDFUNCTION ftn);

     [StructLayout(LayoutKind.Sequential)]
     public struct BLENDFUNCTION
     {
          public byte BlendOp;
          public byte BlendFlags;
          public byte SourceConstantAlpha;
          public byte AlphaFormat;
     }

     internal const byte AC_SRC_OVER = 0x00;
     //internal const byte AC_SRC_ALPHA = 0x01;

     internal const uint DIB_RGB_COLORS = 0;

     // https://docs.microsoft.com/en-us/windows/win32/api/uxtheme/nf-uxtheme-beginbufferedpaint
     [DllImport("uxtheme.dll")]
     internal static extern IntPtr BeginBufferedPaint(IntPtr hdcTarget, [In] ref RECT prcTarget, BP_BUFFERFORMAT dwFormat, IntPtr pPaintParams, out IntPtr phdc);

     // https://docs.microsoft.com/en-us/windows/win32/api/uxtheme/ne-uxtheme-bp_bufferformat
     internal enum BP_BUFFERFORMAT : uint
     {
          BPBF_COMPATIBLEBITMAP,
          BPBF_DIB,
          BPBF_TOPDOWNDIB,
          BPBF_TOPDOWNMONODIB
     }

     [DllImport("user32.dll")]
     internal static extern IntPtr BeginPaint(IntPtr hwnd, out PAINTSTRUCT lpPaint);

     // https://docs.microsoft.com/en-us/windows/win32/api/uxtheme/nf-uxtheme-bufferedpaintclear
     [DllImport("uxtheme.dll")]
     internal static extern uint BufferedPaintClear(IntPtr hBufferedPaint, ref RECT prc);
     //
     [DllImport("uxtheme.dll")]
     internal static extern uint BufferedPaintClear(IntPtr hBufferedPaint, IntPtr prc);

     [DllImport("uxtheme.dll")]
     internal static extern int BufferedPaintInit();

     [DllImport("uxtheme.dll")]
     internal static extern int BufferedPaintUnInit();

     // https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-drawiconex
     [DllImport("user32.dll")]
     internal static extern bool DrawIconEx(IntPtr hdc, int xLeft, int yTop, IntPtr hIcon, int cxWidth, int cyHeight, uint istepIfAniCur, IntPtr hbrFlickerFreeDraw, DrawIconFlags diFlags);

     [Flags]
     internal enum DrawIconFlags : uint
     {
          DI_COMPAT = 0x0004,
          DI_DEFAULTSIZE = 0x0008,
          DI_IMAGE = 0x0002,
          DI_MASK = 0x0001,
          DI_NOMIRROR = 0x0010,
          DI_NORMAL = DI_IMAGE | DI_MASK // 0x0003
     }

     // https://docs.microsoft.com/en-us/windows/win32/api/uxtheme/nf-uxtheme-endbufferedpaint
     [DllImport("uxtheme.dll")]
     internal static extern uint EndBufferedPaint(IntPtr hBufferedPaint, bool fUpdateTarget);

     [DllImport("user32.dll")]
     internal static extern bool EndPaint(IntPtr hWnd, [In] ref PAINTSTRUCT lpPaint);

     //

     [StructLayout(LayoutKind.Sequential)]
     internal struct PAINTSTRUCT
     {
          public IntPtr hdc;
          public bool fErase;
          public RECT rcPaint;
          public bool fRestore;
          public bool fIncUpdate;
          [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
          public byte[] rgbReserved;
     }
#endregion
}
