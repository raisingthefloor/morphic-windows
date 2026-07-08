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

// Categorizes which kind of change the watcher just observed, so subscribers can branch on the
// transition without having to do their own before/after probe. ValueChanged is the common case
// (the target key still exists, but one or more values inside it changed); TargetCreated and
// TargetDeleted fire when the target appears or disappears as a whole.
public enum RegistryKeyChangeKind
{
    ValueChanged,
    TargetCreated,
    TargetDeleted,
}

// EventArgs payload for RegistryKeyChangeWatcher.Changed. Carries the kind of transition that
// just occurred. The watcher doesn't re-query the value(s) for the subscriber -- the subscriber
// is the only one who knows which values it cares about -- so any value data must still come
// from a follow-up read in the handler.
public class RegistryKeyChangedEventArgs(RegistryKeyChangeKind kind) : EventArgs
{
    public RegistryKeyChangeKind Kind { get; } = kind;
}

// Public, assembly-stable selector for which kinds of change RegistryKeyChangeWatcher should watch
// for inside its target key. We expose this rather than the CsWin32-generated REG_NOTIFY_FILTER
// because that generated enum is internal to this assembly, and a public factory method can't take
// an internal type as a parameter. The member values are the Win32 REG_NOTIFY_CHANGE_* constants
// themselves (cast to int) so they stay in lockstep with the system definitions; the cast is a
// compile-time constant baked in as a literal, so no internal type leaks into this public enum's
// metadata. REG_NOTIFY_THREAD_AGNOSTIC is intentionally omitted: the watcher ORs it in internally
// (load-bearing for ThreadPool re-arming) and it isn't a caller-selectable "kind of change."
[Flags]
public enum RegistryKeyChangeFilters
{
    SubkeyNameChanged = (int)Windows.Win32.System.Registry.REG_NOTIFY_FILTER.REG_NOTIFY_CHANGE_NAME,
    AttributesChanged = (int)Windows.Win32.System.Registry.REG_NOTIFY_FILTER.REG_NOTIFY_CHANGE_ATTRIBUTES,
    ValueChanged = (int)Windows.Win32.System.Registry.REG_NOTIFY_FILTER.REG_NOTIFY_CHANGE_LAST_SET,
    SecurityChanged = (int)Windows.Win32.System.Registry.REG_NOTIFY_FILTER.REG_NOTIFY_CHANGE_SECURITY,
}

