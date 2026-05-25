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

using Morphic.MorphicBar.BarControls;
using Morphic.WindowsNative.Display;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Morphic.MorphicBar;

internal static class BarItemDataFactory
{
    public static IBarItemData CreateMagnifierButtonGroup(BarButtonAction? showAction, BarButtonAction? hideAction)
    {
        // "Magnifier" -- two pushbuttons (Show on the left, Hide on the right), equal width.
        return new BarMultiButtonData
        {
            Header = "Magnifier",
            SizingMode = MultiButtonSizingMode.StretchToLargest,
            Buttons = new List<BarButtonData>
                {
                    new BarButtonData
                    {
                        Text = "Show",
                        AccessibleName = "Show magnifier",
                        ActionTag = "magnifier-show",
                        Action = showAction,
                    },
                    new BarButtonData
                    {
                        Text = "Hide",
                        AccessibleName = "Hide magnifier",
                        ActionTag = "magnifier-hide",
                        Action = hideAction,
                    },
                },
        };
    }

    //

    public static IBarItemData CreateContrastColorButtonGroup(BarButtonAction? contrastAction, BarButtonAction? colorAction, BarButtonAction? darkAction, BarButtonAction? nightAction)
    {
        // "Contrast & Color" -- 4 toggle buttons, per-content sized
        var contrastButton = new BarButtonData { Text = "Contrast", IsToggle = true, ActionTag = "contrast", Action = contrastAction };
        var colorButton    = new BarButtonData { Text = "Color",    IsToggle = true, ActionTag = "color",    Action = colorAction };
        var darkButton     = new BarButtonData { Text = "Dark",     IsToggle = true, ActionTag = "dark",     Action = darkAction };
        var nightButton    = new BarButtonData { Text = "Night",    IsToggle = true, ActionTag = "night",    Action = nightAction };

        darkButton.IsChecked = Morphic.SettingsUtils.CachedDarkModeState.GetCurrentIsDark();
        darkButton.IsEnabled = !Morphic.SettingsUtils.CachedDarkModeState.GetCurrentIsHighContrast();
		
		//

        //
        // GetIsActive returns nullable bool; null means the registry value doesn't exist yet
        // because the user has never enabled color filtering, which we treat as "off" for the seed.
        var initialColorFiltersIsActiveResult = ColorFilters.GetIsActive();
        if (initialColorFiltersIsActiveResult.IsError)
        {
            System.Diagnostics.Debug.WriteLine("[BarItemDataFactory] Initial ColorFilters.GetIsActive read failed; defaulting button to unchecked");
        }
        colorButton.IsChecked = initialColorFiltersIsActiveResult.IsSuccess && initialColorFiltersIsActiveResult.Value == true;
		
		//

        // NOTE: we use Win32 SystemParametersInfo + UserPreferenceChanged here rather than the
        // purpose-built WinRT Windows.UI.ViewManagement.AccessibilitySettings because that class
        // requires a CoreWindow context the WinUI 3 unpackaged Morphic app does not provide --
        // constructing the instance throws a COMException at runtime.
        EventHandler<Morphic.WindowsNative.Theme.HighContrastIsOnChangedEventArgs> highContrastIsOnChangedHandler =
            (_, e) => contrastButton.IsChecked = e.NewValue;
        Morphic.WindowsNative.Theme.HighContrast.IsOnChanged += highContrastIsOnChangedHandler;
        contrastButton.AddDisposeAction(() => Morphic.WindowsNative.Theme.HighContrast.IsOnChanged -= highContrastIsOnChangedHandler);
        //
        var initialHighContrastIsOnResult = Morphic.WindowsNative.Theme.HighContrast.GetIsOn();
        if (initialHighContrastIsOnResult.IsError)
        {
            System.Diagnostics.Debug.WriteLine("[BarItemDataFactory] Initial HighContrast.GetIsOn read failed; defaulting button to unchecked");
        }
        contrastButton.IsChecked = initialHighContrastIsOnResult.IsSuccess && initialHighContrastIsOnResult.Value;
		
		//

        return new BarMultiButtonData
        {
            Header = "Contrast & Color",
            SizingMode = MultiButtonSizingMode.AutoSize,
            Buttons = new List<BarButtonData>
                {
                    contrastButton,
                    colorButton,
                    darkButton,
                    nightButton,
                },
        };
    }
}
