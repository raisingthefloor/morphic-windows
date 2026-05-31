// Copyright 2020-2026 Raising the Floor - US, Inc.
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

using System;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace Morphic.WindowsNative.SystemSettings;

// Dedicated background STA worker thread that owns all v-table calls into the
// SystemSettings.DataModel.SettingsDatabase and ISettingItem COM objects.
//
// Why: in CsWinRT (Morphic 2.x), the ISettingItem projection does not carry a free-threaded
// marshaler. If the underlying COM object is first created on an MTA thread pool thread (the
// default when `Task.Run` wraps a setting access), the v-table lands in a corrupted state and
// the very first method call faults with 0xC0000005 STATUS_ACCESS_VIOLATION.
//
// Solution: run every SettingsDatabase / ISettingItem call on a single dedicated STA worker
// thread. Consumers from any apartment (UI STA, thread pool MTA, etc.) submit a delegate to
// the dispatcher; the worker runs it on STA and signals completion. Cross-apartment marshaling
// happens once per call (microseconds), which is irrelevant for user-click handlers.
//
// The worker thread is provided by Windows.System.DispatcherQueueController.CreateOnDedicatedThread(),
// which:
//   * Initializes the new thread as an STA-family apartment in practice. The Microsoft docs for
//     CreateOnDedicatedThread don't explicitly promise this, but the DispatcherQueueOptions.
//     apartmentType field that the framework uses for DQTYPE_THREAD_DEDICATED only supports STA
//     and ASTA (MTA is not even a valid option). We verify it at runtime in EnsureStarted so
//     any future Microsoft behavior change surfaces as a clear init-time failure rather than
//     mystery v-table faults on the first SettingItem call.
//   * Runs a native, optimized Windows message pump on the new thread, so COM/WinRT callbacks
//     (SettingChanged events) are delivered reliably without us having to write a custom
//     PeekMessage/DispatchMessage loop.
//   * Manages the thread's lifetime; we keep the controller in a static field so the thread
//     lives for the process lifetime. We don't call ShutdownQueueAsync at process exit (a
//     long-lived singleton with implicit teardown at process exit is fine for our use case).
//
// Resilience: if the STA worker dies (uncaught native exception in a WinRT call, OS resource
// teardown, etc.), Run/Run<T> detects the failure via the per-call timeout, tears the dead
// dispatcher down, rebuilds it via CreateOnDedicatedThread, and transparently retries the
// failed call once. A 1-2-4-8-16-32 second backoff ladder caps restart frequency so a
// worker that keeps dying back-to-back doesn't melt the CPU on restart attempts. User-
// delegate exceptions (the func/action passed to Run threw) are NOT recovery candidates and
// surface to the caller immediately.
internal static class SettingItemDispatcher
{
    private static readonly object s_startLock = new();
    private static Windows.System.DispatcherQueueController? s_controller;
    private static Windows.System.DispatcherQueue? s_dispatcherQueue;

    // Defensive timeout for sync waits. The diagnostic measured a healthy setting fetch at
    // ~39ms; no individual dispatched call should approach a second in practice. 10s catches
    // a hung worker thread (e.g. WinRT call stuck inside SystemSettings.DataModel) and
    // tolerates realistic backlog (50 callers at 50ms each = 2.5s) without false-positive
    // timeouts on legitimately slow operations. Surfaces as TimeoutException, which the
    // SettingItemProxy catch blocks translate into MorphicResult<.., IXxxError>.ExceptionError,
    // keeping the error union pattern intact at the consumer level.
    private static readonly TimeSpan s_runTimeout = TimeSpan.FromSeconds(10);

    // Init-time timeout for the STA verification probe. Shorter than s_runTimeout because if
    // the brand-new dispatcher queue can't run a no-op delegate within 3 seconds, the
    // controller is broken at birth and waiting longer just delays a bad-news message.
    private static readonly TimeSpan s_initTimeout = TimeSpan.FromSeconds(3);

    // Recovery state: if the STA worker dies (uncaught native exception inside
    // SystemSettings.DataModel, OS resource teardown, etc.), the cached DispatcherQueue stays
    // non-null but every TryEnqueue'd work item silently never runs -- callers would
    // TimeoutException at s_runTimeout forever. The recovery path inside Run/Run<T> tears the
    // dead dispatcher down and rebuilds it, then retries the failed call once.
    //
    // Backoff ladder (seconds before the Nth restart attempt, counting from 1):
    //   1->1, 2->2, 3->4, 4->8, 5->16, 6+->32 (cap)
    // The first restart attempt is immediate. The cooldown is the minimum gap between
    // attempts, enforced relative to s_lastRestartTicksMs. s_consecutiveRestartCount resets
    // to 0 the moment any Run call completes without triggering recovery, so a one-shot death
    // doesn't keep imposing future cooldowns.
    private static int s_consecutiveRestartCount;
    private static long s_lastRestartTicksMs;

