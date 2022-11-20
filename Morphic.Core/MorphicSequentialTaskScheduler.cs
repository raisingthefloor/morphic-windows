// Copyright 2022 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-core-lib-cs/blob/main/LICENSE
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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Morphic.Core
{
    // based on concepts from: https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.taskscheduler
    public class MorphicSequentialTaskScheduler : TaskScheduler, IDisposable
    {
        private readonly LinkedList<Task> _tasks;
        private object _tasksLock;
        private AutoResetEvent _tasksWaitHandle;
        //
        private Thread _tasksBackgroundThread;
        private bool _tasksThreadShouldShutdown;

        private bool _disposedValue;

        //private static MorphicSequentialTaskScheduler _current = null!;

        //public static MorphicSequentialTaskScheduler Current()
        //{
        //    if (_current is null)
        //    {
        //        _current = new MorphicSequentialTaskScheduler();
        //    }
        //    return _current;
        //}

        public MorphicSequentialTaskScheduler()
        {
            _tasks = new LinkedList<Task>();
            _tasksLock = new object();

            _tasksWaitHandle = new AutoResetEvent(false);

            _tasksThreadShouldShutdown = false;
            _tasksBackgroundThread = new Thread(this.ExecuteTasks);
            _tasksBackgroundThread.IsBackground = true;
            _tasksBackgroundThread.Start();
        }

        #region TaskScheduler overrides

        protected override IEnumerable<Task>? GetScheduledTasks()
        {
            return _tasks;
        }

        protected override void QueueTask(Task task)
        {
            lock (_tasksLock)
            {
                _tasks.AddLast(task);
            }
            try
            {
                _tasksWaitHandle.Set();
            }
            catch (ObjectDisposedException) { }
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            // in our current implementation, we don't support inlining
            return false;
        }

        public override int MaximumConcurrencyLevel => 1;

        private void ExecuteTasks()
        {
            while (_tasksThreadShouldShutdown == false)
            {
                // wait for tasks to be added
                _tasksWaitHandle.WaitOne();

                while (_tasksThreadShouldShutdown == false)
                {
                    Task? task = null;
                    lock (_tasksLock)
                    {
                        if (_tasks.Count >= 1)
                        {
                            task = _tasks.First?.Value;
                            _tasks.RemoveFirst();
                        }
                    }

                    // if there are no tasks currently queued, exit this loop so we can wait for another task to be added
                    if (task is null)
                    {
                        break;
                    }

                    // NOTE: if the task raises an exception, we should not catch that exception here (i.e. we should not need a watchdog on this thread)
                    _ = base.TryExecuteTask(task);
                }
            }
        }

        #endregion TaskScheduler overrides


        #region IDispose implementation 

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                _tasksThreadShouldShutdown = true;

                // trigger our tasks wait handle (so that our thread knows it should shut down)
                _tasksWaitHandle.Set();

                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    _tasksWaitHandle.Dispose();
                }

                // free unmanaged resources (unmanaged objects) and override finalizer

                // set large fields to null

                _disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~MorphicSequentialTaskScheduler()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion IDispose implementation
    }
}