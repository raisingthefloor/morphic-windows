namespace Morphic.Client.Bar.UI
{
    using AppBarWindow;
    using Data;
    using Morphic.WindowsNative;
    using Morphic.WindowsNative.Speech;
    using System;
    using System.Linq;
    using System.Windows;
    using System.Windows.Interop;
    using System.Windows.Media.Animation;

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

        public enum CornerPosition
        {
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight
        }
        private CornerPosition? _cornerPosition = null;

        public override double Scale
        {
            get => base.Scale;
            set
            {
                base.Scale = value;
                // Apply the same scale to the secondary bar.
                if (this.secondaryWindow is not null)
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
            if (this.secondaryWindow is null && this.Bar.SecondaryItems.Any())
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
                Rect workArea = this.GetCorrectedWorkArea();
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
                // if the user moved our bar, then animate
                var animate = userMoved;
                this.MoveToCorner(animate, userMoved);
            }
            else if (!userMoved)
            {
                // Move the window back to the edges, if any, it was snapped on.
                Rect workArea = this.GetCorrectedWorkArea();
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
            this.CorrectPositionAfterDisplaySettingsChange();
        }

        private System.Windows.Threading.DispatcherTimer? _workAreaWatchTimer;
        private void CorrectPositionAfterDisplaySettingsChange()
        {
            // since it can take a few seconds for the screen to settle after a display settings change (including WPF catching up with the actual new 
            // resolution so we have good coordinates), we keep checking for a few seconds to make sure things are stabilized before permanently moving the bar

            // when screen settings change, we snap the bar into place (rather than animate it) to prevent from disconcerting animations and problematic timing issues
            // (i.e. avoid moving and then changing course several times)

            Rect previousWorkArea = this.GetCorrectedWorkArea();

            // if a previous dispatcher timer is already working, stop it
            _workAreaWatchTimer?.Stop();

            // correct our position once
            this.CorrectPosition();

            var watchTimerCountdown = 20; // watch for 10 seconds (20 x 500ms)

            // over a period of a few seconds, keep checking to make sure our position is still correct (by checking to see if the work area has changed)
            _workAreaWatchTimer = new System.Windows.Threading.DispatcherTimer();
            _workAreaWatchTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);
            _workAreaWatchTimer.Tick += delegate (object? sender, EventArgs e)
            {
                watchTimerCountdown--;

                if (watchTimerCountdown == 0)
                {
                    _workAreaWatchTimer.Stop();
                }

                var currentWorkArea = this.GetCorrectedWorkArea();
                if (currentWorkArea != previousWorkArea)
                {
                    // update our previous work area
                    previousWorkArea = currentWorkArea;

                    // if the work area has changed, update our bar's position
                    this.CorrectPosition();
                }
            };
            _workAreaWatchTimer.Start();

        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);

            // See if the position is snapped to an edge, so it can stick to it if the screen size changes.
            Rect workArea = this.GetCorrectedWorkArea();
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

        private double GetWinFormsDisplayScale()
        {
            var graphics = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
            return (double)graphics.DpiX / 96;
        }

        private double? GetWpfDisplayScale()
        {
            // get a reference to our presentation source (to measure our DPI)
            // NOTE: this may cause problems on multi-monitor setups; we will need to revisit this in the future (but since our bar should be on the screen we're measuring...it should work well otherwise)
            var presentationSource = PresentationSource.FromVisual(this);
            if (presentationSource is null)
            {
                // NOTE: presentationSource will usually be null if our window is not yet fully loaded
                return null;
            }

            // get a transformation matrix to transform virtual to physical pixels (i.e. to get our current zoom level)
            var transformationMatrix = presentationSource!.CompositionTarget.TransformToDevice;

            // capture the zoom level (which should be the same in both .M11 and .M22 of the matrix
            // NOTE: M11 should measure the width scaling, whereas M22 should measure the height scaling; we're measuring both out of an abundance of caution
            var horizontalZoomFactor = transformationMatrix.M11;
            var verticalZoomFactor = transformationMatrix.M22;

            return horizontalZoomFactor;
        }

        private Rect GetCorrectedWorkArea()
        {
            Rect? workAreaAsNullable = null;

            var barWindowInteropHelper = new WindowInteropHelper(this);
            var barWindowHandle = barWindowInteropHelper.EnsureHandle();

            // get the display which contains the majority of our window's frame
            var getCurrentDisplayResult = Morphic.WindowsNative.Display.Display.GetDisplayForWindow(barWindowHandle);
            if (getCurrentDisplayResult.IsSuccess == true)
            {
                var currentDisplay = getCurrentDisplayResult.Value!;

                var getWorkAreaRectangleInPixelsResult = currentDisplay.GetWorkAreaRectangleInPixels();
                if (getWorkAreaRectangleInPixelsResult.IsSuccess == true)
                {
                    var physicalWorkArea = getWorkAreaRectangleInPixelsResult.Value!;

                    // NOTE: WPF is sometimes out of sync (longer-term) with the actual system DPI, so we have chosen not to use this (far more accurate)
                    //       method for now; WPF thinks the virtual screen is bigger or smaller than it actually is
                    //// method 1: get monitor scale percentage from Windows API (including reverse-engineered 'dpiOffset')
                    //var actualMonitorScale = Morphic.WindowsNative.Display.Display.GetMonitorScalePercentage(null);
                    //if (actualMonitorScale is not null)
                    //{
                    //    workAreaAsNullable = new Rect(
                    //        (double)physicalWorkArea.Value.X / actualMonitorScale.Value,
                    //        (double)physicalWorkArea.Value.Y / actualMonitorScale.Value,
                    //        (double)physicalWorkArea.Value.Width / actualMonitorScale.Value,
                    //        (double)physicalWorkArea.Value.Height / actualMonitorScale.Value
                    //        );
                    //}

                    // method 2: get monitor scale using WPF primitives (which should match up with what WPF expects for our positioning)
                    var wpfMonitorScale = this.GetWpfDisplayScale();
                    if (wpfMonitorScale is not null)
                    {
                        workAreaAsNullable = new Rect(
                            (double)physicalWorkArea.X / wpfMonitorScale.Value,
                            (double)physicalWorkArea.Y / wpfMonitorScale.Value,
                            (double)physicalWorkArea.Width / wpfMonitorScale.Value,
                            (double)physicalWorkArea.Height / wpfMonitorScale.Value
                            );
                    }

                    // method 3: get monitor scale using WinForms primitives
                    //var winformsMonitorScale = this.GetWinFormsDisplayScale();
                    //workAreaAsNullable = new Rect(
                    //    (double)physicalWorkArea.Value.X / winformsMonitorScale,
                    //    (double)physicalWorkArea.Value.Y / winformsMonitorScale,
                    //    (double)physicalWorkArea.Value.Width / winformsMonitorScale,
                    //    (double)physicalWorkArea.Value.Height / winformsMonitorScale
                    //    );
                }
            }

            if (workAreaAsNullable is null)
            {
                // not ideal, but we can fall back to the SystemParameters' WorkArea property (which is often wrong because of changing resolutions/zoom levels during program runtime)
                var systemParametersWorkArea = SystemParameters.WorkArea;

                var wpfMonitorScale = this.GetWpfDisplayScale();
                if (wpfMonitorScale is not null)
                {
                    workAreaAsNullable = new Rect(
                        (double)systemParametersWorkArea.X / wpfMonitorScale.Value,
                        (double)systemParametersWorkArea.Y / wpfMonitorScale.Value,
                        (double)systemParametersWorkArea.Width / wpfMonitorScale.Value,
                        (double)systemParametersWorkArea.Height / wpfMonitorScale.Value
                    );
                }
                else
                {
                    // NOTE: SystemParameters.WorkArea appears to return physical screen resolution in some or all scenarios, instead of the virtual pixels that WPF uses to align our window; this can result in the MorphicBar going below or to the right of the actual display screen real estate).  This was tested in Win10 1809 x64 in a VM.
                    workAreaAsNullable = systemParametersWorkArea;
                }
            }

            // NOTE: at this point, workAreaAsNullable is not null
            return workAreaAsNullable!.Value;
        }

        /// <summary>Moves the window to the nearest corner.</summary>
        private void MoveToCorner(bool animate, bool userMoved)
        {
            Rect workArea = this.GetCorrectedWorkArea();

            workArea.Inflate(-4, -4);

            CornerPosition targetCornerPosition;
            if (_cornerPosition is null || userMoved == true)
            {
                // if the user moved our bar (or we don't have a corner yet), then determine which corner position we should occupy
                var moveToTopCorner = (this.Top + this.Height / 2 < workArea.Top + workArea.Height / 2);
                var moveToLeftCorner = (this.Left + this.Width / 2 < workArea.Left + workArea.Width / 2);
                if (moveToTopCorner == true && moveToLeftCorner == true)
                {
                    targetCornerPosition = CornerPosition.TopLeft;
                }
                else if (moveToTopCorner == true && moveToLeftCorner == false)
                {
                    targetCornerPosition = CornerPosition.TopRight;
                }
                else if (moveToTopCorner == false && moveToLeftCorner == true)
                {
                    targetCornerPosition = CornerPosition.BottomLeft;
                }
                else /* if (moveToTopCorner == false && moveToLeftCorner == false) */
                {
                    targetCornerPosition = CornerPosition.BottomRight;
                }
            }
            else
            {
                // if the user did not move our bar, do not change our corner position; this is especially important during scale/resolution changes
                targetCornerPosition = _cornerPosition.Value;
            }

            // if our corner position has changed, save our new corner position
            _cornerPosition = targetCornerPosition;

            Point newPos;
            switch (targetCornerPosition)
            {
                case CornerPosition.TopLeft:
                case CornerPosition.BottomLeft:
                    newPos.X = workArea.Left;
                    break;
                case CornerPosition.TopRight:
                case CornerPosition.BottomRight:
                    newPos.X = workArea.Right - this.Width;
                    break;
            }
            switch (targetCornerPosition)
            {
                case CornerPosition.TopLeft:
                case CornerPosition.TopRight:
                    newPos.Y = workArea.Top;
                    break;
                case CornerPosition.BottomLeft:
                case CornerPosition.BottomRight:
                    newPos.Y = workArea.Bottom - this.Height;
                    break;
            }

            // if our bar is wider than the screen and snapped to a right corner, make sure it doesn't move off the left side (but don't change its corner)
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

        private void WindowAnimComplete(object? sender, EventArgs e)
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
