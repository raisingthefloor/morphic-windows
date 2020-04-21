// Copyright 2020 Raising the Floor - International
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

namespace MorphicWin
{
    /// <summary>
    /// Interaction logic for QuickStrip.xaml
    /// </summary>
    public partial class QuickStrip : Window
    {
        public QuickStrip(Session session)
        {
            this.session = session;
            InitializeComponent();
            Deactivated += OnDeactivated;
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            Reposition(animated: false);
        }

        private readonly Session session;

        private void OnDeactivated(object? sender, EventArgs e)
        {
        }

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
    }
}
