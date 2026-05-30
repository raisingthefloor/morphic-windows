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

namespace Morphic.WindowsNative.Windowing;

// LastForegroundWindowTracker watches the system foreground window and remembers the most recent
// one that does NOT belong to our own process. Read Selected needs this: when the user clicks the
// Play button, the MorphicBar takes the foreground, so a system-wide "what is focused right now?"
// query would return the bar (which has no selectable text) instead of the document the user was
// reading. By recording the previous foreground window continuously, we can capture from the right
// window even after our own UI has stolen the foreground.
//
// Lifecycle:
//   * Construct on the UI thread. SetWinEventHook is installed with WINEVENT_OUTOFCONTEXT, which
//     delivers callbacks on the installing thread's message pump; the UI thread is the natural
//     choice because WinAppSDK guarantees it has a running pump.
//   * Dispose on the same (UI) thread when the app shuts down. Disposing the returned SafeHandle
//     calls UnhookWinEvent, after which no further callbacks arrive.
//
// Threading:
//   * The callback runs on the UI thread (see above). LastForegroundWindowHandle may be read from
//     any thread, so access to the stored handle is guarded by a lock.
public sealed class LastForegroundWindowTracker : System.IDisposable
{
    // The OS event hook. Disposing this SafeHandle calls UnhookWinEvent for us.
    private Windows.Win32.UnhookWinEventSafeHandle? _winEventHook;

    // Hold the callback delegate in a field so the GC cannot collect it while the native hook
    // still holds a function pointer to it (the same reason HiddenMessageWindow pins its WNDPROC).
    private readonly Windows.Win32.UI.Accessibility.WINEVENTPROC _winEventProcedure;

    // Foreground changes to windows owned by our own process are ignored, so that activating the
    // MorphicBar does not overwrite the user's previous (real) target window.
    private readonly uint _ownProcessId;

    private readonly object _stateLock = new();
    private System.IntPtr _lastForegroundWindowHandle;

    private bool _isDisposed;

    public unsafe LastForegroundWindowTracker()
    {
        _ownProcessId = (uint)System.Environment.ProcessId;
        _winEventProcedure = this.WinEventProcedure;

        // Seed with whatever is in the foreground right now (if it is not one of our own windows),
        // so the very first capture works even if no foreground change happens after construction.
        var initialForegroundWindowHandle = Windows.Win32.PInvoke.GetForegroundWindow();
        if (initialForegroundWindowHandle != Windows.Win32.Foundation.HWND.Null)
        {
            _ = Windows.Win32.PInvoke.GetWindowThreadProcessId(initialForegroundWindowHandle, out uint initialForegroundProcessId);
            if (initialForegroundProcessId != _ownProcessId)
            {
                _lastForegroundWindowHandle = (System.IntPtr)initialForegroundWindowHandle;
            }
        }

        // Install a system-wide hook (idProcess and idThread both 0) for foreground changes.
        _winEventHook = Windows.Win32.PInvoke.SetWinEventHook(
            eventMin: Windows.Win32.PInvoke.EVENT_SYSTEM_FOREGROUND,
            eventMax: Windows.Win32.PInvoke.EVENT_SYSTEM_FOREGROUND,
            hmodWinEventProc: null,
            pfnWinEventProc: _winEventProcedure,
            idProcess: 0,
            idThread: 0,
            dwFlags: Windows.Win32.PInvoke.WINEVENT_OUTOFCONTEXT);

        if (_winEventHook is null || _winEventHook.IsInvalid == true)
        {
            // Not fatal: the app still runs, but last-foreground-window tracking will be inactive
            // (Read Selected would fall back to whatever the seed value captured, if anything).
            System.Diagnostics.Debug.Assert(false, "SetWinEventHook failed to install the EVENT_SYSTEM_FOREGROUND hook; last-foreground-window tracking is inactive.");
        }
    }

    // The most recent foreground window that does not belong to our own process, or IntPtr.Zero if
    // none has been observed. Safe to read from any thread.
    public System.IntPtr LastForegroundWindowHandle
    {
        get
        {
            lock (_stateLock)
            {
                return _lastForegroundWindowHandle;
            }
        }
    }

    public void Dispose()
    {
        if (_isDisposed == true)
        {
            return;
        }
        _isDisposed = true;

        // Disposing the SafeHandle calls UnhookWinEvent (on this thread, which must be the same UI
        // thread that installed the hook). After this returns, no further callbacks will arrive.
        _winEventHook?.Dispose();
        _winEventHook = null;
    }

    private unsafe void WinEventProcedure(Windows.Win32.UI.Accessibility.HWINEVENTHOOK hWinEventHook, uint eventId, Windows.Win32.Foundation.HWND windowHandle, int idObject, int idChild, uint idEventThread, uint dwmsEventTime)
    {
        // We only registered for EVENT_SYSTEM_FOREGROUND, but confirm defensively.
        if (eventId != Windows.Win32.PInvoke.EVENT_SYSTEM_FOREGROUND)
        {
            return;
        }

        // A genuine top-level foreground change reports the window object itself (OBJID_WINDOW, 0)
        // and not a child (CHILDID_SELF, 0). Ignore anything else.
        const int OBJID_WINDOW = 0;
        const int CHILDID_SELF = 0;
        if (idObject != OBJID_WINDOW || idChild != CHILDID_SELF)
        {
            return;
        }

        if (windowHandle == Windows.Win32.Foundation.HWND.Null)
        {
            return;
        }

        // Ignore foreground changes to our own windows (e.g. the MorphicBar gaining the foreground
        // when the user clicks Read Selected), so we retain the user's previous target window.
        _ = Windows.Win32.PInvoke.GetWindowThreadProcessId(windowHandle, out uint windowProcessId);
        if (windowProcessId == _ownProcessId)
        {
            return;
        }

        lock (_stateLock)
        {
            _lastForegroundWindowHandle = (System.IntPtr)windowHandle;
        }
    }
}
