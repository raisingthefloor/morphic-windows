// AppBar.cs: Lets a window be dragged and docked.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

namespace Morphic.Bar.UI.AppBarWindow
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;

    /// <summary>
    /// Makes a Window become a draggable "app bar" window which can be snapped or docked to the desktop edges.
    /// </summary>
    public class AppBar
    {
        private readonly Window window;
        private readonly WindowMovement windowMovement;
        private readonly AppBarApi api;

        private Point mouseDownPos;
        private Size floatingSize = Size.Empty;
        public Edge AppBarEdge { get; private set; } = Edge.None;
        public bool EnableDocking { get; set; } = true;

        public bool SnapToEdges { get; set; } = true;
        public bool Draggable { get; set; } = true;

        public event EventHandler<EdgeChangedEventArgs>? EdgeChanged;
        public event EventHandler<CancelableEventArgs>? BeginDragMove; 

        public AppBar(Window window) : this(window, new WindowMovement(window, true))
        {
        }
        
        public AppBar(Window window, WindowMovement windowMovement)
        {
            if (!(window is IAppBarWindow))
            {
                throw new ArgumentException($"The window must implement {nameof(IAppBarWindow)}.", nameof(window));
            }
            
            this.window = window;
            this.windowMovement = windowMovement;
            this.api = new AppBarApi(this.windowMovement);

            // Make the window draggable.
            this.window.PreviewMouseDown += this.OnPreviewMouseDown;
            this.window.PreviewMouseMove += this.OnPreviewMouseMove;

            this.windowMovement.SizeComplete += this.OnSizeComplete;
            this.windowMovement.MoveComplete += this.OnMoveComplete;
            
            this.windowMovement.Moving += this.OnMoving;
            this.windowMovement.Sizing += this.OnSizing;
        }

        private void OnSizing(object? sender, WindowMovement.MovementEventArgs e)
        {
            // Adjust the size to match the content.
            bool horiz = (e.SizeEdge & WindowMovement.SizeEdge.Horizontal) == WindowMovement.SizeEdge.Horizontal;
            Size newSize = this.GetGoodSize(e.Rect.Size, horiz ? Orientation.Vertical : Orientation.Horizontal, true);
            if (newSize != e.Rect.Size)
            {
                e.Rect.Size = newSize;
                e.Handled = true;
            }
        }

        /// <summary>
        /// Gets a size which better fits the content.
        /// </summary>
        /// <param name="size">The suggested size.</param>
        /// <param name="orientation"></param>
        /// <param name="inPixels">true if the size is in pixels.</param>
        /// <returns>The new size.</returns>
        public Size GetGoodSize(Size size, Orientation orientation, bool inPixels = false)
        {
            bool changed = false;
            Size newSize = inPixels ? this.FromPixels(size) : size;

            void GetHeight()
            {
                if (!this.AppBarEdge.IsVertical())
                {
                    double newHeight = ((IAppBarWindow)this.window).GetHeightFromWidth(newSize.Width);
                    if (!double.IsNaN(newHeight))
                    {
                        newSize.Height = newHeight;
                        changed = true;
                    }
                }
            }

            void GetWidth()
            {
                if (!this.AppBarEdge.IsHorizontal())
                {
                    double newWidth = ((IAppBarWindow)this.window).GetWidthFromHeight(newSize.Height);
                    if (!double.IsNaN(newWidth))
                    {
                        newSize.Width = newWidth;
                        changed = true;
                    }
                }
            }

            if (orientation == Orientation.Vertical)
            {
                GetHeight();
                GetWidth();
            }
            else
            {
                GetWidth();
                GetHeight();
            }

            return changed
                ? (inPixels ? this.ToPixels(newSize) : newSize)
                : size;
        }

        private void OnSizeComplete(object? sender, EventArgs e)
        {
            // Re-adjust the reserved desktop space.
            this.api.Update();
        }

        /// <summary>
        /// Called when the window has stopped being moved or sized.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnMoveComplete(object? sender, EventArgs args)
        {
            // Reserve desktop space for the window.
            this.ApplyAppBar(this.AppBarEdge);
        }
        
        protected virtual void OnBeginDragMove(CancelableEventArgs e)
        {
            this.BeginDragMove?.Invoke(this, e);
        }


        public void ApplyAppBar(Edge edge)
        {
            this.api.Apply(edge);
            this.OnEdgeChanged(edge, false);
        }

        /// <summary>
        /// Adjusts a thickness so the edges that are touching the screen edge are zero.
        /// </summary>
        /// <param name="thickness">The initial thickness.</param>
        /// <param name="invert">true to remove on the non-touching edge.</param>
        /// <param name="none">Value of "zero" thickness.</param>
        public Thickness AdjustThickness(Thickness thickness, bool invert = false, Thickness? none = null)
        {
            none ??= new Thickness(0);

            if (this.AppBarEdge == Edge.None)
            {
                return invert ? none.Value : thickness;
            }

            Edge notTouching = this.AppBarEdge.Opposite();

            Dictionary<Edge, Action> actions = new Dictionary<Edge, Action>()
            {
                {Edge.Left, () => thickness.Left = none.Value.Left },
                {Edge.Right, () => thickness.Right = none.Value.Right },
                {Edge.Top, () => thickness.Top = none.Value.Top },
                {Edge.Bottom, () => thickness.Bottom = none.Value.Bottom }
            };

            foreach ((Edge edge, Action action) in actions)
            {
                if ((edge == notTouching) == invert)
                {
                    action.Invoke();
                }
            }

            return thickness;
        }

        /// <summary>
        /// Called when the window is being moved, to re-adjust the window in-flight.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnMoving(object? sender, WindowMovement.MovementEventArgs args)
        {
            args.Handled = true;

            if (args.IsFirst)
            {
                if (this.AppBarEdge == Edge.None)
                {
                    this.floatingSize = args.Rect.Size;
                }
                else
                {
                    // Un-dock the window so it can be moved.
                    this.ApplyAppBar(Edge.None);

                    // Revert to the original size
                    args.Rect.Size = this.floatingSize;

                    // If the window is not under the cursor, move it so the cursor is in the centre.
                    if (args.Rect.X > args.Cursor.X || args.Rect.Right < args.Cursor.X)
                    {
                        args.Rect.X = args.Cursor.X - args.Rect.Width / 2;
                    }

                    if (args.Rect.Y > args.Cursor.Y || args.Rect.Bottom < args.Cursor.Y)
                    {
                        args.Rect.Y = args.Cursor.Y - args.Rect.Height / 2;
                    }

                    // Make it look like the window was this size when the move started.
                    args.InitialRect = args.NewInitialRect = args.Rect;
                }
            }

            // Like magnifier, if the mouse pointer is on the edge then make the window an app bar on that edge.
            Point mouse = WindowMovement.GetCursorPos();
            Rect workArea = this.windowMovement.GetWorkArea(new Point(mouse.X, mouse.Y));
            if (this.EnableDocking)
            {
                // See what edge the mouse is near (or beyond)
                Rect mouseRect = new Rect(
                    Math.Clamp(mouse.X, workArea.Left, workArea.Right),
                    Math.Clamp(mouse.Y, workArea.Top, workArea.Bottom), 0, 0);

                Edge lastEdge = this.AppBarEdge;
                this.AppBarEdge = NearEdges(workArea, mouseRect, 5).First();
                if (lastEdge != this.AppBarEdge)
                {
                    this.OnEdgeChanged(this.AppBarEdge, true);
                }
            }

            // Reposition the window to fit the edge.
            switch (this.AppBarEdge)
            {
                case Edge.Left:
                case Edge.Right:
                    args.Rect.Height = workArea.Height;
                    args.Rect.Width = this.GetGoodSize(args.Rect.Size, Orientation.Vertical, true).Width;
                    args.Rect.Y = workArea.Top;
                    if (this.AppBarEdge == Edge.Left)
                    {
                        args.Rect.X = workArea.X;
                    }
                    else
                    {
                        args.Rect.X = workArea.Right - args.Rect.Width;
                    }

                    break;

                case Edge.Top:
                case Edge.Bottom:
                    args.Rect.Width = workArea.Width;
                    args.Rect.Height = this.GetGoodSize(args.Rect.Size, Orientation.Horizontal, true).Height;
                    args.Rect.X = workArea.X;
                    if (this.AppBarEdge == Edge.Top)
                    {
                        args.Rect.Y = workArea.Y;
                    }
                    else
                    {
                        args.Rect.Y = workArea.Bottom - args.Rect.Height;
                    }

                    break;

                case Edge.None:
                    args.Rect = args.SupposedRect;
                    // Snap to an edge 
                    if (this.SnapToEdges)
                    {
                        this.SnapToEdge(this.windowMovement.GetWorkArea(), ref args.Rect, 20);
                    }

                    break;
            }
        }

        /// <summary>
        /// The width and height of the Window when it is docked.
        /// </summary>
        public Size DockedSizes { get; set; } = new Size(100, 100);

        public Size ToPixels(Size size) => (Size)this.ToPixels((Point) size);
        public Size FromPixels(Size size) => (Size)this.FromPixels((Point) size);

        public Point ToPixels(Point point)
        {
            return PresentationSource.FromVisual(this.window)?.CompositionTarget.TransformToDevice.Transform(point)
                   ?? point;
        }
        
        public Point FromPixels(Point point)
        {
            return PresentationSource.FromVisual(this.window)?.CompositionTarget.TransformFromDevice.Transform(point)
                ?? point;
        }

        public Rect ToPixels(Rect rect)
        {
            return new Rect(this.ToPixels(rect.Location), this.ToPixels(rect.Size));
        }
        
        public Rect FromPixels(Rect rect)
        {
            return new Rect(this.FromPixels(rect.Location), this.FromPixels(rect.Size));
        }

        public Size DockedSizesPixels { get; set; }

        /// <summary>
        /// Snaps a rectangle to the edges of another, if it's close enough.
        /// </summary>
        /// <param name="outer">The outer rectangle to check against.</param>
        /// <param name="rect">The inner rect to adjust.</param>
        /// <param name="distance">The distance that the edge can be, in order to snap.</param>
        private void SnapToEdge(Rect outer, ref Rect rect, double distance)
        {
            HashSet<Edge> edges = NearEdges(outer, rect, distance);
            if (!edges.Contains(Edge.None))
            {
                if (edges.Contains(Edge.Left))
                {
                    rect.X = outer.X;
                }

                if (edges.Contains(Edge.Top))
                {
                    rect.Y = outer.Y;
                }

                if (edges.Contains(Edge.Right))
                {
                    rect.X = outer.Right - rect.Width;
                }

                if (edges.Contains(Edge.Bottom))
                {
                    rect.Y = outer.Bottom - rect.Height;
                }
            }
        }

        /// <summary>
        /// Determines the edges of a rectangle that are close to the edge of an outer rectangle.
        /// </summary>
        /// <param name="outer">The outer rectangle.</param>
        /// <param name="rect">The inner rectangle.</param>
        /// <param name="distance">The distance that the edges need to be in order to be near.</param>
        /// <returns>Set of edges that are close. Will contain only Edge.None if no edges are close.</returns>
        private static HashSet<Edge> NearEdges(Rect outer, Rect rect, double distance)
        {
            HashSet<Edge> result = new HashSet<Edge>();

            bool Near(double a, double b) => Math.Abs(a - b) <= distance;

            if (Near(outer.X, rect.X))
            {
                result.Add(Edge.Left);
            }
            else if (Near(rect.Right, outer.Right))
            {
                result.Add(Edge.Right);
            }

            if (Near(outer.Y, rect.Y))
            {
                result.Add(Edge.Top);
            }
            else if (Near(rect.Bottom, outer.Bottom))
            {
                result.Add(Edge.Bottom);
            }

            if (result.Count == 0)
            {
                result.Add(Edge.None);
            }

            return result;
        }


        /// <summary>
        /// Keeps an eye on the mouse movement. If it's a drag action, start the window move.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnPreviewMouseMove(object sender, MouseEventArgs args)
        {
            if (this.Draggable && args.LeftButton == MouseButtonState.Pressed)
            {
                Point point = args.GetPosition(this.window);

                if (Math.Abs(point.X - this.mouseDownPos.X) >= SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(point.Y - this.mouseDownPos.Y) >= SystemParameters.MinimumVerticalDragDistance)
                {
                    CancelableEventArgs eventArgs = new CancelableEventArgs();
                    this.OnBeginDragMove(eventArgs);
                    if (!eventArgs.Cancel)
                    {
                        this.windowMovement.DragMove();
                    }
                }
            }
        }

        /// <summary>
        /// Stores the point at which the mouse was pressed, in order to determine if a move becomes a drag.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs args)
        {
            if (this.Draggable && args.LeftButton == MouseButtonState.Pressed)
            {
                this.mouseDownPos = args.GetPosition(this.window);
            }
        }

        protected virtual void OnEdgeChanged(Edge edge, bool preview)
        {
            this.OnEdgeChanged(new EdgeChangedEventArgs(edge, preview));
        }
        
        protected virtual void OnEdgeChanged(EdgeChangedEventArgs args)
        {
            this.EdgeChanged?.Invoke(this, args);
        }
    }

    public interface IAppBarWindow
    {
        /// <summary>A callback that returns a good height from a given width.</summary>
        public double GetHeightFromWidth(double width);

        /// <summary>A callback that returns a good width from a given height.</summary>
        public double GetWidthFromHeight(double height);

    }
    
    public class EdgeChangedEventArgs : EventArgs
    {
        public EdgeChangedEventArgs(Edge edge, bool preview)
        {
            this.Edge = edge;
            this.Preview = preview;
        }

        /// <summary>
        /// The edge of the screen.
        /// </summary>
        public Edge Edge { get; }
        /// <summary>
        /// true if the current change is only a preview, the desktop reservation has not yet been applied.
        /// </summary>
        public bool Preview { get; }
    }

    public static class AppBarExtensionMethods
    {
        public static Edge Opposite(this Edge edge)
        {
            return edge switch
            {
                Edge.None => Edge.None,
                Edge.Left => Edge.Right,
                Edge.Top => Edge.Bottom,
                Edge.Right => Edge.Left,
                Edge.Bottom => Edge.Top,
                _ => Edge.None
            };
        }
        
        /// <summary>
        /// Is the edge horizontal? top or bottom.
        /// </summary>
        /// <param name="edge"></param>
        /// <returns></returns>
        public static bool IsHorizontal(this Edge edge)
        {
            return (edge == Edge.Top || edge == Edge.Bottom);
        }
        
        /// <summary>
        /// Is the edge vertical? left or right.
        /// </summary>
        /// <param name="edge"></param>
        /// <returns></returns>
        public static bool IsVertical(this Edge edge)
        {
            return (edge == Edge.Left || edge == Edge.Right);
        }

        public static Rect GetRect(this Window window)
        {
            return new Rect(window.Left, window.Top, window.Width, window.Height);
        }
    }

    public class CancelableEventArgs : EventArgs
    {
        public bool Cancel { get; set; }
    }

}