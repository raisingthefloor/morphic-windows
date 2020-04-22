﻿// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt
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
using System.Windows;
using System.Windows.Controls;
using MorphicService;
using MorphicSettings;
using System.Windows.Media.Animation;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Imaging;

namespace MorphicWin
{
    /// <summary>
    /// Interaction logic for QuickStrip.xaml
    /// </summary>
    public partial class QuickStrip : Window
    {

        #region Initialization

        public QuickStrip(Session session)
        {
            this.session = session;
            InitializeComponent();
            Deactivated += OnDeactivated;
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            Items = Item.CreateItems(new List<object>()
            {
                new Dictionary<string, object>()
                {
                    ["type"] = "control",
                    ["feature"] = "resolution"
                },
                new Dictionary<string, object>()
                {
                    ["type"] = "control",
                    ["feature"] = "magnifier"
                },
                new Dictionary<string, object>()
                {
                    ["type"] = "control",
                    ["feature"] = "reader"
                },
                new Dictionary<string, object>()
                {
                    ["type"] = "control",
                    ["feature"] = "volume"
                },
                new Dictionary<string, object>()
                {
                    ["type"] = "control",
                    ["feature"] = "contrast"
                }
            });
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            Reposition(animated: false);
        }

        private readonly Session session;

        private void OnDeactivated(object? sender, EventArgs e)
        {
        }

        #endregion

        #region Logo Button & Menu

        /// <summary>
        /// Event handler for when the user clicks on the logo button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LogoButtonClicked(object sender, RoutedEventArgs e)
        {
            LogoButton.ContextMenu.IsOpen = true;
        }

        /// <summary>
        /// Event handler for when the user selects Hide Quick Strip from the logo button's menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HideQuickStrip(object sender, RoutedEventArgs e)
        {
            App.Shared.HideQuickStrip();
        }

        /// <summary>
        /// Event handler for when the user selects Customize Quick Strip from the logo button's menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CustomizeQuickStrip(object sender, RoutedEventArgs e)
        {
            App.Shared.OpenConfigurator();
        }

        /// <summary>
        /// Event handler for when the user selects Quit from the logo button's menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Quit(object sender, RoutedEventArgs e)
        {
            App.Shared.Shutdown();
        }

        #endregion

        #region Items

        /// <summary>
        /// The definition of an item to show on the quick strip
        /// </summary>
        public class Item
        {

            /// <summary>
            /// The raw preference data, stored in generic form to prevent loss of data
            /// </summary>
            public Dictionary<string, object> Payload;

            /// <summary>
            /// Create a new item from generic data
            /// </summary>
            /// <param name="payload"></param>
            public Item(Dictionary<string, object> payload)
            {
                Payload = payload;
            }

            /// <summary>
            /// Get the control that should appear on the quick strip for this item, or <code>null</code> if no valid control exists
            /// </summary>
            /// <returns></returns>
            virtual public QuickStripItemControl? GetControl()
            {
                return null;
            }

            /// <summary>
            /// Create a list of items from the item descriptions
            /// </summary>
            /// <param name="descriptions"></param>
            /// <returns></returns>
            public static List<Item> CreateItems(List<object> descriptions)
            {
                var items = new List<Item>();
                foreach (var obj in descriptions)
                {
                    if (CreateItem(obj) is Item item)
                    {
                        items.Add(item);
                    }
                }
                return items;
            }

            /// <summary>
            /// Create an item from the given description
            /// </summary>
            /// <param name="description"></param>
            /// <returns></returns>
            public static Item? CreateItem(object description)
            {
                if (description is Dictionary<string, object> payload)
                {
                    if (payload.TryGetValue("type", out var typeValue))
                    {
                        if (typeValue is string type)
                        {
                            switch (type)
                            {
                                case "control":
                                    return new ControlItem(payload);
                            }
                        }
                    }
                    return new Item(payload);
                }
                return null;
            }

        }

        /// <summary>
        /// An item representing a known setting control
        /// </summary>
        private class ControlItem : Item
        {

