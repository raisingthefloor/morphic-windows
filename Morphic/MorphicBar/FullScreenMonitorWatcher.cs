// Copyright 2026 Raising the Floor - US, Inc.
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

using Morphic.Core;
using System;

namespace Morphic.MorphicBar;

// Watches for a foreground window going full-screen and reports WHICH monitor (if any) currently
// has a full-screen foreground window. Consumers compare that monitor against their own window's
// monitor to decide whether to step out of the way -- e.g., the MorphicBar drops its always-on-top
// z-order while a video is full-screen on the bar's own monitor.
//
// Detection uses two out-of-context WinEvent hooks (both delivered on the installing/UI thread):
//   * EVENT_SYSTEM_FOREGROUND     -- catches switching TO or away from an already-full-screen app.
//   * EVENT_OBJECT_LOCATIONCHANGE -- (filtered to the foreground top-level window) catches an app
//     toggling full-screen IN PLACE, e.g. the YouTube full-screen button, which resizes the same
//     foreground window WITHOUT a foreground change.
// Both feed one predicate (GetForegroundFullScreenMonitor). Results are coalesced onto the
// dispatcher and de-duplicated, so FullScreenMonitorChanged fires only on an actual transition.
internal sealed class FullScreenMonitorWatcher : IDisposable
{
    // OBJID_WINDOW (winuser.h): the EVENT_OBJECT_LOCATIONCHANGE idObject value identifying the
    // window itself (as opposed to a child UI element / caret). CsWin32 does not surface the
    // OBJID_* family, so the well-known constant is defined locally.
    private const int OBJID_WINDOW = 0;

    // Raised on the UI thread when the full-screen monitor changes. Carries the monitor that now
    // has a full-screen foreground window, or HMONITOR.Null when no monitor does.
    public event EventHandler<Windows.Win32.Graphics.Gdi.HMONITOR>? FullScreenMonitorChanged;

    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue;

    private Windows.Win32.UI.Accessibility.HWINEVENTHOOK _foregroundEventHook = Windows.Win32.UI.Accessibility.HWINEVENTHOOK.Null;
    private Windows.Win32.UI.Accessibility.HWINEVENTHOOK _locationChangeEventHook = Windows.Win32.UI.Accessibility.HWINEVENTHOOK.Null;
    // Keep the delegate instances alive for the lifetime of the hooks; otherwise the GC can collect
    // them out from under the native callback, crashing the .NET execution engine.
    private Windows.Win32.UI.Accessibility.WINEVENTPROC? _foregroundEventProc = null;
    private Windows.Win32.UI.Accessibility.WINEVENTPROC? _locationChangeEventProc = null;

    private Windows.Win32.Graphics.Gdi.HMONITOR _lastFullScreenMonitor = Windows.Win32.Graphics.Gdi.HMONITOR.Null;
    private bool _evaluationEnqueued = false;
    private bool _disposed = false;

    public FullScreenMonitorWatcher(Microsoft.UI.Dispatching.DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
    }

    // Installs the hooks and performs an initial evaluation (so a bar launched while an app is
    // already full-screen starts in the correct state). Must be called from the UI thread:
    // out-of-context hooks deliver on the installing thread, and FullScreenMonitorChanged is
    // raised from there. Idempotent. Returns an error if either hook could not be installed.
    public MorphicResult<MorphicUnit, MorphicUnit> Start()
    {
        if (_foregroundEventHook != Windows.Win32.UI.Accessibility.HWINEVENTHOOK.Null)
        {
            return MorphicResult.OkResult();
        }

        _foregroundEventProc = new Windows.Win32.UI.Accessibility.WINEVENTPROC(this.WindowEventProc);
        _foregroundEventHook = Windows.Win32.PInvoke.SetWinEventHook(
            Windows.Win32.PInvoke.EVENT_SYSTEM_FOREGROUND, // start index
            Windows.Win32.PInvoke.EVENT_SYSTEM_FOREGROUND, // end index
            Windows.Win32.Foundation.HMODULE.Null,
            _foregroundEventProc,
            0, // process (0 = all processes on current desktop)
            0, // thread (0 = all existing threads on current desktop)
            Windows.Win32.PInvoke.WINEVENT_OUTOFCONTEXT | Windows.Win32.PInvoke.WINEVENT_SKIPOWNPROCESS);
        if (_foregroundEventHook == Windows.Win32.UI.Accessibility.HWINEVENTHOOK.Null)
        {
            this.Dispose();
            return MorphicResult.ErrorResult();
        }

        _locationChangeEventProc = new Windows.Win32.UI.Accessibility.WINEVENTPROC(this.WindowEventProc);
        _locationChangeEventHook = Windows.Win32.PInvoke.SetWinEventHook(
            Windows.Win32.PInvoke.EVENT_OBJECT_LOCATIONCHANGE, // start index
            Windows.Win32.PInvoke.EVENT_OBJECT_LOCATIONCHANGE, // end index
            Windows.Win32.Foundation.HMODULE.Null,
            _locationChangeEventProc,
            0, // process (0 = all processes on current desktop)
            0, // thread (0 = all existing threads on current desktop)
            Windows.Win32.PInvoke.WINEVENT_OUTOFCONTEXT | Windows.Win32.PInvoke.WINEVENT_SKIPOWNPROCESS);
        if (_locationChangeEventHook == Windows.Win32.UI.Accessibility.HWINEVENTHOOK.Null)
        {
            this.Dispose();
            return MorphicResult.ErrorResult();
        }

        this.EvaluateAndRaise();

        return MorphicResult.OkResult();
    }

