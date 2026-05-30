// Copyright 2022-2026 Raising the Floor - US, Inc.
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

namespace Morphic.WindowsNative.Speech;

// SelectionReader is the multi-tier "read the user's currently-selected text" orchestrator. It runs four
// capture strategies in order of least-to-most disruptive and returns the first non-empty result:
//
//   Strategy A (UI Automation):   non-destructive; queries the focused control / subtree for a selection.
//   Strategy B (Edit messages):   non-destructive; EM_GETSEL + WM_GETTEXT against a classic Edit/RichEdit control.
//   Strategy C (WM_COPY):         disturbs the clipboard; asks the focused control to copy, then reads + restores.
//   Strategy D (synthesized copy): disturbs the clipboard AND the foreground; activates the target window and
//                                  synthesizes Ctrl+C, then reads + restores.
//
// Tiers C and D disturb the clipboard, so before triggering a copy they snapshot every HGLOBAL-backed
// clipboard format (text, images via CF_DIB/CF_DIBV5, file lists, HTML, RTF, custom formats, ...) and
// restore it afterward. Some content cannot be byte-captured: a metafile, an owner-display format, or a
// palette (CF_PALETTE, a GDI handle). Rather than copy-then-restore and silently DROP such content, the
// snapshot is marked incomplete and the clipboard tier is SKIPPED, leaving the user's clipboard untouched
// and reporting ClipboardProtected.
// Our restoration of the original clipboard is marked CanIncludeInClipboardHistory=0 and
// CanUploadToCloudClipboard=0, so putting the user's content back does NOT create a fresh Win+V history
// entry or a cloud upload. The one thing still not suppressed is the target app's OWN transient copy in
// tiers C/D (it writes to its own clipboard session, which we do not own), so that copy may briefly enter
// clipboard history.
//
// The orchestrator is synchronous (it is meant to be invoked from a background Task) and returns a plain
// string? -- null means "nothing to read" whether that is because there was no selection or because every
// tier failed. Per-tier outcomes (and which tier WON) are written to the diagnostic sink for cross-machine
// testing.
public class SelectionReader
{
    // Why a read produced (or failed to produce) text. Lets the caller tell a genuine empty selection apart
    // from a clipboard-protective abort, so it can surface a distinct message (e.g. an offer to clear the
    // clipboard and retry) rather than silently doing nothing.
    public enum SelectionReadOutcome
    {
        // A non-empty selection was captured; SelectionReadResult.Text is non-null.
        Succeeded,
        // Every applicable tier ran but found nothing to read (no selection, or the target exposed none).
        NoSelectionFound,
        // A clipboard-based tier (C or D) was needed but ABORTED to avoid clobbering existing clipboard
        // content it could not safely snapshot (clipboard held by another app, or content over the size cap).
        // The user's clipboard was left untouched.
        ClipboardProtected,
    }

    // The result of GetSelectedTextForWindow: the captured text (non-null only when Outcome == Succeeded)
    // plus the reason, so a caller can react differently to "nothing selected" vs "we protected the clipboard".
    public readonly record struct SelectionReadResult(SelectionReadOutcome Outcome, string? Text);

    // TEMPORARY diagnostic hook: the app (ReadAloudController) assigns this to RmTraceLog.Log so the per-tier
    // outcome of each capture attempt is written to disk. This library is the lower layer and cannot reference
    // the app's logger directly, so the sink is injected. Remove together with the RmTraceLog instrumentation
    // once the capture cascade is validated across machines and apps.
    public static System.Action<string>? DiagnosticLog;

    private static void LogDiagnostic(string message)
    {
        SelectionReader.DiagnosticLog?.Invoke("ReadAloud/Selection: " + message);
    }

    // CF_UNICODETEXT clipboard format identifier (the wide-character text format). Kept as a local constant
    // rather than a generated P/Invoke because it is a plain numeric format id.
    private const uint CF_UNICODETEXT = 13u;

    // HGLOBAL-backed device-independent bitmap formats. They are captured like any other HGLOBAL format; a
    // captured DIB is also what lets CF_BITMAP below be skipped safely, because Windows re-synthesizes CF_BITMAP
    // from a CF_DIB / CF_DIBV5 on restore. CF_PALETTE is deliberately NOT treated as covered this way (see
    // ClipboardSnapshotCoversAllFormats).
    private const uint CF_DIB = 8u;
    private const uint CF_DIBV5 = 17u;

    // Standard clipboard format identifiers whose data handles are NOT HGLOBAL memory blocks (they are GDI
    // object handles or owner-draw sentinels). CaptureClipboardSnapshot skips these because GlobalSize /
    // GlobalLock are meaningless on them; bitmap pixels still survive because Windows auto-synthesizes the
    // HGLOBAL-backed CF_DIB / CF_DIBV5 from CF_BITMAP (and vice versa). A CF_PALETTE we could not byte-capture
    // instead makes the snapshot incomplete, so the clipboard tier is skipped (see ClipboardSnapshotCoversAllFormats).
    private const uint CF_BITMAP = 2u;
    private const uint CF_METAFILEPICT = 3u;
    private const uint CF_PALETTE = 9u;
    private const uint CF_ENHMETAFILE = 14u;
    private const uint CF_OWNERDISPLAY = 0x80u;
    private const uint CF_DSPBITMAP = 0x82u;
    private const uint CF_DSPMETAFILEPICT = 0x83u;
    private const uint CF_DSPENHMETAFILE = 0x8Eu;
    private const uint CF_GDIOBJFIRST = 0x300u;
    private const uint CF_GDIOBJLAST = 0x3FFu;

