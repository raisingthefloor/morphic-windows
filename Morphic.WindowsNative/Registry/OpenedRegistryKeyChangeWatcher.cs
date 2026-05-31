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

namespace Morphic.WindowsNative.Registry;

// Watches a single open registry key for value changes via Win32's RegNotifyChangeKeyValue.
// The key is provided already-opened by the caller (typically via
// Microsoft.Win32.Registry.CurrentUser.OpenSubKey(...) or similar). Instances are produced
// by the CreateForKey factory so the caller learns up front (via MorphicResult) about any
// arm-time failure (key deleted between open and arm, key lacks KEY_NOTIFY access, etc.).
// The watcher takes ownership of the key and disposes it as part of its own Dispose().
//
// Lifecycle ("eager always-on"):
//   1. CreateForKey validates the key, arms RegNotifyChangeKeyValue, AND registers the
//      ThreadPool wait -- all under a single lock acquisition. Once it returns Ok, the
//      watcher is ALREADY watching. There is no separate "start" step.
//   2. The watcher re-arms inside OnRegistryChanged after each notification. On a re-arm
//      failure (typically the key being deleted), it fires WatcherStopped, unregisters
//      the wait, and stops producing events.
//   3. WatcherStopped fires AT MOST ONCE per watcher (over its entire lifetime). Late
//      subscribers (those attaching after the event already fired) get a deferred
//      ThreadPool dispatch so they don't miss it.
//   4. Dispose tears everything down. Subscribing Changed or WatcherStopped after Dispose
//      throws ObjectDisposedException.
//
// Why eager always-on instead of lazy-on-subscribe: an earlier lazy model only registered
// the ThreadPool wait on the first Changed subscribe and unregistered it on the last
// unsubscribe, with the factory doing a "pre-arm" so it could surface arm-time errors.
// That created a stale-session race: a notification queued by the factory's pre-arm could
// fire after a subscribe/unsubscribe cycle had reset the watcher's session state, leading
// to a dropped event roughly 7% of the time in the harness. Always-on eliminates the gap
// between arming and waiting (both happen under a single lock acquisition), so no
// notification can ever be queued before the wait is ready to pick it up.
//
// Usage:
//   var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\...")!;
//   var createResult = OpenedRegistryKeyChangeWatcher.CreateForKey(key);
//   if (createResult.IsError) { /* react to ICreateError */ }
//   using var watcher = createResult.Value!;
//   watcher.Changed += (s, e) => { /* re-query the value */ };
//   watcher.WatcherStopped += (s, e) => { /* watcher is permanently done; e.Reason explains why */ };
//   ...
//   // watcher.Dispose() releases the wait, disposes the event, disposes the key.
//
// Behavior notes:
//   - RegNotifyChangeKeyValue does NOT report WHICH value inside the key changed; the
//     Changed event fires once per change-batch and consumers must re-read the values they
//     care about. This matches the underlying Win32 API.
//   - Win32 limitation: RegNotifyChangeKeyValue is a one-shot registration. There is a
//     small window between when our wait fires for a signal and when our callback re-arms
//     RegNotifyChangeKeyValue for the next signal. A change that occurs in that window may
//     coalesce with the next change's signal, but if no further change ever happens, it is
//     lost. The watcher does NOT work around this -- it is a property of the Win32 API.
//   - Changes that occur BEFORE the first Changed `+=` are silently dropped:
//     OnRegistryChanged sees a null _changed and just re-arms. Standard .NET event
//     semantics; equivalent to subscribing to any auto-event after it's already fired.
//   - Changes that occur while NO Changed handlers are subscribed (e.g., during a window
//     between unsubscribe and resubscribe) are similarly dropped. Always-on means the
//     watcher keeps consuming notifications even when nobody's listening; it does not
//     buffer them.
//   - Changed handlers are invoked on the ThreadPool callback thread (NOT the UI thread).
//     Subscribers that touch UI must marshal back to their dispatcher.
//   - Each handler is wrapped in try/catch so a throwing handler doesn't prevent the
//     others in the invocation list from running; exceptions are logged via
//     Debug.WriteLine.
//   - Re-arming RegNotifyChangeKeyValue happens INSIDE the watcher's lock to serialize
//     with Dispose -- without that, a callback could try to re-arm against a key the
//     disposer is simultaneously closing. The lock is released BEFORE dispatching handlers
//     so a handler that subscribes/unsubscribes can't deadlock.
internal sealed class OpenedRegistryKeyChangeWatcher : IDisposable
{
    // Discriminated error for ArmNotificationLocked. KeyDeleted is the documented
    // end-of-lifecycle for a watcher when the watched key is removed
    // (RegNotifyChangeKeyValue returns ERROR_KEY_DELETED on re-arm); callers that wrap
    // this watcher (e.g. RegistryKeyChangeWatcher) detect this case and transition to
    // ancestor-watching. OtherWin32Error carries through any unexpected status code so the
    // caller can decide how to surface it; this watcher's own callers currently treat any
    // error as "stop watching" but a future caller could distinguish.
    public interface IArmError
    {
        public record KeyDeleted : IArmError;
        public record OtherWin32Error(uint Win32ErrorCode) : IArmError;
    }

