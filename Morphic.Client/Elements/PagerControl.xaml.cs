using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Morphic.Client.Elements
{
    /// <summary>
    /// Interaction logic for PagerControl.xaml
    /// </summary>
    public partial class PagerControl : UserControl
    {
        public PagerControl()
        {
            InitializeComponent();
        }

        private int numberOfPages = 1;
        public int NumberOfPages
        {
            get
            {
                return numberOfPages;
            }
            set
            {
                numberOfPages = value;
                UpdateItems();
            }
        }

        private int currentPage = -1;
        public int CurrentPage
        {
            get
            {
                return currentPage;
            }
            set
            {
                currentPage = value;
                UpdateItems();
            }
        }

        private Brush color = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));
        public Brush Color
        {
            get
            {
                return color;
            }
            set
            {
                color = value;
                UpdateItems();
            }
        }

        private void UpdateItems()
        {
            StackPanel.Children.Clear();
            for (var i = 0; i < NumberOfPages; ++i)
            {
                if (i > 0)
                {
                    StackPanel.Children.Add(CreateLine());
                }
                StackPanel.Children.Add(CreateDot(i == CurrentPage));
            }
            InvalidateMeasure();
        }

        private Rectangle CreateLine()
        {
            var rect = new Rectangle();
            rect.Width = Height;
            rect.Height = 1;
            rect.Fill = Color;
            return rect;
        }

        private Ellipse CreateDot(bool selected)
        {
            var ellipse = new Ellipse();
            ellipse.Width = Height;
            ellipse.Height = ellipse.Width;
            ellipse.Stroke = Color;
            if (selected)
            {
                ellipse.Fill = Color;
            }
            return ellipse;
        }
    }
}