            /// <summary>
            /// Create a control item from a generic payload, extracting the <code>Feature</code> if present
            /// </summary>
            /// <param name="payload"></param>
            public ControlItem(Dictionary<string, object> payload): base(payload)
            {
                if (payload.TryGetValue("feature", out var obj))
                {
                    if (obj is string feature)
                    {
                        Feature = feature;
                    }
                }
            }

            public string Feature { get; private set; } = "";

            public override QuickStripItemControl? GetControl()
            {
                switch (Feature)
                {
                    case "resolution":
                        {
                            var control = new QuickStripSegmentedButtonControl();
                            control.TitleLabel.Content = Properties.Resources.QuickStrip_Resolution_Title;
                            control.AddButton(new Image() { Source = new BitmapImage(new Uri("Plus.png", UriKind.Relative)) }, Properties.Resources.QuickStrip_Resolution_Bigger_HelpTitle, Properties.Resources.QuickStrip_Resolution_Bigger_HelpMessage, isPrimary: true);
                            control.AddButton(new Image() { Source = new BitmapImage(new Uri("Minus.png", UriKind.Relative)) }, Properties.Resources.QuickStrip_Resolution_Bigger_HelpTitle, Properties.Resources.QuickStrip_Resolution_Bigger_HelpMessage, isPrimary: false);
                            control.Action += Zoom;
                            return control;
                        }
                    case "magnifier":
                        {
                            var control = new QuickStripSegmentedButtonControl();
                            control.TitleLabel.Content = "Magnifier";
                            control.AddButton("On", "Show a Magnifying Glass", "Magnify a part of the screen as you move around", isPrimary: true);
                            control.AddButton("Off", "Hide the Magnifying Glass", "Stop magnifying part of the screen as you move", isPrimary: false);
                            control.Action += OnMagnify;
                            return control;
                        }
                    case "reader":
                        {
                            var control = new QuickStripSegmentedButtonControl();
                            control.TitleLabel.Content = "Screen Reader";
                            control.AddButton("On", "Turn On Screen Reader", "Have the computer read text aloud", isPrimary: true);
                            control.AddButton("Off", "Turn Off Screen Reader", "Stop the computer from reading text aloud", isPrimary: false);
                            return control;
                        }
                    case "volume":
                        {
                            var control = new QuickStripSegmentedButtonControl();
                            control.TitleLabel.Content = "Volume";
                            control.AddButton(new Image() { Source = new BitmapImage(new Uri("Plus.png", UriKind.Relative)) }, "Turn the Volume Up", "Make all the sounds louder", isPrimary: true);
                            control.AddButton(new Image() { Source = new BitmapImage(new Uri("Minus.png", UriKind.Relative)) }, "Turn the Volume Down", "Make all the sounds quieter", isPrimary: false);
                            control.AddButton("Mute", "Mute All Sounds", "Turn off all sounds from the computer", isPrimary: true);
                            return control;
                        }
                    case "contrast":
                        {
                            var control = new QuickStripSegmentedButtonControl();
                            control.TitleLabel.Content = "High Contrast";
                            control.AddButton("On", "Turn On High Contrast", "Make it easier to distinguish items", isPrimary: true);
                            control.AddButton("Off", "Turn Off High Contrast", "Make it harder to distinguish items", isPrimary: false);
                            return control;
                        }
                    default:
                        return null;
                }
            }

            private void Zoom(object sender, QuickStripSegmentedButtonControl.ActionEventArgs e)
            {
                if (e.SelectedIndex == 0)
                {
                    // Zoom in
                }
                else
                {
                    // Zoom out
                }
            }

            private void OnMagnify(object sender, QuickStripSegmentedButtonControl.ActionEventArgs e)
            {
                if (e.SelectedIndex == 0)
                {
                    // Show magnifier
                }
                else if (e.SelectedIndex == 1)
                {
                    // Hide magnifier
                }
            }
        }

