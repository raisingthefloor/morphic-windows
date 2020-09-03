// BarWindow.xaml.cs: The bar window.
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
    using System;
    using System.ComponentModel;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Shell;
    using AppBarWindow;
    using Bar;
    using BarControls;

    /// <summary>
    /// The window for the main bar.
    /// </summary>
    public partial class BarWindow : Window, INotifyPropertyChanged, IAppBarWindow
    {
        protected internal readonly AppBar AppBar;
        private BarData bar;

        private Thickness? initialResizeBorder;

        protected internal WindowMovement WindowMovement;
        private double scale;

        public virtual BarWindow? OtherWindow => null;

        public virtual bool IsExpanded { get; set; }
        public virtual ExpanderWindow? ExpanderWindow => null;

        /// <summary>
        /// The overall scale of the items.
        /// </summary>
        public virtual double Scale
        {
            get => this.scale;
            set
            {
                this.scale = value;
                this.BarControl.Scale = this.Bar.Scale * this.scale;
                this.BarControl.ApplyScale();
            }
        }

        public event EventHandler? BarLoaded;

        public BarWindow() : this(new BarData())
        {
        }

        protected BarWindow(BarData barData)
        {
            this.bar = barData;

            this.DataContext = this;

            bool isPrimary = this is PrimaryBarWindow;
            this.WindowMovement = new WindowMovement(this, isPrimary);
            this.AppBar = new AppBar(this, this.WindowMovement)
            {
                EnableDocking = isPrimary,
                FixedSize = this.bar.Overflow != BarOverflow.Wrap
            };

            // Move it off the screen until it's loaded.
            this.Left = -0xffff;

            this.InitializeComponent();
            App.Current.AddMouseOverWindow(this);

            this.Scale = 1;

            // Set the size and position after the bar is loaded and window is rendered.
            bool rendered = false;
            this.ContentRendered += (sender, args) => rendered = true;
            this.BarControl.BarLoaded += (sender, args) =>
            {
                if (rendered)
                {
                    this.OnBarLoaded();
                }
                else
                {
                    this.ContentRendered += (s, a) => this.OnBarLoaded();
                }

                this.SizeChanged += this.OnSizeChanged;
            };

            this.AppBar.EdgeChanged += this.AppBarOnEdgeChanged;

            this.Loaded += (sender, args) => this.BarControl.LoadBar(this.Bar, isPrimary);

            this.BarControl.EndTab += (sender, args) =>
            {
                if (this.OtherWindow is SecondaryBarWindow)
                {
                    this.IsExpanded = true;
                }
                this.OtherWindow?.Activate();
            };

        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs args)
        {
            //this.BarControl.Orientation = Orientation.Vertical;
        }

        /// <summary>
        /// The bar for which this bar window is displaying.
        /// </summary>
        public virtual BarData Bar
        {
            get => this.bar;
            protected set
            {
                this.bar = value;
                this.OnBarChanged();
            }
        }

        public Edge DockedEdge => this.AppBar.AppBarEdge;

        /// <summary>Additional width added to the window.</summary>
        public double ExtraWidth =>
            this.BorderThickness.Left + this.BorderThickness.Right +
            this.Padding.Left + this.Padding.Right +
            this.BarControl.Margin.Left + this.BarControl.Margin.Right;

        /// <summary>Additional height added to the window.</summary>
        public double ExtraHeight =>
            this.BorderThickness.Top + this.BorderThickness.Bottom +
            this.Padding.Top + this.Padding.Bottom +
            this.BarControl.Margin.Top + this.BarControl.Margin.Bottom + 1;

        protected virtual void OnBarLoaded()
        {
            this.SetBorder();

            this.SetInitialPosition();

            this.BarLoaded?.Invoke(this, new EventArgs());

            this.Opacity = 1;
        }

        /// <summary>
        /// Scales the window to fit into the screen.
        /// </summary>
        /// <param name="size"></param>
        /// <param name="apply"></param>
        /// <returns></returns>
        protected Size Rescale(Size size, bool apply = false)
        {
            Rect workArea = this.GetWorkArea();
            bool retry;
            do
            {
                size = this.BarControl.GetSize(size);
                size.Width += this.ExtraWidth;
                size.Height += this.ExtraHeight;

                retry = false;
                if (size.Height > workArea.Height)
                {
                    switch (this.Bar.Overflow)
                    {
                        case BarOverflow.Resize:
                        case BarOverflow.Secondary:
                            retry = this.ReduceSize();
                            this.BarControl.UpdateLayout();
                            break;
                        case BarOverflow.Scale:
                            this.Scale = workArea.Height / size.Height;
                            this.BarControl.UpdateLayout();
                            size = this.BarControl.GetSize(size);
                            size.Width += this.ExtraWidth;
                            size.Height += this.ExtraHeight;
                            retry = false;
                            break;
                        default:
                            size.Height = workArea.Height;
                            break;
                    }
                }

            } while (retry);


            if (apply)
            {
                this.Height = size.Height;
                this.Width = size.Width;
            }

            return size;
        }

        /// <summary>
        /// Sets the initial position and size.
        /// </summary>
        protected virtual void SetInitialPosition()
        {
        }

        /// <summary>
        /// Gets the initial orientation of the bar.
        /// </summary>
        /// <param name="appBarEdge"></param>
        /// <returns></returns>
        protected internal virtual Orientation GetBestOrientation(Edge appBarEdge)
        {
            Orientation orientation = Orientation.Vertical;

            if (appBarEdge == Edge.Left || appBarEdge == Edge.Right)
            {
                // Always vertical when docked on a side.
                orientation = Orientation.Vertical;
            }
            else if (appBarEdge == Edge.Top || appBarEdge == Edge.Bottom)
            {
                // Always horizontal when docked on top or bottom.
                orientation = Orientation.Horizontal;
            }
            else if (this.Bar.Position.Orientation != null)
            {
                // Use the configured direction
                orientation = (Orientation) this.Bar.Position.Orientation;
            }
            else
            {
                BarPosition.RelativePosition barPosition = this.Bar.Position.Primary;
                // Guess a direction, if it's touching an edge
                if (barPosition.X == 0 ||
                    (barPosition.X.IsRelative && Math.Abs(barPosition.X - 1) < 0.1))
                {
                    orientation = Orientation.Vertical;
                }
                else if (barPosition.Y == 0 ||
                         (barPosition.Y.IsRelative && Math.Abs(barPosition.Y - 1) < 0.1))
                {
                    orientation = Orientation.Horizontal;
                }
            }

            return orientation;
        }

        private void SetBorder()
        {
            // On the edges that touch the screen, replace the border with a padding.
            //using (this.Dispatcher.DisableProcessing())
            {
                Thickness thickness = new Thickness(this.Bar.BarTheme.BorderSize);
                this.BorderThickness = this.AppBar.AdjustThickness(thickness);
            }

            // Remove the resizable area, and window borders, on the sides which are against the screen edges.
            WindowChrome chrome = WindowChrome.GetWindowChrome(this);
            this.initialResizeBorder ??= chrome.ResizeBorderThickness;

            // Make sure the size is not below the system defined width.
            Thickness resize = this.initialResizeBorder.Value;
            resize.Left = Math.Max(resize.Left, this.BorderThickness.Left + 1);
            resize.Top = Math.Max(resize.Top, this.BorderThickness.Top + 1);
            resize.Right = Math.Max(resize.Right, this.BorderThickness.Right + 1);
            resize.Bottom = Math.Max(resize.Bottom, this.BorderThickness.Bottom + 1);

            chrome.ResizeBorderThickness = this.AppBar.AdjustThickness(resize);
        }

        /// <summary>
        /// Asks the bar control for a good size.
        /// This is the equivalent to setting SizeToContent, but is done manually due to the snapping during resize.
        /// </summary>
        public Size GetGoodSize()
        {
            Orientation orientation = this.GetBestOrientation(this.Bar.Position.DockEdge);

            this.BarControl.IsHorizontal = orientation == Orientation.Horizontal;
            Size size = this.AppBar.GetGoodSize(new Size(this.ExtraWidth, this.ExtraHeight), orientation);
            return size;
        }

        public Rect GetRect()
        {
            return new Rect(this.Left, this.Top, this.Width, this.Height);
        }

        /// <summary>
        /// Get the work area of the display this window is on.
        /// </summary>
        /// <returns></returns>
        public Rect GetWorkArea()
        {
            return this.AppBar.FromPixels(this.WindowMovement.GetWorkArea());
        }

        private void AppBarOnEdgeChanged(object? sender, EdgeChangedEventArgs e)
        {
            this.SetBorder();
        }

        protected internal void OnBarChanged()
        {
            this.OnPropertyChanged(nameof(this.Bar));
        }

        public Size GetSize(Size availableSize, Orientation orientation, Rect workArea)
        {
            if (orientation == Orientation.Horizontal)
            {
                availableSize.Width = workArea.Width;
                availableSize.Height -= this.ExtraHeight;
            }
            else
            {
                if (this.BarControl.FixedSize)
                {
                    availableSize.Width -= this.ExtraWidth;
                }
                else
                {
                    availableSize.Width = double.PositiveInfinity;
                }

                availableSize.Height = workArea.Height;
            }

            Size newSize = this.BarControl.GetSize(availableSize, orientation);
            newSize.Width += this.ExtraWidth;
            newSize.Height += this.ExtraHeight;

            return newSize;
        }

        /// <summary>
        /// Attempts to reduce the size of the bar, by lowering the item size of an item.
        /// </summary>
        /// <returns></returns>
        private bool ReduceSize()
        {
            if (this.Bar.Overflow == BarOverflow.Resize)
            {
                BarItemControl largest = this.BarControl.Children.OfType<BarItemControl>()
                    .OrderByDescending(item => item.ItemSize)
                    .ThenByDescending(item => item.DesiredSize.Height)
                    .First();
                if (largest.ItemSize <= BarItemSize.TextOnly)
                {
                    return false;
                }

                largest.MaxItemSize = largest.ItemSize - 1;
            }
            else if (this.Bar.Overflow == BarOverflow.Secondary && this is PrimaryBarWindow primaryBarWindow)
            {
                this.Bar.PrimaryItems.Last(item => !item.NoOverflow).IsPrimary = false;
                this.BarControl.ReloadItems();
                primaryBarWindow.OtherWindow?.BarControl.ReloadItems();
            }

            return true;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null!)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void MakeActive()
        {
            this.Activate();
            this.Focus();
        }

        private void BarWindow_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                // ctrl+tab: move between bars
                case Key.Tab when (e.KeyboardDevice.Modifiers & ModifierKeys.Control) == ModifierKeys.Control:
                    this.OtherWindow?.MakeActive();
                    e.Handled = true;
                    break;
                // Escape: Close secondary (if active)
                case Key.Escape:
                    this.IsExpanded = false;
                    e.Handled = true;

                    goto case Key.Home;

                // Home: Move to first item
                case Key.Home:

                    BarWindow? primary = this is PrimaryBarWindow ? this : this.OtherWindow;
                    primary?.MakeActive();

                    if (Keyboard.FocusedElement is UIElement focused)
                    {
                        focused.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
                    }

                    e.Handled = true;
                    break;
            }
        }
    }
}