// Copyright 2026 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-windows/blob/master/LICENSE.txt
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

namespace Morphic;

// Persists the MorphicBar's visibility + docking location to the v1.x-compatible registry key
// (HKCU\Software\Raising the Floor\Morphic) and keeps the bar and the registry in two-way sync:
//   * bar -> registry: the user shows/hides or re-docks the bar -> the new value is written.
//   * registry -> bar: an external editor (a script, reg.exe, the v1.x app) changes a value -> the
//     bar is updated live (a docking change animates; a visibility change shows/hides).
//
// Loop-break: a cache of the last-synced values is the consensus between the two sides. Before any
// handler propagates a change to the OTHER side it updates the cache first; the echo that comes
// back from the other side then sees "new == cache" and stops. All state is confined to the bar's
// UI thread (the registry watcher's ThreadPool callback marshals onto the DispatcherQueue), so the
// cache needs no lock.
//
// Lifecycle:
//   * Load() reads (and first-run seeds) the persisted values up front so the caller can use them
//     to position + show the bar during startup.
//   * StartSync() begins two-way sync; call it AFTER the bar is in its restored state so the
//     initial application doesn't round-trip through the registry.
//   * PersistFinalState() writes the bar's final state at shutdown (belt-and-suspenders: also
//     re-creates the key if an external editor deleted it -- see ApplyRegistryToBar).
//   * Dispose() detaches the watcher + bar subscriptions.
internal sealed class AppRegistrySettings : IDisposable
{
    private const string MORPHIC_SUBKEY_PATH = "Software\\Raising the Floor\\Morphic";
    private const string IS_VISIBLE_VALUE_NAME = "MorphicBarIsVisible";
    private const string DOCKING_LOCATION_VALUE_NAME = "MorphicBarDockingLocation";
    private const string ORIENTATION_VALUE_NAME = "MorphicBarOrientation";

    private const bool DEFAULT_IS_VISIBLE = true;
    private const Morphic.MorphicBar.DockingLocation DEFAULT_DOCKING_LOCATION = Morphic.MorphicBar.DockingLocation.FloatingBottomTrailing;
    private const Microsoft.UI.Xaml.Controls.Orientation DEFAULT_ORIENTATION = Microsoft.UI.Xaml.Controls.Orientation.Horizontal;

    // The MorphicBarOrientation registry value uses an EXPLICIT 0/1 convention (0 = horizontal,
    // 1 = vertical) that we map by hand (ReadOrientation / WriteOrientation). We deliberately do NOT
    // cast the WinUI Orientation enum to/from int, because that enum's intrinsic values are the
    // REVERSE of this convention (Microsoft.UI.Xaml.Controls.Orientation.Vertical = 0, Horizontal = 1).
    private const int ORIENTATION_REG_HORIZONTAL = 0;
    private const int ORIENTATION_REG_VERTICAL = 1;

    // Consensus between bar and registry; UI-thread confined (see class remarks), so no lock.
    private bool _cachedIsVisible;
    private Morphic.MorphicBar.DockingLocation _cachedDockingLocation;
    private Microsoft.UI.Xaml.Controls.Orientation _cachedOrientation;

    private Morphic.MorphicBar.MorphicBarManager? _barManager;
    private Microsoft.UI.Dispatching.DispatcherQueue? _dispatcherQueue;
    private Morphic.WindowsNative.Registry.RegistryKeyChangeWatcher? _watcher;
    private bool _syncStarted;
    private bool _disposed;

    private AppRegistrySettings(bool isVisible, Morphic.MorphicBar.DockingLocation dockingLocation, Microsoft.UI.Xaml.Controls.Orientation orientation)
    {
        _cachedIsVisible = isVisible;
        _cachedDockingLocation = dockingLocation;
        _cachedOrientation = orientation;
    }

    // The persisted bar visibility (default applied when no value is stored).
    public bool IsBarVisible => _cachedIsVisible;

    // The persisted bar docking location (default applied when no value is stored).
    public Morphic.MorphicBar.DockingLocation DockingLocation => _cachedDockingLocation;

    // The persisted bar orientation (default applied when no value is stored).
    public Microsoft.UI.Xaml.Controls.Orientation Orientation => _cachedOrientation;