    // Upper bound on the total bytes CaptureClipboardSnapshot will copy. A clipboard larger than this aborts
    // the snapshot (and therefore the tier) so an enormous payload cannot pin hundreds of megabytes during a
    // single read.
    private const long MaximumClipboardSnapshotByteCount = 256L * 1024L * 1024L;

    // Lazily-registered ids for the two well-known clipboard formats that opt a clipboard update out of
    // clipboard history (Win+V) and the cloud clipboard. 0 means "not yet registered" (or registration
    // failed). See EnsureClipboardExclusionFormatsRegistered. RegisterClipboardFormat is stable per session,
    // so these are cached once.
    private static uint s_canIncludeInClipboardHistoryFormat = 0;
    private static uint s_canUploadToCloudClipboardFormat = 0;

    // Per-tier timeouts (see docs/read-selected-plan.md). SendMessageTimeout uses SMTO_ABORTIFHUNG so a wedged
    // target application cannot hang the capture.
    private const uint EditMessageTimeoutMilliseconds = 250u;
    // tuned down from 500ms: WM_COPY rarely wins and, when it does, the target responds fast; a doomed wait should not add half a second before the synthesized-copy tier runs
    private const uint WindowMessageCopyTimeoutMilliseconds = 175u;
    private const uint SynthesizedCopyTimeoutMilliseconds = 1500u;
    private const uint ModifierReleaseWaitMilliseconds = 250u;

    private enum ClipboardCopyTrigger
    {
        WindowMessageCopy,
        SynthesizedControlC,
    }

    // Best-effort multi-tier capture. Returns the selected text, or null when nothing could be read.
    //
    // allowForegroundActivation gates Strategy D only: when false, the cascade never activates the
    // target window / synthesizes Ctrl+C, so it cannot steal the foreground. Callers pass false when
    // the foreground must stay put (e.g. a keyboard-invoked read, where the bar should keep focus).
    // A/B/C are always run; they never change the foreground.
    public static SelectionReadResult GetSelectedTextForWindow(System.IntPtr windowHandle, bool allowForegroundActivation = true)
    {
        string? winningText = null;
        string winningTier = "none";
        string strategyAOutcome;
        string strategyBOutcome = "skipped";
        string strategyCOutcome = "skipped";
        string strategyDOutcome = "skipped";
        bool clipboardProtected = false;

        // Strategy A: UI Automation (non-destructive).
        try
        {
            var strategyAResult = Morphic.WindowsNative.UIAutomation.UIAutomationSelectedTextScripts.GetSelectedTextForWindow(windowHandle);
            if (strategyAResult.IsError == true)
            {
                strategyAOutcome = "error(" + strategyAResult.Error!.GetType().Name + ")";
            }
            else if (string.IsNullOrWhiteSpace(strategyAResult.Value) == true)
            {
                strategyAOutcome = "noSelection";
            }
            else
            {
                strategyAOutcome = "succeeded(" + strategyAResult.Value!.Length.ToString() + ")";
                winningText = strategyAResult.Value;
                winningTier = "A";
            }
        }
        catch (System.Exception ex)
        {
            strategyAOutcome = "exception(" + ex.GetType().Name + ")";
        }

        if (winningText is null)
        {
            // Resolve the focused control once; tiers B/C/D all target it. If resolution throws, fall back to
            // the top-level window handle (still a valid message/activation target).
            Windows.Win32.Foundation.HWND controlHandle;
            try
            {
                controlHandle = Morphic.WindowsNative.UIAutomation.UIAutomationSelectedTextScripts.GetFocusedControlHandleForWindow((Windows.Win32.Foundation.HWND)windowHandle);
            }
            catch (System.Exception ex)
            {
                controlHandle = (Windows.Win32.Foundation.HWND)windowHandle;
                SelectionReader.LogDiagnostic("Focused-control resolution threw " + ex.GetType().Name + "; using the top-level handle.");
            }
            var topLevelHandle = (Windows.Win32.Foundation.HWND)windowHandle;

            // Strategy B: clipboard-free Edit/RichEdit selection read (non-destructive).
            try
            {
                var readSelectionViaEditMessagesResult = SelectionReader.ReadSelectionViaEditMessages(controlHandle);
                if (readSelectionViaEditMessagesResult.IsSuccess && readSelectionViaEditMessagesResult.Value!.Length > 0) 
                {
                    string strategyBText = readSelectionViaEditMessagesResult.Value!;
                    strategyBOutcome = "succeeded(" + strategyBText.Length.ToString() + ")";
                    winningText = strategyBText;
                    winningTier = "B";
                }
                else
                {
                    strategyBOutcome = "noSelection";
                }
            }
            catch (System.Exception ex)
            {
                strategyBOutcome = "exception(" + ex.GetType().Name + ")";
            }

            // Strategy C: ask the focused control to copy via WM_COPY (no activation), then read + restore.
            if (winningText is null)
            {
                try
                {
                    ClipboardTierResult strategyCResult = SelectionReader.TryReadSelectionViaClipboard(controlHandle, topLevelHandle, ClipboardCopyTrigger.WindowMessageCopy);
                    if (strategyCResult.ClipboardProtected == true)
                    {
                        clipboardProtected = true;
                    }
                    if (strategyCResult.Text is not null && strategyCResult.Text.Length > 0)
                    {
                        strategyCOutcome = "succeeded(" + strategyCResult.Text.Length.ToString() + ")";
                        winningText = strategyCResult.Text;
                        winningTier = "C";
                    }
                    else if (strategyCResult.ClipboardProtected == true)
                    {
                        strategyCOutcome = "clipboardProtected";
                    }
                    else
                    {
                        strategyCOutcome = "noSelection";
                    }
                }
                catch (System.Exception ex)
                {
                    strategyCOutcome = "exception(" + ex.GetType().Name + ")";
                }
            }

            // Strategy D: activate the target window and synthesize Ctrl+C, then read + restore.
            // Skipped when the caller forbids foreground activation (e.g. a keyboard-invoked read,
            // where stealing the foreground would pull focus off the bar). A/B/C never touch the
            // foreground, so gating only D preserves the bar's focus for keyboard users. When
            // skipped, strategyDOutcome stays "skipped" and the summary log reflects that.
            if (winningText is null && allowForegroundActivation == true)
            {
                try
                {
                    ClipboardTierResult strategyDResult = SelectionReader.TryReadSelectionViaClipboard(controlHandle, topLevelHandle, ClipboardCopyTrigger.SynthesizedControlC);
                    if (strategyDResult.ClipboardProtected == true)
                    {
                        clipboardProtected = true;
                    }
                    if (strategyDResult.Text is not null && strategyDResult.Text.Length > 0)
                    {
                        strategyDOutcome = "succeeded(" + strategyDResult.Text.Length.ToString() + ")";
                        winningText = strategyDResult.Text;
                        winningTier = "D";
                    }
                    else if (strategyDResult.ClipboardProtected == true)
                    {
                        strategyDOutcome = "clipboardProtected";
                    }
                    else
                    {
                        strategyDOutcome = "noSelection";
                    }
                }
                catch (System.Exception ex)
                {
                    strategyDOutcome = "exception(" + ex.GetType().Name + ")";
                }
            }
        }

        SelectionReader.LogDiagnostic("outcome summary -- A=" + strategyAOutcome + ", B=" + strategyBOutcome + ", C=" + strategyCOutcome + ", D=" + strategyDOutcome + "; WINNER=" + winningTier + ".");
        if (winningText is not null)
        {
            return new SelectionReadResult(SelectionReadOutcome.Succeeded, winningText);
        }
        if (clipboardProtected == true)
        {
            return new SelectionReadResult(SelectionReadOutcome.ClipboardProtected, null);
        }
        return new SelectionReadResult(SelectionReadOutcome.NoSelectionFound, null);
    }

