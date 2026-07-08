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

// 'using System;' is required so the C# compiler can find the GetAwaiter extension method that
// makes a WinRT IAsyncOperation directly awaitable (the same pattern the rest of the app uses,
// e.g. AboutWindow's 'await Windows.System.Launcher.LaunchUriAsync(...)'). All other type
// references in this file are intentionally fully qualified rather than imported.
using System;

namespace Morphic.ReadAloud;

// ReadAloudController is the long-lived, App-owned engine behind the MorphicBar's "Read Selected"
// feature. It captures the text the user had selected in their previous foreground window and
// speaks it aloud.
//
// Lifecycle:
//   * Construct once on the UI thread during App.OnLaunched. The constructor builds a
//     LastForegroundWindowTracker, which installs a system WinEvent hook on the calling thread's
//     message pump, so it MUST be created on the UI thread (the thread with the running pump).
//   * Dispose once on the UI thread during App shutdown. Disposing stops any audio, removes the
//     foreground hook, and releases the speech and playback objects.
//
// Threading:
//   * All public members are intended to be called on the UI thread (the MorphicBar button
//     handlers run there). The awaits inside PlayAsync deliberately do NOT use
//     ConfigureAwait(false), so their continuations resume on the UI thread; that keeps every
//     touch of the media player and the controller's state on a single thread and avoids racing
//     Dispose. The only work pushed off the UI thread is the UI Automation capture, which runs on
//     a thread-pool (MTA) thread: that is both the apartment Microsoft recommends for UI
//     Automation clients and a guard against a slow or hung target app freezing the bar.
//
// Engine note (v1 = WinRT):
//   * Speech is produced by Windows.Media.SpeechSynthesis.SpeechSynthesizer (which enumerates the
//     modern OneCore voices and whose default voice matches the user's Settings choice) and played
//     through Windows.Media.Playback.MediaPlayer. A second engine (System.Speech / SAPI5) and a
//     user-facing voice picker are planned follow-ups; PlayAsync and Stop are kept engine agnostic
//     so an ITextToSpeechEngine seam can be extracted when that work begins.
internal sealed class ReadAloudController : System.IDisposable
{
    // The previous (non-own-process) foreground window, used as the capture target. Owned here
    // because its lifetime matches the controller's and it is only meaningful to Read Selected.
    private readonly Morphic.WindowsNative.Windowing.LastForegroundWindowTracker _lastForegroundWindowTracker;

    // WinRT speech synthesis (text to audio stream) and playback (audio stream to speakers).
    private readonly Windows.Media.SpeechSynthesis.SpeechSynthesizer _speechSynthesizer;
    private readonly Windows.Media.Playback.MediaPlayer _mediaPlayer;

    // Guards the cancellation-token swap so that starting (or stopping) one utterance cannot race
    // another. The controller is otherwise UI-thread-affine; this lock is defensive.
    private readonly object _stateLock = new();

    // The cancellation source for the utterance currently being prepared (capture plus synthesis).
    // Once playback has started the async pipeline is finished and this is cleared; stopping audio
    // after that point is handled by pausing the media player, not by cancellation.
    private System.Threading.CancellationTokenSource? _currentUtteranceCancellationTokenSource;

    // The media source backing the current (or most recent) utterance. Held so the previous one
    // can be disposed when a new utterance replaces it, and so the last one can be disposed at
    // shutdown. Touched only on the UI thread.
    private Windows.Media.Core.MediaSource? _currentMediaSource;

    private bool _isDisposed;

    // Upper bound on how many characters we hand to the speech synthesizer for a single utterance. A very large
    // selection (for example Select-All in a long document) would otherwise spend a long time synthesizing audio
    // the user almost certainly did not intend to hear in full; we truncate to this many characters and log it.
    private const int MaximumCharactersToSynthesize = 50000;

    public ReadAloudController()
    {
        _lastForegroundWindowTracker = new Morphic.WindowsNative.Windowing.LastForegroundWindowTracker();

        _speechSynthesizer = new Windows.Media.SpeechSynthesis.SpeechSynthesizer();
        // NOTE: we intentionally leave SpeechSynthesizer.Voice at its default. The default is
        // SpeechSynthesizer.DefaultVoice, which is the modern system default voice (the one the
        // user picked under Settings, Time and Language, Speech). Setting it explicitly would only
        // risk an exception on the rare machine that reports no installed voices.

        _mediaPlayer = new Windows.Media.Playback.MediaPlayer();
        // With AutoPlay set, assigning Source begins playback immediately (no explicit Play call).
        _mediaPlayer.AutoPlay = true;
    }

