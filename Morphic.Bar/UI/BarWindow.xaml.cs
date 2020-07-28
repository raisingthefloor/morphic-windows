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
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using System.Windows.Media;
    using System.Windows.Shell;
    using AppBar;
    using Config;

    /// <summary>
    /// The window for the main bar.
    /// </summary>
    public partial class BarWindow : Window, INotifyPropertyChanged
    {
        private readonly AppBar.AppBar appBar;
        private BarData bar;

        private string barFile = string.Empty;
        private Thickness? initialResizeBorder;

        public BarWindow() : this(null, false)
        {
        }

        public BarWindow(BarData? barData, bool isPullout)
        {
            this.IsPullout = isPullout;
            this.appBar = new AppBar.AppBar(this)
            {
                EnableDocking = !this.IsPullout
            };

            this.bar = barData ?? new BarData();
            this.DataContext = this;
            
            // Move it off the screen until it's loaded.
            this.Left = -0xffff;
            
            this.InitializeComponent();

            // Accept bar files to be dropped.
            this.AllowDrop = true;
            this.Drop += (sender, args) =>
            {
                if (args.Data.GetDataPresent(DataFormats.FileDrop) &&
                    args.Data.GetData(DataFormats.FileDrop) is string[] files)
                {
                    string file = files.FirstOrDefault();
                    this.SetBarSource(file);
                }
            };

            // Tell the app bar to ask the bar control for a good size.
            this.appBar.GetHeightFromWidth = (width)
                => this.BarControl.GetHeightFromWidth(width - this.ExtraWidth) + this.ExtraHeight;
            this.appBar.GetWidthFromHeight = (height)
                => this.BarControl.GetWidthFromHeight(height - this.ExtraHeight) + this.ExtraWidth;

            this.BarControl.BarLoaded += this.OnBarLoaded;
            this.appBar.EdgeChanged += this.AppBarOnEdgeChanged;

            if (barData != null)
            {
                this.Bar = barData;
            }

            this.Loaded += (sender, args) =>
                this.SetBarSource(Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    "test-bar.json5"));
        }

        public bool IsPullout { get; }

        public BarData Bar
        {
            get => this.bar;
            private set
            {
                this.bar = value;
                this.OnBarChanged();
            }
        }

        /// <summary>Additional width added to the window.</summary>
        public double ExtraWidth =>
            this.BorderThickness.Left + this.BorderThickness.Right +
            this.Padding.Left + this.Padding.Right;

        /// <summary>Additional height added to the window.</summary>
        public double ExtraHeight =>
            this.BorderThickness.Top + this.BorderThickness.Bottom +
            this.Padding.Top + this.Padding.Bottom;

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnBarLoaded(object? sender, EventArgs e)
        {
            this.SetBorder();

            Orientation orientation = this.GetBestOrientation(this.Bar.Position.DockEdge);

            Size size = this.GetSize(orientation);
            this.Height = size.Height;
            this.Width = size.Width;

            if (!this.IsPullout)
            {
                this.appBar.ApplyAppBar(this.Bar.Position.DockEdge);
                if (this.Bar.Position.DockEdge == Edge.None)
                {
                    Rect workArea = SystemParameters.WorkArea;
                    Point pos = this.Bar.Position.GetPosition(workArea, size);
                    this.Left = pos.X;
                    this.Top = pos.Y;
                }
            }
        }

        /// <summary>
        /// Gets the initial orientation of the bar.
        /// </summary>
        /// <param name="appBarEdge"></param>
        /// <returns></returns>
        private Orientation GetBestOrientation(Edge appBarEdge)
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
                // Guess a direction, if it's touching an edge
                if (this.Bar.Position.X == 0 || (this.Bar.Position.XIsRelative && Math.Abs(this.Bar.Position.X - 1) < 0.1))
                {
                    orientation = Orientation.Vertical;
                }
                else if (this.Bar.Position.Y == 0 || (this.Bar.Position.YIsRelative && Math.Abs(this.Bar.Position.Y - 1) < 0.1))
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
                this.BorderThickness = this.appBar.AdjustThickness(thickness);
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

            chrome.ResizeBorderThickness = this.appBar.AdjustThickness(resize);
        }

        /// <summary>
        /// The bar has changed.
        /// </summary>
        public event EventHandler? BarChanged;

        /// <summary>
        /// Asks the bar control for a good size.
        /// This is the equivalent to setting SizeToContent, but is done manually due to the snapping during resize.
        /// </summary>
        public Size GetSize(Orientation orientation)
        {
            Orientation o = orientation == Orientation.Horizontal ? Orientation.Vertical : Orientation.Horizontal;
            Size size = this.appBar.GetGoodSize(new Size(100, 100), o);
            return size;
        }

        /// <summary>
        /// Set the source of the json data, and loads it.
        /// </summary>
        /// <param name="path"></param>
        private async void SetBarSource(string path)
        {
            try
            {
                this.Bar = BarData.FromFile(path);
                this.barFile = path;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                Console.Error.WriteLine(e.ToString());

                this.BarControl.RemoveItems();
                this.BarControl.AddItem(new BarButton()
                {
                    Theme = new BarItemTheme()
                    {
                        TextColor = Colors.DarkRed,
                        Background = Colors.White
                    },
                    Text = e.Message,
                    ToolTip = e.Message,
                    ToolTipInfo = e.ToString()
                });
            }

            // Monitor the file for changes (not using FileSystemWatcher because it doesn't work on network mounts)
            FileInfo lastInfo = new FileInfo(path);
            while (this.barFile == path)
            {
                await Task.Delay(500);
                FileInfo info = new FileInfo(path);
                bool changed = info.Length != lastInfo.Length ||
                               info.CreationTime != lastInfo.CreationTime ||
                               info.LastWriteTime != lastInfo.LastWriteTime;
                if (changed)
                {
                    this.SetBarSource(path);
                    break;
                }
            }
        }

        private void AppBarOnEdgeChanged(object? sender, EdgeChangedEventArgs e)
        {
            this.SetBorder();
        }

        protected virtual void OnBarChanged()
        {
            this.BarControl.LoadBar(this.Bar);
            this.BarChanged?.Invoke(this, EventArgs.Empty);
            this.OnPropertyChanged(nameof(this.Bar));
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
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