// Watches a registry key by PATH (rather than by an already-opened handle), and defers wiring up
// the real change watcher until the key actually exists. When the target key is missing, the
// watcher walks up the path to find the nearest existing ancestor and watches THAT for subkey
// creation/deletion via REG_NOTIFY_CHANGE_NAME. As soon as the target appears (or an intermediate
// key is created so a closer ancestor becomes watchable), the watcher transitions and starts
// watching the target proper. If the target is later deleted, the watcher transparently falls back
// to ancestor-watching so creation can be detected again -- callers don't have to re-subscribe.
//
// Use this instead of OpenedRegistryKeyChangeWatcher whenever the target key MAY not exist at
// subscription time, or MAY be deleted and re-created during the lifetime of the subscription
// (e.g. Windows feature toggle keys like HKCU\SOFTWARE\Microsoft\ColorFiltering, which only get
// created when the user first toggles the feature). Using OpenedRegistryKeyChangeWatcher directly in
// those cases silently drops the subscription if the key isn't there at start time, and never
// notices when the key later appears -- the bug this class exists to fix.
//
// The Changed event fires for:
//   * a value change inside the target key (when the target exists) -- Kind = ValueChanged,
//   * the target appearing (transition from missing to present)    -- Kind = TargetCreated,
//   * the target disappearing (transition from present to missing) -- Kind = TargetDeleted.
// The RegistryKeyChangedEventArgs payload carries the Kind so callers can branch on the
// transition without doing their own before/after probe; it does NOT carry the new value
// data, since the watcher doesn't know which values the caller cares about. Callers that
// maintain a cached state (e.g. a typed *Changed event with new value args) should treat
// "target doesn't exist" as the system default for the setting they're tracking
// (e.g. ColorFilters treats a missing key as "filtering is off").
//
// Threading:
//   * Changed fires on a ThreadPool callback (inherited from the inner OpenedRegistryKeyChangeWatcher).
//     Subscribers that touch UI must marshal back to their dispatcher.
//   * Each handler is wrapped in try/catch so a throwing handler doesn't break the others.
//
// Lifecycle:
//   * Construction is factory-only (CreateForPath / CreateForKey) so the caller can distinguish
//     invalid-argument errors (returned as MorphicResult.ErrorResult) from runtime watch failures
//     (silent re-arm retry).
//   * The inner watcher (and its kernel handle) is lazily created on the first Changed
//     subscription and torn down when the last subscriber detaches.
//   * Disposing this watcher tears down the inner watcher and stops all dispatching.
//
// Dispose contract (matches standard .NET event semantics; intentionally weaker than a
// hard barrier):
//   * After Dispose returns, no NEW Changed events will be dispatched -- the inner
//     watcher's wait registration has been cancelled and any future kernel signal is
//     ignored.
//   * However, a Changed handler that was ALREADY snapshotted into a dispatch invocation
//     list (OnInnerWatcherChanged / OnInnerWatcherStopped grab _changed under _lock, then
//     release the lock before invoking handlers) MAY still be executing when Dispose
//     returns, or may even start executing slightly after. This is a deliberate trade-off
//     to avoid a hard fence that would block Dispose against an arbitrarily-slow caller
//     handler. Callers that touch their own state from inside the Changed handler should
//     either be tolerant of "I ran one extra time after I unsubscribed" (idempotent
//     handlers) or take their own lock + check a disposed flag at the top of the handler.
//   * Subscribing Changed after Dispose throws ObjectDisposedException; unsubscribing
//     after Dispose is harmless (no-op).
//   * Calling Dispose twice is harmless (no-op on the second call).
public sealed class RegistryKeyChangeWatcher : IDisposable
{
    // Errors returned by the factory methods. Runtime errors during watching (failed re-arm,
    // ancestor walk surprises, etc.) are NOT surfaced here -- they're handled internally and
    // the watcher keeps trying. ICreateError covers only the cases where construction itself
    // can't proceed.
    // FUTURE: this error result-type interface is slated to become a discriminated union (via the new C# 'union' language feature) once C# 11 or 12 (long-term) ships.
    public interface ICreateError
    {
        // The supplied path or key was null.
        public record NullArgument(string ArgumentName) : ICreateError;

        // The supplied path contains a null character (\0). Other character validation is left to
        // the OS (RegOpenKey will reject paths with invalid chars at open time and the deferred
        // watcher will just keep ancestor-watching forever -- not a hard failure, but pointing
        // out the null char up front catches obviously-broken paths.)
        public record InvalidPathCharacters : ICreateError;

        // The supplied hive isn't one of the standard hives that OpenBaseKey supports
        // (CurrentUser, LocalMachine, ClassesRoot, Users, CurrentConfig, PerformanceData).
        public record UnsupportedHive(Microsoft.Win32.RegistryHive Hive) : ICreateError;

        // The supplied key is in a disposed state and its Name can't be read.
        public record KeyDisposed : ICreateError;

        // The supplied key's Name doesn't start with a recognized HKEY_* hive prefix, so we
        // can't derive a (hive, subKeyPath) pair from it. Vanishingly unlikely for keys obtained
        // through the standard BCL APIs.
        public record UnparseableKeyName(string Name) : ICreateError;
    }

    private readonly Microsoft.Win32.RegistryHive _hive;
    private readonly Microsoft.Win32.RegistryView _view;
    private readonly string _targetSubKeyPath;
    private readonly RegistryKeyChangeFilters _targetNotifyFilter;
    private readonly bool _targetWatchSubtree;
    private readonly object _lock = new();

