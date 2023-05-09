// BarWindow.xaml.cs: The bar window.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

namespace Morphic.Client.Bar.UI
{
    using AppBarWindow;
    using BarControls;
    using Data;
    using Microsoft.Win32;
    using Mouse = Morphic.WindowsNative.Input.Mouse;
    using Settings.SettingsHandlers;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Shell;

    /// <summary>
    /// The window for the main bar.
    /// </summary>
    public partial class BarWindow : Window, INotifyPropertyChanged, IAppBarWindow
    {
        protected internal readonly AppBar AppBar;

        private Thickness? initialResizeBorder;

        protected internal WindowMovement WindowMovement;
        private double scale;

        public virtual BarWindow? OtherWindow => null;

        public virtual bool IsExpanded { get; set; }
        public virtual ExpanderWindow? ExpanderWindow => null;

        private Orientation orientationValue;
        public static readonly DependencyProperty IsDockedProperty = DependencyProperty.Register("IsDocked", typeof(bool), typeof(BarWindow), new PropertyMetadata(default(bool)));

        protected WindowMessageHook messageHook;

        private bool _showCloseButton = true;

        public bool ShowCloseButton
        {
            get => this is PrimaryBarWindow && _showCloseButton;
            set
            {
                _showCloseButton = value;
                OnPropertyChanged(nameof(ShowCloseButton));
                OnPropertyChanged(nameof(HeaderRowHeight));
                OnPropertyChanged(nameof(CloseButtonColumnWidth));
            }
        }

        public double HeaderRowHeight => ShowCloseButton && Orientation == Orientation.Vertical ? 20 : 0;
        public double CloseButtonColumnWidth => ShowCloseButton && Orientation == Orientation.Horizontal ? 20 : 0;

        public Orientation Orientation
        {
            get => this.orientationValue;
            set
            {
                this.orientationValue = value;
                this.OrientationChanged?.Invoke(this, EventArgs.Empty);
                OnPropertyChanged(nameof(Orientation));
                OnPropertyChanged(nameof(HeaderRowHeight));
                OnPropertyChanged(nameof(CloseButtonColumnWidth));
            }
        }

        public event EventHandler? OrientationChanged;


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
            this.QuickHelpWindow = QuickHelpWindow.AddBar(this);
            bool isPrimary = this is PrimaryBarWindow;
            this.Bar = barData;

            this.UpdateBarItems();

            this.DataContext = this;

            this.WindowMovement = new WindowMovement(this);
            this.AppBar = new AppBar(this, this.WindowMovement)
            {
                EnableDocking = isPrimary && this.Bar.Position.AllowDocking
            };

            // Move it off the screen until it's loaded.
            this.Left = -0xffff;

            this.InitializeComponent();

            this.OrientationChanged += this.OnOrientationChanged;
            this.Orientation = this.GetOrientation(this.Bar.Position.DockEdge);

            this.BarControl.Bar = this.Bar;
            this.BarControl.IsPrimary = this is PrimaryBarWindow;
            this.BarControl.EndTab += (sender, args) =>
            {
                if (this.OtherWindow is SecondaryBarWindow)
                {
                    this.IsExpanded = true;
                }
                this.OtherWindow?.Activate();
            };

            this.SizeChanged += this.OnSizeChanged;
            //this.Loaded += (sender, args) => this.OnBarLoaded();

            AppFocus.Current.AddMouseOverWindow(this);

            this.AppBar.EdgeChanged += this.AppBarOnEdgeChanged;

            SystemEvents.DisplaySettingsChanged += this.SystemEventsOnDisplaySettingsChanged;
            this.Closed += (sender, args) => SystemEvents.DisplaySettingsChanged -= this.SystemEventsOnDisplaySettingsChanged;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.Rescale(e.NewSize);
        }

        public QuickHelpWindow QuickHelpWindow { get; set; }

        private void OnOrientationChanged(object? sender, EventArgs e)
        {
            this.BarControl.Orientation = this.Orientation;
            if (this.Orientation == Orientation.Horizontal)
            {
                this.BarControl.ItemWidth = double.NaN;
            }
            else
            {
                this.BarControl.ItemWidth = this.Bar.Sizes.ItemWidth;
            }

            foreach (BarItemControl itemControl in this.BarControl.ItemControls)
            {
                itemControl.Orientation = this.Orientation;
            }

            if (this is PrimaryBarWindow)
            {
                this.Bar.AllItems.ForEach(item => item.Overflow = false);
                this.UpdateBarItems();
            }
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            this.OnBarLoaded();
        }

        protected void SystemEventsOnSettingsChanged(object? sender, EventArgs e)
        {
            SettingsHandler.SystemSettingChanged();
        }

