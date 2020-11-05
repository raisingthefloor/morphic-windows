namespace Morphic.Client.Bar.UI
{
    using System.Windows;
    using System.Windows.Controls;

    /// <summary>
    /// A wrapper for <see cref="Size"/> or <see cref="Point"/>, where the coordinated get swapped depending on an
    /// orientation. This simplifies the positioning algorithms, so they do not need to be concerned about the
    /// orientation.
    /// </summary>
    public struct CorrectedCoords
    {
        public readonly Orientation Orientation;
        private readonly bool swap;

        public CorrectedCoords(Size size, Orientation orientation) : this(size.Width, size.Height, orientation)
        {
        }
        public CorrectedCoords(Point size, Orientation orientation) : this(size.X, size.Y, orientation)
        {
        }
        public CorrectedCoords(Orientation orientation) : this(0, 0, orientation)
        {
        }

        public override string ToString()
        {
            return $"{this.Width}x{this.Height}{(this.swap ? "(swap)" : "")}";
        }

        public CorrectedCoords(double x, double y, Orientation orientation)
        {
            this.Orientation = orientation;
            this.swap = this.Orientation == Orientation.Vertical;
            this.X = this.swap ? y : x;
            this.Y = this.swap ? x : y;
        }

        public static implicit operator Size(CorrectedCoords size) => size.ToSize();

        public Size ToSize()
        {
            return this.swap
                ? new Size(this.Height, this.Width)
                : new Size(this.Width, this.Height);
        }

        public Point ToPoint()
        {
            return this.swap
                ? new Point(this.Y, this.X)
                : new Point(this.X, this.Y);
        }

        public double X { get; set; }
        public double Y { get; set; }

        public double Width
        {
            get => this.X;
            set => this.X = value;
        }

        public double Height
        {
            get => this.Y;
            set => this.Y = value;
        }
    }
}