    private static void EnsureStarted()
    {
        if (s_dispatcherQueue is not null)
        {
            return;
        }
        lock (s_startLock)
        {
            if (s_dispatcherQueue is not null)
            {
                return;
            }

            var controller = Windows.System.DispatcherQueueController.CreateOnDedicatedThread();
            var dispatcherQueue = controller.DispatcherQueue;

            // Verify the worker is STA before publishing. If Microsoft ever changes
            // CreateOnDedicatedThread to produce a non-STA thread, every subsequent
            // SettingItem call would crash with 0xC0000005 (the original Dark-button bug).
            // Catching it here gives a clear, actionable exception instead. Drop down to
            // the lower-level CoreMessaging CreateDispatcherQueueController P/Invoke with
            // explicit DQTAT_COM_STA if that ever happens.
            //
            // Bounded wait so a broken-at-birth dispatcher queue can't hang init forever.
            using var verificationDone = new ManualResetEvent(false);
            System.Threading.ApartmentState observedApartment = System.Threading.ApartmentState.Unknown;
            var enqueued = dispatcherQueue.TryEnqueue(() =>
            {
                observedApartment = System.Threading.Thread.CurrentThread.GetApartmentState();
                _ = verificationDone.Set();
            });
            if (enqueued == false)
            {
                throw new InvalidOperationException("SettingItemDispatcher could not enqueue STA verification probe.");
            }
            if (verificationDone.WaitOne(s_initTimeout) == false)
            {
                throw new InvalidOperationException(
                    $"SettingItemDispatcher STA verification probe did not complete within {s_initTimeout.TotalSeconds}s. " +
                    "The dispatcher queue's worker thread did not run our probe; the controller may be broken at init.");
            }
            if (observedApartment != System.Threading.ApartmentState.STA)
            {
                throw new InvalidOperationException(
                    $"SettingItemDispatcher worker apartment is {observedApartment}, expected STA. " +
                    "CreateOnDedicatedThread behavior may have changed; switch to the CoreMessaging " +
                    "CreateDispatcherQueueController P/Invoke with explicit DQTAT_COM_STA.");
            }

            s_controller = controller;
            s_dispatcherQueue = dispatcherQueue;
        }
    }

    /// <summary>
    /// Synchronously runs <paramref name="func"/> on the STA worker thread and returns its result.
    /// </summary>
    /// <remarks>
    /// If <paramref name="func"/> throws, the exception is rethrown on the calling thread with
    /// its original stack preserved via <see cref="ExceptionDispatchInfo"/>. If the caller is
    /// already running ON the worker thread (e.g. a SettingChanged event handler doing a
    /// follow-up Get/Set), runs inline to avoid queuing behind itself and self-deadlocking.
    /// Blocks until the work completes or <c>s_runTimeout</c> elapses; if the timeout fires
    /// (the worker may be hung or dead), attempts to recover the dispatcher and retries the
    /// call once transparently before surfacing <see cref="TimeoutException"/>. We block via
    /// <see cref="ManualResetEvent.WaitOne()"/> rather than <c>ManualResetEventSlim.Wait()</c>
    /// because the kernel-handle wait routes through the .NET STA waiting machinery and
    /// typically pumps messages while blocked, whereas the slim variant is managed-only and
    /// would not pump; the empirical pumping behavior in this codebase is NOT verified, so
    /// reverify here if you see UI hangs or COM-callback misses when the UI thread
    /// synchronously calls Run.
    /// </remarks>
    /// <exception cref="TimeoutException">The work item did not complete within <c>s_runTimeout</c>, and either recovery was refused due to backoff or the post-recovery retry also timed out.</exception>
    /// <exception cref="InvalidOperationException">The dispatcher queue rejected the enqueue.</exception>
    public static T Run<T>(Func<T> func)
    {
        SettingItemDispatcher.EnsureStarted();

        if (s_dispatcherQueue!.HasThreadAccess)
        {
            return func();
        }

        // First attempt.
        var outcome = SettingItemDispatcher.TryDispatchFuncOnce(func);
        if (outcome.success == true)
        {
            SettingItemDispatcher.ResetRestartCounterIfNeeded();
            return outcome.result;
        }
        if (outcome.userException is not null)
        {
            // Work item ran but the user's func threw -- this is NOT a dispatcher failure;
            // surface immediately without attempting recovery.
            outcome.userException.Throw();
        }

        // Dispatcher itself failed (enqueue rejected or wait timed out). Attempt recovery and,
        // if recovery succeeds, retry the call once transparently.
        if (SettingItemDispatcher.TryRecoverDispatcher() == true)
        {
            outcome = SettingItemDispatcher.TryDispatchFuncOnce(func);
            if (outcome.success == true)
            {
                return outcome.result;
            }
            if (outcome.userException is not null)
            {
                outcome.userException.Throw();
            }
        }

        // Recovery was refused (still in backoff window) or the post-recovery retry also
        // failed. Surface the dispatcher failure to the caller.
        outcome.dispatcherFailure!.Throw();
        return default!; // unreachable -- Throw() above never returns
    }

