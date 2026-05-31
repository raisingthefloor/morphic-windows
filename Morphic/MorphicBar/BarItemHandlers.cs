// Copyright 2020-2026 Raising the Floor - US, Inc.
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
using System.Diagnostics;
using System.Threading.Tasks;

namespace Morphic.MorphicBar;

internal class BarItemHandlers
{
    // text size

    private static async Task<MorphicResult<MorphicUnit, MorphicUnit>> StepBarDisplayDpiOffsetAsync(string? actionTag, int step)
    {
        var barManager = ((App)Microsoft.UI.Xaml.Application.Current).MorphicBarManager;
        if (barManager is null)
        {
            return MorphicResult.ErrorResult();
        }
        var barHwnd = barManager.GetBarWindowHandle();
        if (barHwnd == IntPtr.Zero)
        {
            return MorphicResult.ErrorResult();
        }

        var displayResult = Morphic.WindowsNative.Display.Display.GetDisplayNearestWindowHandle(barHwnd);
        if (displayResult.IsError)
        {
            return MorphicResult.ErrorResult();
        }
        var display = displayResult.Value!;

        var rangeResult = display.GetCurrentDpiOffsetAndRange();
        if (rangeResult.IsError)
        {
            return MorphicResult.ErrorResult();
        }
        var range = rangeResult.Value;

        if (Morphic.WindowsNative.Display.Display.IsCustomScalingPercentage(range.CurrentDpiOffset))
        {
            return MorphicResult.ErrorResult();
        }

        var newDpiOffset = range.CurrentDpiOffset + step;
        if (newDpiOffset < range.MinimumDpiOffset || newDpiOffset > range.MaximumDpiOffset)
        {
            return MorphicResult.ErrorResult();
        }

        // Subscribe to the next rasterization-scale change BEFORE we kick off the DPI change so we
        // don't race the event (it could fire between SetDpiOffsetAsync returning and us starting
        // the await). The helper subscribes synchronously up to its first internal await, so by
        // the time WaitForBarRasterizationScaleChangeAsync returns its Task the listener
        // is already in place.
        //
        // Why we hold the in-progress visual until the scale change is observed: without this
        // wait, the click feels broken at higher zoom levels. SetDpiOffsetAsync returns quickly
        // (it's just a configuration write + verify), but WinUI takes a few hundred ms to
        // re-rasterize every window at the new scale. The user clicks +, sees the in-progress
        // visual stop, then sees the bar (and everything else) finally pop to the new scale half
        // a second later -- which reads as the click being "stuck". By holding the in-progress
        // visual until the rasterization signal arrives the click feels synchronous.
        //
        // Timeout (5 s) is a fallback for the unusual case where the DPI write succeeded but no
        // visible scale change occurred (e.g. some other client raced and reverted the change, or
        // the change was applied to a monitor the bar isn't on). The handler still returns success
        // -- the DPI write itself did succeed -- the wait is purely UX feedback.
        var rasterizationChangeWait = barManager.WaitForBarRasterizationScaleChangeAsync(TimeSpan.FromSeconds(5));

        // The rasterization-scale change causes WinUI to relayout the bar, which drops the focused
        // element. Snapshot focus before the DPI write and restore it after the relayout settles
        // so a keyboard user stays on the +/- button they just pressed (alternative: focus
        // disappears and Alt+Tab still treats the bar as activated -- a confusing state).
        var focusSnapshot = barManager.CaptureBarFocus();

        // Windows recenters the mouse cursor toward the primary monitor when a display's scaling
        // changes. Snapshot the cursor now so we can put it back on this display (where the user
        // clicked the +/- button) after the change settles. A DPI-scale change does not move the
        // monitor's physical pixel rect, so the original physical position stays valid; the clamp in
        // the finally is purely a safety net.
        var cursorPositionSnapshot = Morphic.WindowsNative.Mouse.Mouse.GetCurrentPosition();

        // If the mouse was OVER THE BAR when +/- was pressed (i.e. the user clicked the button with
        // the mouse), remember WHERE on the bar it was, as a normalized [0,1] proportion, so we can
        // keep the cursor over the same button after the bar resizes for the new scale (otherwise a
        // user zooming several steps loses the button out from under the pointer). If the cursor was
        // NOT over the bar (e.g. keyboard/space activation), these stay null and the finally just
        // restores the cursor to its display-relative position.
        double? cursorBarProportionX = null;
        double? cursorBarProportionY = null;
        if (cursorPositionSnapshot.IsSuccess == true
            && Windows.Win32.PInvoke.GetWindowRect((Windows.Win32.Foundation.HWND)barHwnd, out var barRectangleBeforeResize) == true)
        {
            var cursorBeforeResize = cursorPositionSnapshot.Value!;
            var barWidthBeforeResize = barRectangleBeforeResize.right - barRectangleBeforeResize.left;
            var barHeightBeforeResize = barRectangleBeforeResize.bottom - barRectangleBeforeResize.top;
            bool cursorWasOverBar = barWidthBeforeResize > 0 && barHeightBeforeResize > 0
                && cursorBeforeResize.X >= barRectangleBeforeResize.left && cursorBeforeResize.X < barRectangleBeforeResize.right
                && cursorBeforeResize.Y >= barRectangleBeforeResize.top && cursorBeforeResize.Y < barRectangleBeforeResize.bottom;
            if (cursorWasOverBar == true)
            {
                cursorBarProportionX = (double)(cursorBeforeResize.X - barRectangleBeforeResize.left) / barWidthBeforeResize;
                cursorBarProportionY = (double)(cursorBeforeResize.Y - barRectangleBeforeResize.top) / barHeightBeforeResize;
            }
        }

        try
        {
            // SetDpiOffsetAsync wraps the SPI call in Task.Run internally, so this is already off
            // the UI thread; no extra .ConfigureAwait dance needed.
            var setResult = await display.SetDpiOffsetAsync(newDpiOffset);
            if (setResult.IsError)
            {
                return MorphicResult.ErrorResult();
            }

            await rasterizationChangeWait;
            return MorphicResult.OkResult();
        }
        finally
        {
            barManager.RestoreBarFocus(focusSnapshot);

            // Put the cursor back (Windows recenters it toward the primary monitor when a display's
            // scaling changes). Done at the same settle point as focus -- after rasterizationChangeWait,
            // so the bar has finished resizing and Windows' recenter has already happened.
            if (cursorBarProportionX is double proportionX && cursorBarProportionY is double proportionY
                && Windows.Win32.PInvoke.GetWindowRect((Windows.Win32.Foundation.HWND)barHwnd, out var barRectangleAfterResize) == true)
            {
                // Mouse was over the bar: keep it over the SAME normalized point on the bar's NEW
                // (resized + re-docked) rect, so it stays on the +/- button across repeated presses.
                // Clamp inside the bar so rounding cannot nudge the cursor just off its edge.
                var barWidthAfterResize = barRectangleAfterResize.right - barRectangleAfterResize.left;
                var barHeightAfterResize = barRectangleAfterResize.bottom - barRectangleAfterResize.top;
                var targetCursorX = System.Math.Clamp(
                    barRectangleAfterResize.left + (int)System.Math.Round(proportionX * barWidthAfterResize),
                    barRectangleAfterResize.left, barRectangleAfterResize.right - 1);
                var targetCursorY = System.Math.Clamp(
                    barRectangleAfterResize.top + (int)System.Math.Round(proportionY * barHeightAfterResize),
                    barRectangleAfterResize.top, barRectangleAfterResize.bottom - 1);
                _ = Morphic.WindowsNative.Mouse.Mouse.MoveCursorToPosition(new System.Drawing.Point(targetCursorX, targetCursorY));
            }
            else if (cursorPositionSnapshot.IsSuccess == true)
            {
                // Mouse was NOT over the bar (keyboard/space activation, or the rect read failed):
                // restore the cursor to where it was on its display. The display's physical rect does
                // not move on a DPI-scale change, so the clamp is normally a no-op safety net.
                var restoreCursorPosition = cursorPositionSnapshot.Value!;
                var displayRectangleResult = display.GetDisplayRectangleInPixels();
                if (displayRectangleResult.IsSuccess == true)
                {
                    var displayRectangle = displayRectangleResult.Value!;
                    restoreCursorPosition = new System.Drawing.Point(
                        System.Math.Clamp(restoreCursorPosition.X, displayRectangle.Left, displayRectangle.Right - 1),
                        System.Math.Clamp(restoreCursorPosition.Y, displayRectangle.Top, displayRectangle.Bottom - 1));
                }
                _ = Morphic.WindowsNative.Mouse.Mouse.MoveCursorToPosition(restoreCursorPosition);
            }
        }
    }

