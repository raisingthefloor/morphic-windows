// Copyright 2020-2024 Raising the Floor - US, Inc.
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
using System.Windows;

namespace Morphic.UI;

public class ThemeAwareWindow : Window
{
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // capture the current light/dark theme state
        bool appsUseLightTheme = !Morphic.UI.ThemeColors.GetIsDarkColorTheme();

        // if the app/system dark mode is enabled, color it appropriately
        this.SetNonClientUIDarkModeAttribute(!appsUseLightTheme);

        // wire up theme color change event (to detect dark/light mode changes as well as high contrast-related color changes and other theme color changes)
        Morphic.UI.ThemeColors.ThemeColorsChanged += ThemeColors_ThemeColorsChanged;
    }

    private MorphicResult<MorphicUnit, MorphicUnit> SetNonClientUIDarkModeAttribute(bool value)
    {
        var success = Application.Current.Dispatcher.Invoke(bool () =>
        {
            var windowInteropHelper = new System.Windows.Interop.WindowInteropHelper(this);
            var windowHandle = windowInteropHelper.Handle;

            var setAttributeResult = Morphic.WindowsNative.Theme.WindowUtils.SetNonClientUIDarkModeAttribute(windowHandle, value);
            return setAttributeResult.IsSuccess;
        });

        return success == true ? MorphicResult.OkResult() : MorphicResult.ErrorResult();
    }

    private void ThemeColors_ThemeColorsChanged(object? sender, ThemeColors.ThemeColorsChangedEventArgs e)
    {
        this.SetNonClientUIDarkModeAttribute(!e.IsDarkColorTheme);
    }
}
