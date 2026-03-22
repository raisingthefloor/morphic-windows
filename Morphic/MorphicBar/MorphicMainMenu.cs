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

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Windows.Win32;

namespace Morphic.MorphicBar;

internal class MorphicMainMenu
{
    MenuFlyout _menuFlyout;

    MenuFlyoutItem _showMorphicBarMenuItem;
    MenuFlyoutItem _hideMorphicBarMenuItem;
    

    public event EventHandler<EventArgs>? HideMorphicBarMenuItemClicked;
    public event EventHandler<EventArgs>? ShowMorphicBarMenuItemClicked;
    public event EventHandler<EventArgs>? QuitMorphicMenuItemClicked;

    public MorphicMainMenu()
    {
        // initialize the underlying menu flyout
        var menuFlyout = new MenuFlyout();

        // create padding that approximates native Windows 11 styling
        var padding = new Thickness(11, 4, 11, 5);

        // NOTE: Show/Hide MorphicBar are mutually exclusive; only one will be visible at any given time
        _showMorphicBarMenuItem = new MenuFlyoutItem { Text = "Show MorphicBar", Padding = padding, MinHeight = 0 };
        _showMorphicBarMenuItem.Click += (s, _) => { this.ShowMorphicBarMenuItemClicked?.Invoke(s, EventArgs.Empty); };
        menuFlyout.Items.Add(_showMorphicBarMenuItem);
        //
        _hideMorphicBarMenuItem = new MenuFlyoutItem { Text = "Hide MorphicBar", Padding = padding, MinHeight = 0 };
        _hideMorphicBarMenuItem.Click += (s, _) => { this.HideMorphicBarMenuItemClicked?.Invoke(s, EventArgs.Empty); };
        menuFlyout.Items.Add(_hideMorphicBarMenuItem);

        menuFlyout.Items.Add(new MenuFlyoutSeparator());

        var quitMorphicMenuItem = new MenuFlyoutItem { Text = "Quit Morphic", /*Icon = new SymbolIcon(Symbol.Paste), */Padding = padding, MinHeight = 0 };
        quitMorphicMenuItem.KeyboardAccelerators.Add(new Microsoft.UI.Xaml.Input.KeyboardAccelerator
        {
            Key = Windows.System.VirtualKey.Q,
            Modifiers = Windows.System.VirtualKeyModifiers.None
        });
        quitMorphicMenuItem.Click += (s, _) => { this.QuitMorphicMenuItemClicked?.Invoke(s, EventArgs.Empty); };
        menuFlyout.Items.Add(quitMorphicMenuItem);

        _menuFlyout = menuFlyout;
    }

    // NOTE: owner will be used both to capture a XAML root (required to show the menu) and also to know which MorphicBar to show/hide
    public void Show(Microsoft.UI.Xaml.Window ownerWindow, bool morphicBarIsVisible, int x, int y)
    {
        // Show/Hide MorphicBar menu items
        switch (morphicBarIsVisible)
        {
            case true:
                _showMorphicBarMenuItem.Visibility = Visibility.Collapsed;
                _hideMorphicBarMenuItem.Visibility = Visibility.Visible;
                break;
            case false:
                _showMorphicBarMenuItem.Visibility = Visibility.Visible;
                _hideMorphicBarMenuItem.Visibility = Visibility.Collapsed;
                break;
        }

        // capture the content root of the owner (required to show the menu)
        var root = (FrameworkElement)ownerWindow.Content;
        //
        // capture the position of the current window on the screen; we'll pop up the flyout relative to this coordinate
        var hwnd = new Windows.Win32.Foundation.HWND(WinRT.Interop.WindowNative.GetWindowHandle(ownerWindow));
        var ownerPosition = System.Drawing.Point.Empty;
        Windows.Win32.PInvoke.ClientToScreen(hwnd, ref ownerPosition);
        //
        // convert absolute screen position to DIPs relative to the content root; this should always use the rasterizationScale of the current window
        var rasterizationScale = root.XamlRoot.RasterizationScale;
        double relativeX = (x - ownerPosition.X) / rasterizationScale;
        double relativeY = (y - ownerPosition.Y) / rasterizationScale;

        // pop up the menu flyout
        _menuFlyout.ShowAt(root, new FlyoutShowOptions
        {
            Position = new Windows.Foundation.Point(relativeX, relativeY),
            ShowMode = FlyoutShowMode.Standard
        });
    }
}