    /// <summary>
    /// Synchronously runs <paramref name="action"/> on the STA worker thread.
    /// </summary>
    /// <remarks>
    /// Same semantics as <see cref="Run{T}(Func{T})"/> without a return value, including the
    /// transparent recovery + single retry behavior on dispatcher failure.
    /// </remarks>
    /// <exception cref="TimeoutException">The work item did not complete within <c>s_runTimeout</c>, and either recovery was refused due to backoff or the post-recovery retry also timed out.</exception>
    /// <exception cref="InvalidOperationException">The dispatcher queue rejected the enqueue.</exception>
    public static void Run(Action action)
    {
        SettingItemDispatcher.EnsureStarted();

        if (s_dispatcherQueue!.HasThreadAccess)
        {
            action();
            return;
        }

        // First attempt.
        var outcome = SettingItemDispatcher.TryDispatchActionOnce(action);
        if (outcome.success == true)
        {
            SettingItemDispatcher.ResetRestartCounterIfNeeded();
            return;
        }
        if (outcome.userException is not null)
        {
            outcome.userException.Throw();
        }

        // Dispatcher failure -- attempt recovery + retry once.
        if (SettingItemDispatcher.TryRecoverDispatcher() == true)
        {
            outcome = SettingItemDispatcher.TryDispatchActionOnce(action);
            if (outcome.success == true)
            {
                return;
            }
            if (outcome.userException is not null)
            {
                outcome.userException.Throw();
            }
        }

        outcome.dispatcherFailure!.Throw();
    }

    // ----------------------------------------------------------------------------------------
    // Recovery infrastructure
    // ----------------------------------------------------------------------------------------

    // Result of a single dispatch attempt. Discriminated three ways so callers can tell
    // dispatcher-level failures (recovery candidate) from user-delegate exceptions (surface
    // immediately, don't trigger recovery).
    //   success=true                    -> result valid, both exception slots null
    //   userException != null           -> work item ran, user's func/action threw
    //   dispatcherFailure != null       -> enqueue rejected or wait timed out before completion
    private static (bool success, T result, ExceptionDispatchInfo? userException, ExceptionDispatchInfo? dispatcherFailure)
        TryDispatchFuncOnce<T>(Func<T> func)
    {
        using var doneSignal = new ManualResetEvent(false);
        T workResult = default!;
        ExceptionDispatchInfo? capturedException = null;

        var enqueued = s_dispatcherQueue!.TryEnqueue(() =>
        {
            try
            {
                workResult = func();
            }
            catch (Exception ex)
            {
                capturedException = ExceptionDispatchInfo.Capture(ex);
            }
            finally
            {
                // If the caller already timed out below, the using-disposed doneSignal will
                // throw ObjectDisposedException here. Swallow it -- the caller has already
                // taken its dispatcher-failure path; a late completion shouldn't tear down
                // the dispatcher worker.
                try
                {
                    _ = doneSignal.Set();
                }
                catch (ObjectDisposedException)
                {
                }
            }
        });

        if (enqueued == false)
        {
            return (false, default!, null, ExceptionDispatchInfo.Capture(
                new InvalidOperationException("SettingItemDispatcher queue rejected enqueue (queue shutting down or worker dead?).")));
        }

        if (doneSignal.WaitOne(s_runTimeout) == false)
        {
            return (false, default!, null, ExceptionDispatchInfo.Capture(
                new TimeoutException(
                    $"SettingItemDispatcher work item did not complete within {s_runTimeout.TotalSeconds}s. " +
                    "The worker thread may be hung or dead inside a WinRT call into SystemSettings.DataModel.")));
        }

        if (capturedException is not null)
        {
            return (false, default!, capturedException, null);
        }

        return (true, workResult, null, null);
    }