    // Strategy B: read the selection of a classic Edit/RichEdit control without touching the clipboard.
    // EM_GETSEL reports the selection's character range; WM_GETTEXT copies the whole control text; we return the
    // substring. A control that does not support EM_GETSEL simply reports start == end (no selection), so this
    // returns null and the cascade continues.
    private static unsafe MorphicResult<string, MorphicUnit> ReadSelectionViaEditMessages(Windows.Win32.Foundation.HWND controlHandle)
    {
        uint selectionStartIndex = 0;
        uint selectionEndIndex = 0;
        _ = SelectionReader.SendMessageWithTimeout(
            controlHandle,
            Windows.Win32.PInvoke.EM_GETSEL,
            (Windows.Win32.Foundation.WPARAM)(nuint)(&selectionStartIndex),
            (Windows.Win32.Foundation.LPARAM)(nint)(&selectionEndIndex),
            SelectionReader.EditMessageTimeoutMilliseconds);
        if (selectionEndIndex <= selectionStartIndex)
        {
            return MorphicResult.ErrorResult();
        }

        nuint textLengthResult = SelectionReader.SendMessageWithTimeout(
            controlHandle,
            Windows.Win32.PInvoke.WM_GETTEXTLENGTH,
            default,
            default,
            SelectionReader.EditMessageTimeoutMilliseconds);
        int textLength = (int)textLengthResult;
        if (textLength <= 0)
        {
            return MorphicResult.ErrorResult();
        }

        char[] buffer = new char[textLength + 1];
        nuint copiedCharacterCountResult;
        fixed (char* bufferPointer = buffer)
        {
            copiedCharacterCountResult = SelectionReader.SendMessageWithTimeout(
                controlHandle,
                Windows.Win32.PInvoke.WM_GETTEXT,
                (Windows.Win32.Foundation.WPARAM)(nuint)buffer.Length,
                (Windows.Win32.Foundation.LPARAM)(nint)bufferPointer,
                SelectionReader.EditMessageTimeoutMilliseconds);
        }
        int copiedCharacterCount = (int)copiedCharacterCountResult;

        // Clamp on the raw uint indices before narrowing to int. A buggy or custom control could report an index
        // above int.MaxValue; casting to int first would wrap it negative, slip past the end <= start guard, and
        // throw inside the new string(...) allocation. Clamping in uint keeps both indices within [0, copiedCharacterCount].
        uint clampedEndIndex = System.Math.Min(selectionEndIndex, (uint)copiedCharacterCount);
        uint clampedStartIndex = System.Math.Min(selectionStartIndex, clampedEndIndex);
        if (clampedEndIndex <= clampedStartIndex)
        {
            return MorphicResult.ErrorResult();
        }
        return MorphicResult.OkResult(new string(buffer, (int)clampedStartIndex, (int)(clampedEndIndex - clampedStartIndex)));
    }

