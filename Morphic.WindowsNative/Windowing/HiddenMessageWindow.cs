// Copyright 2026 Raising the Floor - US, Inc.
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
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Morphic.WindowsNative.Windowing;

// EventArgs for HiddenMessageWindow.MessageReceived. Carries the raw Win32 message tuple so
// consumers can filter by message type and decode wParam/lParam as the message requires.
public class WindowMessageEventArgs(uint msg, IntPtr wParam, IntPtr lParam) : EventArgs
{
    public uint Msg { get; } = msg;
    public IntPtr WParam { get; } = wParam;
    public IntPtr LParam { get; } = lParam;
}

// HiddenMessageWindow is a singleton top-level invisible Win32 window that exists solely to
// receive process-wide broadcast messages (WM_SETTINGCHANGE, WM_DISPLAYCHANGE, etc.) that
// Windows posts via SendMessage(HWND_BROADCAST, ...). Top-level windows in the current desktop
// receive these; message-only windows (HWND_MESSAGE parent) do NOT.
//
// Why not Microsoft.Win32.SystemEvents (the BCL surface for the same thing)? the BCL creates
// its own hidden window on a dedicated thread, but in WinAppSDK processes those broadcasts
// silently fail to reach the BCL window -- some interaction between WinAppSDK's process setup
// and the BCL's window-creation path. Result: SystemEvents.UserPreferenceChanged never fires.
// Owning our own window in our own process (specifically created on the UI thread that already
// has a working message pump) sidesteps the issue.
//
// Lifecycle:
//   * Initialize() must be called from a thread with a running Win32 message pump (the UI
//     thread is the natural choice -- WinAppSDK guarantees it has one). Idempotent; safe to
//     call repeatedly from anywhere after the first call. Subsequent message dispatching
//     happens on that same thread, since the window's WndProc is invoked by whichever message
//     pump owns it.
//   * No explicit teardown -- the window lives for the process lifetime. Windows reclaims it
//     on process exit.
//
// Threading:
//   * MessageReceived fires synchronously on the thread that owns the window (UI thread).
//     Subscribers that want to do slow work should marshal off; subscribers that need to update
//     UI can do so directly.
//   * Static subscriptions and the static WndProc delegate field are intentional -- there is
//     exactly one window per process and instance trampolining (GCHandle + GWL_USERDATA, as
//     TrayButtonNativeWindow uses) is overkill for a singleton.

public static class HiddenMessageWindow
{
    private const string WINDOW_CLASS_NAME = "Morphic-MessageWindow";

    private static readonly object s_initializeLock = new();
    private static bool s_isInitialized;
	
    private static ushort s_classAtom;
    private static Windows.Win32.Foundation.HWND s_hwnd;

    // Static reference to the WndProc delegate so it isn't GC'd while the window is alive.
    // The marshaller would otherwise collect the delegate after the RegisterClassEx call
    // returns, leaving Windows holding a dangling function pointer.
    private static Windows.Win32.UI.WindowsAndMessaging.WNDPROC? s_wndProcDelegate;

    public static event EventHandler<WindowMessageEventArgs>? MessageReceived;

