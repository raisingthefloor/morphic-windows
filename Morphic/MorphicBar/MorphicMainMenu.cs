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

namespace Morphic.MorphicBar;

internal class MorphicMainMenu
{
    MenuFlyout _menuFlyout;

    MenuFlyoutItem _showMorphicBarMenuItem;
    MenuFlyoutItem _hideMorphicBarMenuItem;

    // === RTL menu placement: cached menu width (DIPs) ===
    //
    // Under RTL the menu opens to the LEFT of the click (native RTL behavior). A flyout's Position is ALWAYS its
    // top-LEFT corner regardless of FlowDirection, so Show() shifts the position left by the menu's own width so
    // its RIGHT edge lands at the click. The width depends on which of Show/Hide MorphicBar is the visible item --
    // those two strings can differ a LOT in width across languages (only coincidentally similar in English) -- so
    // we cache the two configurations SEPARATELY and measure BOTH (see MeasureWidthInvisibly): up front at startup
    // (PrewarmWidth) and again on any item change (RefreshWidth). Widths are in DIPs, which are scale-independent
    // (a DPI change does not invalidate them).
    private double _cachedWidthWhenBarVisibleInDips = 0.0;   // the "Hide MorphicBar" item is the one showing
    private double _cachedWidthWhenBarHiddenInDips = 0.0;    // the "Show MorphicBar" item is the one showing
    private const double DEFAULT_MENU_WIDTH_ESTIMATE_IN_DIPS = 200.0;
    // The menu-owner window, captured by PrewarmWidth so RefreshWidth can re-measure later without re-passing it.
    private Microsoft.UI.Xaml.Window? _ownerWindowForMeasuring = null;

    public event EventHandler<EventArgs>? AboutMorphicMenuItemClicked;
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
        _showMorphicBarMenuItem = new MenuFlyoutItem { Text = Morphic.Localization.Strings.ShowMorphicBar, Padding = padding, MinHeight = 0 };
        _showMorphicBarMenuItem.Click += (s, _) => { this.ShowMorphicBarMenuItemClicked?.Invoke(s, EventArgs.Empty); };
        menuFlyout.Items.Add(_showMorphicBarMenuItem);
        //
        _hideMorphicBarMenuItem = new MenuFlyoutItem { Text = Morphic.Localization.Strings.HideMorphicBar, Padding = padding, MinHeight = 0 };
        _hideMorphicBarMenuItem.Click += (s, _) => { this.HideMorphicBarMenuItemClicked?.Invoke(s, EventArgs.Empty); };
        menuFlyout.Items.Add(_hideMorphicBarMenuItem);

        menuFlyout.Items.Add(new MenuFlyoutSeparator());