    // Errors returned by the CreateForKey factory. Failure here means "we could not
    // produce a working watcher with the supplied key" -- caller is responsible for
    // falling back (e.g., RegistryKeyChangeWatcher catches KeyDeleted and transitions to
    // ancestor-watching). NullArgument and KeyDisposed are caller bugs; KeyDeleted and
    // OtherWin32Error are environmental conditions (the key was deleted between open and
    // arm; the handle lacks KEY_NOTIFY access; etc.).
    public interface ICreateError
    {
        public record NullArgument : ICreateError;
        public record KeyDisposed : ICreateError;
        public record KeyDeleted : ICreateError;
        public record OtherWin32Error(uint Win32ErrorCode) : ICreateError;
    }

    private readonly Microsoft.Win32.RegistryKey _key;
    private readonly bool _watchSubtree;
    private readonly Windows.Win32.System.Registry.REG_NOTIFY_FILTER _notifyFilter;
    private readonly AutoResetEvent _notificationEvent;
    private readonly object _lock = new();

    private RegisteredWaitHandle? _registeredWait;
    private EventHandler? _changed;
    private EventHandler<WatcherStoppedEventArgs>? _watcherStoppedHandlers;
    // Set true the first time WatcherStopped fires for this watcher. Used to enforce
    // "fires at most once per watcher (lifetime)" and to deliver the event to late
    // WatcherStopped subscribers via deferred ThreadPool dispatch when they attach after
    // the event already fired. Never reset; remains true until Dispose.
    private bool _watcherStoppedFired;
    // Reason recorded when _watcherStoppedFired flipped to true; replayed to any late
    // WatcherStopped subscriber.
    private IArmError? _watcherStoppedReason;
    private bool _disposed;

    // Private constructor: all instances are created via CreateForKey so factory-time
    // validation (null/disposed key checks, RegNotifyChangeKeyValue arm + ThreadPool wait
    // registration) happens up front and the caller gets a MorphicResult instead of having
    // to handle a partially-constructed or silently-dead watcher.
    private OpenedRegistryKeyChangeWatcher(
        Microsoft.Win32.RegistryKey key,
        bool watchSubtree,
        Windows.Win32.System.Registry.REG_NOTIFY_FILTER notifyFilter)
    {
        _key = key;
        _watchSubtree = watchSubtree;
        _notifyFilter = notifyFilter;
        _notificationEvent = new AutoResetEvent(initialState: false);
    }

