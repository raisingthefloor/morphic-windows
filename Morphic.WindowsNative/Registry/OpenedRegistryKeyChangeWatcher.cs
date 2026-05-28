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

internal sealed class OpenedRegistryKeyChangeWatcher : IDisposable
{
    public interface IArmError
    {
        public record KeyDeleted : IArmError;
        public record OtherWin32Error(uint Win32ErrorCode) : IArmError;
    }

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
    private bool _watcherStoppedFired;
    private IArmError? _watcherStoppedReason;
    private bool _disposed;

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
            _ = key.Name;
        }
        catch
        {
            return MorphicResult.ErrorResult<ICreateError>(new ICreateError.KeyDisposed());
        }

        var watcher = new OpenedRegistryKeyChangeWatcher(key, watchSubtree, notifyFilter);
        MorphicResult<MorphicUnit, IArmError> armResult;
        try
        {
            lock (watcher._lock)
            {
                armResult = watcher.ArmNotificationLocked();
                if (armResult.IsSuccess == true)
                {
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
            watcher.Dispose();
            return MorphicResult.ErrorResult<ICreateError>(new ICreateError.KeyDisposed());
        }
        if (armResult.IsError == true)
        {
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

    public sealed class WatcherStoppedEventArgs : EventArgs
    {
        public IArmError Reason { get; }
        public WatcherStoppedEventArgs(IArmError reason) { this.Reason = reason; }
    }

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

    private bool IsDisposed()
    {
        lock (_lock)
        {
            return _disposed;
        }
    }

    private MorphicResult<MorphicUnit, IArmError> ArmNotificationLocked()
    {
        var modifiedNotifyFilter = _notifyFilter
            | Windows.Win32.System.Registry.REG_NOTIFY_FILTER.REG_NOTIFY_THREAD_AGNOSTIC;

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
            return MorphicResult.ErrorResult<IArmError>(new IArmError.KeyDeleted());
        }
        Debug.Assert(false, $"RegNotifyChangeKeyValue failed with unexpected win32 error {(uint)status}");
        return MorphicResult.ErrorResult<IArmError>(new IArmError.OtherWin32Error((uint)status));
    }

    private void UnregisterWaitLocked()
    {
        _registeredWait?.Unregister(waitObject: null);
        _registeredWait = null;
    }

    private void OnRegistryChanged(object? state, bool timedOut)
    {
        EventHandler? changedHandlers;
        EventHandler<WatcherStoppedEventArgs>? watcherStoppedHandlersToFire = null;
        IArmError? stoppedReason = null;

        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }
            if (_watcherStoppedFired == true)
            {
                return;
            }
            var armResult = this.ArmNotificationLocked();
            changedHandlers = _changed;

            if (armResult.IsError == true)
            {
                _watcherStoppedFired = true;
                _watcherStoppedReason = armResult.Error;
                stoppedReason = armResult.Error;
                watcherStoppedHandlersToFire = _watcherStoppedHandlers;
                this.UnregisterWaitLocked();
            }
        }

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
