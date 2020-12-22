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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Morphic.Client
{
    internal class MessageWatcherNativeWindow : NativeWindow, IDisposable
    {
        private List<uint> _watchedMessages;

        private const string NATIVE_WINDOW_CLASS_NAME = "Morphic-MessageWatcher";

        public class WatchedMessageEventArgs : EventArgs
        {
            public uint Msg;
            public IntPtr wParam;
            public IntPtr lParam;
        }

        public delegate void WatchedMessageReceived(object sender, WatchedMessageEventArgs args);
        public event WatchedMessageReceived WatchedMessageEvent;

        internal MessageWatcherNativeWindow(List<uint> watchedMessages)
        {
            // capture the list of messages to watch; we do this one time at initialization to avoid any need for thread safety around this list
            _watchedMessages = watchedMessages;
        }

        public void Initialize()
        {
            // register our custom native window class
            var pointerToWndProcCallback = Marshal.GetFunctionPointerForDelegate(new WinApi.WndProc(this.WndProcCallback));
            var lpWndClass = new WinApi.WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf(typeof(WinApi.WNDCLASSEX)),
                lpfnWndProc = pointerToWndProcCallback,
                lpszClassName = NATIVE_WINDOW_CLASS_NAME,
                hCursor = WinApi.LoadCursor(IntPtr.Zero, (int)WinApi.Cursors.IDC_ARROW)
            };

            var registerClassResult = WinApi.RegisterClassEx(ref lpWndClass);
            if (registerClassResult == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var windowParams = new CreateParams();
            windowParams.ExStyle = (int)WinApi.WindowStylesEx.WS_EX_NOACTIVATE;
            /* NOTE: as we want to be able to ensure that we're referencing the exact class we just registered, we pass the RegisterClassEx results into the 
             * CreateWindow function (and we encode that result as a ushort here in a proprietary way) */
            windowParams.ClassName = registerClassResult.ToString(); // nativeWindowClassName;
            //windowParams.Caption = nativeWindowClassName;
            windowParams.Style = 0;
            windowParams.X = 0;
            windowParams.Y = 0;
            windowParams.Width = 0;
            windowParams.Height = 0;
            windowParams.Parent = WinApi.HWND_MESSAGE;
            //
            // NOTE: CreateHandle can throw InvalidOperationException, OutOfMemoryException, or Win32Exception
            this.CreateHandle(windowParams);
        }

        // NOTE: intial creation events are captured by this callback, but afterwards window messages are captured by WndProc instead
        private IntPtr WndProcCallback(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            // NOTE: we do not need to handle any of the initial creation-time windows messages; if that ever changes, we can process them here
            //switch ((WinApi.WindowMessage)msg)
            //{
            //    default:
            //        break;
            //}

            // pass all non-handled messages through to DefWindowProc
            return WinApi.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        // NOTE: the built-in CreateHandle function couldn't handle our custom class, so we have overridden CreateHandle and are calling CreateWindowEx ourselves
        public override void CreateHandle(CreateParams cp)
        {
            // NOTE: if cp.ClassName is a string parseable as a (UInt16) number, convert that value to an IntPtr; otherwise capture a pointer to the string
            IntPtr classNameAsIntPtr;
            bool mustReleaseClassNameAsIntPtr = false;
            //
            ushort classNameAsUInt16 = 0;
            if (ushort.TryParse(cp.ClassName, out classNameAsUInt16) == true)
            {
                classNameAsIntPtr = (IntPtr)classNameAsUInt16;
                mustReleaseClassNameAsIntPtr = false;
            }
            else
            {
                classNameAsIntPtr = Marshal.StringToHGlobalUni(cp.ClassName);
                mustReleaseClassNameAsIntPtr = true;
            }

            try
            {
                var handle = WinApi.CreateWindowEx(
                    (WinApi.WindowStylesEx)cp.ExStyle,
                    (IntPtr)Int64.Parse(cp.ClassName),
                    cp.Caption,
                    (WinApi.WindowStyles)cp.Style,
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
                    // if we could not create the handle, throw an exception
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                this.AssignHandle(handle);
            }
            finally
            {
                if (mustReleaseClassNameAsIntPtr == true)
                {
                    Marshal.Release(classNameAsIntPtr);
                }
            }
        }

        public void Dispose()
        {
            this.DestroyHandle();
        }

        protected override void WndProc(ref Message m)
        {
            var uMsg = (uint)m.Msg;

            if (_watchedMessages.Contains(uMsg))
            {
                var eventArgs = new WatchedMessageEventArgs();
                eventArgs.Msg = uMsg;
                eventArgs.wParam = m.WParam;
                eventArgs.lParam = m.LParam;

                this.WatchedMessageEvent?.Invoke(this, eventArgs);
            }

            // pass the message through to he base handler (out of an abundance of caution; we could probably just leave m.Result as 0 instead)
            m.Result = WinApi.DefWindowProc(m.HWnd, (uint)m.Msg, m.WParam, m.LParam);
        }

        internal static void PostMessage(uint messageId, IntPtr wParam, IntPtr lParam)
        {
            // find the instance of our watch window; note that is designed to only find one instance
            IntPtr watchWindowHandle = WinApi.FindWindow(NATIVE_WINDOW_CLASS_NAME, null);

            // send the message to the single instance watch window
            WinApi.PostMessage(watchWindowHandle, messageId, wParam, lParam);
        }
    }
}
