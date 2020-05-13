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

using Microsoft.Extensions.Logging;
using MorphicService;
using System;
using System.Threading;
using System.Timers;
using System.Windows;
using System.Windows.Controls;

namespace MorphicWin
{
    /// <summary>
    /// The Capture panel is one of the steps shown when the user is walked through the process of taking their settings with them.
    /// It shows a progress indicator while a <code>CaptureSession</code> is run behind the scenes.  No user interaction is required.
    /// </summary>
    public partial class CapturePanel : StackPanel
    {

        #region Creating a Panel

        public CapturePanel(Session session, ILogger<CapturePanel> logger)
        {
            this.session = session;
            this.logger = logger;
            InitializeComponent();
            Loaded += OnLoaded;
        }

        /// <summary>
        /// A logger to use
        /// </summary>
        private readonly ILogger<CapturePanel> logger;

        #endregion

        #region Completion Events

        /// <summary>
        /// Dispatched when the capture process is complete and the minimum time has elapsed
        /// </summary>
        public event EventHandler? Completed;

        #endregion

        #region Lifecycle

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            timerSynchronizatinContext = SynchronizationContext.Current;
            minimumIntervalTimer = new System.Timers.Timer(minimumWaitTimeInSeconds * 1000);
            minimumIntervalTimer.AutoReset = false;
            minimumIntervalTimer.Elapsed += OnTimerElapsed;
            minimumIntervalTimer.Start();
        }

        #endregion

        #region Minimum Time

        /// <summary>
        /// The capture panel is shown for a minimum amount of time regardless of how quickly the capture session runs.
        /// This gives the user enough time to read the screen and understand what is going on.
        /// </summary>
        private System.Timers.Timer? minimumIntervalTimer;

        /// <summary>
        /// The minimum time to wait before continuing
        /// </summary>
        private const int minimumWaitTimeInSeconds = 1;

        /// <summary>
        /// Used to get back to the main thread when the timer fires
        /// </summary>
        private SynchronizationContext? timerSynchronizatinContext;

        /// <summary>
        /// Called when the minimum time has elapsed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnTimerElapsed(object? sender, EventArgs e)
        {
            minimumIntervalTimer = null;
            if (timerSynchronizatinContext is SynchronizationContext context)
            {
                timerSynchronizatinContext = null;
                context.Post(state =>
                {
                    Completed?.Invoke(this, new EventArgs());
                }, null);
            }
            
        }

        #endregion

        #region Capture

        /// <summary>
        /// The Morphic Session to use for making requests
        /// </summary>
        private readonly Session session;

        #endregion
    }
}