        private List<Item> items = new List<Item>();
        public List<Item> Items
        {
            get
            {
                return items;
            }
            set
            {
                items = value;
                UpdateControls();
            }
        }

        #endregion

        #region Item Controls

        private List<QuickStripItemControl> itemControls = new List<QuickStripItemControl>();

        private void UpdateControls()
        {
            ItemGrid.Children.RemoveRange(0, ItemGrid.Children.Count);
            itemControls.Clear();
            foreach (var item in items)
            {
                if (item.GetControl() is QuickStripItemControl control)
                {
                    itemControls.Add(control);
                    ItemGrid.Children.Add(control);
                }
            }
        }

        #endregion

        #region Layout & Position

        /// <summary>
        /// Amount the Quick Strip should be inset from the edge of each screen
        /// </summary>
        public double ScreenEdgeInset = 4;

        /// <summary>
        /// The possible positions for the quick strip 
        /// </summary>
        public enum FixedPosition
        {
            BottomRight,
            BottomLeft,
            TopLeft,
            TopRight
        }

        /// <summary>
        /// The preferred position of the quick strip
        /// </summary>
        private FixedPosition position = QuickStrip.FixedPosition.BottomRight;

        /// <summary>
        /// The preferred position of the quick strip
        /// </summary>
        public FixedPosition Position
        {
            get
            {
                return position;
            }
            set
            {
                position = value;
                Reposition(animated: true);
            }
        }

        /// <summary>
        /// Reposition the Quick Strip to its current fixed position
        /// </summary>
        /// <param name="animated"></param>
        private void Reposition(bool animated)
        {
            var screenSize = SystemParameters.WorkArea;
            double top = Top;
            double left = Left;
            switch (position)
            {
                case FixedPosition.BottomRight:
                    top = screenSize.Height - Height - ScreenEdgeInset;
                    left = screenSize.Width - Width - ScreenEdgeInset;
                    break;
                case FixedPosition.BottomLeft:
                    top = screenSize.Height - Height - ScreenEdgeInset;
                    left = ScreenEdgeInset;
                    break;
                case FixedPosition.TopLeft:
                    top = ScreenEdgeInset;
                    left = ScreenEdgeInset;
                    break;
                case FixedPosition.TopRight:
                    top = ScreenEdgeInset;
                    left = screenSize.Width - Width - ScreenEdgeInset;
                    break;
            }
            if (animated)
            {
                var duration = new Duration(TimeSpan.FromSeconds(0.5));
                var topAnimation = new DoubleAnimation();
                topAnimation.From = Top;
                topAnimation.To = top;
                topAnimation.Duration = duration;
                topAnimation.FillBehavior = FillBehavior.Stop;

                var leftAnimation = new DoubleAnimation();
                leftAnimation.From = Left;
                leftAnimation.To = left;
                leftAnimation.Duration = duration;
                leftAnimation.FillBehavior = FillBehavior.Stop;

                BeginAnimation(Window.TopProperty, topAnimation);
                BeginAnimation(Window.LeftProperty, leftAnimation);
            }
            else
            {
                Top = top;
                Left = left;
            }
        }

        /// <summary>
        /// Get the nearest fixed position to the window's current position
        /// </summary>
        /// <remarks>
        /// Helpful for snapping into the correct place after the user moves the window
        /// </remarks>
        private FixedPosition NearestPosition
        {
            get
            {
                var screenSize = SystemParameters.WorkArea;
                if (Left < screenSize.Width / 2){
                    if (Top < screenSize.Height / 2)
                    {
                        return FixedPosition.TopLeft;
                    }
                    return FixedPosition.BottomLeft;
                }
                else
                {
                    if (Top < screenSize.Height / 2)
                    {
                        return FixedPosition.TopRight;
                    }
                    return FixedPosition.BottomRight;
                }
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Event handler for mouse down to move the window
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                DragMove();
            }
        }

        /// <summary>
        /// Event handler for mouse up to snap into place after moving the window
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                Position = NearestPosition;
            }
        }

        #endregion
    }
}
