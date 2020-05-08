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
    /// Interaction logic for CapturePanel.xaml
    /// </summary>
    public partial class CapturePanel : StackPanel
    {
        public CapturePanel(Session session, ILogger<CapturePanel> logger)
        {
            this.session = session;
            this.logger = logger;
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private System.Timers.Timer? minimumIntervalTimer;

        private const int minimumWaitTimeInSeconds = 10;

        private SynchronizationContext? timerSynchronizatinContext;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            timerSynchronizatinContext = SynchronizationContext.Current;
            minimumIntervalTimer = new System.Timers.Timer(minimumWaitTimeInSeconds * 1000);
            minimumIntervalTimer.AutoReset = false;
            minimumIntervalTimer.Elapsed += OnTimerElapsed;
            minimumIntervalTimer.Start();
        }

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

        private readonly Session session;

        private readonly ILogger<CapturePanel> logger;

        public event EventHandler? Completed;
    }
}
