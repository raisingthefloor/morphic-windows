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
using System.Collections.Concurrent;
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
// The worker is `IsBackground=true` so it doesn't block process exit, and is started lazily
// on first dispatch so projects that never touch SystemSettings pay nothing for it.
internal static class SettingItemDispatcher
{
    private static readonly object s_startLock = new();
    private static Thread? s_workerThread;
    private static BlockingCollection<Action>? s_workQueue;

    private static void EnsureStarted()
    {
        if (s_workerThread is not null)
        {
            return;
        }
        lock (s_startLock)
        {
            if (s_workerThread is not null)
            {
                return;
            }

            var queue = new BlockingCollection<Action>();
            var thread = new Thread(() => SettingItemDispatcher.WorkerLoop(queue))
            {
                IsBackground = true,
                Name = "Morphic.SettingItem.STA",
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            s_workQueue = queue;
            s_workerThread = thread;
        }
    }

    private static void WorkerLoop(BlockingCollection<Action> queue)
    {
        foreach (var workItem in queue.GetConsumingEnumerable())
        {
            // Per-item try/catch is defensive only. The submit-side wrappers (Run/Run<T>)
            // already wrap the caller delegate in a try/catch that captures exceptions via
            // ExceptionDispatchInfo; anything reaching here would be a bug in the wrapper
            // itself (e.g. signal not set) or in BlockingCollection. Swallow rather than tear
            // down the worker thread.
            try
            {
                workItem();
            }
            catch
            {
            }
        }
    }

    // Synchronously runs `func` on the STA worker thread and returns its result. If `func`
    // throws, the exception is rethrown on the calling thread with its original stack
    // preserved via ExceptionDispatchInfo.
    public static T Run<T>(Func<T> func)
    {
        SettingItemDispatcher.EnsureStarted();

        using var doneSignal = new ManualResetEventSlim(false);
        T result = default!;
        ExceptionDispatchInfo? capturedException = null;

        s_workQueue!.Add(() =>
        {
            try
            {
                result = func();
            }
            catch (Exception ex)
            {
                capturedException = ExceptionDispatchInfo.Capture(ex);
            }
            finally
            {
                doneSignal.Set();
            }
        });

        doneSignal.Wait();

        capturedException?.Throw();
        return result;
    }

    // Synchronously runs `action` on the STA worker thread. If `action` throws, the exception
    // is rethrown on the calling thread with its original stack preserved.
    public static void Run(Action action)
    {
        SettingItemDispatcher.EnsureStarted();

        using var doneSignal = new ManualResetEventSlim(false);
        ExceptionDispatchInfo? capturedException = null;

        s_workQueue!.Add(() =>
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
                doneSignal.Set();
            }
        });

        doneSignal.Wait();

        capturedException?.Throw();
    }
}