    public void Dispose()
    {
        if (_isDisposed == true)
        {
            return;
        }
        _isDisposed = true;

        // Cancel any in-flight utterance preparation and stop any audio. This is teardown, not a
        // user gesture, so use the keyboard path (invokedViaKeyboard: true) to leave the foreground
        // untouched -- the app must not yank the foreground around during shutdown.
        this.Stop(invokedViaKeyboard: true);

        // Release the media source for the last utterance (if any).
        if (_currentMediaSource is not null)
        {
            try { _currentMediaSource.Dispose(); } catch (System.Exception) { }
            _currentMediaSource = null;
        }

        try { _mediaPlayer.Dispose(); } catch (System.Exception) { }

        // Removes the system WinEvent hook. Must run on the UI thread that installed it, which is
        // the same UI thread App uses to dispose the controller.
        _lastForegroundWindowTracker.Dispose();
    }

    // Captures the text selected in the user's previous foreground window and speaks it. If an
    // utterance is already playing or being prepared, it is superseded by this one. An empty or
    // whitespace-only selection (or a window with no text-capable focused control) is a silent
    // no-op, by design.
    //
    // invokedViaKeyboard selects the focus-landing policy (the read-from target is unaffected --
    // always the prior foreground window):
    //   * true  (keyboard / assistive-technology): leave the bar foreground so its Play button keeps
    //            focus, and forbid the capture cascade's foreground-stealing tier (Strategy D);
    //   * false (mouse / touch / pen): allow Strategy D, and return the foreground to the prior
    //            window once capture is done so the user's caret/selection are restored.
    public async System.Threading.Tasks.Task PlayAsync(bool invokedViaKeyboard)
    {
        if (_isDisposed == true)
        {
            return;
        }

        // Start a fresh cancellation scope for this utterance and supersede any previous one.
        var cancellationTokenSource = new System.Threading.CancellationTokenSource();
        System.Threading.CancellationTokenSource? previousCancellationTokenSource;
        lock (_stateLock)
        {
            previousCancellationTokenSource = _currentUtteranceCancellationTokenSource;
            _currentUtteranceCancellationTokenSource = cancellationTokenSource;
        }
        previousCancellationTokenSource?.Cancel();
        // Halt any audio from a prior utterance right away (the prior PlayAsync may already have
        // started playback, in which case cancellation alone would not stop the sound).
        this.StopPlayback();

        var cancellationToken = cancellationTokenSource.Token;
        try
        {
            // Resolve the capture target: the last foreground window that was not one of ours.
            var targetWindowHandle = _lastForegroundWindowTracker.LastForegroundWindowHandle;
            if (targetWindowHandle == System.IntPtr.Zero)
            {
                return;
            }

            // Capture the selected text off the UI thread (UI Automation prefers an MTA thread, and
            // a hung target app must not freeze the bar). Continuation resumes on the UI thread.
            // Gate Strategy D (synthesized Ctrl+C, which activates the target window) OFF for keyboard
            // invocations: A/B/C never change the foreground, so keeping D off lets the bar stay
            // foreground and Play keep keyboard focus. Pointer invocations allow D (the foreground is
            // handed back to the target below regardless).
            var selectionReadResult = await System.Threading.Tasks.Task.Run(
                () => Morphic.WindowsNative.Speech.SelectionReader.GetSelectedTextForWindow(targetWindowHandle, allowForegroundActivation: invokedViaKeyboard == false),
                cancellationToken);
            if (cancellationToken.IsCancellationRequested == true)
            {
                return;
            }
            string? selectedText = selectionReadResult.Text;

            // Pointer / touch / pen invocation: hand the foreground back to the user's prior window
            // now that capture is finished, so their caret and selection return to where they were
            // working. Keyboard invocation deliberately leaves the bar foreground so focus stays on
            // Play (the user can Tab to Stop or press Space again to restart). Morphic owns the
            // foreground here (the click activated the bar), so SetForegroundWindow is permitted.
            if (invokedViaKeyboard == false)
            {
                _ = Windows.Win32.PInvoke.SetForegroundWindow((Windows.Win32.Foundation.HWND)targetWindowHandle);
            }

            // The multi-tier SelectionReader is best-effort. A null/whitespace result means "nothing to speak",
            // but the Outcome distinguishes WHY: an empty selection vs. a clipboard-protective abort (a
            // clipboard tier was needed but bailed to avoid clobbering existing clipboard content). The latter
            // is where a future near-the-bar notification (e.g. "Couldn't read the selected text without
            // disturbing your clipboard -- clear it and retry?") will be raised; for now it raises an interim
            // toast, while a plain empty selection stays a silent no-op.
            if (string.IsNullOrWhiteSpace(selectedText) == true)
            {
                if (selectionReadResult.Outcome == Morphic.WindowsNative.Speech.SelectionReader.SelectionReadOutcome.ClipboardProtected)
                {
                    // Interim alert: a clipboard-based capture tier had to abort to avoid clobbering clipboard
                    // content we could not safely back up, so tell the user the read did not happen. Only this
                    // protective-abort case raises UI; a plain empty selection stays a silent no-op by design.
                    // FUTURE: replace this toast with the near-the-bar "always on top" notification popup
                    // (design captured in memory), which can also offer a "clear clipboard and retry" action
                    // that App Notifications cannot.
                    Morphic.Notifications.ToastNotifications.ShowText(Morphic.Localization.Strings.ReadSelectedHeader, Morphic.Localization.Strings.ReadSelectedCaptureFailedMessage);
                }
                return;
            }

            // Cap the text handed to synthesis so an enormous selection cannot tie the engine up producing audio
            // the user did not intend to hear. Truncating to a character boundary is fine here: the cap is far
            // larger than any reasonable spoken passage, so a split mid-word at the very end is immaterial.
            if (selectedText.Length > ReadAloudController.MaximumCharactersToSynthesize)
            {
                selectedText = selectedText.Substring(0, ReadAloudController.MaximumCharactersToSynthesize);
            }

            // Synthesize the text to an audio stream (WinRT). AsTask(cancellationToken) lets a supersession or a
            // Stop abandon the synthesis itself instead of waiting it out; cancellation surfaces as an
            // OperationCanceledException handled below. We still re-check the token immediately after, to cover the
            // narrow window where cancellation lands just as synthesis completes.
            var synthesisStream = await _speechSynthesizer.SynthesizeTextToStreamAsync(selectedText).AsTask(cancellationToken);
            if (cancellationToken.IsCancellationRequested == true)
            {
                synthesisStream.Dispose();
                return;
            }

            // Hand the stream to the media player; AutoPlay starts it. Replace and dispose the
            // previous source. No await occurs between the cancellation check above and this
            // assignment, so Dispose (UI thread) cannot interleave and touch a disposed player.
            var mediaSource = Windows.Media.Core.MediaSource.CreateFromStream(synthesisStream, synthesisStream.ContentType);
            var previousMediaSource = _currentMediaSource;
            _currentMediaSource = mediaSource;
            _mediaPlayer.Source = mediaSource;
            if (previousMediaSource is not null)
            {
                try { previousMediaSource.Dispose(); } catch (System.Exception) { }
            }
        }
        catch (System.OperationCanceledException)
        {
            // Superseded by a newer Play, or stopped before synthesis finished. Nothing to do.
        }
        catch (System.Exception)
        {
            // v1: keep failures non-fatal and silent to the user. Surfacing them is a later step.
        }
        finally
        {
            // Clear the current cancellation source if it is still ours, then dispose it. We clear
            // under the lock before disposing so Stop cannot observe a disposed source.
            lock (_stateLock)
            {
                if (object.ReferenceEquals(_currentUtteranceCancellationTokenSource, cancellationTokenSource) == true)
                {
                    _currentUtteranceCancellationTokenSource = null;
                }
            }
            cancellationTokenSource.Dispose();
        }
    }

