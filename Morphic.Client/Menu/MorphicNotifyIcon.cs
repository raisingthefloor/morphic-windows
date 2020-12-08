using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Morphic.Windows.Native;

namespace Morphic.Client.Menu
{
    public class MorphicNotifyIcon : Component
    {
        [Flags]
        private enum ButtonState
        {
            STATE_NORMAL = 1,
            STATE_HOVER = 2,
            STATE_PRESSED = 4,
            STATE_CHECKED = 8
        }

        WinApi.RECT taskRect, notifyRect, trayClient;

        private const int ICON_SIZE = 16;
        private const int BUTTON_WIDTH = 24;

        private IntPtr tooltipWindow;
        private int currentDpi = 0;

        private static readonly object mouseDownEventKey = new object();
        private static readonly object mouseMoveEventKey = new object();
        private static readonly object mouseUpEventKey = new object();
        private static readonly object clickEventKey = new object();
        private static readonly object doubleClickEventKey = new object();
        private static readonly object mouseClickEventKey = new object();
        private static readonly object mouseDoubleClickEventKey = new object();

        private readonly object syncObj = new object();

        private Icon icon;
        private Icon hcIcon;

        private string text = string.Empty;
        private readonly MorphicNotifyIconNativeWindow window;
        private bool doubleClick;
        private int lastClick;

        private bool visible;

        private bool highContrast;

        private static IntPtr hIcon;
        private static IntPtr hHighContrastIcon;

        private ButtonState buttonState = ButtonState.STATE_NORMAL;

        public MorphicNotifyIcon()
        {
            window = new MorphicNotifyIconNativeWindow(this);
            UpdateIcon(visible);
        }

        public MorphicNotifyIcon(IContainer container) : this()
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            container.Add(this);
        }

        public Icon Icon
        {
            get => icon;
            set
            {
                if (icon == value) return;

                icon = value;
                hIcon = icon.Handle;
                //setImage();
            }
        }

        public Icon HighContrastIcon
        {
            get => hcIcon;
            set
            {
                if (hcIcon == value) return;

                hcIcon = value;
                hHighContrastIcon = hcIcon.Handle;
                //setImage();
            }
        }

        public bool Visible
        {
            get => visible;
            set
            {
                if (visible == value) return;

                UpdateIcon(value);
                visible = value;
            }
        }

        public string Text
        {
            get => text;
            set
            {
                text = value;
                SetToolTip(text);
            }
        }

        #region Events
        public event EventHandler Click
        {
            add => Events.AddHandler(clickEventKey, value);
            remove => Events.RemoveHandler(clickEventKey, value);
        }

        public event EventHandler DoubleClick
        {
            add => Events.AddHandler(doubleClickEventKey, value);
            remove => Events.RemoveHandler(doubleClickEventKey, value);
        }

        public event MouseEventHandler MouseClick
        {
            add => Events.AddHandler(mouseClickEventKey, value);
            remove => Events.RemoveHandler(mouseClickEventKey, value);
        }

        public event MouseEventHandler MouseDoubleClick
        {
            add => Events.AddHandler(mouseDoubleClickEventKey, value);
            remove => Events.RemoveHandler(mouseDoubleClickEventKey, value);
        }

        public event MouseEventHandler MouseDown
        {
            add => Events.AddHandler(mouseDownEventKey, value);
            remove => Events.RemoveHandler(mouseDownEventKey, value);
        }

        public event MouseEventHandler MouseUp
        {
            add => Events.AddHandler(mouseUpEventKey, value);
            remove => Events.RemoveHandler(mouseUpEventKey, value);
        }

        private void OnClick(EventArgs e)
        {
            ((EventHandler)Events[clickEventKey])?.Invoke(this, e);
        }

        private void OnDoubleClick(EventArgs e)
        {
            ((EventHandler)Events[doubleClickEventKey])?.Invoke(this, e);
        }

        private void OnMouseClick(MouseEventArgs mea)
        {
            ((MouseEventHandler)Events[mouseClickEventKey])?.Invoke(this, mea);
        }

        private void OnMouseDoubleClick(MouseEventArgs mea)
        {
            ((MouseEventHandler)Events[mouseDoubleClickEventKey])?.Invoke(this, mea);
        }

