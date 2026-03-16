using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Morphic.Core;
using PInvoke;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Morphic.MorphicBar;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MorphicBarWindow : Window
{
    Window _dummyParentWindow;
    System.Drawing.Icon _icon;

    public MorphicBarWindow()
    {
        this.InitializeComponent();

        this.ExtendsContentIntoTitleBar = true;
        this.SetTitleBar(null);

        

        // Get the AppWindow instance
        var appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(this)));

        // Access the OverlappedPresenter
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMinimizable = false; // Disable Minimize button
            presenter.IsMaximizable = false; // Disable Maximize button
            presenter.IsResizable = false; // Disable resizing
            presenter.IsAlwaysOnTop = true;
//            presenter.SetBorderAndTitleBar(false, false); // NOTE: this may not actually do anything
        }

        var hwnd = WindowNative.GetWindowHandle(this);
        //System.Diagnostics.Debug.WriteLine("hwnd: " + hwnd.ToString());

        // set our window icon
        var morphicIconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Morphic.Assets.Icons.morphic.ico")!;
        _icon = new(morphicIconStream);
        SendMessage(hwnd, WM_SETICON, (IntPtr)1, _icon.Handle);  // ICON_BIG
        SendMessage(hwnd, WM_SETICON, (IntPtr)0, _icon.Handle);  // ICON_SMALL

        // create a "helper window" to be the parent of the MorphicBar (so that it doesn't show in the taskbar)
        _dummyParentWindow = new Window();
        IntPtr helperHwnd = WindowNative.GetWindowHandle(_dummyParentWindow);
        //
        SetWindowLongPtr(hwnd, GWLP_HWNDPARENT, helperHwnd);

        // remove WS_DLGFRAME style
        int style = GetWindowLong(hwnd, GWL_STYLE);
        style &= ~WS_DLGFRAME;
        //style |= WS_CLIPCHILDREN;
        //
        //style &= ~WS_CAPTION;
        //style &= ~WS_THICKFRAME;
        //style |= WS_POPUP;
        //
        SetWindowLong(hwnd, GWL_STYLE, style);
        //
        // remove WS_EX_WINDOWEDGE exstyle
        int exstyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        exstyle &= ~WS_EX_WINDOWEDGE;
        // exstyle |= WS_EX_TOOLWINDOW; // don't do this
        SetWindowLong(hwnd, GWL_EXSTYLE, GWL_EXSTYLE);
        //
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
        SWP_FRAMECHANGED | SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER);

        this.SizeChanged += MorphicBarWindow_SizeChanged;
        this.AppWindow.Resize(new Windows.Graphics.SizeInt32(1200, 140));
    }

    private void MorphicBarWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args)
    {
        this.ApplyCornerRadius(5);

        //AppWindow.TitleBar.SetDragRectangles(new Windows.Graphics.RectInt32[] { new Windows.Graphics.RectInt32(rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top) });
        //AppWindow.TitleBar.SetDragRectangles(new Windows.Graphics.RectInt32[] { new Windows.Graphics.RectInt32(0, 0, rect.right - rect.left, rect.bottom - rect.top) });
    }

    private MorphicResult<MorphicUnit, MorphicUnit> ApplyCornerRadius(int radius)
    {
        var hwnd = WindowNative.GetWindowHandle(this);

        bool getWindowRectSuccess = GetWindowRect(hwnd, out RECT rect);
        if (getWindowRectSuccess == false)
        {
            // TODO: get last error
            return MorphicResult.ErrorResult();
        }

        int width = rect.right - rect.left;
        int height = rect.bottom - rect.top;
        var region = CreateRoundRectRgn(0, 0, width + 1, height + 1, 100, 100);
        var setWindowRgnSuccess = SetWindowRgn(hwnd, region, true);
        if (setWindowRgnSuccess == 0)
        {
            return MorphicResult.ErrorResult();
        }

        return MorphicResult.OkResult();
    }

    #region Win32 API

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2,
    int cx, int cy);

    //

    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    
    private const int GWLP_HWNDPARENT = -8;

    private const uint WM_SETICON = 0x0080;

    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_FRAMECHANGED = 0x0020;

    private const int WS_CAPTION = 0x00C00000;
    private const int WS_CLIPCHILDREN = 0x02000000;
    private const int WS_DLGFRAME = 0x00400000;
    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WS_THICKFRAME = 0x00040000;
    //
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_WINDOWEDGE = 0x00000100;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

    #endregion Win32 API
}