        // "More settings to make the computer easier" (OS accessibility-related settings) submenu
        var moreSettingsMenuItem = new MenuFlyoutSubItem { Text = Morphic.Localization.Strings.MoreSettingsToMakeComputerEasierMenuItem, Padding = padding, MinHeight = 0 };
        moreSettingsMenuItem.Items.Add(new MenuFlyoutItem { Text = Morphic.Localization.Strings.OsSettingsSectionHeader, IsEnabled = false, Padding = padding, MinHeight = 0 });
        moreSettingsMenuItem.Items.Add(new MenuFlyoutItem { Text = Morphic.Localization.Strings.OsMagnifierSettingsMenuItem, Padding = padding, MinHeight = 0 });
        moreSettingsMenuItem.Items.Add(new MenuFlyoutItem { Text = Morphic.Localization.Strings.OsReadAloudSettingsMenuItem, Padding = padding, MinHeight = 0 });
        moreSettingsMenuItem.Items.Add(new MenuFlyoutItem { Text = Morphic.Localization.Strings.OsColorVisionSettingsMenuItem, Padding = padding, MinHeight = 0 });        // ms-settings:easeofaccess-colorfilter
        moreSettingsMenuItem.Items.Add(new MenuFlyoutItem { Text = Morphic.Localization.Strings.OsNightModeSettingsMenuItem, Padding = padding, MinHeight = 0 });          // ms-settings:nightlight
        moreSettingsMenuItem.Items.Add(new MenuFlyoutItem { Text = Morphic.Localization.Strings.OsDarkModeSettingsMenuItem, Padding = padding, MinHeight = 0 });           // ms-settings:colors
        moreSettingsMenuItem.Items.Add(new MenuFlyoutItem { Text = Morphic.Localization.Strings.OsContrastSettingsMenuItem, Padding = padding, MinHeight = 0 });           // ms-settings:easeofaccess-highcontrast
        moreSettingsMenuItem.Items.Add(new MenuFlyoutItem { Text = Morphic.Localization.Strings.OsVoiceSettingsMenuItem, Padding = padding, MinHeight = 0 });              // ms-settings:easeofaccess-speechrecognition
        //
        moreSettingsMenuItem.Items.Add(new MenuFlyoutItem { Text = Morphic.Localization.Strings.OsOtherSettingsSectionHeader, IsEnabled = false, Padding = padding, MinHeight = 0 });   // section header
        moreSettingsMenuItem.Items.Add(new MenuFlyoutItem { Text = Morphic.Localization.Strings.OsMouseSettingsMenuItem, Padding = padding, MinHeight = 0 });              // ms-settings:mousetouchpad
        moreSettingsMenuItem.Items.Add(new MenuFlyoutItem { Text = Morphic.Localization.Strings.OsPointerSizeSettingsMenuItem, Padding = padding, MinHeight = 0 });        // ms-settings:easeofaccess-mousepointer
        moreSettingsMenuItem.Items.Add(new MenuFlyoutItem { Text = Morphic.Localization.Strings.OsKeyboardSettingsMenuItem, Padding = padding, MinHeight = 0 });           // ms-settings:easeofaccess-keyboard
        moreSettingsMenuItem.Items.Add(new MenuFlyoutItem { Text = Morphic.Localization.Strings.OsLanguageSettingsMenuItem, Padding = padding, MinHeight = 0 });           // ms-settings:regionlanguage
        moreSettingsMenuItem.Items.Add(new MenuFlyoutItem { Text = Morphic.Localization.Strings.OsAllAccessibilityOptionsMenuItem, Padding = padding, MinHeight = 0 });    // ms-settings:easeofaccess
        //
        menuFlyout.Items.Add(moreSettingsMenuItem);

        menuFlyout.Items.Add(new MenuFlyoutSeparator());

        var aboutMorphicMenuItem = new MenuFlyoutItem { Text = Morphic.Localization.Strings.AboutMorphicMenuItem, Padding = padding, MinHeight = 0 };
        aboutMorphicMenuItem.KeyboardAccelerators.Add(new Microsoft.UI.Xaml.Input.KeyboardAccelerator
        {
            Key = Windows.System.VirtualKey.A,
            Modifiers = Windows.System.VirtualKeyModifiers.None
        });
        aboutMorphicMenuItem.Click += (s, _) => { this.AboutMorphicMenuItemClicked?.Invoke(s, EventArgs.Empty); };
        menuFlyout.Items.Add(aboutMorphicMenuItem);

        var quitMorphicMenuItem = new MenuFlyoutItem { Text = Morphic.Localization.Strings.QuitMorphic, /*Icon = new SymbolIcon(Symbol.Paste), */Padding = padding, MinHeight = 0 };
        quitMorphicMenuItem.KeyboardAccelerators.Add(new Microsoft.UI.Xaml.Input.KeyboardAccelerator
        {
            Key = Windows.System.VirtualKey.Q,
            Modifiers = Windows.System.VirtualKeyModifiers.None
        });
        quitMorphicMenuItem.Click += (s, _) => { this.QuitMorphicMenuItemClicked?.Invoke(s, EventArgs.Empty); };
        menuFlyout.Items.Add(quitMorphicMenuItem);

