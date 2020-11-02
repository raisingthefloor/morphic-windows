namespace Morphic.Client.Dialogs.Elements
{
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Shapes;

    /// <summary>
    /// Interaction logic for PagerControl.xaml
    /// </summary>
    public partial class PagerControl : UserControl
    {
        public PagerControl()
        {
            this.InitializeComponent();
        }

        public int Offset { get; set; } = 0;

        private int numberOfPages = 1;
        public int NumberOfPages
        {
            get
            {
                return this.numberOfPages;
            }
            set
            {
                this.numberOfPages = value;
                this.UpdateItems();
            }
        }

        private int currentPage = -1;
        public int CurrentPage
        {
            get
            {
                return this.currentPage;
            }
            set
            {
                this.currentPage = value;
                this.UpdateItems();
            }
        }

        private Brush color = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));
        public Brush Color
        {
            get
            {
                return this.color;
            }
            set
            {
                this.color = value;
                this.UpdateItems();
            }
        }

        private void UpdateItems()
        {
            this.StackPanel.Children.Clear();
            for (var i = 0; i < this.NumberOfPages; ++i)
            {
                if (i > 0)
                {
                    this.StackPanel.Children.Add(this.CreateLine());
                }
                this.StackPanel.Children.Add(this.CreateDot(i == this.CurrentPage - this.Offset));
            }
            this.InvalidateMeasure();
        }

        private Rectangle CreateLine()
        {
            var rect = new Rectangle();
            rect.Width = this.Height;
            rect.Height = 1;
            rect.Fill = this.Color;
            return rect;
        }

        private Ellipse CreateDot(bool selected)
        {
            var ellipse = new Ellipse();
            ellipse.Width = this.Height;
            ellipse.Height = ellipse.Width;
            ellipse.Stroke = this.Color;
            if (selected)
            {
                ellipse.Fill = this.Color;
            }
            return ellipse;
        }
    }
}
