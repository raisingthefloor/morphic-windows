// ExpanderWindow.xaml.cs: Displays the expander button.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

namespace Morphic.Bar.UI
{
    using System.Windows;
    using System;
    using System.Threading.Tasks;
    using System.Windows.Automation;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media.Animation;
    using AppBarWindow;
    using Bar;

    /// <summary>
    /// A window containing just the expander button, which is on the edge of either the primary or secondary bar.
    /// </summary>
    public partial class ExpanderWindow : Window
    {
        private readonly PrimaryBarWindow primaryBarWindow = null!;
        private readonly SecondaryBarWindow secondaryBarWindow = null!;
        private bool settingPosition;

        public BarData Bar => this.primaryBarWindow.Bar;
        public double Scale => this.Bar.Scale * 2;

        /// <summary>
        /// Raised when the expander control's expanded value has changed.
        /// </summary>
        public event EventHandler? Changed;

        private bool wanted;
        
        /// <summary>
        /// true if the visibility of the expander is required.
        /// </summary>
        public bool IsWanted
        {
            get => (this.wanted || !this.primaryBarWindow.Bar.SecondaryBar.AutoHideExpander) && this.primaryBarWindow.IsVisible;
            set
            {
                if (this.wanted != value)
                {
                    this.wanted = value;
                    DoubleAnimation anim =
                        new DoubleAnimation(this.IsWanted ? 1 : 0, TimeSpan.FromMilliseconds(value ? 200 : 1000),
                            FillBehavior.HoldEnd);
                    this.BeginAnimation(OpacityProperty, anim, HandoffBehavior.SnapshotAndReplace);
                }
            }
        }

        public ExpanderWindow()
        {
            this.DataContext = this;
        }

        public ExpanderWindow(PrimaryBarWindow primaryBarWindow, SecondaryBarWindow secondaryBarWindow)
            : this()
        {
            this.primaryBarWindow = primaryBarWindow;
            this.secondaryBarWindow = secondaryBarWindow;

            this.InitializeComponent();
            
            this.Loaded += this.OnLoaded;

            // track whether or not the expander needs to be shown.
            App.Current.AddMouseOverWindow(this);
            App.Current.MouseEnter += (sender, args) => this.IsWanted = true;
            App.Current.MouseLeave += (sender, args) => this.IsWanted = App.Current.IsActive || this.IsExpanded;
            App.Current.Activated += (sender, args) => this.IsWanted = true;
            App.Current.Deactivated += (sender, args) => this.IsWanted = this.IsExpanded;

            this.Topmost = this.primaryBarWindow.Topmost;

            int focusTime = 0;
            this.Activated += (sender, args) => focusTime = Environment.TickCount;
            // Press tab to move the focus to a bar window.
            this.PreviewKeyDown += (sender, args) =>
            {
                // If focus arrives at this window via tab key, then this event gets raised
                if (args.Key == Key.Tab && Environment.TickCount - focusTime > 100)
                {
                    bool toSecondary =
                        this.IsExpanded && (Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.Shift;

                    BarWindow window = toSecondary ? (BarWindow)this.secondaryBarWindow : this.primaryBarWindow;
                    window.MakeActive();
                }
            };
        }

        private void OnLoaded(object sender, RoutedEventArgs args)
        {
            // The events which cause this window to be repositioned.
            this.primaryBarWindow.LocationChanged += this.OnRepositionRequired;
            this.primaryBarWindow.SizeChanged += this.OnRepositionRequired;
            this.secondaryBarWindow.LocationChanged += this.OnRepositionRequired;
            this.secondaryBarWindow.SizeChanged += this.OnRepositionRequired;
            this.LocationChanged += this.OnRepositionRequired;

            this.primaryBarWindow.ExpandedChange += this.UpdateExpanded;

            this.primaryBarWindow.IsVisibleChanged += (o, eventArgs) =>
            {
                if (eventArgs.NewValue is bool visible)
                {
                    this.Visibility = (visible && this.IsWanted) ? Visibility.Visible : Visibility.Collapsed;
                }
            };

            this.UpdateExpanded(sender, args);
        }

        public bool IsExpanded => this.Expander.IsExpanded;

        /// <summary>
        /// Called when the expanded state has changed, and things need to be updated.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UpdateExpanded(object? sender, EventArgs e)
        {
            BarWindow owner = this.primaryBarWindow.IsExpanded
                ? (BarWindow)this.secondaryBarWindow
                : this.primaryBarWindow;
                
            if (this.IsLoaded && owner.IsLoaded && !this.primaryBarWindow.IsClosing && this.primaryBarWindow.Visibility == Visibility.Visible)
            {
                this.Hide();
                this.Expander.IsExpanded = this.primaryBarWindow.IsExpanded;
                this.Owner = owner;
                this.SetPosition();
                this.Show();

                this.Expander.SetValue(AutomationProperties.NameProperty,
                    this.Expander.IsExpanded ? "Close Secondary Bar" : "Open Secondary Bar");
            }
        }

        /// <summary>
        /// Called when the window needs to be repositioned.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnRepositionRequired(object? sender, EventArgs e)
        {
            this.SetPosition();
        }

        /// <summary>
        /// Set the position of the window. At an edge of either the primary or secondary bar window.
        /// </summary>
        private void SetPosition()
        {
            if (this.settingPosition)
            {
                return;
            }
            this.settingPosition = true;
            
            Rect rc = new Rect(0, 0, this.Width, this.Height);

            BarPosition position = this.primaryBarWindow.Bar.Position;

            Edge edge = this.secondaryBarWindow.AttachedEdge;
            
            Rect primary = this.primaryBarWindow.GetRect();
            Rect secondary = this.secondaryBarWindow.GetRect();
            Rect relativeWindow;
            // Which window to use for the relative position.
            if (position.ExpanderRelative == ExpanderRelative.Primary
                || position.ExpanderRelative == ExpanderRelative.Both && !this.IsExpanded)
            {
                relativeWindow = primary;
            }
            else
            {
                relativeWindow = secondary;
            }

            Rect ownerRect = this.IsExpanded ? secondary : primary;

            // Make it centered over the edge.
            Vector offset = new Vector(0, 0);
             
            if (edge.IsVertical())
            {
                offset.X -= this.Width / 2;
                rc.X = (edge == Edge.Left ? ownerRect.Left : ownerRect.Right) + offset.X;
                rc.Y = position.Expander.Y.GetAbsolute(relativeWindow.Top, relativeWindow.Bottom, this.Height);
            }
            else
            {
                offset.Y -= this.Height / 2;
                rc.X = position.Expander.X.GetAbsolute(relativeWindow.Left, relativeWindow.Right, this.Width);
                rc.Y = (edge == Edge.Top ? ownerRect.Top : ownerRect.Bottom) + offset.Y;
            }

            // Make sure it's on the window.
            rc.X = Math.Clamp(rc.X, ownerRect.Left + offset.X, ownerRect.Right + offset.X);
            rc.Y = Math.Clamp(rc.Y, ownerRect.Top + offset.Y, ownerRect.Bottom + offset.Y);
            
            this.Left = rc.X;
            this.Top = rc.Y;
            
            this.Expander.ExpandDirection = edge switch
            {
                Edge.Left => ExpandDirection.Left,
                Edge.Top => ExpandDirection.Up,
                Edge.Right => ExpandDirection.Right,
                Edge.Bottom => ExpandDirection.Down,
                _ => ExpandDirection.Right
            };

            this.settingPosition = false;
        }

        private void Expander_OnExpanded(object sender, RoutedEventArgs e)
        {
            this.Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}