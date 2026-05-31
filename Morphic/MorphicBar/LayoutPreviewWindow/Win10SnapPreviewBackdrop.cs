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

using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Morphic.MorphicBar.LayoutPreviewWindow;

// Reproduces the Windows 10 snap-assist preview look: a translucent accent-colored ("snap blue")
// acrylic over the blurred desktop. Used by LayoutPreviewWindow on Windows 10 only (on Windows 11
// the preview uses AcrylicGrayBackdrop + DWM-rounded corners).
//
// DesktopAcrylicController is supported on Windows 10 1809+, and it does HOST-backdrop acrylic (it
// samples/blurs the actual desktop behind the window) -- that desktop blur is what makes this read
// as the Win10 snap overlay rather than a flat tint. There is no built-in WinUI "snap" backdrop
// preset; the Win10 snap overlay is shell-drawn, so we reproduce it by tinting the acrylic with the
// system accent color.
internal class Win10SnapPreviewBackdrop : SystemBackdrop
{
    // WIN10-TUNE: first-pass tint/luminosity opacities for the accent wash. Verify against the live
    // Win10 snap preview on a real Win10 machine (screenshots) and adjust. Higher TintOpacity = more
    // saturated accent; higher LuminosityOpacity = more opaque / less see-through.
    private const float SNAP_TINT_OPACITY = 0.4f;
    private const float SNAP_LUMINOSITY_OPACITY = 0.7f;

    private DesktopAcrylicController? _controller;
    private SystemBackdropConfiguration? _configOverride;

    // NOTE: the caller must initialize a DispatcherQueue on this thread before creating and connecting an instance of this class
    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);

        // Tint with the user's current system accent color (Win10's snap overlay tints with the
        // accent, which is blue by default). Read once at connect time; live accent-change tracking
        // (UISettings.ColorValuesChanged) is not wired because the preview is a short-lived surface.
        var accentColor = new Windows.UI.ViewManagement.UISettings().GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);

        _controller = new DesktopAcrylicController
        {
            // Default (not Thin) for the standard, more-saturated acrylic that matches the snap overlay.
            Kind = DesktopAcrylicKind.Default,
            TintColor = accentColor,
            TintOpacity = Win10SnapPreviewBackdrop.SNAP_TINT_OPACITY,
            LuminosityOpacity = Win10SnapPreviewBackdrop.SNAP_LUMINOSITY_OPACITY,
            // Solid accent shown when acrylic is unavailable (transparency disabled, battery saver, RDP).
            FallbackColor = accentColor,
        };

        // treat the window as 'always active' so the acrylic doesn't fall back to its opaque 'inactive' effect
        _configOverride = new SystemBackdropConfiguration
        {
            IsInputActive = true,
        };

        _controller.AddSystemBackdropTarget(connectedTarget);
        _controller.SetSystemBackdropConfiguration(_configOverride);
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        base.OnTargetDisconnected(disconnectedTarget);

        _controller?.RemoveSystemBackdropTarget(disconnectedTarget);
        _controller?.Dispose();
        _controller = null;

        _configOverride = null;
    }

    // NOTE: we drive the controller with our own _configOverride (IsInputActive=true), so WinUI's
    // default SystemBackdropConfiguration is intentionally bypassed. The base implementation tries to
    // apply the default config on theme/high-contrast transitions and can throw E_INVALIDARG when
    // acrylic becomes invalid (e.g. high contrast enabled); override to a no-op so those transitions
    // don't crash the app.
    protected override void OnDefaultSystemBackdropConfigurationChanged(ICompositionSupportsSystemBackdrop target, XamlRoot xamlRoot)
    {
        // No-op: this backdrop ignores the system default config in favor of _configOverride.
    }
}