    // Result of a single clipboard-based tier (C or D): the captured text (null if none) plus whether the
    // tier ABORTED to protect existing clipboard content it could not safely snapshot.
    private readonly record struct ClipboardTierResult(string? Text, bool ClipboardProtected);

    // Strategies C and D: snapshot the full clipboard, trigger a copy into the clipboard, read the copied
    // text, then restore the snapshot. The two strategies differ only in how the copy is triggered.
    private static unsafe ClipboardTierResult TryReadSelectionViaClipboard(Windows.Win32.Foundation.HWND controlHandle, Windows.Win32.Foundation.HWND topLevelHandle, ClipboardCopyTrigger trigger)
    {
        // Snapshot the ENTIRE clipboard (every HGLOBAL-backed format) so images, file lists, HTML, RTF, custom
        // formats, etc. survive the copy we are about to trigger. If the snapshot fails (clipboard unreadable),
        // abort this tier rather than risk clobbering content we could not back up.
        ClipboardSnapshot? clipboardSnapshot = SelectionReader.CaptureClipboardSnapshot();
        if (clipboardSnapshot is null)
        {
            SelectionReader.LogDiagnostic("Clipboard tier aborted: could not snapshot the clipboard before copying.");
            return new ClipboardTierResult(null, true);
        }
        if (clipboardSnapshot.IsComplete == false)
        {
            // The clipboard holds content we cannot back up (for example a metafile or owner-display format).
            // Triggering a copy and then restoring would lose it, so protect the clipboard and skip this tier.
            SelectionReader.LogDiagnostic("Clipboard tier aborted: snapshot is incomplete; protecting un-backupable clipboard content rather than risk losing it.");
            return new ClipboardTierResult(null, true);
        }
        uint originalSequenceNumber = Windows.Win32.PInvoke.GetClipboardSequenceNumber();

        uint waitTimeoutMilliseconds;
        if (trigger == ClipboardCopyTrigger.WindowMessageCopy)
        {
            // WM_COPY asks the control to copy its selection without changing the foreground window.
            _ = SelectionReader.SendMessageWithTimeout(
                controlHandle,
                Windows.Win32.PInvoke.WM_COPY,
                default,
                default,
                SelectionReader.WindowMessageCopyTimeoutMilliseconds);
            waitTimeoutMilliseconds = SelectionReader.WindowMessageCopyTimeoutMilliseconds;
        }
        else
        {
            // Wait (briefly, bounded) for any physically-held modifier keys to be released so they cannot corrupt
            // the synthesized Ctrl+C chord, then activate the target window and synthesize the keystroke.
            var modifierReleaseStopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (SelectionReader.AnyClipboardModifierKeyIsHeld() == true && modifierReleaseStopwatch.ElapsedMilliseconds < SelectionReader.ModifierReleaseWaitMilliseconds)
            {
                System.Threading.Thread.Sleep(15);
            }

            bool broughtToForeground = Windows.Win32.PInvoke.SetForegroundWindow(topLevelHandle);
            SelectionReader.LogDiagnostic("Strategy D: SetForegroundWindow returned " + broughtToForeground.ToString() + " for 0x" + ((System.IntPtr)topLevelHandle).ToString("X") + ".");
            // Give the activation a moment to settle before synthesizing input.
            System.Threading.Thread.Sleep(30);
            SelectionReader.SendControlCKeystroke();
            waitTimeoutMilliseconds = SelectionReader.SynthesizedCopyTimeoutMilliseconds;
        }

        bool clipboardChanged = SelectionReader.WaitForClipboardSequenceChange(originalSequenceNumber, waitTimeoutMilliseconds);

        string? capturedText = null;
        if (clipboardChanged == true)
        {
            capturedText = SelectionReader.ReadClipboardUnicodeText();

            // Record the clipboard sequence number right before we restore. Between reading the copied text and
            // restoring the snapshot, the target app (or any other process) can write the clipboard again; that
            // race is inherent here and cannot be fully prevented, but logging the sequence number makes it
            // detectable after the fact (a value past the expected post-copy bump points to an interleaving writer
            // whose content our restore then overwrote).
            uint sequenceNumberBeforeRestore = Windows.Win32.PInvoke.GetClipboardSequenceNumber();
            SelectionReader.LogDiagnostic("Clipboard sequence number before restore = " + sequenceNumberBeforeRestore.ToString() + " (original before copy = " + originalSequenceNumber.ToString() + ").");

            // Restore the full prior clipboard content (all saved HGLOBAL-backed formats).
            ClipboardRestoreStatus restoreStatus = SelectionReader.RestoreClipboardSnapshot(clipboardSnapshot);
            if (restoreStatus != ClipboardRestoreStatus.RestoredAll)
            {
                // The capture may have succeeded, but we could not fully put the user's clipboard back. Make that
                // loud in the log; escalating it to a user-facing alert is a planned follow-up.
                SelectionReader.LogDiagnostic("WARNING: clipboard restore did not fully succeed (status=" + restoreStatus.ToString() + "); the user's original clipboard may be incomplete.");
            }
        }

        if (capturedText is not null && capturedText.Length > 0)
        {
            return new ClipboardTierResult(capturedText, false);
        }
        return new ClipboardTierResult(null, false);
    }