    private OpenedRegistryKeyChangeWatcher? _innerWatcher;
    // True when _innerWatcher is on the target key (State A); false when on an ancestor (State B).
    private bool _innerIsTarget;
    private EventHandler<RegistryKeyChangedEventArgs>? _changed;
    private bool _disposed;

    // Private: construction is factory-only via CreateForPath / CreateForKey. The factories
    // validate inputs and return ErrorResult on bad arguments; this constructor assumes its
    // arguments are already valid.
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

    // Creates a deferred watcher for the given (hive, subKeyPath) pair.
    //
    // targetSubKeyPath: relative path from the hive root, e.g. "SOFTWARE\Microsoft\ColorFiltering".
    //                   Empty string means "watch the hive root directly" (rarely needed; hive
    //                   roots always exist, so this degrades to a regular watcher on the root).
    // view: 32-bit vs 64-bit registry view (default = process-native).
    // targetNotifyFilter: which kinds of changes to watch for inside the target key (default
    //                     RegistryKeyChangeFilters.ValueChanged = value writes). Only applied when
    //                     the target exists; ancestor-watching always watches for subkey
    //                     creation/deletion regardless of this argument.
    // targetWatchSubtree: passed through to the inner watcher when watching the target. Has no
    //                     effect during ancestor-watching (we always use subtree=false there).
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