    // Idempotent. Must be called from a thread with a running Win32 message pump (UI thread).
    // On success the singleton window exists and its WndProc will dispatch MessageReceived for
    // every Win32 message Windows delivers (including broadcasts like WM_SETTINGCHANGE).
    public static MorphicResult<MorphicUnit, MorphicUnit> Initialize()
    {
        lock (s_initializeLock)
        {
            if (s_isInitialized)
            {
                return MorphicResult.OkResult();
            }

            // Register the window class (once per process). The class atom is cached so future
            // re-entry of Initialize (after a hypothetical UnregisterClass) would skip this.
            if (s_classAtom == 0)
            {
                s_wndProcDelegate = HiddenMessageWindow.StaticWndProc;

                var hCursor = Windows.Win32.PInvoke.LoadCursor(Windows.Win32.Foundation.HINSTANCE.Null, Windows.Win32.PInvoke.IDC_ARROW);
                // NOTE: if hCursor returns null, that's not fatal: the window is invisible and won't paint a cursor

                ushort registerResult;
                unsafe
                {
                    fixed (char* pointerToClassName = HiddenMessageWindow.WINDOW_CLASS_NAME)
                    {
                        var wndClassEx = new Windows.Win32.UI.WindowsAndMessaging.WNDCLASSEXW
                        {
                            cbSize = (uint)Marshal.SizeOf<Windows.Win32.UI.WindowsAndMessaging.WNDCLASSEXW>(),
                            lpfnWndProc = s_wndProcDelegate,
                            lpszClassName = pointerToClassName,
                            hCursor = hCursor,
                        };
                        registerResult = Windows.Win32.PInvoke.RegisterClassEx(wndClassEx);
                    }
                }
                if (registerResult == 0)
                {
                    Debug.Assert(false, $"RegisterClassEx failed with win32 error {Marshal.GetLastWin32Error()}");
                    return MorphicResult.ErrorResult();
                }
                s_classAtom = registerResult;
            }

            // Create the window: top-level (no parent, no HWND_MESSAGE), invisible (no WS_VISIBLE),
            // size irrelevant since we never paint or show. The key requirement is "top-level
            // window in the current desktop" so Windows includes it in HWND_BROADCAST enumeration.
            Windows.Win32.Foundation.HWND handle;
            unsafe
            {
                var classNameAsAtomString = new Windows.Win32.Foundation.PCWSTR((char*)(nint)s_classAtom);
                fixed (char* pointerToWindowName = HiddenMessageWindow.WINDOW_CLASS_NAME)
                {
                    handle = Windows.Win32.PInvoke.CreateWindowEx(
                        dwExStyle: 0,
                        lpClassName: classNameAsAtomString,
                        lpWindowName: pointerToWindowName,
                        dwStyle: Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE.WS_OVERLAPPED, /* (Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE)0 */
                        X: 0,
                        Y: 0,
                        nWidth: 0,
                        nHeight: 0,
                        hWndParent: Windows.Win32.Foundation.HWND.Null,
                        hMenu: Windows.Win32.UI.WindowsAndMessaging.HMENU.Null,
                        hInstance: Windows.Win32.Foundation.HINSTANCE.Null,
                        lpParam: null);
                }
            }
            if (handle == Windows.Win32.Foundation.HWND.Null)
            {
                Debug.Assert(false, $"CreateWindowEx failed with win32 error {Marshal.GetLastWin32Error()}");
                return MorphicResult.ErrorResult();
            }
            s_hwnd = handle;
			
            s_isInitialized = true;
        }

        return MorphicResult.OkResult();
    }

    // Static WndProc. Called by the owning thread's message pump for every Win32 message
    // delivered to our window. We raise MessageReceived for ANY message (consumers filter by
    // Msg) and then delegate to DefWindowProc for default handling.
    //
    // NOTE: handlers run synchronously on this thread (the UI thread that owns the window), so
    // a slow handler blocks the message pump. Subscribers that need to do work should marshal
    // off. 
    private static Windows.Win32.Foundation.LRESULT StaticWndProc(Windows.Win32.Foundation.HWND hWnd, uint msg, Windows.Win32.Foundation.WPARAM wParam, Windows.Win32.Foundation.LPARAM lParam)
    {
        // handlersSnapshot ensures a concurrent subscribe/unsubscribe can't change the invocation list mid-iteration.
        var handlersSnapshot = HiddenMessageWindow.MessageReceived;
        if (handlersSnapshot is not null)
        {
            var eventArgs = new WindowMessageEventArgs(msg, (IntPtr)(nint)wParam.Value, (IntPtr)(nint)lParam.Value);
            foreach (EventHandler<WindowMessageEventArgs> handler in handlersSnapshot.GetInvocationList())
            {
                try
                {
                    handler.Invoke(null, eventArgs);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[HiddenMessageWindow] subscriber threw: {ex}");
                }
            }
        }

        // call through to DefWindowProc to handle the message
        return Windows.Win32.PInvoke.DefWindowProc(hWnd, msg, wParam, lParam);
    }
}
