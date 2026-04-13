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
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Text;

namespace Morphic.MorphicBar.TransparentWindow;

// NOTE: TransparentBackdrop is a custom SystemBackdrop that makes the window background fully transparent
//
// WARNING: WinUI 3's SystemBackdrop infrastructure expects the backdrop brush 
// to be a Windows.UI.Composition.CompositionBrush (the system/DWM-level type),
// NOT a Microsoft.UI.Composition.CompositionBrush (the WinUI 3 "lifted" type).
//
// Despite both namespaces having identically-named classes, they are separate
// COM objects and cannot be cast to each other.
//
// To produce the correct type we create our own Windows.UI.Composition.Compositor.
// This requires a Windows.System.DispatcherQueue on the current thread, which
// TransparentWindow.EnsureSystemDispatcherQueue() provides.
//
// The compositor and brush are kept alive as long as the backdrop is connected;
// disposing the compositor would invalidate the brush.
internal class TransparentBackdrop : Microsoft.UI.Xaml.Media.SystemBackdrop
{
    private Windows.UI.Composition.Compositor? _compositor;

    // NOTE: the caller must initialize a DispatcherQueue on this thread before creating and connecting an instance of this class
    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);

        _compositor = new Windows.UI.Composition.Compositor();
        connectedTarget.SystemBackdrop = _compositor.CreateColorBrush(
            new Windows.UI.Color { A = 0, R = 0, G = 0, B = 0 });
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        base.OnTargetDisconnected(disconnectedTarget);

        disconnectedTarget.SystemBackdrop = null;
        _compositor?.Dispose();
        _compositor = null;
    }

    // NOTE: to prevent WinUI from trying to modify our system backdrop when the theme changes, stub out this override
    protected override void OnDefaultSystemBackdropConfigurationChanged(ICompositionSupportsSystemBackdrop target, XamlRoot xamlRoot)
    {
        //base.OnDefaultSystemBackdropConfigurationChanged(target, xamlRoot);
        // No-op: transparent backdrop doesn't change with theme
    }
}