        private void OnMouseDown(MouseEventArgs e)
        {
            ((MouseEventHandler)Events[mouseDownEventKey])?.Invoke(this, e);
        }

        private void OnMouseUp(MouseEventArgs e)
        {
            ((MouseEventHandler)Events[mouseUpEventKey])?.Invoke(this, e);
        }

        private void HandleMouseDownMessage(ref Message m, MouseButtons button, int clicks)
        {
            if (clicks == 2)
            {
                OnDoubleClick(new MouseEventArgs(button, 2, 0, 0, 0));
                OnMouseDoubleClick(new MouseEventArgs(button, 2, 0, 0, 0));
                doubleClick = true;
            }

            OnMouseDown(new MouseEventArgs(button, clicks, 0, 0, 0));
        }

        private void HandleMouseUpMessage(ref Message m, MouseButtons button)
        {
            OnMouseUp(new MouseEventArgs(button, 0, 0, 0, 0));

            if (!doubleClick)
            {
                OnClick(new MouseEventArgs(button, 0, 0, 0, 0));
                OnMouseClick(new MouseEventArgs(button, 0, 0, 0, 0));
            }

            doubleClick = false;
        }
        #endregion

        private void UpdateIcon(bool showIconInTray)
        {
            lock (syncObj)
            {
                if (DesignMode)
                {
                    return;
                }

                var taskbar = WinApi.FindWindow("Shell_TrayWnd", null);
                if (window.Handle == IntPtr.Zero)
                {
                    try
                    {
                        currentDpi = getDpi(taskbar);
                        CheckHighContrast();

                        window.CreateHandle(new CreateParams
                        {
                            ClassName = "GPII-TrayButton",
                            Parent = taskbar,
                            Width = 24,
                            Height = 40
                        });
                    }
                    catch (Exception ex)
                    {
                        var errorMessage = new Win32Exception(ex.HResult).Message;
                    }
                }
            }
        }