        // Mirror the menu CONTENT for an RTL app language. A MenuFlyout presenter does not inherit
        // FlowDirection from its ShowAt placement target (the tray menu's owner is a utility window, not the
        // RTL bar; even the logo menu's RTL bar target does not propagate it), so set it explicitly. CONTENT
        // axis -> the APP language (ReadingDirection.SessionFlowDirection), consistent with the bar/About box.
        //
        // Set it on TWO layers: the ITEMS (so their text right-aligns + accelerators flip) and the PRESENTER (so
        // the box's own content lays out RTL). NOTE: this does NOT control which side the box opens on -- a flyout
        // always anchors at its top-LEFT corner; the leftward open is done by the width offset in Show(). The
        // presenter style adds only a FlowDirection setter, so it keeps its default theme template/appearance.
        foreach (var menuFlyoutItemElement in menuFlyout.Items)
        {
            menuFlyoutItemElement.FlowDirection = Morphic.Localization.ReadingDirection.SessionFlowDirection;
        }
        if (Morphic.Localization.ReadingDirection.SessionFlowDirection == FlowDirection.RightToLeft)
        {
            var rightToLeftPresenterStyle = new Style(typeof(MenuFlyoutPresenter));
            rightToLeftPresenterStyle.Setters.Add(new Setter(FrameworkElement.FlowDirectionProperty, FlowDirection.RightToLeft));
            menuFlyout.MenuFlyoutPresenterStyle = rightToLeftPresenterStyle;
        }

