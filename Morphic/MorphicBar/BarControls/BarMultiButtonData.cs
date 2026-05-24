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

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Morphic.MorphicBar.BarControls;

public class BarMultiButtonData : IBarItemData, IDisposable
{
    private bool disposedValue;

    // Label displayed above the sub-button group.
    public string Header { get; set; } = "";

    // How the sub-buttons are sized across the group.
    public MultiButtonSizingMode SizingMode { get; set; } = MultiButtonSizingMode.StretchToLargest;

    // NOTE: Sub-buttons in a group must use BarButtonLayoutStyle.TextOnly; MultiButtonBarControl
    // enforces this and throws if any sub-button specifies a different layout style.
    public List<BarButtonData> Buttons { get; set; } = new();

    // When non-null, the control wires the minus/plus keys to invoke the sub-buttons at the
    // specified indices. Requires exactly two sub-buttons (BarMultiButtonControl validates).
    public BarMultiButtonIncDecShortcuts? IncDecShortcuts { get; set; }

    // When true, sub-buttons are arranged side-by-side regardless of the bar's orientation. Used
    // for groups (e.g. Text Size +/-) where the buttons logically belong next to each other and
    // splitting them onto separate rows in a vertical bar would harm usability. Defaults to false.
    // If false, sub-buttons follow the bar's orientation (side-by-side in horizontal bars, stacked 
	// in vertical).
    public bool AlwaysHorizontalSubButtons { get; set; } = false;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // Dispose each sub-button. Each sub-button's Dispose runs its own registered
                // dispose actions, so any external event subscriptions held on behalf of a 
				// sub-button get torn down here. Each .Dispose() is wrapped in try/catch so one 
				// failing sub-button doesn't block the rest -- matches BarButtonData.Dispose's 
				// own per-action isolation.
                foreach (var button in this.Buttons)
                {
                    try
                    {
                        button.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"BarMultiButtonData sub-button dispose threw: {ex}");
                    }
                }
                this.Buttons.Clear();
            }

            // NOTE: free unmanaged resources (unmanaged objects) and override finalizer
            // [nothing to do]

            // NOTE: set large fields to null
            // [nothing to do]

            disposedValue = true;
        }
    }

    // // NOTE: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~BarMultiButtonData()
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

public class BarMultiButtonIncDecShortcuts
{
    // Index of the sub-button the minus key (Subtract / OemMinus) should invoke.
    public int DecrementButtonIndex { get; set; }

    // Index of the sub-button the plus key (Add / OemPlus) should invoke.
    public int IncrementButtonIndex { get; set; }
}
