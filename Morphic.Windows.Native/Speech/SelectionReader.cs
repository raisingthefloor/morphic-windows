// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt
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

namespace Morphic.Windows.Native
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Diagnostics;

    /// <summary>
    /// Reads the selected text from most windows.
    /// </summary>
    public class SelectionReader
    {
        public static readonly SelectionReader Default = new SelectionReader();

        private int shellMessage;

        private IntPtr activeWindow;
        private IntPtr lastWindow;
        private readonly uint processId;
        

        /// <summary>Set when the active window has changed.</summary>
        private readonly AutoResetEvent gotActiveWindow = new AutoResetEvent(false);

        /// <summary>Set when the clipboard has been updated.</summary>
        private readonly AutoResetEvent gotClipboard = new AutoResetEvent(false);

        private SelectionReader()
        {
            using Process process = Process.GetCurrentProcess();
            this.processId = (uint) process.Id;
        }

        public void Initialise(IntPtr hwnd)
        {
            if (this.shellMessage == 0)
            {
                this.shellMessage = WindowsApi.RegisterWindowMessage("SHELLHOOK");
                WindowsApi.RegisterShellHookWindow(hwnd);
                WindowsApi.AddClipboardFormatListener(hwnd);
            }
        }

        /// <summary>
        /// Gets the selected text of the given window, or the last activate window.
        /// </summary>
        /// <param name="sendKeys">The <c>SendKeys.SendWait</c> method.</param>
        /// <param name="windowHandle">The window.</param>
        /// <returns>The selected text.</returns>
        public async Task GetSelectedText(Action<string> sendKeys, IntPtr? windowHandle = null)
        {
            IntPtr hwnd = windowHandle ?? this.lastWindow;
            await Task.Run(() =>
            {
                if (hwnd != IntPtr.Zero)
                {
                    // Activate the window, if it's not already.
                    IntPtr active = WindowsApi.GetForegroundWindow();
                    if (active != hwnd)
                    {
                        this.gotActiveWindow.Reset();
                        WindowsApi.SetForegroundWindow(hwnd);

                        // Wait for it to be activated.
                        this.gotActiveWindow.WaitOne(3000);
                    }
                }

                // Copy the selected text to clipboard
                this.gotClipboard.Reset();
                sendKeys("^c");

                // Wait for the clipboard update.
                this.gotClipboard.WaitOne(3000);
            });

            return;
        }

        /// <summary>
        /// A Window procedure - handles window messages for a window.
        /// Pass this method to HwndSource.AddHook().
        /// </summary>
        /// <param name="hwnd">The window handle.</param>
        /// <param name="msg">The message.</param>
        /// <param name="wParam">Message data.</param>
        /// <param name="lParam">Message data.</param>
        /// <param name="handled">Set to true if the message has been handled</param>
        /// <returns>The message result.</returns>
        public IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch ((WindowsApi.WindowMessages) msg)
            {
                case WindowsApi.WindowMessages.WM_CLIPBOARDUPDATE:
                    // The clipboard has been updated.
                    this.gotClipboard.Set();
                    handled = true;
                    break;

                default:
                    if (msg == this.shellMessage)
                    {
                        // A window has been activated.
                        if (wParam.ToInt32() == WindowsApi.HSHELL_WINDOWACTIVATED
                            || wParam.ToInt32() == WindowsApi.HSHELL_RUDEAPPACTIVATED)
                        {
                            // The activated window is passed via lParam, but this wasn't accurate
                            // for Modern UI apps.
                            IntPtr window = WindowsApi.GetForegroundWindow();
                            if (this.activeWindow != window)
                            {
                                this.activeWindow = window;

                                // Ignore the application's window
                                WindowsApi.GetWindowThreadProcessId(window, out uint pid);
                                if (pid != this.processId)
                                {
                                    this.lastWindow = window;
                                }

                                this.gotActiveWindow.Set();
                            }
                        }
                    }

                    break;
            }

            return IntPtr.Zero;
        }
    }
}