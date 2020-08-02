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
    using System.Globalization;
    using System.Runtime.CompilerServices;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using System.Windows.Media;
    using System.Windows.Shell;
    using AppBarWindow;
    using Bar;

    /// <summary>
    /// The window for the main bar.
    /// </summary>
    public partial class BarWindow : Window, INotifyPropertyChanged, IAppBarWindow
    {
        protected internal readonly AppBar AppBar;
        protected BarData bar = null!;

        private Thickness? initialResizeBorder;

        private readonly bool isPrimary;
        protected internal WindowMovement WindowMovement;

        public virtual bool IsExpanded { get; set; }

        public event EventHandler? BarLoaded;

        public BarWindow() : this(new BarData())
        {
        }

        protected BarWindow(BarData barData)
        {
            this.bar = barData;
            this.isPrimary = this is PrimaryBarWindow;

            this.DataContext = this;

            this.WindowMovement = new WindowMovement(this, this.isPrimary);
            this.AppBar = new AppBar(this, this.WindowMovement)
            {
                EnableDocking = this.isPrimary
            };

            // Move it off the screen until it's loaded.
            this.Left = -0xffff;

            this.InitializeComponent();
            App.Current.AddMouseOverWindow(this);

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
            };

            this.AppBar.EdgeChanged += this.AppBarOnEdgeChanged;
            
            this.Loaded += (sender, args) => this.BarControl.LoadBar(this.bar, !this.isPrimary);

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
            this.Padding.Left + this.Padding.Right;

        /// <summary>Additional height added to the window.</summary>
        public double ExtraHeight =>
            this.BorderThickness.Top + this.BorderThickness.Bottom +
            this.Padding.Top + this.Padding.Bottom;

        protected virtual void OnBarLoaded()
        {
            this.SetBorder();

            Size size = this.GetGoodSize();
            this.Height = size.Height;
            this.Width = size.Width;
            this.SetInitialPosition(size);
            
            this.BarLoaded?.Invoke(this, new EventArgs());
        }

        /// <summary>
        /// Sets the initial position and size.
        /// </summary>
        protected virtual void SetInitialPosition(Size size)
        {
        }

        /// <summary>
        /// Gets the initial orientation of the bar.
        /// </summary>
        /// <param name="appBarEdge"></param>
        /// <returns></returns>
        protected internal virtual Orientation GetBestOrientation(Edge appBarEdge)
        {
            Orientation orientation = Orientation.Horizontal;

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
            Size size = this.AppBar.GetGoodSize(new Size(100, 100), orientation);
            return size;
        }

        public Rect GetRect()
        {
            return new Rect(this.Left, this.Top, this.Width, this.Height);
        }

        private void AppBarOnEdgeChanged(object? sender, EdgeChangedEventArgs e)
        {
            this.SetBorder();
        }

        protected internal void OnBarChanged()
        {
            this.OnPropertyChanged(nameof(this.Bar));
        }

        double IAppBarWindow.GetHeightFromWidth(double width)
        {
            return this.BarControl.GetHeightFromWidth(width - this.ExtraWidth) + this.ExtraHeight;
        }

        double IAppBarWindow.GetWidthFromHeight(double height)
        {
            return this.BarControl.GetWidthFromHeight(height - this.ExtraHeight) + this.ExtraWidth;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null!)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Converter which returns a value depending on whether or not the input value is false/null.
    /// </summary>
    public class Ternary : IValueConverter
    {
        /// <summary>
        /// The value to return if the input value is false, null, or empty string.
        /// </summary>
        public string? False { get; set; }

        /// <summary>
        /// The value to return if the input value is not null or false. Omit to return the input value.
        /// </summary>
        public string? True { get; set; }

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || value as bool? == false || value as string == string.Empty)
            {
                return parameter ?? this.False;
            }
            else
            {
                return this.True ?? value;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}