    // Creates a deferred watcher for an already-opened registry key. Extracts the key's hive,
    // view, and path from key.Name + key.View, then DOES NOT take ownership of the key -- the
    // caller can dispose it (or keep using it) independently. The watcher re-opens the path on
    // demand via OpenBaseKey, so it doesn't need the caller's handle to live on.
    //
    // The captured path lets the watcher handle key deletion / re-creation transparently: if
    // the originally-supplied key is later deleted, the watcher falls back to ancestor-watching
    // and re-attaches when the key is re-created.
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
            // .Name reads from the underlying SafeRegistryHandle; throws ObjectDisposedException
            // (wrapped in IOException on some BCL paths) if the key has already been closed.
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
            // Catastrophic: even the hive root couldn't be opened. This is essentially impossible
            // for the standard hives (CurrentUser etc.) under normal Windows, but treat it as
            // "subscribed but not watching" rather than crashing -- the next subscription cycle
            // will retry.
            Debug.Assert(false, "Could not open any ancestor of the requested registry path; deferred watcher will not fire.");
            return;
        }
        var (key, isTarget) = openResult.Value;
        this.AttachInnerWatcherLocked(key, isTarget);
    }

    // Pre-requisite: caller MUST hold _lock.
    //
    // allowRetry: true on the initial call from StartInnerWatcherLocked / OnInnerWatcherChanged;
    // false on the single recursive retry below. Limits us to one re-walk so we can't loop
    // forever if both the target and its nearest existing ancestor consistently fail to arm
    // (e.g., target is in an access-denied state that survives across re-walks).
    private void AttachInnerWatcherLocked(Microsoft.Win32.RegistryKey key, bool isTarget, bool allowRetry = true)
    {
        // Ancestor-watching always uses REG_NOTIFY_CHANGE_NAME + subtree=false: we want to learn
        // when a subkey is created or deleted directly under the ancestor we're watching, nothing
        // else. Subtree=true would amplify the notification noise unnecessarily (every change
        // anywhere under the ancestor would fire) and watching for value changes on the ancestor
        // doesn't help us detect the target's creation.
        var notifyFilter = isTarget
            ? RegistryKeyChangeWatcher.ToRegNotifyFilter(_targetNotifyFilter)
            : Windows.Win32.System.Registry.REG_NOTIFY_FILTER.REG_NOTIFY_CHANGE_NAME;
        var watchSubtree = isTarget && _targetWatchSubtree;

        // Use the factory so any arm-time failure (key deleted between open and arm, lacks
        // KEY_NOTIFY access, etc.) surfaces here as an error instead of stranding a silent
        // watcher. The factory takes ownership of `key` -- on failure, it disposes the key
        // for us, so we don't need to dispose it ourselves on the error path.
        var createResult = OpenedRegistryKeyChangeWatcher.CreateForKey(key, watchSubtree: watchSubtree, notifyFilter: notifyFilter);
        if (createResult.IsError == true)
        {
            // CreateForKey failed. For the target case, this is most likely a race where
            // the target was just deleted between OpenNearestExistingKey and the arm
            // attempt; re-walk once to find the new nearest existing ancestor and recurse
            // with whatever isTarget that produces. We honor the re-walk's isTarget rather
            // than forcing it to false: if the target was recreated between our first walk
            // and now, attaching a non-target watcher to it would silently use the wrong
            // notify filter (REG_NOTIFY_CHANGE_NAME instead of LAST_SET) and miss every
            // value change the caller cares about. The allowRetry guard caps us at one
            // recursion so a persistently-unwatchable target can't loop.
            //
            // For the ancestor case, this is essentially impossible (we just opened the
            // ancestor a moment ago); silently leave the outer in a "stopped" state -- the
            // next subscribe cycle will retry from scratch.
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
        // WatcherStopped fires if the inner's mid-life re-arm fails (e.g., access revoked,
        // or KeyDeleted in a window where Changed-then-rewalk wouldn't catch it). On any
        // such failure, treat the inner as gone and re-walk, exactly as if the target had
        // disappeared.
        _innerWatcher.WatcherStopped += this.OnInnerWatcherStopped;
        _innerIsTarget = isTarget;
    }

    // Inner watcher signaled that it can no longer produce events for the current session.
    // This event ALWAYS fires after the inner has already fired its final Changed
    // notification, which OnInnerWatcherChanged usually handles in the obvious way (re-walk
    // and transition). So most of the time, by the time this callback runs, _innerWatcher
    // has already been replaced and there's nothing to do here.
    //
    // The case this callback exists to cover is the one OnInnerWatcherChanged can't catch
    // by itself: a mid-life arm failure where the target key still exists from a re-walk's
    // perspective, but the inner watcher is now dead (e.g., access permissions revoked
    // while watching). In that case OnInnerWatcherChanged sees "same state, value
    // changed" and re-arms its inner-watcher reference -- but the inner is dead. We notice
    // here, tear down, and re-walk to attach a fresh inner.
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

            // If the inner that just stopped has already been replaced (typically because
            // OnInnerWatcherChanged ran first for the same delete event and tore the old
            // inner down + attached a fresh one), there is nothing more to do here.
            // Tearing down the new inner would re-introduce the race we just escaped from.
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

            // If we transitioned from target -> not-target, the target has effectively been
            // lost from the caller's perspective even if the inner's stop reason was something
            // other than KeyDeleted; fire TargetDeleted so the caller can update its cached
            // state. Other transitions don't need a Changed event here -- the caller will get
            // one naturally on the next real change.
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

    // Inner watcher's Changed callback (fires on a ThreadPool thread). Re-probes the target path
    // and may transition the inner watcher between target-watching and ancestor-watching. The
    // inner watcher releases its own lock BEFORE invoking handlers, so it's safe to dispose and
    // recreate it from here -- we acquire only our own lock, never the inner's.
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

            // If the inner that fired this Changed has already been replaced by an earlier
            // OnInnerWatcherChanged invocation, ignore the event. Multiple notifications can
            // queue on a single inner (especially during a DeleteKeyTree, which fires once
            // per value deleted plus once for the key itself), and if the first callback
            // triggers a transition, the subsequent queued callbacks still reach this
            // handler with sender == the now-disposed old inner. Processing them as if they
            // were from the current inner would tear down the legitimate replacement --
            // including tearing it down WITH AN IN-FLIGHT KERNEL NOTIFICATION on it from
            // the caller's next action -- and install a fresh inner that has no idea about
            // the change. Same sender-equality pattern as OnInnerWatcherStopped.
            if (object.ReferenceEquals(sender, _innerWatcher) == false)
            {
                return;
            }

            var openResult = this.OpenNearestExistingKey();
            if (openResult is null)
            {
                // Lost the last ancestor too (essentially impossible for normal hives). Tear down
                // and stay torn down until something subscribes again.
                this.StopInnerWatcherLocked();
                return;
            }
            var (newKey, newIsTarget) = openResult.Value;

            // Four transition cases, only some of which require swapping the inner watcher.
            if (_innerIsTarget && newIsTarget)
            {
                // Same state, value change inside the target. The inner watcher already re-armed
                // itself; we don't need the new key handle. Fire Changed so the caller re-queries.
                newKey.Dispose();
                changeKind = RegistryKeyChangeKind.ValueChanged;
            }
            else if (_innerIsTarget && !newIsTarget)
            {
                // Target was deleted out from under us. The old inner watcher is on a dead key;
                // tear it down and start a fresh one on the nearest surviving ancestor. Fire
                // Changed so the caller can update its cached state to "target doesn't exist".
                this.StopInnerWatcherLocked();
                this.AttachInnerWatcherLocked(newKey, isTarget: false);
                changeKind = RegistryKeyChangeKind.TargetDeleted;
            }
            else if (!_innerIsTarget && newIsTarget)
            {
                // Target was created. Swap from ancestor-watching to target-watching. Fire Changed
                // so the caller re-queries (the value is now whatever the new key holds).
                this.StopInnerWatcherLocked();
                this.AttachInnerWatcherLocked(newKey, isTarget: true);
                changeKind = RegistryKeyChangeKind.TargetCreated;
            }
            else
            {
                // Both ancestor: the change under the ancestor was some other subkey activity, not
                // the target being created. Tear down and restart on the (possibly different)
                // nearest ancestor -- the watch might have been on Microsoft, then an intermediate
                // key was created so the nearest ancestor is now Microsoft\Foo. We don't fire
                // Changed because the target still doesn't exist, so the caller has nothing new to
                // read. (Even if the new key handle equals the same path as before, restarting
                // costs us only a brief handle close/open; not worth the path-comparison code.)
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

    // Opens the nearest existing key on the target path. Returns (target key, true) if the full
    // target exists, otherwise (nearest existing ancestor key, false). Returns null only if the
    // hive root itself can't be opened, which should never happen for standard hives.
    //
    // Caller takes ownership of the returned key (the open-attempt below ensures we don't leak
    // intermediate handles on the walk).
    private (Microsoft.Win32.RegistryKey key, bool isTarget)? OpenNearestExistingKey()
    {
        Microsoft.Win32.RegistryKey hiveKey;
        try
        {
            // OpenBaseKey returns a NEW disposable handle (not the static Registry.CurrentUser
            // singleton), so we can hand it to the inner watcher and let it take ownership.
            hiveKey = Microsoft.Win32.RegistryKey.OpenBaseKey(_hive, _view);
        }
        catch
        {
            return null;
        }

        // Empty target path => caller asked us to watch the hive root directly. Degrades to a
        // regular watcher on the root (always "target exists").
        if (string.IsNullOrEmpty(_targetSubKeyPath))
        {
            return (hiveKey, true);
        }

        // Try the full target path first.
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
            // fall through to ancestor walk
        }

        // Walk up the path one segment at a time looking for the nearest ancestor that does
        // exist. The hive root itself always exists for the standard hives, so the loop always
        // terminates.
        string current = _targetSubKeyPath;
        while (true)
        {
            int lastSlash = current.LastIndexOf('\\');
            if (lastSlash < 0)
            {
                // Stripped down to a single segment (e.g. "SOFTWARE") and it doesn't exist; the
                // nearest ancestor is the hive root itself.
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

    // Parses an HKEY_*-prefixed registry path (as returned by RegistryKey.Name) into (hive,
    // subKeyPath). Returns ErrorResult if the prefix isn't recognized. A key Name with no
    // backslash (e.g. "HKEY_CURRENT_USER" for a hive-root handle) parses to (hive, "").
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