    public static Task<MorphicResult<MorphicUnit, MorphicUnit>> IncreaseTextSizeButtonAction(string? actionTag, bool? isChecked)
    {
        return BarItemHandlers.StepBarDisplayDpiOffsetAsync(actionTag, +1);
    }

    public static Task<MorphicResult<MorphicUnit, MorphicUnit>> DecreaseTextSizeButtonAction(string? actionTag, bool? isChecked)
    {
        return BarItemHandlers.StepBarDisplayDpiOffsetAsync(actionTag, -1);
    }

    //

    // magnifier

    public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> ShowMagnifierButtonAction(string? actionTag, bool? isChecked)
    {
        // if the magnifier is already visible, do nothing (treat as success -- requested end-state is satisfied)
        var isMagnifierActiveResult = await Morphic.WindowsNative.Magnifier.Magnifier.IsMagnifierActiveAsync();
        if (isMagnifierActiveResult.IsError)
        {
            return MorphicResult.ErrorResult();
        }
        var isMagnifierActive = isMagnifierActiveResult.Value!;
        if (isMagnifierActive == true)
        {
            return MorphicResult.OkResult();
        }


        // step 1: ensure the magnifier will come up in lens mode

        // If the magnifier is in another mode, this switches it to lens and remembers the prior mode so we can restore it after hiding. If lens mode cannot be guaranteed, we must not re-center the cursor (re-centering only makes sense for the cursor-following lens).
        var ensureLensModeForShowResult = Morphic.WindowsNative.Magnifier.Magnifier.EnsureLensModeForShow();
        var shouldRecenterCursor = ensureLensModeForShowResult.IsSuccess;


        // step 2: re-center the mouse cursor (best-effort)

        // move the cursor to the center of the display it currently sits on, so the lens appears centered when the magnifier comes up
        if (shouldRecenterCursor == true)
        {
            _ = BarItemHandlers.RecenterCursorOnCurrentDisplay();
        }


        // step 3: show the magnifier

        // The SystemSettings calls can block before their first async yield, so keep them off the UI thread; .ConfigureAwait(false) also tells C# not to try to resume execution on the original thread
        var showMagnifierSucceeded = await Task.Run(async () =>
        {
            var showResult = await Morphic.WindowsNative.Magnifier.Magnifier.ShowMagnifierAsync().ConfigureAwait(false);
            return showResult.IsSuccess;
        });

        Debug.WriteLine($"[BarItem] {actionTag}: show -> {(showMagnifierSucceeded ? "ok" : "error")}");

        if (showMagnifierSucceeded == false)
        {
            // the magnifier never came up, so undo any lens-mode switch we made in preparation for it
            Morphic.WindowsNative.Magnifier.Magnifier.RestoreModePriorToShowIfNeeded();
            return MorphicResult.ErrorResult();
        }

        // The magnifier is up. Arm a watch so that if it is closed by any path other than our Hide button (its own X, Win+Esc, Settings toggle), we still restore the pre-show mode.
        BarItemHandlers.BeginWatchingForMagnifierExternalClose();
        return MorphicResult.OkResult();
    }

