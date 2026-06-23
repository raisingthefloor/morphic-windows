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

namespace Morphic.MorphicBar.BarControls;

// Builds the right-click context menu for a MorphicBar button from its BarButtonData. Returns null when the
// button has NO menu items, so the caller (BarButtonBuilder.CreateButton) leaves ContextFlyout unset and no
// menu appears. Both single buttons and multi-button sub-buttons route through CreateButton, so this covers the
// whole bar; the bar's menu and close chrome are not data-backed buttons, so they never get a context menu.
//
// Currently the only item is "Settings" (opens the button's Windows Settings page); more item types (e.g.
// Learn More, Quick Demo) are expected here soon -- add them to the list below.
internal static class BarButtonContextMenuBuilder
{
    public static Microsoft.UI.Xaml.Controls.MenuFlyout? Build(BarButtonData data)
    {
        var flyout = new Microsoft.UI.Xaml.Controls.MenuFlyout();

        // "Settings" -> opens the Windows Settings page this button maps to, via the shared launcher (the same
        // mechanism the tray "More settings" submenu uses).
        if (data.SettingsPage is Morphic.SystemSettings.WindowsSettings.Page settingsPage)
        {
            var settingsItem = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = Morphic.Localization.Strings.BarButtonContextMenuSettings };
            settingsItem.Click += (s, e) => { _ = Morphic.SystemSettings.WindowsSettings.Open(settingsPage); };
            flyout.Items.Add(settingsItem);
        }

        // (future menu items go here)

        return flyout.Items.Count > 0 ? flyout : null;
    }
}