    // Creates a watcher for the supplied key. Validates that the key is non-null and not
    // disposed; arms RegNotifyChangeKeyValue and registers the ThreadPool wait inside the
    // SAME lock acquisition so by the time the factory returns Ok, the watcher is already
    // watching (no race window where a change could fire against an arm before the wait
    // has been registered).
    //
    // OWNERSHIP: the factory takes ownership of `key`. On success, the returned watcher
    // disposes `key` in its own Dispose. On failure, the factory disposes `key` before
    // returning -- caller must NOT use `key` after passing it in.
    //
    // The key must have been opened with KEY_NOTIFY access (or any access that implies it
    // -- the default KEY_READ that Microsoft.Win32.Registry.*.OpenSubKey produces includes
    // KEY_NOTIFY, so the default open path is sufficient).
    //
    // Win32 limitation (for awareness, not actionable): RegNotifyChangeKeyValue is a
    // one-shot registration -- there is a small window between when our wait fires for a
    // signal and when our callback re-arms RegNotifyChangeKeyValue for the next signal.
    // A change that occurs in this window is not delivered as a distinct event (it may
    // coalesce with the next change's signal, but if no further change ever happens, it
    // is lost). This is a property of the Win32 API; the watcher does not work around it.
    public static MorphicResult<OpenedRegistryKeyChangeWatcher, ICreateError> CreateForKey(
        Microsoft.Win32.RegistryKey key,
        bool watchSubtree = false,
        Windows.Win32.System.Registry.REG_NOTIFY_FILTER notifyFilter = Windows.Win32.System.Registry.REG_NOTIFY_FILTER.REG_NOTIFY_CHANGE_LAST_SET)
    {
        if (key is null)
        {
            return MorphicResult.ErrorResult<ICreateError>(new ICreateError.NullArgument());
        }
        try
        {
            // Reading .Name throws if the underlying SafeRegistryHandle has been disposed.
            // Doing this cheap probe up front means a caller-bug (passing a closed key)
            // surfaces as a factory error rather than a silent arm failure later.
            _ = key.Name;
        }
        catch
        {
            // Caller bug: we DO NOT take ownership of a key we can't even read the name
            // of. Returning without disposing matches the convention "factory takes
            // ownership ONLY on success" for the null-or-broken-input case; everything
            // else goes through the success path of the factory and Dispose path of the
            // watcher.
            return MorphicResult.ErrorResult<ICreateError>(new ICreateError.KeyDisposed());
        }

        var watcher = new OpenedRegistryKeyChangeWatcher(key, watchSubtree, notifyFilter);
        MorphicResult<MorphicUnit, IArmError> armResult;
        try
        {
            lock (watcher._lock)
            {
                // Arm + register the wait under the same lock so the watcher is "fully
                // armed and listening" by the time Ok is returned. A signal that arrives
                // any time after the arm completes (even before RegisterWaitForSingleObject
                // returns) is held by the AutoResetEvent and picked up by the ThreadPool
                // wait as soon as the registration takes effect.
                armResult = watcher.ArmNotificationLocked();
                if (armResult.IsSuccess == true)
                {
                    // executeOnlyOnce: false -- the ThreadPool keeps the wait registered
                    // after each signal; the AutoResetEvent resets itself between signals;
                    // our callback re-arms RegNotifyChangeKeyValue (which IS one-shot per
                    // call) for the next change.
                    watcher._registeredWait = ThreadPool.RegisterWaitForSingleObject(
                        watcher._notificationEvent,
                        watcher.OnRegistryChanged,
                        state: null,
                        millisecondsTimeOutInterval: Timeout.Infinite,
                        executeOnlyOnce: false);
                }
            }
        }
        catch (Exception)
        {
            // ArmNotificationLocked dereferences _key.Handle (a SafeRegistryHandle), which
            // throws ObjectDisposedException if the underlying handle has been closed --
            // possible if a different thread disposed the supplied key in the narrow
            // window between our .Name probe and arming. Translate to KeyDisposed and
            // clean up.
            watcher.Dispose();
            return MorphicResult.ErrorResult<ICreateError>(new ICreateError.KeyDisposed());
        }
        if (armResult.IsError == true)
        {
            // Factory took ownership of the key as soon as we passed it to the
            // constructor; dispose the watcher (which disposes the key and the event
            // handle) before returning the failure.
            watcher.Dispose();
            ICreateError createError = armResult.Error switch
            {
                IArmError.KeyDeleted => new ICreateError.KeyDeleted(),
                IArmError.OtherWin32Error otherError => new ICreateError.OtherWin32Error(otherError.Win32ErrorCode),
                _ => new ICreateError.OtherWin32Error(0),
            };
            return MorphicResult.ErrorResult(createError);
        }
        return MorphicResult.OkResult(watcher);
    }

