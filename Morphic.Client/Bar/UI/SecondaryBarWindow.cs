namespace Morphic.Client.Bar.UI
{
    using System;
    using System.Windows;
    using System.Windows.Automation;
    using System.Windows.Controls;
    using AppBarWindow;
    using Data;

    public class SecondaryBarWindow : BarWindow
    {
        private readonly PrimaryBarWindow primaryBarWindow;

        public override bool IsExpanded
        {
            get => this.primaryBarWindow.IsExpanded;
            set => this.primaryBarWindow.IsExpanded = value;
        }

        public override ExpanderWindow? ExpanderWindow => this.primaryBarWindow.ExpanderWindow;
        public override BarWindow OtherWindow => this.primaryBarWindow;

        /// <summary>
        /// The edge of the primary bar window where this window is attached to.
        /// </summary>
        public Edge AttachedEdge { get; private set; }
        
        public SecondaryBarWindow(PrimaryBarWindow primaryBarWindow, BarData barData)
            : base(barData)
        {
            this.primaryBarWindow = primaryBarWindow;
            base.Scale = this.primaryBarWindow.Scale;

            this.SetValue(AutomationProperties.NameProperty,
                this.GetValue(AutomationProperties.NameProperty) + " Secondary");

            this.primaryBarWindow.Loaded += (sender, args) =>
            {
                this.Owner = this.primaryBarWindow;
            };

            this.primaryBarWindow.ContentRendered += (sender, args) => this.UpdatePosition();
            this.primaryBarWindow.LocationChanged += (sender, args) => this.UpdatePosition();
            this.primaryBarWindow.SizeChanged += (sender, args) => this.UpdatePosition();
            this.primaryBarWindow.OrientationChanged +=
                (sender, args) => this.Orientation = this.primaryBarWindow.Orientation;

            App.Current.Deactivated += (sender, args) =>
            {
                if (this.Bar.SecondaryBar.AutoHide)
                {
                    this.IsExpanded = false;
                }
            };

            this.primaryBarWindow.Closed += (sender, args) => this.Close();
            this.AppBar.BeginDragMove += (sender, args) =>
            {
                this.primaryBarWindow.WindowMovement.DragMove();
                args.Cancel = true;
            };
        }

        protected override void OnBarLoaded()
        {
            base.OnBarLoaded();
            this.UpdatePosition();
        }

        public void UpdatePosition()
        {
            if (!this.IsLoaded) return;
            Rect workArea = this.GetWorkArea();
            this.MaxWidth = workArea.Width;
            this.MaxHeight = workArea.Height;
            this.Visibility = this.IsExpanded ? Visibility.Visible : Visibility.Hidden;

            // If docked, attach to the other side. 
            Edge edge = this.primaryBarWindow.DockedEdge.Opposite();
            bool docked = edge != Edge.None;

            if (!docked)
            {
                // Prefer attaching to where there is most room
                if (this.primaryBarWindow.Orientation == Orientation.Horizontal)
                {
                    edge = this.primaryBarWindow.Top - workArea.Top >
                           workArea.Bottom - (this.primaryBarWindow.Top + this.primaryBarWindow.Height)
                        ? Edge.Top
                        : Edge.Bottom;
                }
                else
                {
                    edge = this.primaryBarWindow.Left - workArea.Left >
                           workArea.Right - (this.primaryBarWindow.Left + this.primaryBarWindow.Width)
                        ? Edge.Left
                        : Edge.Right;
                }
            }

            this.AttachedEdge = edge;
            Point pos = this.GetPosition(edge, workArea);
            this.Left = pos.X;
            this.Top = pos.Y;
        }

        private Point GetPosition(Edge attachedEdge, Rect workArea)
        {
            Rect rect = new Rect(this.primaryBarWindow.Left, this.primaryBarWindow.Top, this.Width, this.Height);

            // Move it to the edge.
            switch (attachedEdge)
            {
                case Edge.Left:
                    rect.X -= this.Width - this.BorderThickness.Right;
                    break;
                case Edge.Top:
                    rect.Y -= this.Height;
                    break;
                case Edge.Right:
                    rect.X += this.primaryBarWindow.Width - this.BorderThickness.Left;
                    break;
                case Edge.Bottom:
                    rect.Y += this.primaryBarWindow.Height;
                    break;
            }
            
            // Align along the edge
            if (attachedEdge.IsHorizontal())
            {
                rect.X = this.Bar.Position.Secondary.X.GetAbsolute(this.primaryBarWindow.Left,
                    this.primaryBarWindow.Left + this.primaryBarWindow.Width, this.Width);
            }
            else
            {
                rect.Y = this.Bar.Position.Secondary.Y.GetAbsolute(this.primaryBarWindow.Top,
                    this.primaryBarWindow.Top + this.primaryBarWindow.Height, this.Height);
            }
            
            // Make sure it's on the screen, and within the main bar.
            if (attachedEdge.IsVertical())
            {
                if (this.Height > this.primaryBarWindow.Height)
                {
                    rect.Y = rect.Y + (this.Height - this.primaryBarWindow.Height) / 2;
                }

                if (rect.Bottom > this.primaryBarWindow.Top + this.primaryBarWindow.Height)
                {
                    rect.Y = this.primaryBarWindow.Top + this.primaryBarWindow.Height - rect.Height;
                }

                rect.Y = rect.Height > workArea.Bottom
                    ? workArea.Top
                    : Math.Clamp(rect.Y, workArea.Top, workArea.Bottom - rect.Height);
            }
            else
            {
                if (this.Width > this.primaryBarWindow.Width)
                {
                    rect.X = rect.X + (this.Width - this.primaryBarWindow.Width) / 2;
                }

                if (rect.Right > this.primaryBarWindow.Left + this.primaryBarWindow.Width)
                {
                    rect.X = this.primaryBarWindow.Left + this.primaryBarWindow.Width - rect.Width;
                }

                rect.X = rect.Width > workArea.Right
                    ? workArea.Left
                    : Math.Clamp(rect.X, workArea.Left, workArea.Right - rect.Width);
            }

            return rect.Location;
        }
    }
}