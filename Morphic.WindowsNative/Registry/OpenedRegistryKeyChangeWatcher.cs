// Copyright 2026 Raising the Floor - US, Inc.
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
using System.Diagnostics;
using System.Threading;

namespace Morphic.WindowsNative;

public partial class Registry
{
    // Watches a single open registry key for value changes via Win32's RegNotifyChangeKeyValue.
    // The key is provided already-opened by the caller (typically via
    // Microsoft.Win32.Registry.CurrentUser.OpenSubKey(...) or similar); the watcher takes ownership
    // and disposes it as part of its own Dispose().
    //
    // Usage:
    //   var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\...")!;
    //   var watcher = new OpenedRegistryKeyChangeWatcher(key);
    //   watcher.Changed += (s, e) => { /* re-query the value */ };
    //   ...
    //   watcher.Dispose();   // releases the wait, disposes the event, disposes the key
    //
    // Behavior notes:
    //   - RegNotifyChangeKeyValue does NOT report WHICH value inside the key changed; the
    //     Changed event fires once per change-batch and consumers must re-read the values they
    //     care about. This matches the underlying Win32 API.
    //   - The watcher starts watching lazily on the first subscription to Changed and stops
    //     when the last subscriber detaches. Between those moments it holds no kernel handles.
    //   - Changed handlers are invoked on the ThreadPool callback thread (NOT the UI thread).
    //     Subscribers that touch UI must marshal back to their dispatcher.
    //   - Each handler is wrapped in try/catch so a throwing handler doesn't prevent the others
    //     in the invocation list from running; exceptions are logged via Debug.WriteLine.
    //   - Re-arming RegNotifyChangeKeyValue happens INSIDE the watcher's lock to serialize with
    //     Dispose -- without that, a callback could try to re-arm against a key the disposer is
    //     simultaneously closing. The lock is released BEFORE dispatching handlers so a handler
    //     that subscribes/unsubscribes can't deadlock.
    internal sealed class OpenedRegistryKeyChangeWatcher : IDisposable
    {
        private readonly Microsoft.Win32.RegistryKey _key;
        private readonly bool _watchSubtree;
        private readonly Windows.Win32.System.Registry.REG_NOTIFY_FILTER _notifyFilter;
        private readonly AutoResetEvent _notificationEvent;
        private readonly object _lock = new();

        private RegisteredWaitHandle? _registeredWait;
        private EventHandler? _changed;
        private bool _disposed;

        // Creates a watcher that observes the supplied key for value changes (REG_NOTIFY_CHANGE_LAST_SET
        // by default). The watcher takes OWNERSHIP of the key -- calling Dispose() on the watcher
        // disposes the key. Callers must NOT use the key after passing it in.
        //
        // The key must have been opened with KEY_NOTIFY access (or any access that implies it --
        // the default KEY_READ that Microsoft.Win32.Registry.*.OpenSubKey produces includes KEY_NOTIFY,
        // so the default open path is sufficient).
        public OpenedRegistryKeyChangeWatcher(
            Microsoft.Win32.RegistryKey key,
            bool watchSubtree = false,
            Windows.Win32.System.Registry.REG_NOTIFY_FILTER notifyFilter = Windows.Win32.System.Registry.REG_NOTIFY_FILTER.REG_NOTIFY_CHANGE_LAST_SET)
        {
            ArgumentNullException.ThrowIfNull(key);

            _key = key;
            _watchSubtree = watchSubtree;
            _notifyFilter = notifyFilter;
            _notificationEvent = new AutoResetEvent(initialState: false);
        }

        public event EventHandler Changed
        {
            add
            {
                lock (_lock)
                {
                    if (_disposed)
                    {
                        throw new ObjectDisposedException(nameof(OpenedRegistryKeyChangeWatcher));
                    }

                    bool wasFirstSubscriber = _changed is null;
                    _changed += value;
                    if (wasFirstSubscriber)
                    {
                        this.StartWatchingLocked();
                    }
                }
            }
            remove
            {
                lock (_lock)
                {
                    _changed -= value;
                    if (_changed is null)
                    {
                        this.StopWatchingLocked();
                    }
                }
            }
        }