    // Send a window message with a bounded timeout (SMTO_ABORTIFHUNG) and return the message's result value.
    private static nuint SendMessageWithTimeout(Windows.Win32.Foundation.HWND windowHandle, uint message, Windows.Win32.Foundation.WPARAM wParam, Windows.Win32.Foundation.LPARAM lParam, uint timeoutMilliseconds)
    {
        // NOTE: SendMessageTimeout can return 0 when the error is generic; clearing the last error first lets a
        // caller distinguish a real zero result from a failure if it ever needs to. See:
        // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendmessagetimeoutw
        System.Runtime.InteropServices.Marshal.SetLastPInvokeError(0);
        nuint messageResult;
        _ = Windows.Win32.PInvoke.SendMessageTimeout(
            windowHandle,
            message,
            wParam,
            lParam,
            Windows.Win32.UI.WindowsAndMessaging.SEND_MESSAGE_TIMEOUT_FLAGS.SMTO_ABORTIFHUNG,
            timeoutMilliseconds,
            out messageResult);
        return messageResult;
    }

    // Synthesize a Ctrl+C key chord (Ctrl down, C down, C up, Ctrl up) via SendInput.
    private static unsafe void SendControlCKeystroke()
    {
        const ushort VirtualKeyControl = 0x11;
        const ushort VirtualKeyLetterC = 0x43;

        Windows.Win32.UI.Input.KeyboardAndMouse.INPUT* inputs = stackalloc Windows.Win32.UI.Input.KeyboardAndMouse.INPUT[4];
        inputs[0] = SelectionReader.MakeKeyboardInput((Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY)VirtualKeyControl, false);
        inputs[1] = SelectionReader.MakeKeyboardInput((Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY)VirtualKeyLetterC, false);
        inputs[2] = SelectionReader.MakeKeyboardInput((Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY)VirtualKeyLetterC, true);
        inputs[3] = SelectionReader.MakeKeyboardInput((Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY)VirtualKeyControl, true);

        var inputSpan = new System.ReadOnlySpan<Windows.Win32.UI.Input.KeyboardAndMouse.INPUT>(inputs, 4);
        uint insertedCount = Windows.Win32.PInvoke.SendInput(inputSpan, sizeof(Windows.Win32.UI.Input.KeyboardAndMouse.INPUT));
        if (insertedCount != 4)
        {
            SelectionReader.LogDiagnostic("Strategy D: SendInput inserted " + insertedCount.ToString() + " of 4 events (lastWin32Error=" + System.Runtime.InteropServices.Marshal.GetLastWin32Error().ToString() + ").");
        }
    }

    private static Windows.Win32.UI.Input.KeyboardAndMouse.INPUT MakeKeyboardInput(Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY virtualKey, bool isKeyUp)
    {
        var keyboardEventFlags = isKeyUp == true
            ? Windows.Win32.UI.Input.KeyboardAndMouse.KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP
            : default;

        var input = new Windows.Win32.UI.Input.KeyboardAndMouse.INPUT
        {
            type = Windows.Win32.UI.Input.KeyboardAndMouse.INPUT_TYPE.INPUT_KEYBOARD,
        };
        input.Anonymous.ki = new Windows.Win32.UI.Input.KeyboardAndMouse.KEYBDINPUT
        {
            wVk = virtualKey,
            wScan = 0,
            dwFlags = keyboardEventFlags,
            time = 0,
            dwExtraInfo = 0,
        };
        return input;
    }

    private static bool AnyClipboardModifierKeyIsHeld()
    {
        // VK_MENU is Alt; VK_LWIN / VK_RWIN are the two Windows keys. The high bit (0x8000) of GetAsyncKeyState means the key is currently down.
        return (Windows.Win32.PInvoke.GetAsyncKeyState((int)Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_SHIFT) & 0x8000) != 0
            || (Windows.Win32.PInvoke.GetAsyncKeyState((int)Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_CONTROL) & 0x8000) != 0
            || (Windows.Win32.PInvoke.GetAsyncKeyState((int)Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_MENU) & 0x8000) != 0
            || (Windows.Win32.PInvoke.GetAsyncKeyState((int)Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_LWIN) & 0x8000) != 0
            || (Windows.Win32.PInvoke.GetAsyncKeyState((int)Windows.Win32.UI.Input.KeyboardAndMouse.VIRTUAL_KEY.VK_RWIN) & 0x8000) != 0;
    }

