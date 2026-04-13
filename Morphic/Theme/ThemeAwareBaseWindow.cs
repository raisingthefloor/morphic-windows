// Copyright 2024-2026 Raising the Floor - US, Inc.
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
using Morphic.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.ViewManagement;

namespace Morphic.Theme;

public class ThemeAwareBaseWindow : Window
{
    public ElementTheme CurrentTheme { get; private set; }
    public event EventHandler<ElementTheme>? ThemeChanged;

    public ThemeAwareBaseWindow() : base()
    {
        // set the initial theme based on the legacy Win32 theme detection mechanism
        // NOTE: this won't account for WinUI-specific theme overrides, so the subclass should call SwitchToWinUIThemeTracking() after InitializeComponent() in its constructor
        this.CurrentTheme = Win32AppTheme.GetAppTheme();
        Win32AppTheme.ThemeChanged += AppTheme_ThemeChanged;

        // if dark mode is enabled for the app, color it appropriately
        _ = this.SetNonClientUIDarkModeAttribute(this.CurrentTheme == ElementTheme.Dark);
    }

    private MorphicResult<MorphicUnit, MorphicUnit> SetNonClientUIDarkModeAttribute(bool value)
    {
        var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        return Morphic.WindowsNative.Theme.WindowUtils.SetNonClientUIDarkModeAttribute(windowHandle, value);
    }

    private void AppTheme_ThemeChanged(object? sender, ElementTheme e)
    {
        // NOTE: Win32AppTheme.ThemeChanged gets called even when it's just theme colors changing (i.e. not the overall theme itself), so only process this event when the actual theme has changed
        if (this.CurrentTheme != e)
        {
            this.CurrentTheme = e;
            DispatcherQueue.TryEnqueue(() =>
            {
                _ = this.SetNonClientUIDarkModeAttribute(e == ElementTheme.Dark);
                this.ThemeChanged?.Invoke(this, CurrentTheme);
            });
        }
    }

    // NOTE: this code should be called in the constructor for the subclass, after InitializeComponent();
    protected MorphicResult<MorphicUnit, MorphicUnit> SwitchToWinUIThemeTracking()
    {
        if (Content is FrameworkElement fe)
        {
            // remove fallback theme change event capture (from Win32 app theme-aware code)
            Win32AppTheme.ThemeChanged -= AppTheme_ThemeChanged;

            // Upgrade to the actual element theme (accounts for per-element overrides)
            var actualTheme = fe.ActualTheme;
            if (actualTheme != this.CurrentTheme)
            {
                this.CurrentTheme = actualTheme;
                ThemeChanged?.Invoke(this, this.CurrentTheme);
            }

            // capture theme changes via WinUI instead
            fe.ActualThemeChanged += (sender, _) => { AppTheme_ThemeChanged(sender, sender.ActualTheme); };

            return MorphicResult.OkResult();
        }
        else
        {
            return MorphicResult.ErrorResult();
        }
    }
}