        // Pre-requisite: caller MUST hold _lock.
        private void StartWatchingLocked()
        {
            if (this.ArmNotificationLocked().IsSuccess == false)
            {
                // arming failed (e.g. key was deleted between open and arm); leave the watcher in
                // a "subscribed but not watching" state. A future subscription/unsubscription cycle
                // will retry start.
                return;
            }

            // executeOnlyOnce: false -- the ThreadPool keeps the wait registered after each signal;
            // the AutoResetEvent resets itself between signals; our callback re-arms
            // RegNotifyChangeKeyValue (which IS one-shot per call) for the next change.
            _registeredWait = ThreadPool.RegisterWaitForSingleObject(
                _notificationEvent,
                this.OnRegistryChanged,
                state: null,
                millisecondsTimeOutInterval: Timeout.Infinite,
                executeOnlyOnce: false);
        }

        // Pre-requisite: caller MUST hold _lock OR be inside OnRegistryChanged (which re-acquires the lock).
        private MorphicResult<MorphicUnit, MorphicUnit> ArmNotificationLocked()
        {
            // REG_NOTIFY_THREAD_AGNOSTIC is load-bearing: without it, RegNotifyChangeKeyValue ties the
            // notification to the CALLING thread, and Windows silently drops future notifications once
            // that thread terminates. ThreadPool callback threads are recycled freely between callbacks,
            // so re-arming from OnRegistryChanged would otherwise stop working unpredictably after a
            // thread teardown. With THREAD_AGNOSTIC the notification persists regardless of which
            // thread armed it. The flag is Windows 8+; we target Win10+ so it's always available.
            var modifiedNotifyFilter = _notifyFilter
                | Windows.Win32.System.Registry.REG_NOTIFY_FILTER.REG_NOTIFY_THREAD_AGNOSTIC;

            // fAsynchronous=true: don't block; signal _notificationEvent when the change occurs.
            var status = Windows.Win32.PInvoke.RegNotifyChangeKeyValue(
                _key.Handle,
                bWatchSubtree: _watchSubtree,
                dwNotifyFilter: modifiedNotifyFilter,
                hEvent: _notificationEvent.SafeWaitHandle,
                fAsynchronous: true);

            if (status != Windows.Win32.Foundation.WIN32_ERROR.NO_ERROR)
            {
                Debug.Assert(false, $"RegNotifyChangeKeyValue failed with win32 error {(uint)status}");
                return MorphicResult.ErrorResult();
            }
            return MorphicResult.OkResult();
        }

        // Pre-requisite: caller MUST hold _lock.
        private void StopWatchingLocked()
        {
            _registeredWait?.Unregister(waitObject: null);
            _registeredWait = null;
        }

        // ThreadPool callback: fires when _notificationEvent is signaled (the key changed).
        private void OnRegistryChanged(object? state, bool timedOut)
        {
            EventHandler? handlers;

            // Re-arm and snapshot handlers under the lock to serialize with Dispose. If the watcher
            // was disposed between this notification firing and our callback acquiring the lock,
            // bail out -- the resources we'd re-arm against are gone, and there are no subscribers
            // to dispatch to anyway.
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }
                this.ArmNotificationLocked();
                handlers = _changed;
            }

            // Dispatch outside the lock so a handler that subscribes/unsubscribes can't deadlock.
            // Iterate the invocation list manually with per-handler try/catch so one throwing
            // handler doesn't break the others. We do NOT Task.Run each handler -- callers who
            // want isolation can wrap their own handler in Task.Run; the default of synchronous
            // sequential dispatch matches standard .NET event semantics.
            if (handlers is not null)
            {
                foreach (EventHandler handler in handlers.GetInvocationList())
                {
                    try
                    {
                        handler.Invoke(this, EventArgs.Empty);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"OpenedRegistryKeyChangeWatcher handler threw: {ex}");
                    }
                }
            }
        }

        // Teardown order matters: unregister the ThreadPool wait FIRST so it stops watching the
        // event, THEN dispose the registry key (which invalidates any pending notification
        // request), THEN dispose the event. Done in the other order, the ThreadPool can invoke
        // the callback against a half-disposed watcher.
        //
        // The whole dispose runs inside the lock so a concurrent OnRegistryChanged sees _disposed
        // and bails out before touching resources. The Unregister(null) call doesn't wait for an
        // in-flight callback -- that's intentional, because the callback acquires the same lock
        // we already hold, so it CAN'T currently be inside its critical section. After we leave
        // the lock, any callback that was blocked entering it will see _disposed and return.
        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;

                _registeredWait?.Unregister(waitObject: null);
                _registeredWait = null;
                //
                _key.Dispose();
                //
                _notificationEvent.Dispose();
            }
        }
    }
}