    // Reads the persisted values, seeding the registry with defaults on first run so the values
    // exist for external editors to discover. Never throws: any registry failure degrades to the
    // in-memory defaults.
    public static AppRegistrySettings Load()
    {
        var isVisible = AppRegistrySettings.ReadIsVisible();
        var dockingLocation = AppRegistrySettings.ReadDockingLocation();
        var orientation = AppRegistrySettings.ReadOrientation();

        // First-run seed: if any value is absent, write all three so the key/values exist going
        // forward. Present values (if any) are preserved; only the absent ones get a default.
        if (isVisible is null || dockingLocation is null || orientation is null)
        {
            AppRegistrySettings.WriteValues(
                isVisible ?? DEFAULT_IS_VISIBLE,
                dockingLocation ?? DEFAULT_DOCKING_LOCATION,
                orientation ?? DEFAULT_ORIENTATION);
        }

        return new AppRegistrySettings(
            isVisible ?? DEFAULT_IS_VISIBLE,
            dockingLocation ?? DEFAULT_DOCKING_LOCATION,
            orientation ?? DEFAULT_ORIENTATION);
    }

    // Begins two-way sync. Call AFTER the bar has been positioned + shown/hidden to the loaded
    // state, so the initial application doesn't round-trip through the registry. Idempotent-guarded.
    public void StartSync(Morphic.MorphicBar.MorphicBarManager barManager, Microsoft.UI.Dispatching.DispatcherQueue dispatcherQueue)
    {
        if (_syncStarted == true)
        {
            return;
        }
        _syncStarted = true;
        _barManager = barManager;
        _dispatcherQueue = dispatcherQueue;

        // bar -> registry
        _barManager.BarVisibilityChanged += this.OnBarVisibilityChanged;
        _barManager.DockingLocationChanged += this.OnBarDockingLocationChanged;
        _barManager.OrientationChanged += this.OnBarOrientationChanged;

        // registry -> bar
        var createWatcherResult = Morphic.WindowsNative.Registry.RegistryKeyChangeWatcher.CreateForPath(
            Microsoft.Win32.RegistryHive.CurrentUser,
            MORPHIC_SUBKEY_PATH);
        if (createWatcherResult.IsError == false)
        {
            _watcher = createWatcherResult.Value!;
            _watcher.Changed += this.OnRegistryChanged;
        }
        // A watcher-creation failure is non-fatal: registry->bar sync is simply disabled.
    }

    // bar -> registry (UI thread): the user showed or hid the bar.
    private void OnBarVisibilityChanged(object? sender, EventArgs e)
    {
        if (_disposed == true || _barManager is null)
        {
            return;
        }
        var isVisible = _barManager.IsBarVisible;
        if (isVisible == _cachedIsVisible)
        {
            return; // echo of a registry-driven change; nothing to write
        }
        _cachedIsVisible = isVisible;
        AppRegistrySettings.WriteIsVisible(isVisible);
    }

    // bar -> registry (UI thread): the user re-docked the bar.
    private void OnBarDockingLocationChanged(object? sender, Morphic.MorphicBar.DockingLocation dockingLocation)
    {
        if (_disposed == true)
        {
            return;
        }
        // Compare by physical (absolute) position: the bar may report a dock in a different
        // representation (logical vs physical) than the cached one yet mean the SAME corner/edge --
        // e.g. FloatingBottomTrailing(3) and FloatingBottomRight(7) in LTR. A raw enum compare would
        // treat that as a change and write a redundant value. (_barManager is non-null while this
        // handler is subscribed; the null-coalesce is purely defensive.)
        var isRightToLeft = _barManager?.IsRightToLeft ?? false;
        if (AppRegistrySettings.AreSamePhysicalDockingLocation(dockingLocation, _cachedDockingLocation, isRightToLeft) == true)
        {
            return; // echo of a registry-driven change (or a physically-identical re-dock); nothing to write
        }
        _cachedDockingLocation = dockingLocation;
        AppRegistrySettings.WriteDockingLocation(dockingLocation);
    }

    // bar -> registry (UI thread): the user flipped the bar's orientation (companion to
    // OnBarDockingLocationChanged; orientation + docking are a persisted pair, but each is written
    // independently with its own loop-break check).
    private void OnBarOrientationChanged(object? sender, Microsoft.UI.Xaml.Controls.Orientation orientation)
    {
        if (_disposed == true)
        {
            return;
        }
        if (orientation == _cachedOrientation)
        {
            return; // echo of a registry-driven change; nothing to write
        }
        _cachedOrientation = orientation;
        AppRegistrySettings.WriteOrientation(orientation);
    }

