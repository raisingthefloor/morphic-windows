using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using MorphicService;

namespace MorphicWin
{
    /// <summary>
    /// Shown at the end of the capture process as a review for the user
    /// </summary>
    public partial class TravelCompletedPanel : StackPanel
    {

        #region Creating a Panel

        public TravelCompletedPanel(Session session, ILogger<TravelCompletedPanel> logger)
        {
            this.session = session;
            this.logger = logger;
            InitializeComponent();
        }

        /// <summary>
        /// A logger to use
        /// </summary>
        private readonly ILogger<TravelCompletedPanel> logger;

        #endregion

        #region Completion Events

        /// <summary>
        /// The event that is dispatched when the user clicks the Close button
        /// </summary>
        public event EventHandler? Completed;

        #endregion

        #region Lifecycle

        private readonly Session session;

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // FIXME: enable once Email is added to the user model
            // EmailLabel.Content = session.User?.Email;
        }

        #endregion

        #region Actions

        /// <summary>
        /// Handler for when the user clicks the Close button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnClose(object? sender, RoutedEventArgs e)
        {
            Completed?.Invoke(this, new EventArgs());
        }

        #endregion
    }
}
