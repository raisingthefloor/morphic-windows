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

using System;
using System.Collections.Generic;

namespace Morphic.Theme;

// Friendly Morphic-side enum for the subset of Win32 SYS_COLOR_INDEX values we expose.
// Wrapping the CsWin32 enum keeps caller code readable (MorphicSysColor.Window vs.
// SYS_COLOR_INDEX.COLOR_WINDOW) and shields callers from any CsWin32 metadata renames.
public enum MorphicSysColor
{
    Window,
    WindowText,
    ButtonFace,
    ButtonText,
    Highlight,
    HighlightText,
    GrayText,
    Hotlight,
    ActiveBorder,
    WindowFrame,
}

// Binds the Color of an existing SolidColorBrush to a Win32 system color obtained via
// GetSysColor, and auto-refreshes when the system reports a high-contrast / theme change.
//
// Why use composition (Bind) rather than a SolidColorBrush subclass? WinUI 3's SolidColorBrush
// is sealed. The Bind helper takes an existing brush (typically declared in App.xaml as a
// placeholder, e.g. <SolidColorBrush x:Key="..." Color="Transparent"/>) and wires it up.
//
// Why does this exists at all? WinUI 3's built-in SystemColorWindowBrush does NOT track the active
// HC theme correctly in our context -- it stays at a fixed dark-gray default regardless of
// which HC theme is selected (Aquatic / Desert / Night Sky / Dusk all produced the same brush
// color). Other WinUI HC brushes (SystemColorWindowTextBrush, SystemColorHighlightBrush etc.)
// DO track correctly, and the framework's HighContrastAdjustment text backings correctly use
// GetSysColor under the hood -- so this binder is a targeted patch for the specific keys
// WinUI gets wrong. Use a {StaticResource ResourceKey="SystemColor..."} alias whenever the
// built-in brush actually works.
//
// Lifecycle: bound brushes are app-lifetime resources (declared as ResourceDictionary entries),
// so the static bindings list never needs to release entries. The single subscription on
// SystemSettingsListener.HighContrastChanged is set up on first Bind and stays for the app's
// lifetime.
internal static class MorphicSysColorBrushBinder
{
    private record Binding(
        Microsoft.UI.Xaml.Media.SolidColorBrush Brush,
        Windows.Win32.Graphics.Gdi.SYS_COLOR_INDEX SysColorIndex,
        Microsoft.UI.Dispatching.DispatcherQueue? DispatcherQueue);

    private static readonly object _bindingsLock = new();
    private static readonly List<Binding> _bindings = new();
    public static void Bind(Microsoft.UI.Xaml.Media.SolidColorBrush brush, MorphicSysColor color)
    {
        var sysColorIndex = MorphicSysColorBrushBinder.MapToWin32SysColorIndex(color);

        // Capture the current thread's dispatcher so we can marshal future Color updates back
        // onto the UI thread (SystemSettingsListener.HighContrastChanged fires from its
        // message-window thread).
        var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        var binding = new Binding(brush, sysColorIndex, dispatcherQueue);

        lock (_bindingsLock)
        {
            _bindings.Add(binding);
        }

        // Seed the initial Color synchronously so the brush is correct before its first paint.
        MorphicSysColorBrushBinder.RefreshBinding(binding);
    }

    private static void RefreshBinding(MorphicSysColorBrushBinder.Binding binding)
    {
        var colorref = Windows.Win32.PInvoke.GetSysColor(binding.SysColorIndex);
        uint raw = (uint)colorref;
        // COLORREF is 0x00BBGGRR -- byte 0 is R, byte 1 is G, byte 2 is B
        byte r = (byte)(raw & 0xFF);
        byte g = (byte)((raw >> 8) & 0xFF);
        byte b = (byte)((raw >> 16) & 0xFF);
        binding.Brush.Color = Windows.UI.Color.FromArgb(0xFF, r, g, b);
    }

    private static Windows.Win32.Graphics.Gdi.SYS_COLOR_INDEX MapToWin32SysColorIndex(MorphicSysColor color)
    {
        return color switch
        {
            MorphicSysColor.Window => Windows.Win32.Graphics.Gdi.SYS_COLOR_INDEX.COLOR_WINDOW,
            MorphicSysColor.WindowText => Windows.Win32.Graphics.Gdi.SYS_COLOR_INDEX.COLOR_WINDOWTEXT,
            MorphicSysColor.ButtonFace => Windows.Win32.Graphics.Gdi.SYS_COLOR_INDEX.COLOR_BTNFACE,
            MorphicSysColor.ButtonText => Windows.Win32.Graphics.Gdi.SYS_COLOR_INDEX.COLOR_BTNTEXT,
            MorphicSysColor.Highlight => Windows.Win32.Graphics.Gdi.SYS_COLOR_INDEX.COLOR_HIGHLIGHT,
            MorphicSysColor.HighlightText => Windows.Win32.Graphics.Gdi.SYS_COLOR_INDEX.COLOR_HIGHLIGHTTEXT,
            MorphicSysColor.GrayText => Windows.Win32.Graphics.Gdi.SYS_COLOR_INDEX.COLOR_GRAYTEXT,
            MorphicSysColor.Hotlight => Windows.Win32.Graphics.Gdi.SYS_COLOR_INDEX.COLOR_HOTLIGHT,
            MorphicSysColor.ActiveBorder => Windows.Win32.Graphics.Gdi.SYS_COLOR_INDEX.COLOR_ACTIVEBORDER,
            MorphicSysColor.WindowFrame => Windows.Win32.Graphics.Gdi.SYS_COLOR_INDEX.COLOR_WINDOWFRAME,
            _ => throw new ArgumentOutOfRangeException(nameof(color), color, null),
        };
    }
}
