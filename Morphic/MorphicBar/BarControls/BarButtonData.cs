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
public delegate Task BarButtonAction(string? actionTag, bool? isChecked);

public class BarButtonData : IBarItemData, IDisposable
{
    // Optional label displayed above the button. When null or empty, no header is rendered.
    public string? Header { get; set; }

	// Contents for text component of button
    public string Text { get; set; } = "";

    // Name exposed to screen readers (via AutomationProperties.Name); falls back to `Text` if null.
    public string? AccessibleName { get; set; }

    public string? Tooltip { get; set; }

    public BarButtonLayoutStyle LayoutStyle { get; set; } = BarButtonLayoutStyle.TextOnly;

    // When true, the button renders as a ToggleButton and maintains checked state.
    public bool IsToggle { get; set; }

    private bool disposedValue;

    // Initial checked state; only meaningful when IsToggle is true.
    public bool IsChecked { get; set; }

    // Opaque caller-supplied value passed back to the Action callback.
    public string? ActionTag { get; set; }

    public BarButtonAction? Action { get; set; }

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
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
