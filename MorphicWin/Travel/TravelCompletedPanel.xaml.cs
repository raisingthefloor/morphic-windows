using System.Collections;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using MorphicService;

namespace MorphicWin
{
    /// <summary>
    /// Interaction logic for TravelCompletedPanel.xaml
    /// </summary>
    public partial class TravelCompletedPanel : StackPanel
    {
        public TravelCompletedPanel(Session session, ILogger<TravelCompletedPanel> logger)
        {
            this.session = session;
            this.logger = logger;
            InitializeComponent();
        }

        private readonly Session session;

        private readonly ILogger<TravelCompletedPanel> logger;
    }
}