    // registry -> bar: the watcher fires on a ThreadPool thread; marshal to the UI thread so
    // all cache + bar access stays single-threaded.
    private void OnRegistryChanged(object? sender, Morphic.WindowsNative.Registry.RegistryKeyChangedEventArgs e)
    {
        var kind = e.Kind;
        var dispatcherQueue = _dispatcherQueue;
        if (dispatcherQueue is null)
        {
            return;
        }
        _ = dispatcherQueue.TryEnqueue(() => this.ApplyRegistryToBar(kind));
    }

    private void ApplyRegistryToBar(Morphic.WindowsNative.Registry.RegistryKeyChangeKind kind)
    {
        if (_disposed == true || _barManager is null)
        {
            return;
        }

        // Whole-key deletion by an external editor: by design, leave it deleted at runtime and do
        // nothing to the bar. PersistFinalState (at shutdown) re-creates the key from the bar's
        // final state, so the user's settings are persisted rather than lost.
        if (kind == Morphic.WindowsNative.Registry.RegistryKeyChangeKind.TargetDeleted)
        {
            return;
        }

        // Re-read all three values. The watcher only signals "something in the key changed" -- not
        // which value, nor whether it was added, changed, or DELETED (the underlying
        // REG_NOTIFY_CHANGE_LAST_SET fires identically for all three). We tell them apart by
        // comparing each read against our cache, and we honor a value ONLY when it is present and
        // different (an add or a change). A value that reads back null was DELETED (or is
        // unreadable / corrupt); coalescing it to the CACHED value (?? below) makes a deletion a
        // deliberate no-op -- the bar keeps its current state instead of snapping to a default.
        // Otherwise a script deleting MorphicBarIsVisible would SHOW the bar, and deleting the
        // placement values would yank the bar to its default corner. (The seeded defaults exist
        // only for the first-run Load(); they are deliberately never applied here on the live path.)
        var targetIsVisible = AppRegistrySettings.ReadIsVisible() ?? _cachedIsVisible;
        var targetDockingLocation = AppRegistrySettings.ReadDockingLocation() ?? _cachedDockingLocation;
        var targetOrientation = AppRegistrySettings.ReadOrientation() ?? _cachedOrientation;

        if (targetIsVisible != _cachedIsVisible)
        {
            _cachedIsVisible = targetIsVisible;
            if (targetIsVisible == true)
            {
                _barManager.ShowBar(activateWindow: false);
            }
            else
            {
                _barManager.HideBar();
            }
        }

        // Orientation + docking location are a persisted pair: apply them together in a SINGLE
        // animated move so an external change to either (or both) results in one smooth animation
        // (the same AnimateMoveTo path the user's own drag/flip travels). Update BOTH caches BEFORE
        // the move so the OrientationChanged + DockingLocationChanged echoes both loop-break here.
        // Compare docking by physical (absolute) position so an external write of the physically
        // equivalent dock in a different representation (e.g. FloatingBottomRight(7) when the cache
        // holds FloatingBottomTrailing(3) in LTR) does NOT trigger a redundant re-dock animation.
        var dockingLocationChanged = AppRegistrySettings.AreSamePhysicalDockingLocation(targetDockingLocation, _cachedDockingLocation, _barManager.IsRightToLeft) == false;
        if (targetOrientation != _cachedOrientation || dockingLocationChanged == true)
        {
            _cachedOrientation = targetOrientation;
            _cachedDockingLocation = targetDockingLocation;
            _barManager.MoveToPlacement(targetOrientation, targetDockingLocation);
        }
    }

    // Writes the bar's final visibility + docking location + orientation to the registry. Called
    // once during app shutdown so the latest state is persisted even if an external editor had
    // deleted the key (which we deliberately leave alone at runtime) or a runtime write had failed.
    public void PersistFinalState()
    {
        AppRegistrySettings.WriteValues(_cachedIsVisible, _cachedDockingLocation, _cachedOrientation);
    }

    // True when two docking locations resolve to the SAME physical (absolute) position, regardless of
    // whether either is expressed logically (flow-relative) or physically. Used by the bar<->registry
    // loop-break so a representation-only difference (the same corner/edge) is not mistaken for a move.
    private static bool AreSamePhysicalDockingLocation(Morphic.MorphicBar.DockingLocation first, Morphic.MorphicBar.DockingLocation second, bool isRightToLeft)
    {
        return Morphic.MorphicBar.DockingLocationExtensions.ToPhysicalDockingLocation(first, isRightToLeft)
            == Morphic.MorphicBar.DockingLocationExtensions.ToPhysicalDockingLocation(second, isRightToLeft);
    }

