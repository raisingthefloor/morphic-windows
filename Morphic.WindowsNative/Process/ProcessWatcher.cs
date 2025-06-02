// Copyright 2022-2025 Raising the Floor - US, Inc.
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Morphic.WindowsNative.Process;

public class ProcessWatcher : IDisposable
{
    private static List<WeakReference<ProcessWatcher>> s_allInstances = new();
    private static object s_allInstancesLock = new();

    private static SortedSet<string>? s_runningProcesses;
    private static object s_runningProcessesLock = new();
    //
    private static System.Threading.Timer? s_watchTimer;
    private static object s_watchTimerLock = new();
    private static TimeSpan s_interval = new TimeSpan(0, 0, 1); // default interval of 1 second
    //
    private static System.Diagnostics.Stopwatch s_stopwatch = null!;
    private static long s_lastTimerTimestamp = 0;

    public class ProcessUpdatedEventArgs : EventArgs
    {
        public readonly string ProcessName;

        internal ProcessUpdatedEventArgs(string processName)
        {
            this.ProcessName = processName;
        }
    }

    // NOTE: if ProcessNamesWatchFilter is null, no filtering occurs against the list of process names; if ProcessNamesWatchFilter is not null, only filtered process names raise events
    // NOTE: filters are instantiated per-instance
    public ConcurrentBag<string>? ProcessNamesWatchFilter = null;
    private MorphicStringEqualityComparer StringEqualityComparer = new(StringComparison.InvariantCultureIgnoreCase);

    private event EventHandler<ProcessUpdatedEventArgs>? _processStarted = null;
    public event EventHandler<ProcessUpdatedEventArgs>? ProcessStarted
    {
        add
        {
            lock (_processStartedLock)
            {
                _processStarted += value;
            }
        }
        remove
        {
            lock (_processStartedLock)
            {
                _processStarted -= value;
            }
        }
    }
    private object _processStartedLock = new();
    //
    private event EventHandler<ProcessUpdatedEventArgs>? _processStopped = null;
    public event EventHandler<ProcessUpdatedEventArgs>? ProcessStopped
    {
        add
        {
            lock (_processStoppedLock)
            {
                _processStopped += value;
            }
        }
        remove
        {
            lock (_processStoppedLock)
            {
                _processStopped -= value;
            }
        }
    }
    private object _processStoppedLock = new();

    private ProcessWatcher()
    {
    }
    //
    public static ProcessWatcher CreateNew()
    {
        var processWatcher = new ProcessWatcher();
        lock (s_allInstancesLock)
        {
            s_allInstances.Add(new WeakReference<ProcessWatcher>(processWatcher));
        }
        return processWatcher;
    }
    //
    public void Dispose()
    {
        lock (s_allInstancesLock)
        {
            foreach (var instanceWeakReference in s_allInstances)
            {
                if (instanceWeakReference.TryGetTarget(out var instance) == true)
                {
                    if (instance == this)
                    {
                        s_allInstances.Remove(instanceWeakReference);
                    }
                }
            }
        }
    }

    public static void Start(TimeSpan interval)
    {
        if (interval.TotalMilliseconds > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "Argument 'interval' must not exceed Int32.MaxValue milliseconds");
        }

        // start our watcher with a list of all current process names
        var allProcessNames = Morphic.WindowsNative.Process.Process.GetCurrentProcessNames();
        lock (s_runningProcessesLock)
        {
            s_runningProcesses = new(allProcessNames);
        }