    // Stops speaking (or cancels an utterance still being prepared). A no-op if nothing is playing.
    //
    // invokedViaKeyboard mirrors PlayAsync's focus-landing policy: a pointer / touch / pen
    // invocation returns the foreground to the user's prior window; a keyboard / assistive-technology
    // invocation leaves the bar foreground (so Stop keeps focus and the user can Tab back to Play).
    public void Stop(bool invokedViaKeyboard)
    {
        System.Threading.CancellationTokenSource? cancellationTokenSourceToCancel;
        lock (_stateLock)
        {
            cancellationTokenSourceToCancel = _currentUtteranceCancellationTokenSource;
        }
        // Cancel without disposing: the owning PlayAsync disposes it in its finally, which avoids
        // an ObjectDisposedException if cancellation and that finally race.
        cancellationTokenSourceToCancel?.Cancel();

        this.StopPlayback();

        if (invokedViaKeyboard == false)
        {
            var targetWindowHandle = _lastForegroundWindowTracker.LastForegroundWindowHandle;
            if (targetWindowHandle != System.IntPtr.Zero)
            {
                _ = Windows.Win32.PInvoke.SetForegroundWindow((Windows.Win32.Foundation.HWND)targetWindowHandle);
            }
        }
    }

    // Pauses the media player, which halts audio immediately. Pausing (rather than clearing the
    // source) is enough: the next utterance assigns a brand-new source.
    private void StopPlayback()
    {
        try
        {
            _mediaPlayer.Pause();
        }
        catch (System.Exception)
        {
            // The player may have no source yet, or be mid-teardown. Halting audio is best-effort.
        }
    }
}