    // ----- registry read/write boundary (all failures are non-fatal) -----

    private static bool? ReadIsVisible()
    {
        var raw = AppRegistrySettings.ReadDwordValue(IS_VISIBLE_VALUE_NAME);
        return (raw is null) ? null : (raw.Value != 0);
    }

    private static Morphic.MorphicBar.DockingLocation? ReadDockingLocation()
    {
        var raw = AppRegistrySettings.ReadDwordValue(DOCKING_LOCATION_VALUE_NAME);
        if (raw is null)
        {
            return null;
        }
        if (Enum.IsDefined(typeof(Morphic.MorphicBar.DockingLocation), raw.Value) == false)
        {
            return null; // out-of-range / corrupt -> treat as unset (caller applies the default)
        }
        return (Morphic.MorphicBar.DockingLocation)raw.Value;
    }

    // Maps the explicit 0/1 registry convention (0 = horizontal, 1 = vertical) to the WinUI enum by
    // hand; see the ORIENTATION_REG_* remarks for why we never cast across this boundary.
    private static Microsoft.UI.Xaml.Controls.Orientation? ReadOrientation()
    {
        var raw = AppRegistrySettings.ReadDwordValue(ORIENTATION_VALUE_NAME);
        return raw switch
        {
            ORIENTATION_REG_HORIZONTAL => Microsoft.UI.Xaml.Controls.Orientation.Horizontal,
            ORIENTATION_REG_VERTICAL => Microsoft.UI.Xaml.Controls.Orientation.Vertical,
            _ => null, // absent / out-of-range / corrupt -> treat as unset (caller applies the default)
        };
    }

    private static int? ReadDwordValue(string valueName)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(MORPHIC_SUBKEY_PATH);
            if (key is null)
            {
                return null;
            }
            return (key.GetValue(valueName) is int intValue) ? intValue : null;
        }
        catch (Exception)
        {
            // Registry read is best-effort; a failure is treated as "value absent".
            return null;
        }
    }

    private static void WriteIsVisible(bool isVisible)
    {
        AppRegistrySettings.WriteDwordValue(IS_VISIBLE_VALUE_NAME, isVisible ? 1 : 0);
    }

    private static void WriteDockingLocation(Morphic.MorphicBar.DockingLocation dockingLocation)
    {
        AppRegistrySettings.WriteDwordValue(DOCKING_LOCATION_VALUE_NAME, (int)dockingLocation);
    }

    // Maps the WinUI enum to the explicit 0/1 registry convention by hand (see ReadOrientation).
    private static void WriteOrientation(Microsoft.UI.Xaml.Controls.Orientation orientation)
    {
        var value = (orientation == Microsoft.UI.Xaml.Controls.Orientation.Vertical) ? ORIENTATION_REG_VERTICAL : ORIENTATION_REG_HORIZONTAL;
        AppRegistrySettings.WriteDwordValue(ORIENTATION_VALUE_NAME, value);
    }

    private static void WriteValues(bool isVisible, Morphic.MorphicBar.DockingLocation dockingLocation, Microsoft.UI.Xaml.Controls.Orientation orientation)
    {
        AppRegistrySettings.WriteIsVisible(isVisible);
        AppRegistrySettings.WriteDockingLocation(dockingLocation);
        AppRegistrySettings.WriteOrientation(orientation);
    }

    private static void WriteDwordValue(string valueName, int value)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(MORPHIC_SUBKEY_PATH);
            key?.SetValue(valueName, value, Microsoft.Win32.RegistryValueKind.DWord);
        }
        catch (Exception)
        {
            // Registry write is best-effort; a failure is non-fatal.
        }
    }

    public void Dispose()
    {
        if (_disposed == true)
        {
            return;
        }
        _disposed = true;

        if (_watcher is not null)
        {
            _watcher.Changed -= this.OnRegistryChanged;
            _watcher.Dispose();
            _watcher = null;
        }
        if (_barManager is not null)
        {
            _barManager.BarVisibilityChanged -= this.OnBarVisibilityChanged;
            _barManager.DockingLocationChanged -= this.OnBarDockingLocationChanged;
            _barManager.OrientationChanged -= this.OnBarOrientationChanged;
            _barManager = null;
        }
    }
}
