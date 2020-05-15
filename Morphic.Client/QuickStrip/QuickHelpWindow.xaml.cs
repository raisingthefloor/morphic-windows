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

using System.Timers;
using System.Windows;

namespace Morphic.Client
{
    /// <summary>
    /// A large window that behaves similar to a tooltip, but shows up immediately when hovering over quick strip controls
    /// </summary>
    public partial class QuickHelpWindow : Window
    {
        public QuickHelpWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// The singleton window managed by <code>Show()</code>/<code>Dismiss()</code>
        /// </summary>
        private static QuickHelpWindow? shared = null;

        /// <summary>
        /// Show the given help text in the shared Quick Help Window
        /// </summary>
        /// <param name="title">The title to show</param>
        /// <param name="message">The message to show</param>
        public static void Show(string title, string message)
        {
            if (shared == null)
            {
                shared = new QuickHelpWindow();
            }
            shared.TitleLabel.Content = title;
            shared.MessageLabel.Content = message;
            shared.Reposition();
            shared.Show();
            hideTimer?.Stop();
            hideTimer = null;
        }

        /// <summary>
        /// Timer for hiding the window after a short delay
        /// </summary>
        private static Timer? hideTimer;

        /// <summary>
        /// Hide the shared Quick Help Window
        /// </summary>
        /// <remarks>
        /// Will hide after a short delay to minimize flickering when hovering between two controls
        /// </remarks>
        public static void Dismiss()
        {
            hideTimer = new Timer(200);
            hideTimer.AutoReset = false;
            var mainContext = System.Threading.SynchronizationContext.Current;
            hideTimer.Elapsed += (sender, e) =>
            {
                mainContext?.Send(state => {
                    hideTimer.Dispose();
                    hideTimer = null;
                    shared?.Close();
                    shared = null;
                }, null);
            };
            hideTimer.Start();
        }

        /// <summary>
        /// Center the window in the screen
        /// </summary>
        /// <param name="animated"></param>
        private void Reposition()
        {
            var screenSize = SystemParameters.WorkArea;
            Left = System.Math.Round((screenSize.Width - Width) / 2.0);
            Top = System.Math.Round((screenSize.Height - Height) / 2.0);
        }
    }
}