    private void WindowEventProc(Windows.Win32.UI.Accessibility.HWINEVENTHOOK hWinEventHook, uint eventType, Windows.Win32.Foundation.HWND hwnd, int idObject, int idChild, uint idEventThread, uint dwmsEventTime)
    {
        if (_disposed == true)
        {
            return;
        }

        // EVENT_OBJECT_LOCATIONCHANGE is a desktop-wide firehose (every object that moves or
        // resizes, in every other process). Collapse it to the only case that can change
        // full-screen state: the top-level FOREGROUND window resizing. Ignore non-window objects
        // (carets, child elements) and any window that isn't currently the foreground window.
        if (eventType == Windows.Win32.PInvoke.EVENT_OBJECT_LOCATIONCHANGE)
        {
            if (idObject != FullScreenMonitorWatcher.OBJID_WINDOW)
            {
                return;
            }
            if (hwnd != Windows.Win32.PInvoke.GetForegroundWindow())
            {
                return;
            }
        }

        // Coalesce bursts (a resize emits many LOCATIONCHANGEs) into a single trailing evaluation.
        if (_evaluationEnqueued == true)
        {
            return;
        }
        _evaluationEnqueued = true;
        _ = _dispatcherQueue.TryEnqueue(() =>
        {
            _evaluationEnqueued = false;
            this.EvaluateAndRaise();
        });
    }

    // Computes the monitor that currently has a full-screen foreground window (or HMONITOR.Null)
    // and raises FullScreenMonitorChanged only when that differs from the last reported value.
    private void EvaluateAndRaise()
    {
        if (_disposed == true)
        {
            return;
        }

        var fullScreenMonitor = FullScreenMonitorWatcher.GetForegroundFullScreenMonitor();
        if (fullScreenMonitor == _lastFullScreenMonitor)
        {
            return;
        }
        _lastFullScreenMonitor = fullScreenMonitor;
        this.FullScreenMonitorChanged?.Invoke(this, fullScreenMonitor);
    }

    // Returns the monitor of the foreground window when that window is full-screen (its rect covers
    // the monitor's full bounds), else HMONITOR.Null. The desktop and shell windows are excluded so
    // the wallpaper / Start surface are never mistaken for a full-screen app.
    private static Windows.Win32.Graphics.Gdi.HMONITOR GetForegroundFullScreenMonitor()
    {
        var foregroundWindow = Windows.Win32.PInvoke.GetForegroundWindow();
        if (foregroundWindow == Windows.Win32.Foundation.HWND.Null)
        {
            return Windows.Win32.Graphics.Gdi.HMONITOR.Null;
        }
        if (foregroundWindow == Windows.Win32.PInvoke.GetDesktopWindow() || foregroundWindow == Windows.Win32.PInvoke.GetShellWindow())
        {
            return Windows.Win32.Graphics.Gdi.HMONITOR.Null;
        }

        var monitor = Windows.Win32.PInvoke.MonitorFromWindow(foregroundWindow, Windows.Win32.Graphics.Gdi.MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONULL);
        if (monitor == Windows.Win32.Graphics.Gdi.HMONITOR.Null)
        {
            return Windows.Win32.Graphics.Gdi.HMONITOR.Null;
        }

        if (Windows.Win32.PInvoke.GetWindowRect(foregroundWindow, out var windowRect) == false)
        {
            return Windows.Win32.Graphics.Gdi.HMONITOR.Null;
        }

        var monitorInfo = new Windows.Win32.Graphics.Gdi.MONITORINFO
        {
            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Windows.Win32.Graphics.Gdi.MONITORINFO>(),
        };
        if (Windows.Win32.PInvoke.GetMonitorInfo(monitor, ref monitorInfo) == false)
        {
            return Windows.Win32.Graphics.Gdi.HMONITOR.Null;
        }

        // Full-screen means the window covers (or exceeds) the monitor's FULL bounds (rcMonitor),
        // not merely the work area. Comparing against rcMonitor (rather than rcWork) ensures a
        // merely-maximized window -- which leaves the taskbar visible, so its rect equals the work
        // area -- is NOT treated as full-screen.
        var monitorRect = monitorInfo.rcMonitor;
        bool coversWholeMonitor =
            (windowRect.left <= monitorRect.left) &&
            (windowRect.top <= monitorRect.top) &&
            (windowRect.right >= monitorRect.right) &&
            (windowRect.bottom >= monitorRect.bottom);

        return coversWholeMonitor ? monitor : Windows.Win32.Graphics.Gdi.HMONITOR.Null;
    }

    public void Dispose()
    {
        if (_disposed == true)
        {
            return;
        }
        _disposed = true;

        if (_foregroundEventHook != Windows.Win32.UI.Accessibility.HWINEVENTHOOK.Null)
        {
            _ = Windows.Win32.PInvoke.UnhookWinEvent(_foregroundEventHook);
            _foregroundEventHook = Windows.Win32.UI.Accessibility.HWINEVENTHOOK.Null;
        }
        if (_locationChangeEventHook != Windows.Win32.UI.Accessibility.HWINEVENTHOOK.Null)
        {
            _ = Windows.Win32.PInvoke.UnhookWinEvent(_locationChangeEventHook);
            _locationChangeEventHook = Windows.Win32.UI.Accessibility.HWINEVENTHOOK.Null;
        }
        _foregroundEventProc = null;
        _locationChangeEventProc = null;
    }
}
