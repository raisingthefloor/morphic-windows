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
    // The Text Size group's recompute action is stashed here so the App can trigger a refresh at
    // moments the factory's own subscriptions don't cover -- specifically, right after the bar
    // has been animated to its initial dock corner (where it may have crossed onto a different
    // monitor with a different DPI than the one in effect when the factory ran).
    //
    // Display.DisplayChanged covers ongoing system-wide changes (user changes scale via Settings,
    // monitor add/remove, etc.). It does NOT fire when the BAR window moves between monitors --
    // that's a per-window event, not a display-config event. App.OnLaunched calls
    // RefreshTextSizeButtonState() after AnimateMoveTo for exactly that case.
    private static Action? _refreshTextSizeButtonState;
    internal static void RefreshTextSizeButtonState()
    {
        _refreshTextSizeButtonState?.Invoke();
    }

    public static IBarItemData CreateTextSizeButtonGroup(BarButtonAction? increaseAction, BarButtonAction? decreaseAction)
    {
        const int TextSizeIncrementIndex = 0;
        const int TextSizeDecrementIndex = 1;

        var increaseButton = new BarButtonData
        {
            // U+2795 HEAVY PLUS SIGN + U+FE0E VARIATION SELECTOR-15 (text presentation);
            // the VS forces monochrome text rendering -- without it Windows falls back
            // to Segoe UI Emoji and renders the glyph in color (e.g. purple).
            Text = "\u2795\uFE0E",
            AccessibleName = new IAccessibleName.AccessibleName(Morphic.Localization.Strings.IncreaseTextSizeAccessibleName),
            ActionTag = "increase",
            Action = increaseAction,
        };
        var decreaseButton = new BarButtonData
        {
            // U+2796 HEAVY MINUS SIGN + U+FE0E (see note above)
            Text = "\u2796\uFE0E",
            AccessibleName = new IAccessibleName.AccessibleName(Morphic.Localization.Strings.DecreaseTextSizeAccessibleName),
            ActionTag = "decrease",
            Action = decreaseAction,
        };

        // Bridge the Text Size buttons to the bar's display DPI state. The buttons reflect
        // whether stepping up (+) or down (-) is currently allowed:
        //   * Custom-scaling override (Windows 8.1 legacy "Custom scaling" feature, surfaced in
        //     Settings > Display > Advanced scaling settings): both buttons disabled -- we can't
        //     step relative to an out-of-ladder sentinel.
        //   * At the maximum offset: + disabled, - enabled.
        //   * At the minimum offset: - disabled, + enabled.
        //   * In between: both enabled.
        //   * Bar HWND not resolvable or display lookup failed: both disabled (defensive --
        //     means we can't talk to the system, so the click handler would also fail).
        //
        // Sources of change handled:
        //   * Display.DisplayChanged (WM_DISPLAYCHANGE): user changes scale via Settings on any
        //     monitor, monitor attach/detach, resolution change. Catches our own clicks too --
        //     SetDpiOffsetAsync triggers WM_DISPLAYCHANGE.
        //   * BarItemDataFactory.RefreshTextSizeButtonState() called from App.OnLaunched after
        //     AnimateMoveTo, so the buttons reflect the dock-corner monitor (which may differ
        //     from whatever monitor the bar window was constructed on).
        Action recomputeState = () =>
        {
            var barManager = ((App)Microsoft.UI.Xaml.Application.Current).MorphicBarManager;
            if (barManager is null)
            {
                increaseButton.IsEnabled = false;
                decreaseButton.IsEnabled = false;
                return;
            }
            // Use the bar's INTENDED current monitor, not the live window position: during a
            // drag-release the bar window can straddle the monitor boundary at the moment we
            // recompute, which made GetDisplayNearestWindowHandle pick the wrong monitor and the +/-
            // buttons disable inconsistently. GetBarCurrentMonitorHandle is the authoritative
            // destination monitor and is animation-independent.
            var barMonitorHandle = barManager.GetBarCurrentMonitorHandle();
            if (barMonitorHandle == IntPtr.Zero)
            {
                increaseButton.IsEnabled = false;
                decreaseButton.IsEnabled = false;
                return;
            }
            var displayResult = Morphic.WindowsNative.Display.Display.GetDisplayByMonitorHandle(barMonitorHandle);
            if (displayResult.IsError)
            {
                increaseButton.IsEnabled = false;
                decreaseButton.IsEnabled = false;
                return;
            }
            var display = displayResult.Value!;
            var rangeResult = display.GetCurrentDpiOffsetAndRange();
            if (rangeResult.IsError)
            {
                increaseButton.IsEnabled = false;
                decreaseButton.IsEnabled = false;
                return;
            }
            var range = rangeResult.Value;
            if (Morphic.WindowsNative.Display.Display.IsCustomScalingPercentage(range.CurrentDpiOffset))
            {
                increaseButton.IsEnabled = false;
                decreaseButton.IsEnabled = false;
                return;
            }
            increaseButton.IsEnabled = range.CurrentDpiOffset < range.MaximumDpiOffset;
            decreaseButton.IsEnabled = range.CurrentDpiOffset > range.MinimumDpiOffset;
        };
        //
        EventHandler displayChangedHandler = (_, _) => recomputeState();
        Morphic.WindowsNative.Display.Display.DisplayChanged += displayChangedHandler;
        increaseButton.AddDisposeAction(() =>
        {
            Morphic.WindowsNative.Display.Display.DisplayChanged -= displayChangedHandler;
            _refreshTextSizeButtonState = null;
        });
        _refreshTextSizeButtonState = recomputeState;
        //
        recomputeState();

        // "Text Size" -- two pushbuttons (+ on the left, - on the right), equal width;
        // `-` key invokes the decrement sub-button, `+` key invokes the increment sub-button
        return new BarMultiButtonData
        {
            Header = Morphic.Localization.Strings.SizeOfTextHeader,
            SettingsPage = Morphic.SystemSettings.WindowsSettings.Page.Display,
            SizingMode = MultiButtonSizingMode.StretchToLargest,
            // +/- is an inc/dec pair; keep them side-by-side even when the bar is vertical so the
            // pair reads as one control rather than two stacked rows
            AlwaysHorizontalSubButtons = true,
            Buttons = new List<BarButtonData> { increaseButton, decreaseButton },
            IncDecShortcuts = new BarMultiButtonIncDecShortcuts
            {
                DecrementButtonIndex = TextSizeDecrementIndex,
                IncrementButtonIndex = TextSizeIncrementIndex,
            },
        };
    }

    //

    public static IBarItemData CreateMagnifierButtonGroup(BarButtonAction? showAction, BarButtonAction? hideAction)
    {
        // "Magnifier" -- two pushbuttons (Show on the left, Hide on the right), equal width.
        return new BarMultiButtonData
        {
            Header = Morphic.Localization.Strings.MagnifierHeader,
            SettingsPage = Morphic.SystemSettings.WindowsSettings.Page.Magnifier,
            SizingMode = MultiButtonSizingMode.StretchToLargest,
            Buttons = new List<BarButtonData>
                {
                    new BarButtonData
                    {
                        Text = Morphic.Localization.Strings.MagnifierShowLabel,
                        AccessibleName = new IAccessibleName.AccessibleName(Morphic.Localization.Strings.MagnifierShowAccessibleName),
                        ActionTag = "magnifier-show",
                        Action = showAction,
                    },
                    new BarButtonData
                    {
                        Text = Morphic.Localization.Strings.MagnifierHideLabel,
                        AccessibleName = new IAccessibleName.AccessibleName(Morphic.Localization.Strings.MagnifierHideAccessibleName),
                        ActionTag = "magnifier-hide",
                        Action = hideAction,
                    },
                },
        };
    }

    //

    public static IBarItemData CreateSnipButton(BarButtonAction? action)
    {
        // "Snip" -- single label pushbutton
        return new BarButtonData
        {
            Header = Morphic.Localization.Strings.SnipHeader,
            Text = Morphic.Localization.Strings.SnipCopyLabel,
            AccessibleName = new IAccessibleName.AccessibleName(Morphic.Localization.Strings.SnipAccessibleName),
            ActionTag = "snip-copy",
            Action = action,
        };
    }

    //

    public static IBarItemData CreateReadSelectedButtonGroup(BarButtonAction? playAction, BarButtonAction? stopAction)
    {
        // "Read Selected" -- two pushbuttons (Play on the left, Stop on the right), equal width.
        return new BarMultiButtonData
        {
            Header = Morphic.Localization.Strings.ReadSelectedHeader,
            SettingsPage = Morphic.SystemSettings.WindowsSettings.Page.ReadAloud,
            SizingMode = MultiButtonSizingMode.StretchToLargest,
            // Play/Stop is a transport pair; keep them side-by-side even when the bar is vertical
            // so the pair reads as one control rather than two stacked rows (matches Text Size).
            AlwaysHorizontalSubButtons = true,
            Buttons = new List<BarButtonData>
                {
                    new BarButtonData
                    {
                        // U+25B6 BLACK RIGHT-POINTING TRIANGLE + U+FE0E VARIATION SELECTOR-15
                        // (text presentation); the VS forces monochrome rendering -- without it
                        // Windows can fall back to Segoe UI Emoji and render the glyph in color.
                        Text = "\u25B6\uFE0E", // ▶
                        AccessibleName = new IAccessibleName.AccessibleName(Morphic.Localization.Strings.ReadSelectedPlayAccessibleName),
                        ActionTag = "read-selected-play",
                        Action = playAction,
                    },
                    new BarButtonData
                    {
                        // U+25A0 BLACK SQUARE + U+FE0E (see note above)
                        Text = "\u25A0\uFE0E", // ■
                        AccessibleName = new IAccessibleName.AccessibleName(Morphic.Localization.Strings.ReadSelectedStopAccessibleName),
                        ActionTag = "read-selected-stop",
                        Action = stopAction,
                    },
                },
        };
    }

    //

    public static IBarItemData CreateContrastColorButtonGroup(BarButtonAction? contrastAction, BarButtonAction? colorAction, BarButtonAction? darkAction, BarButtonAction? nightAction)
    {
        // "Contrast & Color" -- 4 toggle buttons, per-content sized
        var contrastButton = new BarButtonData { Text = Morphic.Localization.Strings.ContrastLabel, IsToggle = true, ActionTag = "contrast", Action = contrastAction, AccessibleName = new IAccessibleName.AccessibleName(Morphic.Localization.Strings.ContrastThemeAccessibleName), SettingsPage = Morphic.SystemSettings.WindowsSettings.Page.Contrast };
        var colorButton    = new BarButtonData { Text = Morphic.Localization.Strings.ColorLabel,    IsToggle = true, ActionTag = "color",    Action = colorAction,    AccessibleName = new IAccessibleName.AccessibleName(Morphic.Localization.Strings.ColorFiltersAccessibleName), SettingsPage = Morphic.SystemSettings.WindowsSettings.Page.ColorVision };
        var darkButton     = new BarButtonData { Text = Morphic.Localization.Strings.DarkLabel,     IsToggle = true, ActionTag = "dark",     Action = darkAction,     AccessibleName = new IAccessibleName.AccessibleName(Morphic.Localization.Strings.DarkModeAccessibleName), SettingsPage = Morphic.SystemSettings.WindowsSettings.Page.DarkMode };
        var nightButton    = new BarButtonData { Text = Morphic.Localization.Strings.NightLabel,    IsToggle = true, ActionTag = "night",    Action = nightAction,    AccessibleName = new IAccessibleName.AccessibleName(Morphic.Localization.Strings.NightLightAccessibleName), SettingsPage = Morphic.SystemSettings.WindowsSettings.Page.NightLight };

        // Bridge the Dark button to CachedDarkModeState, which delivers BOTH the effective dark
        // state and whether the button should be toggleable. Under high contrast, IsDark reflects
        // the active HC theme's darkness (HC Black -> dark, HC White / Desert -> light) and
        // IsToggleable is false (the button shows state but doesn't accept clicks, because
        // toggling between HC dark and HC light themes would destroy the user's HC theme choice).
        // Outside of HC, IsDark aggregates the AppsUseDarkMode / SystemUsesDarkMode registry
        // preferences and IsToggleable is true.
        //
        // Subscribe FIRST, then seed the initial values -- doing it the other way around opens
        // a race window between the snapshot read and the subscribe. With subscribe-first, any
        // change that arrives during the seed is delivered through the handler and the INPC
        // equality short-circuit makes a duplicate same-value seed write a no-op.
        EventHandler<Morphic.SettingsUtils.CachedDarkModeStateChangedEventArgs> darkModeStateChangedHandler =
            (_, e) =>
            {
                darkButton.IsChecked = e.IsDark;
                darkButton.IsEnabled = !e.IsHighContrast;
            };
        Morphic.SettingsUtils.CachedDarkModeState.StateChanged += darkModeStateChangedHandler;
        darkButton.AddDisposeAction(() => Morphic.SettingsUtils.CachedDarkModeState.StateChanged -= darkModeStateChangedHandler);
        //
        darkButton.IsChecked = Morphic.SettingsUtils.CachedDarkModeState.GetCurrentIsDark();
        darkButton.IsEnabled = !Morphic.SettingsUtils.CachedDarkModeState.GetCurrentIsHighContrast();
		
		//

        EventHandler<ColorFiltersIsActiveChangedEventArgs> colorFiltersIsActiveChangedHandler =
            (_, e) => colorButton.IsChecked = e.NewValue;
        ColorFilters.IsActiveChanged += colorFiltersIsActiveChangedHandler;
        colorButton.AddDisposeAction(() => ColorFilters.IsActiveChanged -= colorFiltersIsActiveChangedHandler);
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

        // Bridge the Night button to NightLight.IsOnChanged; this delivers external state transitions 
		// (Action Center tile, 'Settings app > Display > Night light', scheduled on/off, etc.
        //
        // The initial GetIsOnAsync read serves two purposes:
        //   1. Seeds nightButton.IsChecked from the live system state at startup.
        //   2. Primes the SettingItem so its IsEnabled flag is true before the user's first
        //      click. The WinRT SettingItem starts with IsEnabled=false and the OS raises it
        //      asynchronously; the 5-second timeout gives the OS plenty of time to raise
        //      IsEnabled to true. We deliberately await on the ThreadPool (not the UI thread)
        //      since WaitForIsEnabledEventAsync can block briefly before its first async yield.
        EventHandler<Morphic.WindowsNative.Display.NightLightIsOnChangedEventArgs> nightLightIsOnChangedHandler =
            (_, e) => nightButton.IsChecked = e.NewValue;
        // NightLight.IsOnChanged += / -= can throw if the underlying WinRT SettingItem subscribe
        // fails (typically COMException -- see the event's <exception> doc). Wrap so a failed
        // subscribe doesn't tear down the whole CreateContrastColorButtonGroup call; the night
        // button still gets created but won't reflect OS-side state changes.
        try
        {
            Morphic.WindowsNative.Display.NightLight.IsOnChanged += nightLightIsOnChangedHandler;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BarItemDataFactory] NightLight.IsOnChanged subscribe failed: {ex.Message}");
        }
        nightButton.AddDisposeAction(() =>
        {
            try
            {
                Morphic.WindowsNative.Display.NightLight.IsOnChanged -= nightLightIsOnChangedHandler;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BarItemDataFactory] NightLight.IsOnChanged unsubscribe failed: {ex.Message}");
            }
        });
        //
        _ = Task.Run(async () =>
        {
            try
            {
                var initialNightLightIsOnResult = await Morphic.WindowsNative.Display.NightLight.GetIsOnAsync(TimeSpan.FromSeconds(5));
                if (initialNightLightIsOnResult.IsError)
                {
                    System.Diagnostics.Debug.WriteLine("[BarItemDataFactory] Initial NightLight.GetIsOnAsync read failed; defaulting button to unchecked");
                }
                nightButton.IsChecked = initialNightLightIsOnResult.IsSuccess && initialNightLightIsOnResult.Value == true;
            }
            catch (Exception ex)
            {
                // Without this, the fire-and-forget Task's exception would only surface via
                // TaskScheduler.UnobservedTaskException, with no attribution to this site.
                System.Diagnostics.Debug.WriteLine($"NightLight initial prime threw: {ex}");
            }
        });

        return new BarMultiButtonData
        {
            Header = Morphic.Localization.Strings.ContrastAndColorHeader,
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
