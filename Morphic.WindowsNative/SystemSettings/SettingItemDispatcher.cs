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
internal static class SettingItemDispatcher
{
    private static readonly object s_startLock = new();
    private static Windows.System.DispatcherQueueController? s_controller;
    private static Windows.System.DispatcherQueue? s_dispatcherQueue;

    private static readonly TimeSpan s_runTimeout = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan s_initTimeout = TimeSpan.FromSeconds(3);

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

    private static bool TryRecoverDispatcher()
    {
        lock (s_startLock)
        {
            var nowMs = Environment.TickCount64;
            var elapsedSinceLastRestartMs = nowMs - s_lastRestartTicksMs;
            var backoff = SettingItemDispatcher.GetBackoffForRestartCount(s_consecutiveRestartCount);
            if (s_consecutiveRestartCount > 0 && elapsedSinceLastRestartMs < (long)backoff.TotalMilliseconds)
            {
                Debug.WriteLine($"[SettingItemDispatcher] Recovery refused: in backoff window ({elapsedSinceLastRestartMs}ms elapsed of {backoff.TotalMilliseconds}ms cooldown; restart #{s_consecutiveRestartCount} already happened).");
                return false;
            }

            s_lastRestartTicksMs = nowMs;
            s_consecutiveRestartCount++;

            var oldController = s_controller;
            s_controller = null;
            s_dispatcherQueue = null;

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