        _menuFlyout = menuFlyout;
        // Safety-net width re-cache on every open (see MenuFlyout_Opened) -- covers menu-width DRIFT with no item
        // change, e.g. a runtime high-contrast / text-size change that re-sizes the menu text (PrewarmWidth /
        // RefreshWidth would not catch that, and this menu is a singleton so it is never rebuilt).
        _menuFlyout.Opened += this.MenuFlyout_Opened;
    }

    // NOTE: owner will be used both to capture a XAML root (required to show the menu) and also to know which MorphicBar to show/hide.
    // If returnFocusTo is supplied, focus is restored to that control (with FocusState.Keyboard)
    // after the menu closes -- used so keyboard users who opened the menu via Space/Enter get
    // focus back on the originating button (typically the MorphicBar logo) when they press ESC.
    public void Show(Microsoft.UI.Xaml.Window ownerWindow, bool morphicBarIsVisible, int x, int y, Microsoft.UI.Xaml.Controls.Control? returnFocusTo = null)
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

        // Under RTL the menu opens to the LEFT of the click (native RTL behavior). A flyout's Position is
        // always its top-LEFT corner regardless of FlowDirection, so shift it left by the menu's width to put
        // the box's RIGHT edge at the click. The width depends on which Show/Hide item is showing; both are
        // pre-measured (PrewarmWidth). A first RTL show before measuring finishes falls back to an estimate.
        if (Morphic.Localization.ReadingDirection.AppIsRightToLeft == true)
        {
            double menuWidthInDips = (morphicBarIsVisible == true) ? _cachedWidthWhenBarVisibleInDips : _cachedWidthWhenBarHiddenInDips;
            if (menuWidthInDips <= 0.0)
            {
                menuWidthInDips = MorphicMainMenu.DEFAULT_MENU_WIDTH_ESTIMATE_IN_DIPS;
            }
            relativeX -= menuWidthInDips;
        }

        // Restore focus to returnFocusTo ONLY if the menu was dismissed without the user choosing
        // an item (ESC, click-outside, etc.). If they clicked a menu item, that item's handler
        // typically does something focus-relevant (opens About, quits the app, etc.) and we
        // should not yank focus back to the logo button afterward. Track item-Click subscriptions
        // for the duration of this Show; the Closed handler checks the flag and unsubscribes.
        if (returnFocusTo is not null)
        {
            bool itemWasClicked = false;
            var itemHandlers = new System.Collections.Generic.List<(Microsoft.UI.Xaml.Controls.MenuFlyoutItem Item, RoutedEventHandler Handler)>();
            foreach (var item in _menuFlyout.Items)
            {
                if (item is Microsoft.UI.Xaml.Controls.MenuFlyoutItem mfi)
                {
                    RoutedEventHandler handler = (_, _) => { itemWasClicked = true; };
                    mfi.Click += handler;
                    itemHandlers.Add((mfi, handler));
                }
            }
            EventHandler<object>? closedHandler = null;
            closedHandler = (s, e) =>
            {
                _menuFlyout.Closed -= closedHandler;
                foreach (var (mfi, handler) in itemHandlers)
                {
                    mfi.Click -= handler;
                }
                if (itemWasClicked == true)
                {
                    return;
                }
                // Deferred via TryEnqueue so the Focus call lands after the flyout has fully torn
                // down its visual tree -- otherwise the Focus can be clobbered by the teardown.
                _ = returnFocusTo.DispatcherQueue.TryEnqueue(() =>
                {
                    try { _ = returnFocusTo.Focus(Microsoft.UI.Xaml.FocusState.Keyboard); }
                    catch (System.Runtime.InteropServices.COMException) { }
                });
            };
            _menuFlyout.Closed += closedHandler;
        }

        // pop up the menu flyout
        _menuFlyout.ShowAt(root, new FlyoutShowOptions
        {
            Position = new Windows.Foundation.Point(relativeX, relativeY),
            ShowMode = FlyoutShowMode.Standard
        });
    }

    // Pre-measures BOTH configurations' widths ONCE at startup so the menu's first RTL open is already exact.
    // Delegates to MeasureWidthInvisibly (a single synchronous show); RefreshWidth re-runs the same measure when
    // the items change. No-op in LTR (no offset is applied); if the owner content is not laid out yet, waits for
    // Loaded.
    public void PrewarmWidth(Microsoft.UI.Xaml.Window ownerWindow)
    {
        _ownerWindowForMeasuring = ownerWindow;
        if (Morphic.Localization.ReadingDirection.AppIsRightToLeft == false)
        {
            return;
        }
        if (ownerWindow.Content is not FrameworkElement ownerContent)
        {
            return;
        }
        if (ownerContent.XamlRoot is null)
        {
            ownerContent.Loaded += this.OwnerContentLoaded_PrewarmWidth;
            return;
        }
        this.MeasureWidthInvisibly(ownerContent);
    }

    private void OwnerContentLoaded_PrewarmWidth(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement ownerContent)
        {
            return;
        }
        ownerContent.Loaded -= this.OwnerContentLoaded_PrewarmWidth;
        if (ownerContent.XamlRoot is not null)
        {
            this.MeasureWidthInvisibly(ownerContent);
        }
    }

    // Re-measures both configurations' widths so RTL placement is exact on the FIRST open after a menu-item
    // change. Call whenever the menu's items change. Uses the SYNCHRONOUS measure (one show; both configs read
    // in a single block with no message pump in between), which keeps the bar's activation-loss to a blink --
    // unlike an async runtime show, whose ~150ms+ deactivation window swallowed the next click. Falls back to
    // invalidation (re-learned lazily on the next real open) if no owner window is available. No-op in LTR.
    public void RefreshWidth()
    {
        if (Morphic.Localization.ReadingDirection.AppIsRightToLeft == false)
        {
            return;
        }
        if (_ownerWindowForMeasuring?.Content is FrameworkElement ownerContent && ownerContent.XamlRoot is not null)
        {
            this.MeasureWidthInvisibly(ownerContent);
        }
        else
        {
            _cachedWidthWhenBarVisibleInDips = 0.0;
            _cachedWidthWhenBarHiddenInDips = 0.0;
        }
    }

    // Measures BOTH configurations' widths so the FIRST RTL open of either is exact. Does it SYNCHRONOUSLY: shows
    // the flyout with an invisible (Opacity 0) presenter, then -- within ONE show and ONE synchronous block (no
    // message pump in between) -- reads the bar-visible width, toggles the Show/Hide item, re-lays-out, and reads
    // the bar-hidden width. Synchronous matters two ways: (1) no async Opened to lose the race to a fast first
    // click, and (2) the bar's activation loss stays a blink, so a RUNTIME call (RefreshWidth) does not eat the
    // next click the way an async show did. No frame ever renders the menu -> no flash.
    private void MeasureWidthInvisibly(FrameworkElement ownerContent)
    {
        var savedShowMenuItemVisibility = _showMorphicBarMenuItem.Visibility;
        var savedHideMenuItemVisibility = _hideMorphicBarMenuItem.Visibility;
        var savedPresenterStyle = _menuFlyout.MenuFlyoutPresenterStyle;

        var invisiblePresenterStyle = new Style(typeof(MenuFlyoutPresenter));
        invisiblePresenterStyle.Setters.Add(new Setter(UIElement.OpacityProperty, 0.0));
        invisiblePresenterStyle.Setters.Add(new Setter(FrameworkElement.FlowDirectionProperty, Morphic.Localization.ReadingDirection.SessionFlowDirection));
        _menuFlyout.MenuFlyoutPresenterStyle = invisiblePresenterStyle;

        // Begin with the bar-visible (Hide showing) configuration.
        _showMorphicBarMenuItem.Visibility = Visibility.Collapsed;
        _hideMorphicBarMenuItem.Visibility = Visibility.Visible;

        _menuFlyout.ShowAt(ownerContent, new FlyoutShowOptions
        {
            Position = new Windows.Foundation.Point(0, 0),
            ShowMode = FlyoutShowMode.Transient
        });

        var menuFlyoutPresenter = this.FindOpenMenuPresenter();
        if (menuFlyoutPresenter is not null)
        {
            // Measure the bar-visible configuration.
            menuFlyoutPresenter.InvalidateMeasure();
            menuFlyoutPresenter.UpdateLayout();
            if (menuFlyoutPresenter.ActualWidth > 0.0)
            {
                _cachedWidthWhenBarVisibleInDips = menuFlyoutPresenter.ActualWidth;
            }

            // Switch to the bar-hidden (Show showing) configuration and re-measure -- still within this one show.
            _showMorphicBarMenuItem.Visibility = Visibility.Visible;
            _hideMorphicBarMenuItem.Visibility = Visibility.Collapsed;
            menuFlyoutPresenter.InvalidateMeasure();
            menuFlyoutPresenter.UpdateLayout();
            if (menuFlyoutPresenter.ActualWidth > 0.0)
            {
                _cachedWidthWhenBarHiddenInDips = menuFlyoutPresenter.ActualWidth;
            }
        }

        _menuFlyout.MenuFlyoutPresenterStyle = savedPresenterStyle;
        _showMorphicBarMenuItem.Visibility = savedShowMenuItemVisibility;
        _hideMorphicBarMenuItem.Visibility = savedHideMenuItemVisibility;
        _menuFlyout.Hide();
    }

    // Finds the menu's currently-open presenter (a MenuFlyout does not expose it directly) among the open popups
    // for the flyout's XamlRoot, or null if not found.
    private MenuFlyoutPresenter? FindOpenMenuPresenter()
    {
        var xamlRoot = _menuFlyout.XamlRoot;
        if (xamlRoot is null)
        {
            return null;
        }
        foreach (var openPopup in Microsoft.UI.Xaml.Media.VisualTreeHelper.GetOpenPopupsForXamlRoot(xamlRoot))
        {
            if (openPopup.Child is MenuFlyoutPresenter menuFlyoutPresenter)
            {
                return menuFlyoutPresenter;
            }
        }
        return null;
    }

    // Safety net (see ctor subscription): on every real open, re-cache the presenter's actual width into the slot
    // for whichever Show/Hide item is currently showing, so width drift from a runtime display change (high
    // contrast, system text size) self-heals on the next open. PrewarmWidth/RefreshWidth already fill both slots
    // for normal cases; this is only for drift that happens WITHOUT an item change. (It does not run during the
    // invisible measure: that show+hide is synchronous, so the flyout never actually opens and Opened never fires.)
    private void MenuFlyout_Opened(object? sender, object e)
    {
        var menuFlyoutPresenter = this.FindOpenMenuPresenter();
        if (menuFlyoutPresenter is null || menuFlyoutPresenter.ActualWidth <= 0.0)
        {
            return;
        }
        // bar VISIBLE => the "Hide MorphicBar" item is the one showing.
        if (_hideMorphicBarMenuItem.Visibility == Visibility.Visible)
        {
            _cachedWidthWhenBarVisibleInDips = menuFlyoutPresenter.ActualWidth;
        }
        else
        {
            _cachedWidthWhenBarHiddenInDips = menuFlyoutPresenter.ActualWidth;
        }
    }
}
