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

namespace Morphic.Client.Dialogs.Elements
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Media.Animation;
    using Microsoft.Extensions.DependencyInjection;

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

        public T PushPanel<T>(bool animated = true)
            where T : Panel, IStepPanel
        {
            T panel = App.Current.ServiceProvider.GetService<T>();
            this.PushPanel(panel);
            return panel;
        }

        /// <summary>
        /// Show a new page by pushing it in from the right
        /// </summary>
        /// <param name="panel"></param>
        /// <param name="animated"></param>
        public void PushPanel(Panel panel, bool animated = true)
        {
            if (panel is IStepPanel stepPanel)
            {
                stepPanel.StepFrame = this;
            }

            this.dismissedPanel = this.CurrentPanel;
            this.CurrentPanel = panel;
            this.AddVisualChild(panel);
            this.InvalidateVisual();
            if (animated && this.dismissedPanel is not null)
            {

                var duration = new Duration(TimeSpan.FromSeconds(this.PushAnimationDurationInSeconds));

                var dismissAnimation = new DoubleAnimation();
                var dismissTranslate = new TranslateTransform(0, 0);
                this.dismissedPanel.RenderTransform = dismissTranslate;
                dismissAnimation.Duration = duration;
                dismissAnimation.FillBehavior = FillBehavior.Stop;
                dismissAnimation.From = dismissTranslate.X;
                dismissAnimation.To = -this.dismissedPanel.ActualWidth;
                dismissAnimation.Completed += (object? sender, EventArgs e) =>
                {
                    this.CleanupDimissedPanel();
                };
                dismissTranslate.BeginAnimation(TranslateTransform.XProperty, dismissAnimation);

                var presentAnimation = new DoubleAnimation();
                var presentTransform = new TranslateTransform(this.dismissedPanel.ActualWidth, 0);
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
                this.CleanupDimissedPanel();
            }
        }

        /// <summary>
        /// Remove the dismissed page
        /// </summary>
        private void CleanupDimissedPanel()
        {
            if (this.dismissedPanel is Panel panel)
            {
                this.dismissedPanel = null;
                this.RemoveVisualChild(panel);
            }
        }

        protected override int VisualChildrenCount {
            get
            {
                var count = 0;
                if (this.dismissedPanel is not null)
                {
                    ++count;
                }
                if (this.CurrentPanel is not null)
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
                return this.CurrentPanel!;
            }
            return this.dismissedPanel!;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (this.dismissedPanel is Panel dismissed)
            {
                dismissed.Arrange(new Rect(finalSize));
            }
            if (this.CurrentPanel is Panel current)
            {
                current.Arrange(new Rect(finalSize));
            }
            return finalSize;
        }


        public void CloseWindow()
        {
            Window.GetWindow(this)?.Close();
        }
    }

    public interface IStepPanel
    {
        StepFrame StepFrame { get; set; }
        event EventHandler? Completed;
    }
}
