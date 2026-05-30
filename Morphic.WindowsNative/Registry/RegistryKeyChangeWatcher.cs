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

namespace Morphic.WindowsNative.Registry;

public enum RegistryKeyChangeKind
{
    ValueChanged,
    TargetCreated,
    TargetDeleted,
}

public class RegistryKeyChangedEventArgs(RegistryKeyChangeKind kind) : EventArgs
{
    public RegistryKeyChangeKind Kind { get; } = kind;
}

[Flags]
public enum RegistryKeyChangeFilters
{
    SubkeyNameChanged = (int)Windows.Win32.System.Registry.REG_NOTIFY_FILTER.REG_NOTIFY_CHANGE_NAME,
    AttributesChanged = (int)Windows.Win32.System.Registry.REG_NOTIFY_FILTER.REG_NOTIFY_CHANGE_ATTRIBUTES,
    ValueChanged = (int)Windows.Win32.System.Registry.REG_NOTIFY_FILTER.REG_NOTIFY_CHANGE_LAST_SET,
    SecurityChanged = (int)Windows.Win32.System.Registry.REG_NOTIFY_FILTER.REG_NOTIFY_CHANGE_SECURITY,
}

public sealed class RegistryKeyChangeWatcher : IDisposable
{
    public interface ICreateError
    {
        public record NullArgument(string ArgumentName) : ICreateError;

        public record InvalidPathCharacters : ICreateError;

        public record UnsupportedHive(Microsoft.Win32.RegistryHive Hive) : ICreateError;

        public record KeyDisposed : ICreateError;

        public record UnparseableKeyName(string Name) : ICreateError;
    }

    private readonly Microsoft.Win32.RegistryHive _hive;
    private readonly Microsoft.Win32.RegistryView _view;
    private readonly string _targetSubKeyPath;
    private readonly RegistryKeyChangeFilters _targetNotifyFilter;
    private readonly bool _targetWatchSubtree;
    private readonly object _lock = new();

    private OpenedRegistryKeyChangeWatcher? _innerWatcher;
    private bool _innerIsTarget;
    private EventHandler<RegistryKeyChangedEventArgs>? _changed;
    private bool _disposed;

    private RegistryKeyChangeWatcher(
        Microsoft.Win32.RegistryHive hive,
        Microsoft.Win32.RegistryView view,
        string targetSubKeyPath,
        RegistryKeyChangeFilters targetNotifyFilter,
        bool targetWatchSubtree)
    {
        _hive = hive;
        _view = view;
        _targetSubKeyPath = targetSubKeyPath;
        _targetNotifyFilter = targetNotifyFilter;
        _targetWatchSubtree = targetWatchSubtree;
    }

    public static MorphicResult<RegistryKeyChangeWatcher, ICreateError> CreateForPath(
        Microsoft.Win32.RegistryHive hive,
        string targetSubKeyPath,
        Microsoft.Win32.RegistryView view = Microsoft.Win32.RegistryView.Default,
        RegistryKeyChangeFilters targetNotifyFilter = RegistryKeyChangeFilters.ValueChanged,
        bool targetWatchSubtree = false)
    {
        if (targetSubKeyPath is null)
        {
            return MorphicResult.ErrorResult<ICreateError>(new ICreateError.NullArgument(nameof(targetSubKeyPath)));
        }
        if (targetSubKeyPath.IndexOf('\0') >= 0)
        {
            return MorphicResult.ErrorResult<ICreateError>(new ICreateError.InvalidPathCharacters());
        }
        if (RegistryKeyChangeWatcher.IsSupportedHive(hive) == false)
        {
            return MorphicResult.ErrorResult<ICreateError>(new ICreateError.UnsupportedHive(hive));
        }

        var watcher = new RegistryKeyChangeWatcher(hive, view, targetSubKeyPath, targetNotifyFilter, targetWatchSubtree);
        return MorphicResult.OkResult(watcher);
    }

