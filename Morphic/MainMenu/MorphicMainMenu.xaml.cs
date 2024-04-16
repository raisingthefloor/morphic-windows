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

using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Morphic.MainMenu;

/// <summary>
/// Interaction logic for MorphicMainMenu.xaml
/// </summary>
public partial class MorphicMainMenu : ContextMenu
{
    private Morphic.AboutWindow.AboutWindow? _aboutWindow = null;

    public MorphicMainMenu()
    {
        InitializeComponent();
    }

    public interface IShowPlacement
    {
        // NOTE: the passed-in rect must be divided by the current screen scaling before being passed into this function (as WPF will not recognize the absolute position correctly otherwise)
        public record ScaledAbsolutePosition(Rect Rect) : IShowPlacement;
        public record MouseCursor : IShowPlacement;
        public record NextToControl(Control Control) : IShowPlacement;
    }
    //
    public void Show(IShowPlacement showPlacement)
    {
        // see: https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.primitives.placementmode?view=windowsdesktop-8.0
        switch (showPlacement)
        {
            case IShowPlacement.ScaledAbsolutePosition(Rect: var rect):
                {
                    // NOTE: the passed-in rect must be divided by the current screen scaling before being passed into this function (as WPF will not recognize the position correctly otherwise)
                    //
                    // NOTE: we use .Top to prefer that the menu pops up above the control; Windows should automatically open on the other side if the menu would be obscured by popping upwards.
                    this.Placement = PlacementMode.Absolute;
                    this.PlacementTarget = null;
                    this.HorizontalOffset = rect.Left;
                    this.VerticalOffset = rect.Top;
                }
                break;
            case IShowPlacement.MouseCursor:
                {
                    // NOTE: we use .Mouse here instead of .MousePoint, as there is no HorizontalOffset or VerticalOffset requirement
                    this.Placement = PlacementMode.Mouse;
                    this.PlacementTarget = null;
                    this.HorizontalOffset = 0;
                    this.VerticalOffset = 0;
                }
                break;
            case IShowPlacement.NextToControl(Control: var control):
                {
                    // NOTE: we use .Top to prefer that the menu pops up above the control; Windows should automatically open on the other side if the menu would be obscured by popping upwards.
                    this.Placement = PlacementMode.Top;
                    this.PlacementTarget = control;
                    this.HorizontalOffset = 0;
                    this.VerticalOffset = 0;
                }
                break;
            default:
                throw new Exception("invalid code path");
        }

//        var foregroundColor = Morphic.UI.ThemeColors.GetForegroundColor();
//        var foregroundColorAsMediaColor = System.Windows.Media.Color.FromArgb(foregroundColor.A, foregroundColor.R, foregroundColor.G, foregroundColor.B);
//        var backgroundColor = Morphic.UI.ThemeColors.GetBackgroundColor();
//        var backgroundColorAsMediaColor = System.Windows.Media.Color.FromArgb(backgroundColor.A, backgroundColor.R, backgroundColor.G, backgroundColor.B);
//        //
//        this.Background = new SolidColorBrush(backgroundColorAsMediaColor);
//        this.Foreground = new SolidColorBrush(foregroundColorAsMediaColor);

        // open up the menu
        this.IsOpen = true;
    }

    //

    private void AboutMorphicMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // NOTE: we only create one instance of the about window (so that the user doesn't pop up multiple instances)
        if (_aboutWindow is null)
        {
            _aboutWindow = new AboutWindow.AboutWindow();
        }
        // NOTE: we clean up the window's reference when it is closed
        _aboutWindow.Closed += delegate (object? sender, EventArgs e)
        {
            _aboutWindow = null;
        };

        // show the window (and activate it, in case it was already visible--but obscured by another UI element)
        try
        {
            // NOTE: there is a potential edge case where our window was previously closed but not set to null; that should not happen--but if it does then these functions could result in an exception (since a window cannot be shown once it's closed)
            _aboutWindow?.Show();
            _aboutWindow?.Activate();
        }
        catch
        {
            Debug.Assert(false, "Could not show About window.");
        }
    }

    private void QuitMorphicMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    //

}