    // Best-effort re-centering of the mouse cursor within the display it currently sits on. A failure here must not prevent the magnifier from being shown.
    private static MorphicResult<MorphicUnit, MorphicUnit> RecenterCursorOnCurrentDisplay()
    {
        var getCurrentPositionResult = Morphic.WindowsNative.Mouse.Mouse.GetCurrentPosition();
        if (getCurrentPositionResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        var currentMousePosition = getCurrentPositionResult.Value!;

        var getDisplayAtPointResult = Morphic.WindowsNative.Display.Display.GetDisplayAtPoint(currentMousePosition);
        if (getDisplayAtPointResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        var targetDisplay = getDisplayAtPointResult.Value!;

        var moveCursorToCenterOfDisplayResult = Morphic.WindowsNative.Mouse.Mouse.MoveCursorToCenterOfDisplay(targetDisplay);
        if (moveCursorToCenterOfDisplayResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }

        return MorphicResult.OkResult();
    }

    public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> HideMagnifierButtonAction(string? actionTag, bool? isChecked)
    {
        // Stop the external-close watch before we close the magnifier ourselves, so our own close does not trip the watch into a duplicate restore. We perform the restore explicitly below.
        BarItemHandlers.StopWatchingForMagnifierExternalClose();

        // The SystemSettings calls can block before their first async yield, so keep them off the UI thread; .ConfigureAwait(false) also tells C# not to try to resume execution on the original thread
        var hideMagnifierSucceeded = await Task.Run(async () =>
        {
            var hideResult = await Morphic.WindowsNative.Magnifier.Magnifier.HideMagnifierAsync().ConfigureAwait(false);
            return hideResult.IsSuccess;
        });

        Debug.WriteLine($"[BarItem] {actionTag}: hide -> {(hideMagnifierSucceeded ? "ok" : "error")}");

        if (hideMagnifierSucceeded == false)
        {
            // The magnifier is probably still up; re-arm the external-close watch we stopped above.
            BarItemHandlers.BeginWatchingForMagnifierExternalClose();
            return MorphicResult.ErrorResult();
        }

        // now that the magnifier is hidden, restore the mode the user had before we switched it to lens (unless the user switched it away from lens themselves while it was running)
        Morphic.WindowsNative.Magnifier.Magnifier.RestoreModePriorToShowIfNeeded();

        return MorphicResult.OkResult();
    }

    // magnifier external-close watch
    //
    // The bar's Hide button is not the only way the magnifier goes away: the user can close it from its own
    // floating toolbar (the X), press Win+Esc, or toggle it off in Settings. All of those manifest as the
    // Magnify.exe process exiting. We watch for that exit so we can run the same pre-show mode-restore we would
    // run on our own Hide. This mirrors the single-process watch the snip handler uses (GetProcessesByName is a
    // cross-integrity-level-safe system snapshot -- no per-process OpenProcess handle that UIPI could deny).

    // Magnify.exe -> process name "Magnify".
    private static readonly string s_magnifierProcessName = "Magnify";

    // After we ask Windows to show the magnifier, Magnify.exe takes a moment to appear. Wait up to this long for
    // it to show before we start treating "no Magnify process" as "it exited"; otherwise the watch would fire on
    // the launch gap and wrongly restore the mode.
    private static readonly TimeSpan s_magnifierAppearanceGracePeriod = TimeSpan.FromSeconds(5);

    // How often we poll. The restore is invisible to the user (it only affects the NEXT show), so a relaxed
    // interval keeps the background cost negligible.
    private static readonly TimeSpan s_magnifierExitPollInterval = TimeSpan.FromMilliseconds(500);

    // Cancels the in-flight exit watch. Non-null only while a watch is armed.
    private static System.Threading.CancellationTokenSource? _magnifierExitWatchCancellationSource = null;

    // Arms a background watch that restores the pre-show magnifier mode if the magnifier is closed by any path
    // other than our own Hide button. Cancels any prior watch first, so at most one is ever armed.
    private static void BeginWatchingForMagnifierExternalClose()
    {
        BarItemHandlers.StopWatchingForMagnifierExternalClose();

        var cancellationSource = new System.Threading.CancellationTokenSource();
        _magnifierExitWatchCancellationSource = cancellationSource;
        _ = Task.Run(() => BarItemHandlers.WatchForMagnifierExternalCloseAsync(cancellationSource.Token));
    }

    // Disarms the exit watch (used by our own Hide path, which performs the restore itself). Safe to call when no
    // watch is armed.
    private static void StopWatchingForMagnifierExternalClose()
    {
        var cancellationSource = _magnifierExitWatchCancellationSource;
        _magnifierExitWatchCancellationSource = null;
        if (cancellationSource is not null)
        {
            cancellationSource.Cancel();
            cancellationSource.Dispose();
        }
    }

    private static async Task WatchForMagnifierExternalCloseAsync(System.Threading.CancellationToken cancellationToken)
    {
        try
        {
            // Phase 1: wait (bounded) for Magnify.exe to actually appear after we asked Windows to show it. If it
            // never appears within the grace period, the magnifier did not come up; exit quietly, no restore.
            var appearanceStopwatch = System.Diagnostics.Stopwatch.StartNew();
            bool magnifierAppeared = false;
            while (appearanceStopwatch.Elapsed < s_magnifierAppearanceGracePeriod)
            {
                if (BarItemHandlers.MagnifierProcessIsRunning() == true)
                {
                    magnifierAppeared = true;
                    break;
                }
                await Task.Delay(s_magnifierExitPollInterval, cancellationToken);
            }

            if (magnifierAppeared == false)
            {
                return;
            }

            // Phase 2: the magnifier is up; wait for Magnify.exe to disappear. When it does, the user closed it by
            // some path other than our Hide button (which cancels this watch before closing), so we restore here.
            while (BarItemHandlers.MagnifierProcessIsRunning() == true)
            {
                await Task.Delay(s_magnifierExitPollInterval, cancellationToken);
            }

            Morphic.WindowsNative.Magnifier.Magnifier.RestoreModePriorToShowIfNeeded();
        }
        catch (OperationCanceledException)
        {
            // Our own Hide path cancelled the watch; it restores the mode itself, so do nothing here.
        }
    }

    private static bool MagnifierProcessIsRunning()
    {
        var magnifierProcesses = Process.GetProcessesByName(s_magnifierProcessName);
        try
        {
            return magnifierProcesses.Length > 0;
        }
        finally
        {
            foreach (var magnifierProcess in magnifierProcesses)
            {
                magnifierProcess.Dispose();
            }
        }
    }

    //

    // snip + copy

    // Process names the snip overlay runs under across Windows versions / builds.
    // - ScreenClippingHost: older Win10/early-Win11 overlay host
    // - ScreenSketch: original modern Snip & Sketch app
    // - SnippingTool: redesigned Windows 11 22H2+ Snipping Tool
    // Add new candidates here if the diagnostic log shows snip running under a name we
    // haven't seen.
    private static readonly string[] s_snipOverlayProcessNames = new[] { "ScreenClippingHost", "ScreenSketch", "SnippingTool" };

    // How long we wait (after launching ms-screenclip:) for the snip overlay process to
    // appear. If it doesn't, snip failed to launch and we return Error. Generous enough
    // to cover a cold-start of the snip tool on slower systems (first invocation since
    // boot can be slow); the user only sees the bar restored after this window if the
    // launch genuinely failed.
    private static readonly TimeSpan s_snipLaunchGracePeriod = TimeSpan.FromSeconds(5);

    // Polling interval for the foreground-window check (the only condition we can't get
    // an event-driven signal for; the process exit uses WaitForExitAsync).
    private static readonly TimeSpan s_foregroundPollInterval = TimeSpan.FromMilliseconds(200);

    // Number of CONSECUTIVE "foreground is not a snip process" polls required before we
    // restore the bar. Avoids early restoration during the brief transitions that happen
    // as the snip tool spins up its overlay (foreground briefly bounces through the
    // previous app / desktop before settling on the overlay). At 200 ms / poll, 3
    // samples = 600 ms which is plenty to ride out those transitions but still feels
    // snappy when the user genuinely moves on to another app.
    private const int s_foregroundLeftSnipConsecutiveSamplesRequired = 3;

    public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> SnipCopyButtonAction(string? actionTag, bool? isChecked)
    {
        bool snipLaunched = false;

        var barManager = ((App)Microsoft.UI.Xaml.Application.Current).MorphicBarManager;
        if (barManager is null)
        {
            return MorphicResult.ErrorResult();
        }

        await barManager.RunWithBarHiddenAsync(async () =>
        {
            // Open the Windows Screen Snipping tool overlay via its shell URI scheme.
            // Simulating Win+Shift+S via keystroke injection does NOT work when Morphic
            // is run with uiAccess=true (the uiAccess context bypasses the snipping
            // tool's hotkey handler); the ms-screenclip: shell URI is the path that
            // works in all configurations. With UseShellExecute=true on a URI handler,
            // Process.Start typically returns null (the shell, not the target), and on
            // Win11 22H2+ the snip tool is a singleton -- ms-screenclip: may just signal
            // an EXISTING SnippingTool.exe rather than spawning a new one. So we can't
            // rely on a specific PID; instead we watch whether the foreground window is
            // owned by ANY known snip-tool process.
            try
            {
                _ = Process.Start(new ProcessStartInfo("ms-screenclip:") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BarItem] {actionTag}: Process.Start(ms-screenclip:) failed: {ex.Message}");
                return;
            }

            // Wait up to s_snipLaunchGracePeriod for the snip tool to put a visible
            // top-level window on screen. If it never does, snip failed to launch and we
            // return -- bar restored by the App helper, return value stays Error.
            var graceDeadline = DateTime.UtcNow + s_snipLaunchGracePeriod;
            while (DateTime.UtcNow < graceDeadline)
            {
                if (IsAnyVisibleSnipToolWindow())
                {
                    snipLaunched = true;
                    break;
                }
                await Task.Delay(100);
            }

            if (snipLaunched == false)
            {
                Debug.WriteLine($"[BarItem] {actionTag}: snip tool did not put a visible window on screen within {s_snipLaunchGracePeriod.TotalSeconds}s.");
                return;
            }

            // Snip is in progress. Keep the bar hidden as long as ANY snip-tool process
            // has a visible top-level window on screen. We check visibility rather than
            // foreground because the snip tool's video-recording controls stay visible
            // (with WDA_EXCLUDEFROMCAPTURE applied so they don't appear in the recording)
            // but don't hold the foreground, and the user is still mid-flow there.
            // When the user finally dismisses all snip-tool windows, the bar restores.
            //
            // Hysteresis: require several consecutive "no visible snip window" polls
            // before we restore. The snip tool transitions between modes (overlay ->
            // toast -> editor -> recording controls) can briefly leave a gap where no
            // snip window is visible mid-transition. A few consecutive samples (~600 ms
            // total) confirms the user is truly done.
            //
            // No max timeout: the user might be editing or recording for a long time;
            // we don't want to pop the bar back into the middle of that.
            int consecutiveNoVisibleSnipSamples = 0;
            while (true)
            {
                await Task.Delay(s_foregroundPollInterval);
                if (IsAnyVisibleSnipToolWindow())
                {
                    consecutiveNoVisibleSnipSamples = 0;
                }
                else
                {
                    consecutiveNoVisibleSnipSamples++;
                    if (consecutiveNoVisibleSnipSamples >= s_foregroundLeftSnipConsecutiveSamplesRequired)
                    {
                        break;
                    }
                }
            }
        });

        return snipLaunched ? MorphicResult.OkResult() : MorphicResult.ErrorResult();
    }

    // Returns true if any top-level visible AND always-on-top window on screen is owned
    // by any process whose name matches a known snip-tool process name. Used as the
    // "is snip still actively in progress" check.
    //
    // Why visible + always-on-top (rather than just visible):
    //   * The snip overlay and the video-recording controls bar are WS_EX_TOPMOST --
    //     they're floating affordances that always need to be above everything else.
    //     If one of those is on screen, the user is still mid-snip-flow.
    //   * The snip tool's post-recording preview window and the post-snip editor are
    //     ordinary (non-AOT) windows. From the user's perspective the snip is "done"
    //     at that point -- they're reviewing the result. So we should NOT keep the
    //     MorphicBar hidden during those.
    // The AOT filter cleanly distinguishes those two cases.
    private static bool IsAnyVisibleSnipToolWindow()
    {
        // Snapshot snip-tool PIDs once; the EnumWindows callback only does a set lookup.
        var snipPids = new System.Collections.Generic.HashSet<uint>();
        foreach (var name in s_snipOverlayProcessNames)
        {
            var processes = Process.GetProcessesByName(name);
            foreach (var p in processes)
            {
                try { snipPids.Add((uint)p.Id); }
                catch { /* process exited */ }
                finally { p.Dispose(); }
            }
        }
        if (snipPids.Count == 0) { return false; }

        bool found = false;
        Windows.Win32.PInvoke.EnumWindows((hwnd, lParam) =>
        {
            if (Windows.Win32.PInvoke.IsWindowVisible(hwnd) == false)
            {
                return true; // continue enumeration
            }
            var exStyle = (Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE)Windows.Win32.PInvoke.GetWindowLongPtr(hwnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
            if ((exStyle & Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE.WS_EX_TOPMOST) == 0)
            {
                return true; // not always-on-top -- this isn't an in-progress snip window
            }
            uint pid = 0;
            unsafe { _ = Windows.Win32.PInvoke.GetWindowThreadProcessId(hwnd, &pid); }
            if (pid != 0 && snipPids.Contains(pid))
            {
                found = true;
                return false; // stop enumeration; we found what we needed
            }
            return true;
        }, IntPtr.Zero);

        return found;
    }

    //

    // read selected

    // Play action for Read Selected. Dispatches into the App-owned ReadAloudController, which
    // captures the text selected in the user's previous foreground window (the one focused before
    // they reached the bar) and speaks it. The capture target is ALWAYS that prior window (resolved
    // by the foreground-window tracker), regardless of how Play was invoked.
    //
    // Where focus LANDS afterward is modality-aware (read from BarButtonInvocationContext, which the
    // shared click dispatcher set for this click):
    //   * keyboard / assistive-technology invocation -> stay on the bar (Play keeps focus, so the
    //     user can Tab to Stop or press Space again to restart), and do NOT synthesize input;
    //   * mouse / touch / pen invocation -> return the foreground to the user's prior window once
    //     capture is done, so their caret and selection are where they left them.
    public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> ReadSelectedPlayButtonActionAsync(string? actionTag, bool? isChecked)
    {
        bool invokedViaKeyboard = Morphic.MorphicBar.BarControls.BarButtonInvocationContext.InvokedViaKeyboard;
        var readAloudController = ((App)Microsoft.UI.Xaml.Application.Current).ReadAloudController;
        if (readAloudController is null)
        {
            return MorphicResult.ErrorResult();
        }

        await readAloudController.PlayAsync(invokedViaKeyboard);
        return MorphicResult.OkResult();
    }

    // Stop action for Read Selected. Stops any in-progress speech (and cancels an utterance that is
    // still being prepared). A no-op if nothing is currently being read. Focus-landing mirrors Play:
    // a keyboard / assistive-technology invocation stays on the bar; a mouse / touch / pen
    // invocation returns the foreground to the user's prior window.
    public static Task<MorphicResult<MorphicUnit, MorphicUnit>> ReadSelectedStopButtonAction(string? actionTag, bool? isChecked)
    {
        bool invokedViaKeyboard = Morphic.MorphicBar.BarControls.BarButtonInvocationContext.InvokedViaKeyboard;
        var readAloudController = ((App)Microsoft.UI.Xaml.Application.Current).ReadAloudController;
        readAloudController?.Stop(invokedViaKeyboard);
        return Task.FromResult<MorphicResult<MorphicUnit, MorphicUnit>>(MorphicResult.OkResult());
    }

    //

    // contrast and color buttons

    public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> ContrastButtonAction(string? actionTag, bool? isChecked)
    {
        if (isChecked is null)
        {
            // non-toggle invocation makes no sense for high contrast -- treat as error so caller doesn't pretend it took effect
            return MorphicResult.ErrorResult();
        }

        var enableHighContrast = isChecked.Value;

        // HighContrast.SetIsOn is a synchronous SPI call (it broadcasts WM_SETTINGCHANGE inline,
        // which can block briefly); wrap in Task.Run so the click handler stays off the UI thread.
        var highContrastSucceeded = await Task.Run(() =>
        {
            var setResult = Morphic.WindowsNative.Theme.HighContrast.SetIsOn(enableHighContrast);
            return setResult.IsSuccess;
        });

        return highContrastSucceeded ? MorphicResult.OkResult() : MorphicResult.ErrorResult();
    }

    public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> DarkButtonAction(string? actionTag, bool? isChecked)
    {
        if (isChecked is null)
        {
            // non-toggle invocation makes no sense for dark-mode -- treat as error so caller doesn't pretend it took effect
            return MorphicResult.ErrorResult();
        }

        var useDarkMode = isChecked.Value;

        // The SystemSettings calls can block before their first async yield, so keep them off the UI thread; .ConfigureAwait(false) also tells C# not to try to resume execution on the original thread.
        // Run all three calls and report overall success only if every step succeeded; partial-failure cases (e.g. system flag flipped but broadcast failed) are treated as failure so the toggle reverts.
        var darkModeSucceeded = await Task.Run(async () =>
        {
            // update the two dark states first, and then send the broadcast ONCE for efficiency
            var systemResult = await Morphic.WindowsNative.Theme.DarkMode.SetSystemUsesDarkModeAsync(useDarkMode).ConfigureAwait(false);
            if (systemResult.IsError) { return false; }
            var appsResult = await Morphic.WindowsNative.Theme.DarkMode.SetAppsUseDarkModeAsync(useDarkMode).ConfigureAwait(false);
            if (appsResult.IsError) { return false; }
            //
            // broadcast the change to all apps
            var broadcastResult = await Morphic.WindowsNative.Theme.DarkMode.BroadcastChangeMessageAsync().ConfigureAwait(false);
            if (broadcastResult.IsError) { return false; }

            return true; // set operation succeeded
        });

        return darkModeSucceeded ? MorphicResult.OkResult() : MorphicResult.ErrorResult();
    }

    public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> ColorButtonAction(string? actionTag, bool? isChecked)
    {
        if (isChecked is null)
        {
            // non-toggle invocation makes no sense for color filtering -- treat as error so caller doesn't pretend it took effect
            return MorphicResult.ErrorResult();
        }

        var enableColorFiltering = isChecked.Value;

        // The SystemSettings call can block before its first async yield, so keep it off the UI thread; .ConfigureAwait(false) also tells C# not to try to resume execution on the original thread.
        var colorFilteringSucceeded = await Task.Run(async () =>
        {
            var setResult = await Morphic.WindowsNative.Display.ColorFilters.SetIsActiveAsync(enableColorFiltering).ConfigureAwait(false);
            return setResult.IsSuccess;
        });

        return colorFilteringSucceeded ? MorphicResult.OkResult() : MorphicResult.ErrorResult();
    }

    public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> NightButtonAction(string? actionTag, bool? isChecked)
    {
        if (isChecked is null)
        {
            // non-toggle invocation makes no sense for night light -- treat as error so caller doesn't pretend it took effect
            return MorphicResult.ErrorResult();
        }

        var enableNightLight = isChecked.Value;

        // The SystemSettings call can block before its first async yield, so keep it off the UI thread; 
        // .ConfigureAwait(false) also tells C# not to try to resume execution on the original thread.
        // NOTE A 5-second timeout covers the edge case where the user clicks before the factory's
        // startup prime (BarItemDataFactory.CreateContrastColorButtonGroup) has finished settling the
        // SettingItem's IsEnabled flag.
        var nightLightSucceeded = await Task.Run(async () =>
        {
            var setResult = await Morphic.WindowsNative.Display.NightLight.SetIsOnAsync(enableNightLight, TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            return setResult.IsSuccess;
        });

        return nightLightSucceeded ? MorphicResult.OkResult() : MorphicResult.ErrorResult();
    }
}