    private static (bool success, ExceptionDispatchInfo? userException, ExceptionDispatchInfo? dispatcherFailure) TryDispatchActionOnce(Action action)
    {
        using var doneSignal = new ManualResetEvent(false);
        ExceptionDispatchInfo? capturedException = null;

        var enqueued = s_dispatcherQueue!.TryEnqueue(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                capturedException = ExceptionDispatchInfo.Capture(ex);
            }
            finally
            {
                try
                {
                    _ = doneSignal.Set();
                }
                catch (ObjectDisposedException)
                {
                }
            }
        });

        if (enqueued == false)
        {
            return (false, null, ExceptionDispatchInfo.Capture(
                new InvalidOperationException("SettingItemDispatcher queue rejected enqueue (queue shutting down or worker dead?).")));
        }

        if (doneSignal.WaitOne(s_runTimeout) == false)
        {
            return (false, null, ExceptionDispatchInfo.Capture(
                new TimeoutException(
                    $"SettingItemDispatcher work item did not complete within {s_runTimeout.TotalSeconds}s. " +
                    "The worker thread may be hung or dead inside a WinRT call into SystemSettings.DataModel.")));
        }

        if (capturedException is not null)
        {
            return (false, capturedException, null);
        }

        return (true, null, null);
    }

    // Backoff ladder: seconds the caller must wait since the most recent restart before
    // initiating another. The count is the number of restarts that have ALREADY happened,
    // so count==0 is the very first attempt and returns 0s (immediate).
    //   restartsSoFar | wait before next attempt
    //   0             | 0s (immediate)
    //   1             | 1s
    //   2             | 2s
    //   3             | 4s
    //   4             | 8s
    //   5             | 16s
    //   6+            | 32s (cap)
    private static TimeSpan GetBackoffForRestartCount(int restartsSoFar)
    {
        int seconds = restartsSoFar switch
        {
            <= 0 => 0,
            1 => 1,
            2 => 2,
            3 => 4,
            4 => 8,
            5 => 16,
            _ => 32,
        };
        return TimeSpan.FromSeconds(seconds);
    }

    // Attempts to recover the dispatcher after a presumed-dead worker. Returns true if a fresh
    // dispatcher is in place (either we restarted it or another caller already did), false if
    // recovery was refused due to the backoff cooldown or the restart itself failed.
    private static bool TryRecoverDispatcher()
    {
        lock (s_startLock)
        {
            // Cooldown check: enforce a gap between consecutive restart attempts. Without this,
            // a worker that dies again immediately after restart would melt the CPU spinning on
            // CreateOnDedicatedThread + STA probe + die + repeat.
            var nowMs = Environment.TickCount64;
            var elapsedSinceLastRestartMs = nowMs - s_lastRestartTicksMs;
            var backoff = SettingItemDispatcher.GetBackoffForRestartCount(s_consecutiveRestartCount);
            if (s_consecutiveRestartCount > 0 && elapsedSinceLastRestartMs < (long)backoff.TotalMilliseconds)
            {
                Debug.WriteLine($"[SettingItemDispatcher] Recovery refused: in backoff window ({elapsedSinceLastRestartMs}ms elapsed of {backoff.TotalMilliseconds}ms cooldown; restart #{s_consecutiveRestartCount} already happened).");
                return false;
            }

            // Cooldown cleared. Tear down the old controller and rebuild.
            s_lastRestartTicksMs = nowMs;
            s_consecutiveRestartCount++;

            var oldController = s_controller;
            s_controller = null;
            s_dispatcherQueue = null;

            // Fire-and-forget shutdown. If the worker is dead, ShutdownQueueAsync might block
            // forever waiting for it; we don't await the returned IAsyncAction. The controller
            // and its dead thread get GC'd eventually.
            if (oldController is not null)
            {
                try
                {
                    _ = oldController.ShutdownQueueAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SettingItemDispatcher] ShutdownQueueAsync on dead controller threw: {ex.Message}");
                }
            }

            // Recreate. EnsureStarted's outer null-check now sees null and walks the lock-then-
            // create-then-verify path. If creation or STA verification fails, it throws and we
            // surface false; the next call will try again (subject to backoff).
            try
            {
                SettingItemDispatcher.EnsureStarted();
                return s_dispatcherQueue is not null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingItemDispatcher] Recovery EnsureStarted failed: {ex.Message}");
                return false;
            }
        }
    }

    // Called on every successful Run/Run<T> call that didn't need recovery. Resets the
    // consecutive-restart counter so a one-off death doesn't impose future cooldowns.
    private static void ResetRestartCounterIfNeeded()
    {
        if (Volatile.Read(ref s_consecutiveRestartCount) == 0)
        {
            return;
        }
        lock (s_startLock)
        {
            s_consecutiveRestartCount = 0;
        }
    }
}
