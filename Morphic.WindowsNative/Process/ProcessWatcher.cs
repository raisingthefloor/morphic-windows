// Copyright 2022-2023 Raising the Floor - US, Inc.
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

public class ProcessWatcher
{
     private System.Threading.Timer? _watchTimer;

     public ConcurrentBag<string>? ProcessNamesWatchFilter = null;

     private System.Collections.Generic.SortedSet<string>? _runningProcesses;
     private object _runningProcessesLock = new();

     private MorphicStringEqualityComparer _stringEqualityComparer = new(StringComparison.InvariantCultureIgnoreCase);

     public class ProcessUpdatedEventArgs : EventArgs
     {
          public string ProcessName;

          internal ProcessUpdatedEventArgs(string processName)
          {
               this.ProcessName = processName;
          }
     }
     //
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

     public void Start(TimeSpan interval)
     {
          if (interval.TotalMilliseconds > int.MaxValue)
          {
               throw new ArgumentOutOfRangeException(nameof(interval), "Argument 'interval' must not exceed Int32.MaxValue milliseconds");
          }

          var allProcessNames = Morphic.WindowsNative.Process.Process.GetCurrentProcessNames();
          _runningProcesses = new(allProcessNames);

          _watchTimer = new System.Threading.Timer(this.OnTimer, null, (int)interval.TotalMilliseconds, (int)interval.TotalMilliseconds);
     }

     public void Stop()
     {
          _watchTimer?.Dispose();
     }

     //

     //private SemaphoreSlim _onTimerSemaphoreLock = new(0, 1);
     private SemaphoreSlim _onTimerSemaphoreLock = new(1, 1);
     private void OnTimer(object? state)
     {
          var semaphoreEntered = _onTimerSemaphoreLock.Wait(0);
          if (semaphoreEntered == false)
          {
               // if the lock was already taken, just skip this iteration; this should only happen with extremely short intervals (if .NET isn't smart enough to skip callbacks
               // when this function doesn't complete before the interval has elapsed)
               return;
          }
          //
          try
          {
               // capture an updated list of running processes
               var allProcessNames = Morphic.WindowsNative.Process.Process.GetCurrentProcessNames();
               var currentCheckProcesses = new SortedSet<string>(allProcessNames);

               SortedSet<string> lastCheckProcesses;
               lock (_runningProcessesLock)
               {
                    if (_runningProcesses is not null)
                    {
                         var runningProcessesAsStringArray = new string[_runningProcesses!.Count];
                         _runningProcesses!.CopyTo(runningProcessesAsStringArray);
                         //
                         lastCheckProcesses = new(runningProcessesAsStringArray);
                    }
                    else
                    {
                         lastCheckProcesses = new();
                    }
               }

               // detect started/stopped processes
               //
               // check for stopped processes
               foreach (var lastCheckProcess in lastCheckProcesses)
               {
                    if (currentCheckProcesses.Contains(lastCheckProcess) == false)
                    {
                         var shouldFireEvent = this.ProcessNamesWatchFilter?.Contains<string>(lastCheckProcess, _stringEqualityComparer) ?? true;
                         if (shouldFireEvent == true)
                         {
                              // notify any watchers that a process has stopped
                              Delegate[]? invocationList;
                              lock (_processStartedLock)
                              {
                                   invocationList = _processStopped?.GetInvocationList();
                              }
                              if (invocationList is not null)
                              {
                                   foreach (EventHandler<ProcessUpdatedEventArgs> element in invocationList!)
                                   {
                                        // NOTE: each invocation for this event will effectively be fired in parallel; it is the responsibility of the caller to provide thread safety (i.e. handle events sequentially)
                                        Task.Run(() => {
                                             element.Invoke(this, new ProcessUpdatedEventArgs(processName: lastCheckProcess));
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
                    if (lastCheckProcesses.Contains(currentCheckProcess) == false)
                    {
                         var shouldFireEvent = this.ProcessNamesWatchFilter?.Contains<string>(currentCheckProcess, _stringEqualityComparer) ?? true;
                         if (shouldFireEvent == true)
                         {
                              // notify any watchers that a process has started
                              Delegate[]? invocationList;
                              lock (_processStartedLock)
                              {
                                   invocationList = _processStarted?.GetInvocationList();
                              }
                              if (invocationList is not null)
                              {
                                   foreach (EventHandler<ProcessUpdatedEventArgs> element in invocationList!)
                                   {
                                        // NOTE: each invocation for this event will effectively be fired in parallel; it is the responsibility of the caller to provide thread safety (i.e. handle events sequentially)
                                        Task.Run(() =>
                                        {
                                             element.Invoke(this, new ProcessUpdatedEventArgs(processName: currentCheckProcess));
                                        });
                                   }
                              }
                         }
                    }
               }

               // update our set of running processes
               lock (_runningProcessesLock)
               {
                    _runningProcesses = currentCheckProcesses;
               }
          }
          finally
          {
               _onTimerSemaphoreLock.Release();
          }
     }
}
