// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt
//
// The R&D leading to these results received funding from the:
// * Rehabilitation Services Administration, US Dept. of Education under 
//   grant H421A150006 (APCP)
// * National Institute on Disability, Independent Living, and 
//   Rehabilitation Research (NIDILRR)
// * Administration for Independent Living & Dept. of Education under grants 
//   H133E080022 (RERC-IT) and H133E130028/90RE5003-01-00 (UIITA-RERC)
// * European Union's Seventh Framework Programme (FP7/2007-2013) grant 
//   agreement nos. 289016 (Cloud4all) and 610510 (Prosperity4All)
// * William and Flora Hewlett Foundation
// * Ontario Ministry of Research and Innovation
// * Canadian Foundation for Innovation
// * Adobe Foundation
// * Consumer Electronics Association Foundation

using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using Morphic.Service;

namespace Morphic.Client
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
            EmailLabel.Content = session.User?.Email;
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