    // Changed handlers receive notifications when the watched key's contents change.
    // Subscribing AFTER Dispose throws ObjectDisposedException. Subscribing or
    // unsubscribing while the watcher is alive is a pure list-mutation: the underlying
    // wait is always armed (eager always-on), so there are no side effects beyond the
    // add/remove itself.
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
                _changed += value;
            }
        }
        remove
        {
            lock (_lock)
            {
                _changed -= value;
            }
        }
    }

    // Payload for the WatcherStopped event. Reason explains why the watcher stopped --
    // typically IArmError.KeyDeleted (the documented end-of-lifecycle when the watched key
    // is removed) or IArmError.OtherWin32Error (an unexpected Win32 status, e.g., access
    // revoked mid-watch). Callers branch on the concrete type only if they need to react
    // differently to deletion vs. other failures.
    public sealed class WatcherStoppedEventArgs : EventArgs
    {
        public IArmError Reason { get; }
        public WatcherStoppedEventArgs(IArmError reason) { this.Reason = reason; }
    }

    // Fired AT MOST ONCE per watcher (over its entire lifetime) when
    // RegNotifyChangeKeyValue's re-arm fails. After the event fires, no further Changed
    // events will arrive -- the watcher is permanently done. The watcher remains in this
    // "stopped" state until Dispose; subsequent Changed subscriptions are legal but won't
    // see any events.
    //
    // Adding a handler AFTER WatcherStopped has already fired will deliver the event to
    // the new subscriber via a deferred ThreadPool dispatch, so late subscribers don't
    // miss it. The early-vs-late race is otherwise hard to avoid: Changed must usually be
    // subscribed before any arming happens, but WatcherStopped depends on Changed being
    // subscribed long enough to provoke the failure.
    //
    // Threading: handlers fire on a ThreadPool callback thread, like Changed. Subscribers
    // that touch UI must marshal back to their dispatcher. Per-handler try/catch isolates
    // a throwing handler from breaking the others.
    public event EventHandler<WatcherStoppedEventArgs> WatcherStopped
    {
        add
        {
            EventHandler<WatcherStoppedEventArgs>? deferredFireHandler = null;
            IArmError? deferredReason = null;
            lock (_lock)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(OpenedRegistryKeyChangeWatcher));
                }
                _watcherStoppedHandlers += value;
                if (_watcherStoppedFired == true)
                {
                    deferredFireHandler = value;
                    deferredReason = _watcherStoppedReason;
                }
            }
            if (deferredFireHandler is not null && deferredReason is not null)
            {
                // Defer outside the lock so the subscriber's `+=` call returns immediately
                // and the handler isn't invoked reentrantly from inside its own subscribe
                // call.
                _ = ThreadPool.QueueUserWorkItem(_ =>
                {
                    if (this.IsDisposed() == true)
                    {
                        return;
                    }
                    try
                    {
                        deferredFireHandler.Invoke(this, new WatcherStoppedEventArgs(deferredReason));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"OpenedRegistryKeyChangeWatcher late-fire WatcherStopped handler threw: {ex}");
                    }
                });
            }
        }
        remove
        {
            lock (_lock)
            {
                _watcherStoppedHandlers -= value;
            }
        }
    }

    // Helper for callbacks deferred via ThreadPool that need to skip firing if a Dispose
    // happened between the schedule and the run.
    private bool IsDisposed()
    {
        lock (_lock)
        {
            return _disposed;
        }
    }

    // Pre-requisite: caller MUST hold _lock OR be inside OnRegistryChanged (which
    // re-acquires the lock).
    private MorphicResult<MorphicUnit, IArmError> ArmNotificationLocked()
    {
        // REG_NOTIFY_THREAD_AGNOSTIC is load-bearing: without it, RegNotifyChangeKeyValue
        // ties the notification to the CALLING thread, and Windows silently drops future
        // notifications once that thread terminates. ThreadPool callback threads are
        // recycled freely between callbacks, so re-arming from OnRegistryChanged would
        // otherwise stop working unpredictably after a thread teardown. With
        // THREAD_AGNOSTIC the notification persists regardless of which thread armed it.
        // The flag is Windows 8+; we target Win10+ so it's always available.
        var modifiedNotifyFilter = _notifyFilter
            | Windows.Win32.System.Registry.REG_NOTIFY_FILTER.REG_NOTIFY_THREAD_AGNOSTIC;

        // fAsynchronous=true: don't block; signal _notificationEvent when the change occurs.
        var status = Windows.Win32.PInvoke.RegNotifyChangeKeyValue(
            _key.Handle,
            bWatchSubtree: _watchSubtree,
            dwNotifyFilter: modifiedNotifyFilter,
            hEvent: _notificationEvent.SafeWaitHandle,
            fAsynchronous: true);

        if (status == Windows.Win32.Foundation.WIN32_ERROR.NO_ERROR)
        {
            return MorphicResult.OkResult();
        }
        if (status == Windows.Win32.Foundation.WIN32_ERROR.ERROR_KEY_DELETED)
        {
            // Documented end-of-lifecycle: the key the watcher is anchored on has been
            // deleted. Re-arming is no longer possible; the caller is expected to either
            // replace the watcher or accept that further notifications won't fire. NOT an
            // assertion failure.
            return MorphicResult.ErrorResult<IArmError>(new IArmError.KeyDeleted());
        }
        // Anything else is genuinely unexpected (invalid handle, out of memory, etc.) --
        // worth alerting in Debug builds, but still surface it through MorphicResult so
        // the caller can decide; Debug.Assert in Release is compiled out and the
        // result-based handling takes over.
        Debug.Assert(false, $"RegNotifyChangeKeyValue failed with unexpected win32 error {(uint)status}");
        return MorphicResult.ErrorResult<IArmError>(new IArmError.OtherWin32Error((uint)status));
    }

    // Unregisters the ThreadPool wait. Used by OnRegistryChanged on arm failure (we won't
    // be re-arming again, so leaving the wait registered would leak it) and by Dispose.
    // The Unregister(null) call doesn't wait for an in-flight callback; that's intentional
    // because the callback acquires the same lock we already hold, so it can't currently
    // be inside its critical section.
    //
    // Pre-requisite: caller MUST hold _lock.
    private void UnregisterWaitLocked()
    {
        _registeredWait?.Unregister(waitObject: null);
        _registeredWait = null;
    }

    // ThreadPool callback: fires when _notificationEvent is signaled (the key changed).
    private void OnRegistryChanged(object? state, bool timedOut)
    {
        EventHandler? changedHandlers;
        EventHandler<WatcherStoppedEventArgs>? watcherStoppedHandlersToFire = null;
        IArmError? stoppedReason = null;

        // Re-arm and snapshot handlers under the lock to serialize with Dispose. If the
        // watcher was disposed between this notification firing and our callback acquiring
        // the lock, bail out -- the resources we'd re-arm against are gone, and there are
        // no subscribers to dispatch to anyway.
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }
            // If WatcherStopped already fired on an earlier callback, bail out. The
            // ThreadPool wait should already be unregistered, but the kernel may have
            // re-signaled the event once before the unregister fully took effect, leaving
            // one queued callback in flight; this guard makes that callback a no-op
            // instead of trying to re-arm against a key that's already been marked dead.
            if (_watcherStoppedFired == true)
            {
                return;
            }
            var armResult = this.ArmNotificationLocked();
            changedHandlers = _changed;

            if (armResult.IsError == true)
            {
                // Re-arm failed. This notification is the final one we will ever deliver.
                // Mark the watcher as permanently stopped, capture the WatcherStopped
                // handler snapshot under this same lock, and release the ThreadPool wait
                // registration so it doesn't leak.
                _watcherStoppedFired = true;
                _watcherStoppedReason = armResult.Error;
                stoppedReason = armResult.Error;
                watcherStoppedHandlersToFire = _watcherStoppedHandlers;
                this.UnregisterWaitLocked();
            }
        }

        // Dispatch outside the lock so a handler that subscribes/unsubscribes can't
        // deadlock. Iterate the invocation list manually with per-handler try/catch so
        // one throwing handler doesn't break the others. We do NOT Task.Run each handler
        // -- callers who want isolation can wrap their own handler in Task.Run; the
        // default of synchronous sequential dispatch matches standard .NET event
        // semantics.
        //
        // Order: Changed FIRST, WatcherStopped SECOND. A caller that re-reads the value
        // in the Changed handler will get a sensible "key gone" error from the BCL APIs
        // and can then learn (from WatcherStopped) that the watcher is permanently done.
        if (changedHandlers is not null)
        {
            foreach (EventHandler handler in changedHandlers.GetInvocationList())
            {
                try
                {
                    handler.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"OpenedRegistryKeyChangeWatcher Changed handler threw: {ex}");
                }
            }
        }
        if (watcherStoppedHandlersToFire is not null && stoppedReason is not null)
        {
            var eventArgs = new WatcherStoppedEventArgs(stoppedReason);
            foreach (EventHandler<WatcherStoppedEventArgs> handler in watcherStoppedHandlersToFire.GetInvocationList())
            {
                try
                {
                    handler.Invoke(this, eventArgs);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"OpenedRegistryKeyChangeWatcher WatcherStopped handler threw: {ex}");
                }
            }
        }
    }

    // Teardown order matters: unregister the ThreadPool wait FIRST so it stops watching
    // the event, THEN dispose the registry key (which invalidates any pending
    // notification request), THEN dispose the event. Done in the other order, the
    // ThreadPool can invoke the callback against a half-disposed watcher.
    //
    // The whole dispose runs inside the lock so a concurrent OnRegistryChanged sees
    // _disposed and bails out before touching resources. The Unregister(null) call
    // doesn't wait for an in-flight callback -- that's intentional, because the callback
    // acquires the same lock we already hold, so it CAN'T currently be inside its
    // critical section. After we leave the lock, any callback that was blocked entering
    // it will see _disposed and return.
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            this.UnregisterWaitLocked();
            //
            _key.Dispose();
            //
            _notificationEvent.Dispose();
        }
    }
}
