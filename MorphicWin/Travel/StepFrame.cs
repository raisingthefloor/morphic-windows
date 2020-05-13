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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MorphicWin
{
    /// <summary>
    /// Contains a single page at time, but animates when switching to a new page
    /// </summary>
    public class StepFrame : FrameworkElement
    {

        /// <summary>
        /// The currently visible page
        /// </summary>
        public Panel? CurrentPanel { get; private set; }

        private Panel? dismissedPanel;

        /// <summary>
        /// How long the push animation should take when presenting a new page
        /// </summary>
        public double PushAnimationDurationInSeconds = 0.3;

        /// <summary>
        /// Show a new page by pushing it in from the right
        /// </summary>
        /// <param name="panel"></param>
        /// <param name="animated"></param>
        public void PushPanel(Panel panel, bool animated = true)
        {
            dismissedPanel = CurrentPanel;
            CurrentPanel = panel;
            AddVisualChild(panel);
            InvalidateVisual();
            if (animated && dismissedPanel != null)
            {

                var duration = new Duration(TimeSpan.FromSeconds(PushAnimationDurationInSeconds));

                var dismissAnimation = new DoubleAnimation();
                var dismissTranslate = new TranslateTransform(0, 0);
                dismissedPanel.RenderTransform = dismissTranslate;
                dismissAnimation.Duration = duration;
                dismissAnimation.FillBehavior = FillBehavior.Stop;
                dismissAnimation.From = dismissTranslate.X;
                dismissAnimation.To = -dismissedPanel.ActualWidth;
                dismissAnimation.Completed += (object? sender, EventArgs e) =>
                {
                    CleanupDimissedPanel();
                };
                dismissTranslate.BeginAnimation(TranslateTransform.XProperty, dismissAnimation);

                var presentAnimation = new DoubleAnimation();
                var presentTransform = new TranslateTransform(dismissedPanel.ActualWidth, 0);
                panel.RenderTransform = presentTransform;
                presentAnimation.Duration = duration;
                presentAnimation.FillBehavior = FillBehavior.Stop;
                presentAnimation.From = presentTransform.X;
                presentAnimation.To = 0;
                presentAnimation.Completed += (object? sender, EventArgs e) =>
                {
                    panel.RenderTransform = Transform.Identity;
                };
                presentTransform.BeginAnimation(TranslateTransform.XProperty, presentAnimation);
            }
            else
            {
                CleanupDimissedPanel();
            }
        }

        /// <summary>
        /// Remove the dismissed page 
        /// </summary>
        private void CleanupDimissedPanel()
        {
            if (dismissedPanel is Panel panel)
            {
                dismissedPanel = null;
                RemoveVisualChild(panel);
            }
        }

        protected override int VisualChildrenCount {
            get
            {
                var count = 0;
                if (dismissedPanel != null)
                {
                    ++count;
                }
                if (CurrentPanel != null)
                {
                    ++count;
                }
                return count;
            }
        }

        protected override Visual GetVisualChild(int index)
        {
            if (index == 0)
            {
                return CurrentPanel!;
            }
            return dismissedPanel!;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (dismissedPanel is Panel dismissed)
            {
                dismissed.Arrange(new Rect(finalSize));
            }
            if (CurrentPanel is Panel current)
            {
                current.Arrange(new Rect(finalSize));
            }
            return finalSize;
        }

    }
}