    public static MorphicResult<RegistryKeyChangeWatcher, ICreateError> CreateForKey(
        Microsoft.Win32.RegistryKey key,
        RegistryKeyChangeFilters targetNotifyFilter = RegistryKeyChangeFilters.ValueChanged,
        bool targetWatchSubtree = false)
    {
        if (key is null)
        {
            return MorphicResult.ErrorResult<ICreateError>(new ICreateError.NullArgument(nameof(key)));
        }

        string keyName;
        Microsoft.Win32.RegistryView view;
        try
        {
            keyName = key.Name;
            view = key.View;
        }
        catch
        {
            return MorphicResult.ErrorResult<ICreateError>(new ICreateError.KeyDisposed());
        }

        var parseResult = RegistryKeyChangeWatcher.ParseRegistryKeyName(keyName);
        if (parseResult.IsError)
        {
            return MorphicResult.ErrorResult<ICreateError>(new ICreateError.UnparseableKeyName(keyName));
        }
        var (hive, subKeyPath) = parseResult.Value!;

        return RegistryKeyChangeWatcher.CreateForPath(hive, subKeyPath, view, targetNotifyFilter, targetWatchSubtree);
    }

    public event EventHandler<RegistryKeyChangedEventArgs> Changed
    {
        add
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(RegistryKeyChangeWatcher));
                }

                bool wasFirstSubscriber = _changed is null;
                _changed += value;
                if (wasFirstSubscriber)
                {
                    this.StartInnerWatcherLocked();
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
                    this.StopInnerWatcherLocked();
                }
            }
        }
    }

    // Pre-requisite: caller MUST hold _lock.
    private void StartInnerWatcherLocked()
    {
        var openResult = this.OpenNearestExistingKey();
        if (openResult is null)
        {
            Debug.Assert(false, "Could not open any ancestor of the requested registry path; deferred watcher will not fire.");
            return;
        }
        var (key, isTarget) = openResult.Value;
        this.AttachInnerWatcherLocked(key, isTarget);
    }

    // Pre-requisite: caller MUST hold _lock.
    private void AttachInnerWatcherLocked(Microsoft.Win32.RegistryKey key, bool isTarget, bool allowRetry = true)
    {
        var notifyFilter = isTarget
            ? RegistryKeyChangeWatcher.ToRegNotifyFilter(_targetNotifyFilter)
            : Windows.Win32.System.Registry.REG_NOTIFY_FILTER.REG_NOTIFY_CHANGE_NAME;
        var watchSubtree = isTarget && _targetWatchSubtree;

        var createResult = OpenedRegistryKeyChangeWatcher.CreateForKey(key, watchSubtree: watchSubtree, notifyFilter: notifyFilter);
        if (createResult.IsError == true)
        {
            if (isTarget == true && allowRetry == true)
            {
                var ancestorResult = this.OpenNearestExistingKey();
                if (ancestorResult is not null)
                {
                    var (newKey, newIsTarget) = ancestorResult.Value;
                    this.AttachInnerWatcherLocked(newKey, isTarget: newIsTarget, allowRetry: false);
                }
            }
            return;
        }

        _innerWatcher = createResult.Value!;
        _innerWatcher.Changed += this.OnInnerWatcherChanged;
        _innerWatcher.WatcherStopped += this.OnInnerWatcherStopped;
        _innerIsTarget = isTarget;
    }

    private void OnInnerWatcherStopped(object? sender, OpenedRegistryKeyChangeWatcher.WatcherStoppedEventArgs args)
    {
        Morphic.WindowsNative.Registry.RegistryKeyChangeKind? changeKind = null;
        EventHandler<RegistryKeyChangedEventArgs>? handlersToFire = null;

        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            if (object.ReferenceEquals(sender, _innerWatcher) == false)
            {
                return;
            }

            var openResult = this.OpenNearestExistingKey();
            if (openResult is null)
            {
                this.StopInnerWatcherLocked();
                return;
            }
            var (newKey, newIsTarget) = openResult.Value;

            bool wasTarget = _innerIsTarget;
            this.StopInnerWatcherLocked();
            this.AttachInnerWatcherLocked(newKey, isTarget: newIsTarget);

            if (wasTarget == true && newIsTarget == false)
            {
                changeKind = Morphic.WindowsNative.Registry.RegistryKeyChangeKind.TargetDeleted;
                handlersToFire = _changed;
            }
        }

        if (handlersToFire is not null && changeKind is not null)
        {
            var eventArgs = new RegistryKeyChangedEventArgs(changeKind.Value);
            foreach (EventHandler<RegistryKeyChangedEventArgs> handler in handlersToFire.GetInvocationList())
            {
                try
                {
                    handler.Invoke(this, eventArgs);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"RegistryKeyChangeWatcher handler threw: {ex}");
                }
            }
        }
    }

    // Pre-requisite: caller MUST hold _lock.
    private void StopInnerWatcherLocked()
    {
        if (_innerWatcher is null)
        {
            return;
        }
        _innerWatcher.Changed -= this.OnInnerWatcherChanged;
        _innerWatcher.WatcherStopped -= this.OnInnerWatcherStopped;
        _innerWatcher.Dispose();
        _innerWatcher = null;
    }

    private void OnInnerWatcherChanged(object? sender, EventArgs e)
    {
        RegistryKeyChangeKind? changeKind = null;
        EventHandler<RegistryKeyChangedEventArgs>? handlersToFire = null;

        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            if (object.ReferenceEquals(sender, _innerWatcher) == false)
            {
                return;
            }

            var openResult = this.OpenNearestExistingKey();
            if (openResult is null)
            {
                this.StopInnerWatcherLocked();
                return;
            }
            var (newKey, newIsTarget) = openResult.Value;

            if (_innerIsTarget && newIsTarget)
            {
                newKey.Dispose();
                changeKind = RegistryKeyChangeKind.ValueChanged;
            }
            else if (_innerIsTarget && !newIsTarget)
            {
                this.StopInnerWatcherLocked();
                this.AttachInnerWatcherLocked(newKey, isTarget: false);
                changeKind = RegistryKeyChangeKind.TargetDeleted;
            }
            else if (!_innerIsTarget && newIsTarget)
            {
                this.StopInnerWatcherLocked();
                this.AttachInnerWatcherLocked(newKey, isTarget: true);
                changeKind = RegistryKeyChangeKind.TargetCreated;
            }
            else
            {
                this.StopInnerWatcherLocked();
                this.AttachInnerWatcherLocked(newKey, isTarget: false);
            }

            if (changeKind is not null)
            {
                handlersToFire = _changed;
            }
        }

        if (handlersToFire is not null && changeKind is not null)
        {
            var eventArgs = new RegistryKeyChangedEventArgs(changeKind.Value);
            foreach (EventHandler<RegistryKeyChangedEventArgs> handler in handlersToFire.GetInvocationList())
            {
                try
                {
                    handler.Invoke(this, eventArgs);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"RegistryKeyChangeWatcher handler threw: {ex}");
                }
            }
        }
    }

    private (Microsoft.Win32.RegistryKey key, bool isTarget)? OpenNearestExistingKey()
    {
        Microsoft.Win32.RegistryKey hiveKey;
        try
        {
            hiveKey = Microsoft.Win32.RegistryKey.OpenBaseKey(_hive, _view);
        }
        catch
        {
            return null;
        }

        if (string.IsNullOrEmpty(_targetSubKeyPath))
        {
            return (hiveKey, true);
        }

        try
        {
            var target = hiveKey.OpenSubKey(_targetSubKeyPath);
            if (target is not null)
            {
                hiveKey.Dispose();
                return (target, true);
            }
        }
        catch
        {
        }

        string current = _targetSubKeyPath;
        while (true)
        {
            int lastSlash = current.LastIndexOf('\\');
            if (lastSlash < 0)
            {
                return (hiveKey, false);
            }
            string parent = current.Substring(0, lastSlash);
            Microsoft.Win32.RegistryKey? parentKey;
            try
            {
                parentKey = hiveKey.OpenSubKey(parent);
            }
            catch
            {
                parentKey = null;
            }
            if (parentKey is not null)
            {
                hiveKey.Dispose();
                return (parentKey, false);
            }
            current = parent;
        }
    }

    private static MorphicResult<(Microsoft.Win32.RegistryHive Hive, string SubKeyPath), MorphicUnit> ParseRegistryKeyName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return MorphicResult.ErrorResult();
        }

        int firstSlash = name.IndexOf('\\');
        string hivePrefix = (firstSlash < 0) ? name : name.Substring(0, firstSlash);
        string subKeyPath = (firstSlash < 0) ? string.Empty : name.Substring(firstSlash + 1);

        Microsoft.Win32.RegistryHive? hive = hivePrefix switch
        {
            "HKEY_CLASSES_ROOT" => Microsoft.Win32.RegistryHive.ClassesRoot,
            "HKEY_CURRENT_USER" => Microsoft.Win32.RegistryHive.CurrentUser,
            "HKEY_LOCAL_MACHINE" => Microsoft.Win32.RegistryHive.LocalMachine,
            "HKEY_USERS" => Microsoft.Win32.RegistryHive.Users,
            "HKEY_CURRENT_CONFIG" => Microsoft.Win32.RegistryHive.CurrentConfig,
            "HKEY_PERFORMANCE_DATA" => Microsoft.Win32.RegistryHive.PerformanceData,
            _ => null,
        };
        if (hive is null)
        {
            return MorphicResult.ErrorResult();
        }

        return MorphicResult.OkResult((hive.Value, subKeyPath));
    }

    // Maps the public RegistryKeyChangeFilters flags to the internal CsWin32 REG_NOTIFY_FILTER
    // bitmask passed to RegNotifyChangeKeyValue. REG_NOTIFY_THREAD_AGNOSTIC is NOT added here: the
    // inner OpenedRegistryKeyChangeWatcher ORs it in at arm time (load-bearing for ThreadPool
    // re-arming, not a caller-selectable "kind of change").
    private static Windows.Win32.System.Registry.REG_NOTIFY_FILTER ToRegNotifyFilter(RegistryKeyChangeFilters filters)
    {
        Windows.Win32.System.Registry.REG_NOTIFY_FILTER result = 0;
        if (filters.HasFlag(RegistryKeyChangeFilters.SubkeyNameChanged))
        {
            result |= Windows.Win32.System.Registry.REG_NOTIFY_FILTER.REG_NOTIFY_CHANGE_NAME;
        }
        if (filters.HasFlag(RegistryKeyChangeFilters.AttributesChanged))
        {
            result |= Windows.Win32.System.Registry.REG_NOTIFY_FILTER.REG_NOTIFY_CHANGE_ATTRIBUTES;
        }
        if (filters.HasFlag(RegistryKeyChangeFilters.ValueChanged))
        {
            result |= Windows.Win32.System.Registry.REG_NOTIFY_FILTER.REG_NOTIFY_CHANGE_LAST_SET;
        }
        if (filters.HasFlag(RegistryKeyChangeFilters.SecurityChanged))
        {
            result |= Windows.Win32.System.Registry.REG_NOTIFY_FILTER.REG_NOTIFY_CHANGE_SECURITY;
        }
        return result;
    }

    private static bool IsSupportedHive(Microsoft.Win32.RegistryHive hive)
    {
        return hive switch
        {
            Microsoft.Win32.RegistryHive.ClassesRoot => true,
            Microsoft.Win32.RegistryHive.CurrentUser => true,
            Microsoft.Win32.RegistryHive.LocalMachine => true,
            Microsoft.Win32.RegistryHive.Users => true,
            Microsoft.Win32.RegistryHive.CurrentConfig => true,
            Microsoft.Win32.RegistryHive.PerformanceData => true,
            _ => false,
        };
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
            this.StopInnerWatcherLocked();
        }
    }
}