        protected virtual void SystemEventsOnDisplaySettingsChanged(object? sender, EventArgs e)
        {
            this.SystemEventsOnSettingsChanged(sender, e);

            // If the screen resolution has changed due to a button click, put the mouse back over that button.
            if (MultiButtonBarControl.LastClickedTime.HasValue
                && DateTime.Now - MultiButtonBarControl.LastClickedTime.Value < TimeSpan.FromSeconds(10))
            {
                Control? button = MultiButtonBarControl.LastClickedControl;
                if (button is not null)
                {
                    try
                    {
                        Point pos = button.PointToScreen(new Point(button.RenderSize.Width / 2,
                        button.RenderSize.Height / 2));

                        Mouse.SetPosition((int)pos.X, (int)pos.Y);
                    }
                    catch (InvalidOperationException)
                    {
                        // if the button is not connected to a presentation source, we will get this exception; 
                        // swallow it and fail gracefully (as this is not a critical action)
                    }
                }
            }
        }

        /// <summary>
        /// The bar for which this bar window is displaying.
        /// </summary>
        public BarData Bar { get; set; }

        private List<BarItem> barItems = null!;
        public List<BarItem> BarItems
        {
            get => this.barItems;
            set
            {
                this.barItems = value;
                if (this.BarControl is not null)
                {
                    this.BarControl.ItemsSource = this.barItems;
                }
            }
        }

        public Edge DockedEdge => this.AppBar.AppBarEdge;

        /// <summary>Additional width added to the window.</summary>
        public double ExtraWidth =>
            this.BorderThickness.Left + this.BorderThickness.Right +
            this.Padding.Left + this.Padding.Right +
            this.BarControl.Margin.Left + this.BarControl.Margin.Right +
            CloseButtonColumnWidth;

        /// <summary>Additional height added to the window.</summary>
        public double ExtraHeight =>
            this.BorderThickness.Top + this.BorderThickness.Bottom +
            this.Padding.Top + this.Padding.Bottom +
            this.BarControl.Margin.Top + this.BarControl.Margin.Bottom + 1
            + HeaderRowHeight;

        public bool IsDocked
        {
            get => (bool)this.GetValue(IsDockedProperty);
            protected set => this.SetValue(IsDockedProperty, value);
        }

        protected virtual void OnBarLoaded()
        {
            this.SetBorder();
            this.SetBorder();
            this.SetInitialPosition();
            this.SetInitialPosition();

            this.BarLoaded?.Invoke(this, new EventArgs());

            this.Opacity = 1;
        }

        private void BarControl_Loaded(object sender, RoutedEventArgs e)
        {
        }

        /// <summary>Reload the bar items.</summary>
        public void UpdateBarItems()
        {
            IEnumerable<BarItem> items = (this is PrimaryBarWindow) ? this.Bar.PrimaryItems : this.Bar.SecondaryItems;
            this.BarItems = items.ToList();
            if (this is PrimaryBarWindow)
            {
                if (this.BarControl is not null)
                {
                    foreach (BarItemControl control in this.BarControl.ItemControls)
                    {
                        control.Visibility = control.BarItem.Overflow ? Visibility.Collapsed : Visibility.Visible;
                    }
                }

                this.OtherWindow?.UpdateBarItems();
            }
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
            this.SetMaxSize(this.DockedEdge);
        }

        /// <summary>
        /// Gets the initial orientation of the bar.
        /// </summary>
        /// <param name="appBarEdge"></param>
        /// <returns></returns>
        protected internal Orientation GetOrientation(Edge? appBarEdge = null)
        {
            Orientation orientation;

            appBarEdge ??= this.AppBar.AppBarEdge;

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
            else
            {
                // Use the configured direction
                orientation = this.Bar.Position.Orientation;
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
            this.Orientation = this.GetOrientation(e.Edge);
            this.SetMaxSize(e.Edge);
            this.SetBorder();
            this.IsDocked = e.Edge != Edge.None;
        }

        private void SetMaxSize(Edge edge)
        {
            if (edge == Edge.None)
            {
                Rect workArea = this.GetWorkArea();
                this.MaxWidth = workArea.Width;
                this.MaxHeight = workArea.Height;
                this.BarControl.MaxWidth = Math.Max(0, this.MaxWidth - this.ExtraWidth);
                this.BarControl.MaxHeight = Math.Max(0, this.MaxHeight - this.ExtraHeight);
            }
            else
            {
                this.MaxWidth = double.PositiveInfinity;
                this.MaxHeight = double.PositiveInfinity;
            }
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
                if (this.BarControl.NoWrap)
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
                // Hide the last item
                BarItem? last = this.Bar.PrimaryItems.LastOrDefault(item => !item.NoOverflow);
                if (last is not null)
                {
                    last.Overflow = true;
                    this.UpdateBarItems();
                }
            } 
            else if (this.Bar.Overflow == BarOverflow.Secondary && this is SecondaryBarWindow secondaryBarWindow) 
            {
                System.Diagnostics.Debug.Assert(false, "Unexpected condition; this issue must be resolved; returning false to avoid infinite loop");
                return false;
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

        public BarItem? GetBarItemFromElement(FrameworkElement element)
        {
            BarItemControl? control = element.FindVisualParent<BarItemControl>();
            return control?.BarItem;
        }

        private async void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                App.Current.BarManager.HideBar();
            } 
            finally 
            {
                await App.Current.Telemetry_RecordEventAsync("morphicBarHide");
            }
        }
    }
}