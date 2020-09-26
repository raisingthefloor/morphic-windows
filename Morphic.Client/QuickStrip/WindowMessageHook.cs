// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

namespace Morphic.Client.QuickStrip
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Windows;
    using System.Windows.Interop;

    /// <summary>
    /// Raises an event when a window message has been received by a window.
    /// </summary>
    public class WindowMessageHook : IDisposable
    {
        private readonly HashSet<int> messagesWanted = new HashSet<int>();
        private HwndSource? hwndSource;

        public event EventHandler<MessageEventArgs>? GotMessage;

        public Window Window { get; }

        public IntPtr Handle { get; private set; }

        public WindowMessageHook(Window window)
        {
            if (!(window is IMessageHook))
            {
                throw new ArgumentException(
                    $"{nameof(window)} is expected implement the {nameof(IMessageHook)} interface.");
            }

            this.Window = window;
            window.SourceInitialized += (sender, args) => this.AddHook();
        }

        /// <summary>
        /// Add a message to the list of messages listened for.
        /// </summary>
        /// <param name="message"></param>
        public int AddMessage(int message)
        {
            this.messagesWanted.Add(message);
            return message;
        }

        /// <summary>
        /// Add a message to the list of messages listened for.
        /// </summary>
        /// <param name="messageName">Name of the window message.</param>
        public int AddMessage(string messageName)
        {
            return this.AddMessage(RegisterMessage(messageName));
        }

        /// <summary>
        /// Registers a globally named window message.
        /// </summary>
        /// <param name="messageName">The name.</param>
        /// <returns>The message identifier.</returns>
        public static int RegisterMessage(string messageName)
        {
            return RegisterWindowMessage(messageName);
        }

        /// <summary>
        /// Start capturing messages.
        /// </summary>
        private void AddHook()
        {
            WindowInteropHelper nativeWindow = new WindowInteropHelper(this.Window);
            this.Handle = nativeWindow.Handle;
            this.hwndSource = HwndSource.FromHwnd(this.Handle);
            this.hwndSource?.AddHook(this.WindowProc);
            this.Window.Closed += (sender, args) => this.RemoveHook();
        }

        /// <summary>
        /// Stop capturing messages.
        /// </summary>
        private void RemoveHook()
        {
            if (this.hwndSource?.IsDisposed == false)
            {
                this.hwndSource.RemoveHook(this.WindowProc);
            }
        }

        /// <summary>
        /// A Window Procedure
        /// </summary>
        private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (this.messagesWanted.Contains(msg))
            {
                MessageEventArgs eventArgs = new MessageEventArgs(hwnd, msg, wParam, lParam);
                this.GotMessage?.Invoke(this, eventArgs);
                handled = eventArgs.Handled;
                return eventArgs.Result;
            }
            else
            {
                return IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            if (this.hwndSource?.IsDisposed == false)
            {
                this.hwndSource.Dispose();
                this.hwndSource = null;
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int RegisterWindowMessage(string lpString);

    }

    /// <summary>
    /// Identifies a class (Window) that has a message hook.
    /// </summary>
    public interface IMessageHook : IDisposable
    {
        WindowMessageHook Messages { get; }
    }

    /// <summary>
    /// Event arguments for WindowMessageHook.GotMessage
    /// </summary>
    public class MessageEventArgs
    {
        public IntPtr Hwnd { get; }
        public int Msg { get; }
        public IntPtr WParam { get; }
        public IntPtr LParam { get; }

        public bool Handled { get; set; }
        public IntPtr Result { get; set; }

        public MessageEventArgs(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam)
        {
            this.Hwnd = hwnd;
            this.Msg = msg;
            this.WParam = wParam;
            this.LParam = lParam;
        }
    }
}
