// Copyright 2020-2026 Raising the Floor - US, Inc.
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

using Morphic.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Morphic.MorphicBar.BarControls;

// Invoked when a BarButtonData-backed button is clicked.
// - param `actionTag` is the button's optional ActionTag (opaque caller-supplied value).
// - param `isChecked` is the new checked state for toggle buttons (null for non-toggle buttons).
// - return value indicates whether the action succeeded; on failure, the click handler reverts any
//   toggle state-change so the visual reflects actual system state. Errors handled internally by the
//   action implementation (e.g. logging) need not be re-encoded here; ErrorResult() with no value is
//   sufficient to signal "did not take effect".
public delegate Task<MorphicResult<MorphicUnit, MorphicUnit>> BarButtonAction(string? actionTag, bool? isChecked);

// BarButtonData is the source of truth for a button's state. It implements INotifyPropertyChanged
// so that external producers can write to the observable properties and have any attached UI 
// automatically reflect the change. The UI control subscribes to PropertyChanged in ApplyData 
// and unsubscribes in Unloaded.
public class BarButtonData : IBarItemData, INotifyPropertyChanged, IDisposable
{
    // Optional label displayed above the button. When null or empty, no header is rendered.
    public string? Header { get; set; }

	// Contents for text component of button
    public string Text { get; set; } = "";

    // Name exposed to screen readers (via AutomationProperties.Name). See IAccessibleName for the
    // available naming strategies (currently a single fixed name). Falls back to `Text` when null.
    public IAccessibleName? AccessibleName { get; set; }

    public string? Tooltip { get; set; }

    // Contextual help shown by the Info panel (the hover "Info panel"/"Info tip"; see Morphic.MorphicBar.Info).
    // InfoTitle is the name shown (falls back to Header, then Text, when null); InfoSubtitle is a short
    // description. When InfoSubtitle is null/empty AND there is no other info to show, the control gets no Info
    // surface on hover. Authored as localized .resw strings by the factory.
    public string? InfoTitle { get; set; }
    public string? InfoSubtitle { get; set; }
    // Optional LIVE value indicator (the Size of Text zoom "dots"): invoked at each hover so it tracks the current
    // level. Null = no dots. Set by the factory for the Text Size buttons.
    internal System.Func<Morphic.MorphicBar.Info.InfoValueDots?>? InfoDotsProvider { get; set; }
    // Message the Info panel shows INSTEAD of InfoSubtitle while this button is DISABLED. A WinUI-disabled button
    // swallows hover, so BarMultiButtonControl overlays a transparent hover catcher (only while disabled) carrying
    // this. Currently the Text Size +/- "can't go any bigger/smaller" limit. Null = no disabled-state panel.
    public string? DisabledInfoSubtitle { get; set; }

    public BarButtonLayoutStyle LayoutStyle { get; set; } = BarButtonLayoutStyle.TextOnly;

    // When true, the button renders as a ToggleButton and maintains checked state.
    public bool IsToggle { get; set; }

    private bool disposedValue;

    // Observable checked state. Only meaningful when IsToggle is true. May be written from any
    // thread (external system-event listeners often fire off the UI thread); UI subscribers are
    // responsible for marshalling the resulting PropertyChanged callback to their dispatcher.
    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            // short-circuit equal writes so the UI's own post-action data write doesn't cause a
            // redundant PropertyChanged round-trip, and to break any feedback loop between data
            // and UI
            if (_isChecked == value)
            {
                return;
            }
            _isChecked = value;
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
        }
    }

    // Observable enabled state. When false, the rendered button is disabled (does not respond to
    // clicks) and visually reflects the disabled VisualState. Default true for backwards compatibility
    // with call sites that don't set it. May be written from any thread; UI subscribers marshal the 
    // resulting PropertyChanged callback to their dispatcher.
    private bool _isEnabled = true;
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
            {
                return;
            }
            _isEnabled = value;
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
        }
    }

    // Opaque caller-supplied value passed back to the Action callback.
    public string? ActionTag { get; set; }

    // When set, the button's right-click context menu includes a "Settings" item that opens this Windows
    // Settings page (via the shared WindowsSettings launcher). When null AND no other menu items are present,
    // the button has no context menu at all. For a sub-button in a group, the group's SettingsPage is the
    // fallback when the sub-button does not set its own (see BarMultiButtonControl). Internal because the type
    // (WindowsSettings.Page) is app-internal; only the factory sets it and the context-menu builder reads it.
    internal Morphic.SystemSettings.WindowsSettings.Page? SettingsPage { get; set; }

    public BarButtonAction? Action { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    // Disposal-actions list. Code that subscribes external state-source events (e.g. a system
    // dark-mode listener) to this BarButtonData registers an unsubscribe call here, so the
    // subscription is torn down when the button is no longer in use. Without this, the
    // subscription's strong reference to the handler closure -- which captures this BarButtonData
    // -- would keep the button alive forever, even after the bar tears down. C# events don't
    // self-cleanup via GC; explicit disposal is the only fix.
    //
    // Threading: AddDisposeAction and Dispose are NOT thread-safe relative to each other. The
    // expected pattern is that both are called from the UI thread (factory setup + bar teardown),
    // so no locking is added. If we ever need off-thread Add/Dispose, add a lock around the list.
    private readonly List<Action> _disposeActions = new();

    // Registers an action to run when Dispose() is called. If Dispose has already run, the action
    // is invoked immediately so a late caller's subscription doesn't silently leak.
    public void AddDisposeAction(Action action)
    {
        if (disposedValue)
        {
            action();
            return;
        }
        _disposeActions.Add(action);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // Invoke registered dispose actions (typically event unsubscriptions wired by
                // external state-source bridges). Each is wrapped in try/catch so one failing
                // action doesn't block the rest.
                foreach (var action in _disposeActions)
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"BarButtonData dispose action threw: {ex}");
                    }
                }
                _disposeActions.Clear();
            }

            // NOTE: free unmanaged resources (unmanaged objects) and override finalizer
            // [nothing to do]

            // NOTE: set large fields to null
            // [nothing to do]

            disposedValue = true;
        }
    }

    // // NOTE: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~BarButtonData()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
