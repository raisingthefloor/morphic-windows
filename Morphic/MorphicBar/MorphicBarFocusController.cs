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

namespace Morphic.MorphicBar;

internal sealed class MorphicBarFocusController
{
    private readonly MorphicBarWindow _barWindow;

    private long _suppressUpgradeUntilTickCount64;

    public MorphicBarFocusController(MorphicBarWindow barWindow)
    {
        _barWindow = barWindow;
    }


    #region Public types

    public readonly record struct FocusSnapshot(
        Microsoft.UI.Xaml.Controls.Control? Control,
        Microsoft.UI.Xaml.FocusState State);

    public enum SnapshotScope
    {
        BarOwnedOnly,
        AnyInBarXamlRoot,
    }

    #endregion Public types


    #region Suppression timer (Step 2)

    public void SuppressUpgradeFor(System.TimeSpan duration)
    {
        throw new System.NotImplementedException("MorphicBarFocusController.SuppressUpgradeFor: not yet migrated from MorphicBarWindow.SuppressFocusUpgradeFor (Step 2).");
    }

    #endregion Suppression timer (Step 2)


    #region Initial focus placement (Step 3)

    public void SetInitialFocus(Microsoft.UI.Xaml.FocusState focusState = Microsoft.UI.Xaml.FocusState.Programmatic)
    {
        throw new System.NotImplementedException("MorphicBarFocusController.SetInitialFocus: not yet migrated from MorphicBarWindow.SetInitialFocus (Step 3).");
    }

    #endregion Initial focus placement (Step 3)


    #region Activation drive (Step 4)

    public void OnWindowActivated(Microsoft.UI.Xaml.WindowActivationState activationState)
    {
        throw new System.NotImplementedException("MorphicBarFocusController.OnWindowActivated: not yet migrated from MorphicBarWindow.ScheduleDeferredFocusUpdate (Step 4).");
    }

    #endregion Activation drive (Step 4)


    #region Snapshot capture / restore (Step 5)

    public FocusSnapshot Capture(SnapshotScope scope)
    {
        throw new System.NotImplementedException("MorphicBarFocusController.Capture: not yet migrated (Step 5).");
    }

    public void Restore(FocusSnapshot snapshot)
    {
        throw new System.NotImplementedException("MorphicBarFocusController.Restore: not yet migrated (Step 5).");
    }

    #endregion Snapshot capture / restore (Step 5)


    #region Stale-ring cleanup (Step 6)

    public void DowngradeKeyboardFocusInBar()
    {
        throw new System.NotImplementedException("MorphicBarFocusController.DowngradeKeyboardFocusInBar: not yet migrated from MorphicBarWindow.DowngradeKeyboardFocusedControlsInBar (Step 6).");
    }

    #endregion Stale-ring cleanup (Step 6)


    #region Show-time bundle

    public void PrepareForShow()
    {
        this.DowngradeKeyboardFocusInBar();
        this.SuppressUpgradeFor(System.TimeSpan.FromMilliseconds(500));
    }

    #endregion Show-time bundle
}