    private static bool WaitForClipboardSequenceChange(uint originalSequenceNumber, uint timeoutMilliseconds)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < timeoutMilliseconds)
        {
            if (Windows.Win32.PInvoke.GetClipboardSequenceNumber() != originalSequenceNumber)
            {
                return true;
            }
            System.Threading.Thread.Sleep(15);
        }
        return Windows.Win32.PInvoke.GetClipboardSequenceNumber() != originalSequenceNumber;
    }

    private static bool OpenClipboardWithRetry()
    {
        // Another process may briefly hold the clipboard open; retry a few times before giving up.
        for (int attempt = 0; attempt < 6; attempt += 1)
        {
            if (Windows.Win32.PInvoke.OpenClipboard(default) == true)
            {
                return true;
            }
            System.Threading.Thread.Sleep(10);
        }
        return false;
    }

    private static unsafe string? ReadClipboardUnicodeText()
    {
        if (SelectionReader.OpenClipboardWithRetry() == false)
        {
            return null;
        }
        try
        {
            var dataHandle = Windows.Win32.PInvoke.GetClipboardData(SelectionReader.CF_UNICODETEXT);
            if ((nint)dataHandle == 0)
            {
                return null;
            }

            var globalHandle = (Windows.Win32.Foundation.HGLOBAL)(nint)dataHandle;
            void* lockedPointer = Windows.Win32.PInvoke.GlobalLock(globalHandle);
            if (lockedPointer is null)
            {
                return null;
            }
            try
            {
                return new string((char*)lockedPointer);
            }
            finally
            {
                _ = Windows.Win32.PInvoke.GlobalUnlock(globalHandle);
            }
        }
        finally
        {
            _ = Windows.Win32.PInvoke.CloseClipboard();
        }
    }

    // One saved clipboard format: its format id plus a byte-for-byte copy of the HGLOBAL it pointed to.
    private readonly record struct ClipboardFormatEntry(uint Format, byte[] Data);

    // An ordered snapshot of every HGLOBAL-backed clipboard format present at capture time.
    private sealed class ClipboardSnapshot
    {
        public System.Collections.Generic.List<ClipboardFormatEntry> Entries { get; } = new System.Collections.Generic.List<ClipboardFormatEntry>();

        // False when the clipboard ALSO held data we could not copy (a metafile, owner-display, palette-only, or
        // private GDI format with no HGLOBAL companion we captured). Restoring such a snapshot would empty the
        // clipboard and put back only the formats we have, silently dropping the rest, so the caller must skip the
        // clipboard tier and protect the clipboard instead. See ClipboardSnapshotCoversAllFormats.
        public bool IsComplete { get; set; } = true;
    }

    // Outcome of restoring the user's original clipboard after a clipboard-tier copy. Anything other than
    // RestoredAll means the user's clipboard may be left incomplete, so the caller logs / escalates it.
    private enum ClipboardRestoreStatus
    {
        RestoredAll,
        RestoredPartial,
        RestoredNone,
        CouldNotOpenClipboard,
    }

    // True when the clipboard format's data handle is a GDI object / owner-draw sentinel rather than an
    // HGLOBAL memory block. Such formats cannot be copied with GlobalSize / GlobalLock and are skipped.
    private static bool ClipboardFormatIsNonGlobalHandle(uint format)
    {
        switch (format)
        {
            case SelectionReader.CF_BITMAP:
            case SelectionReader.CF_METAFILEPICT:
            case SelectionReader.CF_PALETTE:
            case SelectionReader.CF_ENHMETAFILE:
            case SelectionReader.CF_OWNERDISPLAY:
            case SelectionReader.CF_DSPBITMAP:
            case SelectionReader.CF_DSPMETAFILEPICT:
            case SelectionReader.CF_DSPENHMETAFILE:
                return true;
            default:
                if (format >= SelectionReader.CF_GDIOBJFIRST && format <= SelectionReader.CF_GDIOBJLAST)
                {
                    return true;
                }
                return false;
        }
    }

    // Decide whether a captured snapshot can be FULLY restored. Every format that was present on the clipboard
    // must either have been captured byte-for-byte, or be CF_BITMAP covered by a captured CF_DIB / CF_DIBV5
    // (Windows re-synthesizes CF_BITMAP from the DIB's identical pixels on restore). Any other un-captured format
    // (a metafile, owner-display sentinel, private GDI handle, or a CF_PALETTE -- which we cannot byte-capture and
    // do not assume a DIB can reproduce) means a restore would silently drop that user data, so the snapshot is
    // treated as incomplete and the clipboard tier is skipped.
    private static bool ClipboardSnapshotCoversAllFormats(System.Collections.Generic.List<uint> presentFormats, System.Collections.Generic.HashSet<uint> capturedFormats)
    {
        bool capturedDibCompanion = capturedFormats.Contains(SelectionReader.CF_DIB) || capturedFormats.Contains(SelectionReader.CF_DIBV5);
        foreach (uint presentFormat in presentFormats)
        {
            if (capturedFormats.Contains(presentFormat) == true)
            {
                continue;
            }
            // CF_BITMAP rides back from any captured CF_DIB / CF_DIBV5 (identical pixels). We deliberately do NOT
            // extend this to CF_PALETTE: a palette only re-synthesizes from a palettized DIB, which we do not
            // verify, so any CF_PALETTE we could not byte-capture (always -- it is a GDI handle) leaves the
            // snapshot incomplete and the clipboard tier is skipped to protect it.
            if (presentFormat == SelectionReader.CF_BITMAP && capturedDibCompanion == true)
            {
                continue;
            }
            return false;
        }
        return true;
    }

    // Capture a byte-for-byte copy of every HGLOBAL-backed clipboard format currently present. Returns null ONLY
    // when the clipboard cannot be opened or the content exceeds MaximumClipboardSnapshotByteCount, so the caller
    // can abort the tier rather than risk clobbering a clipboard it could not read. An empty clipboard yields an
    // empty (non-null) snapshot. GDI-handle formats are skipped (see ClipboardFormatIsNonGlobalHandle); when one
    // is present with no HGLOBAL companion we captured, the snapshot's IsComplete is set false so the caller skips
    // the tier instead of restoring a snapshot that would drop that content.
    private static unsafe ClipboardSnapshot? CaptureClipboardSnapshot()
    {
        if (SelectionReader.OpenClipboardWithRetry() == false)
        {
            return null;
        }
        try
        {
            var snapshot = new ClipboardSnapshot();
            long runningByteCount = 0;

            // Track every format the clipboard advertised (presentFormats) versus the ones we actually byte-copied
            // (capturedFormats) so we can decide afterward whether the snapshot can be FULLY restored. See
            // ClipboardSnapshotCoversAllFormats.
            var presentFormats = new System.Collections.Generic.List<uint>();
            var capturedFormats = new System.Collections.Generic.HashSet<uint>();

            uint format = 0;
            while (true)
            {
                format = Windows.Win32.PInvoke.EnumClipboardFormats(format);
                if (format == 0)
                {
                    break;
                }
                presentFormats.Add(format);

                if (SelectionReader.ClipboardFormatIsNonGlobalHandle(format) == true)
                {
                    continue;
                }

                var dataHandle = Windows.Win32.PInvoke.GetClipboardData(format);
                if ((nint)dataHandle == 0)
                {
                    continue;
                }

                var globalHandle = (Windows.Win32.Foundation.HGLOBAL)(nint)dataHandle;
                nuint blockSize = Windows.Win32.PInvoke.GlobalSize(globalHandle);
                if (blockSize == 0)
                {
                    continue;
                }

                runningByteCount += (long)blockSize;
                if (runningByteCount > SelectionReader.MaximumClipboardSnapshotByteCount)
                {
                    SelectionReader.LogDiagnostic("Clipboard snapshot aborted: content exceeds " + SelectionReader.MaximumClipboardSnapshotByteCount.ToString() + " bytes.");
                    return null;
                }

                void* lockedPointer = Windows.Win32.PInvoke.GlobalLock(globalHandle);
                if (lockedPointer is null)
                {
                    continue;
                }
                try
                {
                    byte[] data = new byte[(int)blockSize];
                    System.Runtime.InteropServices.Marshal.Copy((System.IntPtr)lockedPointer, data, 0, (int)blockSize);
                    snapshot.Entries.Add(new ClipboardFormatEntry(format, data));
                    _ = capturedFormats.Add(format);
                }
                finally
                {
                    _ = Windows.Win32.PInvoke.GlobalUnlock(globalHandle);
                }
            }

            snapshot.IsComplete = SelectionReader.ClipboardSnapshotCoversAllFormats(presentFormats, capturedFormats);
            if (snapshot.IsComplete == false)
            {
                SelectionReader.LogDiagnostic("Clipboard snapshot is incomplete: the clipboard holds a format with no HGLOBAL companion we can restore (metafile, owner-display, or private GDI). The clipboard tier will be skipped to avoid losing it.");
            }

            return snapshot;
        }
        finally
        {
            _ = Windows.Win32.PInvoke.CloseClipboard();
        }
    }

    // Restore a previously captured snapshot: empty the clipboard, then re-publish every saved format from its
    // byte copy. Best-effort -- a format that fails to allocate or set is skipped and the rest still restore. The
    // returned status tells the caller whether the user's original clipboard was put back in full.
    private static unsafe ClipboardRestoreStatus RestoreClipboardSnapshot(ClipboardSnapshot snapshot)
    {
        if (SelectionReader.OpenClipboardWithRetry() == false)
        {
            SelectionReader.LogDiagnostic("Clipboard restore skipped: could not open the clipboard.");
            return ClipboardRestoreStatus.CouldNotOpenClipboard;
        }
        try
        {
            _ = Windows.Win32.PInvoke.EmptyClipboard();

            int restoredCount = 0;
            foreach (ClipboardFormatEntry entry in snapshot.Entries)
            {
                nuint byteCount = (nuint)entry.Data.Length;
                var globalHandle = Windows.Win32.PInvoke.GlobalAlloc(Windows.Win32.System.Memory.GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE, byteCount);
                if ((nint)globalHandle == 0)
                {
                    continue;
                }

                void* lockedPointer = Windows.Win32.PInvoke.GlobalLock(globalHandle);
                if (lockedPointer is null)
                {
                    _ = Windows.Win32.PInvoke.GlobalFree(globalHandle);
                    continue;
                }
                System.Runtime.InteropServices.Marshal.Copy(entry.Data, 0, (System.IntPtr)lockedPointer, entry.Data.Length);
                _ = Windows.Win32.PInvoke.GlobalUnlock(globalHandle);

                var setHandle = Windows.Win32.PInvoke.SetClipboardData(entry.Format, (Windows.Win32.Foundation.HANDLE)(nint)globalHandle);
                if ((nint)setHandle == 0)
                {
                    // Ownership was not transferred to the clipboard; free the block ourselves.
                    _ = Windows.Win32.PInvoke.GlobalFree(globalHandle);
                    continue;
                }
                restoredCount += 1;
            }

            // Keep this restoration of the user's ORIGINAL clipboard out of Windows clipboard-history (Win+V)
            // and the cloud clipboard: we are only putting back what was already there, so it must not create a
            // fresh history entry or be re-uploaded. The two registered formats (DWORD value 0) opt this
            // clipboard update out. NOTE: this does NOT suppress the target app's transient copy in tiers C/D --
            // that data is written by the target app's own clipboard session, which we do not own.
            if (restoredCount > 0)
            {
                SelectionReader.EnsureClipboardExclusionFormatsRegistered();
                if (s_canIncludeInClipboardHistoryFormat != 0)
                {
                    SelectionReader.SetClipboardExclusionFormat(s_canIncludeInClipboardHistoryFormat);
                }
                if (s_canUploadToCloudClipboardFormat != 0)
                {
                    SelectionReader.SetClipboardExclusionFormat(s_canUploadToCloudClipboardFormat);
                }
            }

            ClipboardRestoreStatus status;
            if (restoredCount == snapshot.Entries.Count)
            {
                // Also covers the empty-snapshot case (0 of 0): nothing to put back is a full restore.
                status = ClipboardRestoreStatus.RestoredAll;
            }
            else if (restoredCount == 0)
            {
                status = ClipboardRestoreStatus.RestoredNone;
            }
            else
            {
                status = ClipboardRestoreStatus.RestoredPartial;
            }
            SelectionReader.LogDiagnostic("Clipboard restore: re-published " + restoredCount.ToString() + " of " + snapshot.Entries.Count.ToString() + " saved format(s) (status=" + status.ToString() + ").");
            return status;
        }
        finally
        {
            _ = Windows.Win32.PInvoke.CloseClipboard();
        }
    }

    // Register the two well-known clipboard formats that control clipboard-history inclusion and cloud upload.
    // Idempotent and cheap: RegisterClipboardFormat returns the same id for a given name for the life of the
    // session, so we cache each id and stop calling once it is non-zero.
    private static unsafe void EnsureClipboardExclusionFormatsRegistered()
    {
        if (s_canIncludeInClipboardHistoryFormat == 0)
        {
            fixed (char* formatNamePointer = "CanIncludeInClipboardHistory")
            {
                s_canIncludeInClipboardHistoryFormat = Windows.Win32.PInvoke.RegisterClipboardFormat(new Windows.Win32.Foundation.PCWSTR(formatNamePointer));
            }
        }
        if (s_canUploadToCloudClipboardFormat == 0)
        {
            fixed (char* formatNamePointer = "CanUploadToCloudClipboard")
            {
                s_canUploadToCloudClipboardFormat = Windows.Win32.PInvoke.RegisterClipboardFormat(new Windows.Win32.Foundation.PCWSTR(formatNamePointer));
            }
        }
    }

    // Publish a single DWORD-valued (= 0) clipboard format into the currently-open clipboard, used to set the
    // CanIncludeInClipboardHistory / CanUploadToCloudClipboard opt-out flags. MUST be called inside an open
    // clipboard session (RestoreClipboardSnapshot's). Best-effort: silently does nothing on allocation failure.
    private static unsafe void SetClipboardExclusionFormat(uint format)
    {
        nuint byteCount = (nuint)sizeof(uint);
        var globalHandle = Windows.Win32.PInvoke.GlobalAlloc(Windows.Win32.System.Memory.GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE, byteCount);
        if ((nint)globalHandle == 0)
        {
            return;
        }

        void* lockedPointer = Windows.Win32.PInvoke.GlobalLock(globalHandle);
        if (lockedPointer is null)
        {
            _ = Windows.Win32.PInvoke.GlobalFree(globalHandle);
            return;
        }
        *(uint*)lockedPointer = 0u;
        _ = Windows.Win32.PInvoke.GlobalUnlock(globalHandle);

        var setHandle = Windows.Win32.PInvoke.SetClipboardData(format, (Windows.Win32.Foundation.HANDLE)(nint)globalHandle);
        if ((nint)setHandle == 0)
        {
            // Ownership was not transferred to the clipboard; free the block ourselves.
            _ = Windows.Win32.PInvoke.GlobalFree(globalHandle);
        }
    }
}