        // NOTE: we must start the timer AFTER populating our initial list of running processes; the timer event relies on an already-populated list
        lock (s_watchTimerLock)
        {
            if (s_watchTimer is not null)
            {
                throw new Exception("Cannot start a process watcher which is already running.");
            }

            s_stopwatch = new System.Diagnostics.Stopwatch();
            s_stopwatch.Start();

            s_watchTimer = new System.Threading.Timer(ProcessWatcher.OnTimer, null, (int)interval.TotalMilliseconds, (int)interval.TotalMilliseconds);
        }
    }

    // set (or update) the current interval 
    public static TimeSpan Interval
    {
        get
        {
            return s_interval;
        }
        set
        {
            var newInterval = value;
            var timeSinceLastEventInMilliseconds = s_stopwatch.ElapsedMilliseconds - s_lastTimerTimestamp;

            s_interval = newInterval;

            // if the timer is already running, then change its interval (and count any already-elapsed intra-timer-event time against the new interval)
            if (s_watchTimer is not null)
            {
                long newIntervalInMilliseconds = (long)value.TotalMilliseconds;
                long newDueTimeInMilliseconds = Math.Max(newIntervalInMilliseconds - timeSinceLastEventInMilliseconds, 0);
                var newDueTime = TimeSpan.FromMilliseconds((double)newDueTimeInMilliseconds);
                s_watchTimer?.Change(newDueTime, newInterval);
            }
        }
    }

    public static void Stop()
    {
        lock (s_watchTimerLock)
        {
            s_watchTimer?.Dispose();
            s_watchTimer = null;
        }
    }

    //

    //private static SemaphoreSlim s_onTimerSemaphoreLock = new(0, 1);
    private static SemaphoreSlim s_onTimerSemaphoreLock = new(1, 1);
    private static void OnTimer(object? state)
    {
        s_lastTimerTimestamp = s_stopwatch.ElapsedMilliseconds;

        var semaphoreEntered = s_onTimerSemaphoreLock.Wait(0);
        if (semaphoreEntered == false)
        {
            // if the lock was already taken, just skip this iteration; this should only happen with extremely short intervals (if .NET isn't smart enough to skip timer firing
            // when this function doesn't complete before a new interval has elapsed; this might be particularly of concern if the timer interval has been modified while OnTimer is still running)
            return;
        }
        //
        try
        {
            // capture an updated list of running processes
            var allProcessNames = Morphic.WindowsNative.Process.Process.GetCurrentProcessNames();
            var currentCheckProcesses = new SortedSet<string>(allProcessNames);

            SortedSet<string> previousCheckProcesses;
            lock (s_runningProcessesLock)
            {
                if (s_runningProcesses is not null)
                {
                    // make a deep copy of our list of running processes (to ensure that we're not just seeing references in an existing list)
                    previousCheckProcesses = new(s_runningProcesses!);
                }
                else
                {
                    // if there is no list, gracefully degrade by aborting
                    // NOTE: we may want to concern a more dramatic behavior like System.Diagnostics.Process.GetCurrentProcess().Kill()
                    System.Diagnostics.Debug.Assert(false, "List of running processes is null; this should never happen with a running timer (since we capture the list prior to initialization.  Aborting process watcher check.");
                    return;
                }
            }

            List<ProcessWatcher> allInstances = new();
            lock (s_allInstancesLock)
            {
                // make a deep copy of our list of all instances (unwrapping the weak references in the process)
                foreach (var instanceWeakReference in s_allInstances)
                {
                    if (instanceWeakReference.TryGetTarget(out var instance) == true)
                    {
                        allInstances.Add(instance);
                    }
                }
            }

            foreach (var instance in allInstances)
            {
                // detect started/stopped processes
                //
                // check for stopped processes
                foreach (var previousCheckProcess in previousCheckProcesses)
                {
                    if (currentCheckProcesses.Contains(previousCheckProcess) == false) // a process is no longer in the list of running processes (i.e. has exited)
                    {
                        // NOTE: ProcessNamesWatchFilter is thread safe (and may be updated by our object's consumer at will, even while this function is running)
                        var shouldFireEvent = instance.ProcessNamesWatchFilter?.Contains<string>(previousCheckProcess, instance.StringEqualityComparer) ?? true;
                        if (shouldFireEvent == true)
                        {
                            // notify any watchers that a process has stopped
                            Delegate[]? invocationList;
                            lock (instance._processStoppedLock)
                            {
                                invocationList = instance._processStopped?.GetInvocationList();
                            }
                            if (invocationList is not null)
                            {
                                foreach (EventHandler<ProcessUpdatedEventArgs> element in invocationList!)
                                {
                                    // NOTE: each invocation for this event will effectively be fired in parallel; it is the responsibility of the caller to provide thread safety (i.e. handle events sequentially)
                                    Task.Run(() =>
                                    {
                                        element.Invoke(instance, new ProcessUpdatedEventArgs(processName: previousCheckProcess));
                                    });
                                }
                            }
                        }
                    }
                }
                //
                // check for started processes
                foreach (var currentCheckProcess in currentCheckProcesses)
                {
                    if (previousCheckProcesses.Contains(currentCheckProcess) == false) // a process has been added to our list of running processes (i.e. has started)
                    {
                        var shouldFireEvent = instance.ProcessNamesWatchFilter?.Contains<string>(currentCheckProcess, instance.StringEqualityComparer) ?? true;
                        if (shouldFireEvent == true)
                        {
                            // notify any watchers that a process has started
                            Delegate[]? invocationList;
                            lock (instance._processStartedLock)
                            {
                                invocationList = instance._processStarted?.GetInvocationList();
                            }
                            if (invocationList is not null)
                            {
                                foreach (EventHandler<ProcessUpdatedEventArgs> element in invocationList!)
                                {
                                    // NOTE: each invocation for this event will effectively be fired in parallel; it is the responsibility of the caller to provide thread safety (i.e. handle events sequentially)
                                    Task.Run(() =>
                                    {
                                        element.Invoke(instance, new ProcessUpdatedEventArgs(processName: currentCheckProcess));
                                    });
                                }
                            }
                        }
                    }
                }
            }

            // update our set of running processes
            lock (s_runningProcessesLock)
            {
                s_runningProcesses = currentCheckProcesses;
            }
        }
        finally
        {
            s_onTimerSemaphoreLock.Release();
        }
    }
}
