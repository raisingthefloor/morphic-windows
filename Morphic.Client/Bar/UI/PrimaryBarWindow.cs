namespace Morphic.Client.Bar.UI
{
    using System;
    using System.Linq;
    using System.Windows;
    using System.Windows.Interop;
    using System.Windows.Media.Animation;
    using Windows.Native;
    using AppBarWindow;
    using Data;

    public sealed class PrimaryBarWindow : BarWindow
    {
        private SecondaryBarWindow? secondaryWindow;
        private ExpanderWindow? expanderWindow;
        private Edge snapX;
        private Edge snapY;

        public override BarWindow? OtherWindow => this.secondaryWindow;

        public event EventHandler? ExpandedChange;

        public override ExpanderWindow? ExpanderWindow => this.expanderWindow;

        public override bool IsExpanded
        {
            get => base.IsExpanded;
            set
            {
                base.IsExpanded = value;
                this.OnExpandedChange();
            }
        }

        public override double Scale
        {
            get => base.Scale;
            set
            {
                base.Scale = value;
                // Apply the same scale to the secondary bar.
                if (this.secondaryWindow != null)
                {
                    this.secondaryWindow.Scale = value;
                }
            }
        }

        public PrimaryBarWindow(BarData barData) : base(barData)
        {
#if TESTING
            // Accept bar files to be dropped.
            this.AllowDrop = true;
            this.Drop += (sender, args) =>
            {
                if (args.Data.GetDataPresent(DataFormats.FileDrop) &&
                    args.Data.GetData(DataFormats.FileDrop) is string[] files)
                {
                    string file = files.FirstOrDefault() ?? this.Bar.Source;
                    this.Bar = BarData.Load(file)!;
                }
            };
#endif
            this.Closed += this.OnClosed;
            this.Bar = barData;
            this.Scale = 1;

            this.SourceInitialized += (sender, args) =>
            {
                // Start monitoring the active window.
                WindowInteropHelper nativeWindow = new WindowInteropHelper(this);
                HwndSource? hwndSource = HwndSource.FromHwnd(nativeWindow.Handle);
                SelectionReader.Default.Initialise(nativeWindow.Handle);
                hwndSource?.AddHook(SelectionReader.Default.WindowProc);
            };

            this.WindowMovement.MoveComplete += this.WindowMoved;
        }

        private void WindowMoved(object? sender, EventArgs e)
        {
            this.CorrectPosition(true);
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            this.IsClosing = true;
            this.expanderWindow?.Close();
            this.secondaryWindow?.Close();
        }

        public bool IsClosing { get; set; }

        protected override void OnBarLoaded()
        {
            base.OnBarLoaded();
            this.LoadSecondaryBar();
        }

        /// <summary>
        /// Loads the secondary, if required.
        /// </summary>
        private void LoadSecondaryBar()
        {
            if (this.secondaryWindow == null && this.Bar.SecondaryItems.Any())
            {
                this.secondaryWindow = new SecondaryBarWindow(this, this.Bar);
                this.expanderWindow = new ExpanderWindow(this, this.secondaryWindow);

                this.secondaryWindow.Loaded += (s, a) => this.expanderWindow.Show();
                this.expanderWindow.Changed += (s, a) => this.IsExpanded = this.expanderWindow.IsExpanded;

                this.secondaryWindow.Show();
            }
        }

        protected override void SetInitialPosition()
        {
            base.SetInitialPosition();
            Size size = new Size(this.Width, this.Height);
            //size = this.Rescale(size, true);

            if (this.Bar.Position.DockEdge == Edge.None)
            {
                Rect workArea = SystemParameters.WorkArea;
                Point pos = this.Bar.Position.Primary.GetPosition(workArea, size);
                this.Left = pos.X;
                this.Top = pos.Y;
            }
            else
            {
                this.AppBar.ApplyAppBar(this.Bar.Position.DockEdge);
            }

            this.CorrectPosition();
        }

        /// <summary>
        /// Ensure the window is in a good position after it was moved, or the screen size changes.
        /// </summary>
        private void CorrectPosition(bool userMoved = false)
        {
            if (this.DockedEdge != Edge.None)
            {
                // do nothing.
                return;
            }

            if (this.Bar.Position.Restricted)
            {
                this.MoveToCorner(userMoved);
            }
            else if (!userMoved)
            {
                // Move the window back to the edges, if any, it was snapped on.
                Rect workArea = SystemParameters.WorkArea;
                double left = this.snapX switch
                {
                    Edge.Left => workArea.Left,
                    Edge.Right => workArea.Right - this.Width,
                    _ => this.Left
                };

                double top = this.snapY switch
                {
                    Edge.Top => workArea.Top,
                    Edge.Bottom => workArea.Bottom - this.Height,
                    _ => this.Top
                };

                // Make sure it's within the work area
                this.Left = workArea.Width > this.Width
                    ? Math.Clamp(left, workArea.Left, workArea.Right - this.Width)
                    : workArea.Left;
                this.Top = workArea.Height > this.Height
                    ? Math.Clamp(top, workArea.Top, workArea.Bottom - this.Height)
                    : workArea.Top;
            }
        }

        protected override void SystemEventsOnDisplaySettingsChanged(object? sender, EventArgs e)
        {
            base.SystemEventsOnDisplaySettingsChanged(sender, e);
            this.CorrectPosition();
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);

            // See if the position is snapped to an edge, so it can stick to it if the screen size changes.
            Rect workArea = SystemParameters.WorkArea;
            if (Math.Abs(this.Left - workArea.Left) < 3)
            {
                this.snapX = Edge.Left;
            }
            else if (Math.Abs(this.Left + this.Width - workArea.Right) < 3)
            {
                this.snapX = Edge.Right;
            }
            else
            {
                this.snapX = Edge.None;
            }

            if (Math.Abs(this.Top - workArea.Top) < 3)
            {
                this.snapY = Edge.Top;
            }
            else if (Math.Abs(this.Top + this.Height - workArea.Bottom) < 3)
            {
                this.snapY = Edge.Bottom;
            }
            else
            {
                this.snapY = Edge.None;
            }
        }

        /// <summary>Moves the window to the nearest corner.</summary>
        private void MoveToCorner(bool animate = false)
        {
            Rect workArea = SystemParameters.WorkArea;
            workArea.Inflate(-4, -4);

            Point newPos;
            newPos.X = this.Left + this.Width / 2 < workArea.Left + workArea.Width / 2
                ? workArea.Left
                : workArea.Right - this.Width;
            newPos.Y = this.Top + this.Height / 2 < workArea.Top + workArea.Height / 2
                ? workArea.Top
                : workArea.Bottom - this.Height;

            newPos.X = Math.Max(newPos.X, workArea.Left);

            if (animate)
            {
                Duration duration = new Duration(TimeSpan.FromMilliseconds(500));
                DoubleAnimation xAnim = new DoubleAnimation(this.Left, newPos.X, duration, FillBehavior.Stop) {
                    Name = "Left"
                };
                DoubleAnimation yAnim = new DoubleAnimation(this.Top, newPos.Y, duration, FillBehavior.Stop) {
                    Name = "Top"
                };

                xAnim.Completed += this.WindowAnimComplete;
                yAnim.Completed += this.WindowAnimComplete;

                this.BeginAnimation(Window.LeftProperty, xAnim);
                this.BeginAnimation(Window.TopProperty, yAnim);
            }
            else
            {
                this.Left = newPos.X;
                this.Top = newPos.Y;
            }
        }

        private void WindowAnimComplete(object sender, EventArgs e)
        {
            if (sender is AnimationClock clock && clock.Timeline is DoubleAnimation anim && anim.To.HasValue)
            {
                if (anim.Name == "Left")
                {
                    this.Left = anim.To.Value;
                }
                else if (anim.Name == "Top")
                {
                    this.Top = anim.To.Value;
                }
            }
        }

        private void OnExpandedChange()
        {
            if (this.IsExpanded)
            {
                this.secondaryWindow?.Show();
                this.secondaryWindow?.Activate();
            }
            else
            {
                this.Activate();
                this.secondaryWindow?.Hide();
            }
            
            this.ExpandedChange?.Invoke(this, EventArgs.Empty);
        }
    }
}