        private void SetToolTip(string tooltip)
        {
            if (tooltipWindow == IntPtr.Zero)
            {
                tooltipWindow = WinApi.CreateWindowEx(0,
                    "tooltips_class32", null,
                    WinApi.WindowStyles.Ws_popup | (WinApi.WindowStyles)WinApi.TTS_ALWAYSTIP | (WinApi.WindowStyles)WinApi.TTS_NOPREFIX | (WinApi.WindowStyles)WinApi.TTS_BALLOON,
                    0, 0,
                    0, 0,
                    window.Handle,
                    IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            }

            var ti = new WinApi.ToolInfo
            {
                cbSize = Marshal.SizeOf(typeof(WinApi.ToolInfo)),
                hwnd = window.Handle,
                lpszText = tooltip,
                uFlags = WinApi.TTF_SUBCLASS
            };

            WinApi.GetClientRect(window.Handle, out ti.rect);

            WinApi.SendMessage(tooltipWindow, WinApi.TTM_ADDTOOL, IntPtr.Zero, ref ti);
        }

        private void WndProc(ref Message msg)
        {
            Debug.WriteLine($"{(WinApi.WindowMessage)msg.Msg}");
            switch ((WinApi.WindowMessage)msg.Msg)
            {
                case WinApi.WindowMessage.WM_SETCURSOR:
                    var mouseMsg = (WinApi.WindowMessage)(msg.LParam.ToInt32() >> 16);
                    Debug.WriteLine($"{mouseMsg}");
                    switch (mouseMsg)
                    {
                        case WinApi.WindowMessage.WM_MOUSEMOVE:
                            if (!buttonState.HasFlag(ButtonState.STATE_HOVER))
                            {
                                var tme = new WinApi.TRACKMOUSEEVENT(WinApi.TMEFlags.TME_LEAVE, window.Handle);
                                WinApi.TrackMouseEvent(ref tme);

                                // handle mouse enter
                                buttonState |= ButtonState.STATE_HOVER;
                            }
                            break;
                        case WinApi.WindowMessage.WM_LBUTTONDOWN:
                            buttonState |= ButtonState.STATE_PRESSED;

                            var span = Environment.TickCount - this.lastClick;
                            if (doubleClick || span <= SystemInformation.DoubleClickTime)
                            {
                                HandleMouseDownMessage(ref msg, MouseButtons.Left, 2);
                            }
                            else
                            {
                                HandleMouseDownMessage(ref msg, MouseButtons.Left, 1);
                            }

                            this.lastClick = Environment.TickCount;

                            break;
                        case WinApi.WindowMessage.WM_LBUTTONUP:
                            buttonState &= ~ButtonState.STATE_PRESSED;
                            HandleMouseUpMessage(ref msg, MouseButtons.Left);
                            break;
                        case WinApi.WindowMessage.WM_RBUTTONUP:
                            HandleMouseUpMessage(ref msg, MouseButtons.Right);
                            break;
                    }
                    break;

                case WinApi.WindowMessage.WM_MOUSELEAVE:
                    buttonState &= ~(ButtonState.STATE_HOVER | ButtonState.STATE_PRESSED);
                    Redraw();
                    break;
                case WinApi.WindowMessage.WM_SIZE:
                case WinApi.WindowMessage.WM_WINDOWPOSCHANGED:
                    positionTrayWindows(true);
                    break;
                case WinApi.WindowMessage.WM_ERASEBKGND:
                    positionTrayWindows(false);
                    break;
                case WinApi.WindowMessage.WM_DISPLAYCHANGE:
                    positionTrayWindows(true);
                    break;
                case WinApi.WindowMessage.WM_PAINT:
                    Paint();
                    break;
                case WinApi.WindowMessage.WM_SETTINGCHANGE:
                    if (CheckHighContrast())
                    {
                        setImage();
                    }
                    break;
                default:
                    if (!positionTrayWindows(false))
                    {
                        Redraw();
                    }
                    break;
            }
        }

        public IntPtr getTaskbarWindow()
        {
            return WinApi.FindWindow("Shell_TrayWnd", null);
        }

        private int getDpi(IntPtr window)
        {
            return WinApi.GetDpiForWindow(window);
        }

        private bool positionTrayWindows(bool force)
        {
            if (window.Handle == IntPtr.Zero)
            {
                return false;
            }

            if (Icon == null)
            {
                return false;
            }

            // Task bar
            var tray = getTaskbarWindow();
            // Container of the window list (and toolbars)
            var tasks = WinApi.FindWindowEx(tray, IntPtr.Zero, "ReBarWindow32", null);
            // The notification icons
            var notify = WinApi.FindWindowEx(tray, IntPtr.Zero, "TrayNotifyWnd", null);

            var dpi = getDpi(tray);
            if (dpi != currentDpi)
            {
                currentDpi = dpi;

                setImage();

                return true;
            }

            WinApi.RECT buttonRect;

            WinApi.GetWindowRect(tasks, out var newTaskRect);
            WinApi.GetWindowRect(notify, out var newNotifyRect);
            WinApi.GetClientRect(tray, out var newTrayClient);

            var changed = !WinApi.EqualRect(taskRect, newTaskRect)
                           || !WinApi.EqualRect(notifyRect, newNotifyRect)
                           || !WinApi.EqualRect(trayClient, newTrayClient);

            WinApi.CopyRect(out taskRect, newTaskRect);
            WinApi.CopyRect(out notifyRect, newNotifyRect);
            WinApi.CopyRect(out trayClient, newTrayClient);

            WinApi.GetWindowRect(tray, out var trayRect);
            var vert = trayRect.Top == 0 && trayRect.Bottom > WinApi.GetSystemMetrics(WinApi.SM_CYFULLSCREEN) - 10;

            if (vert)
            {
                // Shrink the tasks window
                taskRect.Bottom = notifyRect.Top - FixDpi(BUTTON_WIDTH);
                // Put the button between
                buttonRect.Top = taskRect.Bottom;
                buttonRect.Bottom = notifyRect.Top;
                buttonRect.Left = highContrast ? 1 : 0;
                buttonRect.Right = trayClient.Right;
            }
            else
            {
                var rtl = notifyRect.Left < taskRect.Left;

                if (rtl)
                {
                    // notification area is on the left
                    // Shrink the tasks window
                    taskRect.Left = notifyRect.Right + FixDpi(BUTTON_WIDTH);
                    // Put the button between
                    buttonRect.Left = trayClient.Right - taskRect.Left;
                    buttonRect.Right = trayClient.Right - notifyRect.Right;
                }
                else
                {
                    // notification area is on the right (the common way)
                    // Shrink the tasks window
                    taskRect.Right = notifyRect.Left - FixDpi(BUTTON_WIDTH);
                    // Put the button between
                    buttonRect.Left = taskRect.Right;
                    buttonRect.Right = notifyRect.Left;
                }

                buttonRect.Top = highContrast ? 1 : 0;
                buttonRect.Bottom = trayClient.Bottom;
            }

            if (!force || !changed)
            {
                // See if the button needs to be moved
                WinApi.GetWindowRect(window.Handle, out var currentRect);
                WinApi.MapWindowPoints(WinApi.SpecialWindowHandles.HWND_DESKTOP, tray, ref currentRect, 2);
                changed = changed || !WinApi.EqualRect(buttonRect, currentRect);
            }

            if (force || changed)
            {
                // shrink the task list
                WinApi.SetWindowPos(tasks, WinApi.SpecialWindowHandles.HWND_BOTTOM,
                    0, 0,
                    taskRect.Right - taskRect.Left,
                    taskRect.Bottom - taskRect.Top,
                    WinApi.SetWindowPosFlags.SWP_NOACTIVATE | WinApi.SetWindowPosFlags.SWP_NOMOVE);

                // Move the button between the tasks and notification area
                WinApi.SetWindowPos(window.Handle, WinApi.SpecialWindowHandles.HWND_TOP,
                    buttonRect.Left, buttonRect.Top,
                    buttonRect.Right - buttonRect.Left,
                    buttonRect.Bottom - buttonRect.Top,
                    WinApi.SetWindowPosFlags.SWP_NOACTIVATE | WinApi.SetWindowPosFlags.SWP_SHOWWINDOW);

                Redraw();
            }

            return changed;
        }

        private int FixDpi(int size)
        {
            return (size * currentDpi) / 96;
        }

        private void setImage()
        {
            hIcon = Icon.Handle;

            if(HighContrastIcon != null)
                hHighContrastIcon = HighContrastIcon.Handle;

            positionTrayWindows(true);
        }

        private void Redraw()
        {
            WinApi.RedrawWindow(window.Handle, IntPtr.Zero, IntPtr.Zero, WinApi.RedrawWindowFlags.Erase | WinApi.RedrawWindowFlags.Invalidate | WinApi.RedrawWindowFlags.Frame | WinApi.RedrawWindowFlags.AllChildren);
        }

        private void Paint()
        {
            WinApi.GetClientRect(window.Handle, out var rc);

            var dcPaint = WinApi.BeginPaint(window.Handle, out var ps);
            var paintBuffer = WinApi.BeginBufferedPaint(dcPaint, ref rc, WinApi.BP_BUFFERFORMAT.TopDownDIB, IntPtr.Zero, out var dc);
            WinApi.BufferedPaintClear(paintBuffer, IntPtr.Zero);

            var x = (rc.Right - 16) / 2;
            var y = (rc.Bottom - 16) / 2;

            if (highContrast)
            {
                AlphaRect(dc, rc, Color.Black, 255);

                WinApi.DrawIconEx(dc, x, y, hHighContrastIcon, 16, 16, 0, IntPtr.Zero, 3);
            }
            else
            {
                var alpha = 0;

                // Draw the not-high-contrast icon
                // Values come from what looks right.
                if (buttonState.HasFlag(ButtonState.STATE_PRESSED))
                {
                    alpha = 10;
                }
                else if (buttonState.HasFlag(ButtonState.STATE_HOVER))
                {
                    alpha = 25;
                }

                if (alpha > 0)
                {
                    AlphaRect(dc, rc, Color.White, alpha);
                }

                WinApi.DrawIconEx(dc, x, y, hIcon, 16, 16, 0, IntPtr.Zero, 3);
            }

            WinApi.EndBufferedPaint(paintBuffer, true);
            WinApi.EndPaint(window.Handle, ref ps);
        }

        private void AlphaRect(IntPtr dc, WinApi.RECT rc, Color color, int alpha)
        {
            // GDI doesn't have a concept of semi-transparent pixels - the only function that honours them is AlphaBlend.
            // Create a bitmap containing a single pixel, and use AlphaBlend to stretch it to the size of the rect.

            // The bitmap
            var bmi = new WinApi.BITMAPINFO
            {
                biSize = 40,
                biPlanes = 1,
                biBitCount = 32,
                biWidth = 1,
                biHeight = 1
            };

            // The pixel
            var pixel = (
                ((alpha * color.R / 0xff) << 16) |
                ((alpha * color.G / 0xff) << 8) |
                (alpha * color.B / 0xff)
            );
            pixel = pixel | (alpha << 24);

            //// Make the bitmap DC.
            var dcPixel = WinApi.CreateCompatibleDC(dc);
            var bmpPixel = WinApi.CreateDIBSection(dcPixel, ref bmi, WinApi.DIB_RGB_COLORS, out var bits, IntPtr.Zero, 0);
            var bmpOrig = WinApi.SelectObject(dcPixel, bmpPixel);

            Marshal.WriteIntPtr(bits, new IntPtr(pixel));

            //// Draw the "rectangle"
            WinApi.BLENDFUNCTION bf = new WinApi.BLENDFUNCTION
            {
                BlendOp = (byte)WinApi.AC_SRC_OVER,
                BlendFlags = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = (byte)WinApi.AC_SRC_ALPHA
            };
            WinApi.AlphaBlend(dc, rc.Left, rc.Top, rc.Right - rc.Left, rc.Bottom - rc.Top, dcPixel, 0, 0, 1, 1, bf);

            WinApi.SelectObject(dcPixel, bmpOrig);
            WinApi.DeleteObject(bmpPixel);
            WinApi.DeleteDC(dcPixel);
        }

        private bool CheckHighContrast()
        {
            var last = highContrast;
            highContrast = (Spi.Instance.GetHighContrast() & Spi.HighContrastOptions.HCF_HIGHCONTRASTON) != 0;

            return highContrast != last;
        }

        private class MorphicNotifyIconNativeWindow : NativeWindow
        {
            private readonly MorphicNotifyIcon componentReference;

            internal MorphicNotifyIconNativeWindow(MorphicNotifyIcon component)
            {
                componentReference = component;
            }

            protected override void OnThreadException(Exception e)
            {
                Application.OnThreadException(e);
            }

            private IntPtr Callback(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam)
            {
                return WinApi.DefWindowProc(hWnd, msg, wParam, lParam);
            }

            protected override void WndProc(ref Message m)
            {
                componentReference.WndProc(ref m);
            }

            public override void CreateHandle(CreateParams cp)
            {
                lock (this)
                {
                    var lpWndClass = new WinApi.WNDCLASSEX
                    {
                        cbSize = Marshal.SizeOf(typeof(WinApi.WNDCLASSEX)),
                        lpfnWndProc = Callback,
                        lpszClassName = cp.ClassName,
                        hCursor = WinApi.LoadCursor(IntPtr.Zero, (int)WinApi.Cursors.IDC_ARROW)
                    };

                    var result = WinApi.RegisterClassEx(ref lpWndClass);

                    if (WinApi.BufferedPaintInit() != 0)
                    {
                        var error = Marshal.GetLastWin32Error();
                        var errorMessage = new Win32Exception(error).Message;
                        return;
                    }

                    if (Handle != IntPtr.Zero)
                    {
                        return;
                    }

                    var createResult = IntPtr.Zero;

                    do
                    {
                        createResult = WinApi.CreateWindowEx(
                            WinApi.WindowStylesEx.Ws_ex_toolwindow,
                            cp.ClassName,
                            cp.ClassName,
                            WinApi.WindowStyles.Ws_visible | WinApi.WindowStyles.Ws_child | WinApi.WindowStyles.Ws_clipsiblings | WinApi.WindowStyles.Ws_tabstop,
                            0, 0, cp.Width, cp.Height,
                            cp.Parent,
                            IntPtr.Zero,
                            IntPtr.Zero,
                            IntPtr.Zero);

                        if (createResult == IntPtr.Zero)
                        {
                            int lastError = Marshal.GetLastWin32Error();
                            string errorMessage = new Win32Exception(lastError).Message;

                            Thread.Sleep(1000);
                        }
                    } while (createResult == IntPtr.Zero);

                    AssignHandle(createResult);
                }
            }
        }
    }
}
