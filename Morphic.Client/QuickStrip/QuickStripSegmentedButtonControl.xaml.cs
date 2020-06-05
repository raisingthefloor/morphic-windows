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
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Controls;

namespace Morphic.Client
{
    /// <summary>
    /// Interaction logic for QuickStripSegmentedButtonControl.xaml
    /// </summary>
    public partial class QuickStripSegmentedButtonControl
    {

        public QuickStripSegmentedButtonControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// The color for primary action button segments
        /// </summary>
        public Brush PrimaryButtonBackground = new SolidColorBrush(Color.FromRgb(0, 129, 69));

        /// <summary>
        /// The color for secondary action button segments
        /// </summary>
        public Brush SecondaryButtonBackground = new SolidColorBrush(Color.FromRgb(102, 181, 90));

        private Style CreateBaseButtonStyle()
        {
            var style = new Style();
            var template = new ControlTemplate(typeof(ActionButton));
            var factory = new FrameworkElementFactory(typeof(Border));
            factory.SetBinding(Border.BackgroundProperty, new Binding { RelativeSource = RelativeSource.TemplatedParent, Path = new PropertyPath("Background") });
            factory.SetBinding(Border.PaddingProperty, new Binding { RelativeSource = RelativeSource.TemplatedParent, Path = new PropertyPath("Padding") });
            template.VisualTree = factory;
            factory.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
            style.Setters.Add(new Setter { Property = ActionButton.TemplateProperty, Value = template });
            style.Setters.Add(new Setter { Property = ActionButton.SnapsToDevicePixelsProperty, Value = true });
            style.Setters.Add(new Setter { Property = ActionButton.PaddingProperty, Value = new Thickness(7, 4, 7, 4) });
            style.Setters.Add(new Setter { Property = ActionButton.VerticalContentAlignmentProperty, Value = VerticalAlignment.Center });
            style.Setters.Add(new Setter { Property = ActionButton.HorizontalContentAlignmentProperty, Value = HorizontalAlignment.Center });
            style.Setters.Add(new Setter { Property = ActionButton.ForegroundProperty, Value = new SolidColorBrush(Color.FromRgb(255, 255, 255)) });
            style.Setters.Add(new Setter { Property = ActionButton.FontFamilyProperty, Value = SystemFonts.MessageFontFamily });
            style.Setters.Add(new Setter { Property = ActionButton.FontSizeProperty, Value = 14.0 });
            style.Setters.Add(new Setter { Property = ActionButton.FontWeightProperty, Value = FontWeight.FromOpenTypeWeight(700) });
            
            // Fade the button if it's disabled.
            var trigger = new Trigger { Property = ActionButton.IsEnabledProperty, Value = false };
            trigger.Setters.Add(new Setter{ Property =  ActionButton.OpacityProperty, Value = 0.5});
            style.Triggers.Add(trigger);

            return style;
        }

        private Style CreatePrimaryButtonStyle()
        {
            var style = CreateBaseButtonStyle();
            style.Setters.Add(new Setter { Property = ActionButton.BackgroundProperty, Value = PrimaryButtonBackground });
            var trigger = new Trigger { Property = ActionButton.IsPressedProperty, Value = true };
            trigger.Setters.Add(new Setter { Property = ActionButton.BackgroundProperty, Value = PrimaryButtonBackground.DarkenedByPercentage(0.4) });
            style.Triggers.Add(trigger);
            return style;
        }

        private Style CreateSecondaryButtonStyle()
        {
            var style = CreateBaseButtonStyle();
            style.Setters.Add(new Setter { Property = ActionButton.BackgroundProperty, Value = SecondaryButtonBackground });
            var trigger = new Trigger { Property = ActionButton.IsPressedProperty, Value = true };
            trigger.Setters.Add(new Setter { Property = ActionButton.BackgroundProperty, Value = SecondaryButtonBackground.DarkenedByPercentage(0.4) });
            style.Triggers.Add(trigger);
            return style;
        }

        /// <summary>
        /// Private backing value for <code>ShowsHelp</code>
        /// </summary>
        private bool showsHelp = true;

        override public bool ShowsHelp
        {
            get
            {
                return showsHelp;
            }
            set
            {
                showsHelp = value;
                foreach (var element in ActionStack.Children)
                {
                    if (element is ActionButton button)
                    {
                        button.ShowsHelp = showsHelp;
                    }
                }
            }
        }

        /// <summary>
        /// Toggles the enabled state of a button.
        /// </summary>
        /// <param name="index">The button index.</param>
        /// <param name="enabled">The enabled state.</param>
        public void EnableButton(int index, bool enabled)
        {
            this.ActionStack.Children[index].IsEnabled = enabled;
        }

        /// <summary>
        /// Add a text button segment to the control
        /// </summary>
        /// <param name="title">The title of the button</param>
        /// <param name="helpTitle">The title of the help window that appears on hover</param>
        /// <param name="helpMessage">The message in the help window that appears on hover</param>
        /// <param name="isPrimary">Indicates how the button should be styled</param>
        public void AddButton(string title, string? helpTitle, string? helpMessage, bool isPrimary)
        {
            AddButton(title as object, helpTitle, helpMessage, isPrimary);
        }

        /// <summary>
        /// Add an image button segment to the control
        /// </summary>
        /// <param name="image">The image of the button</param>
        /// <param name="helpTitle">The title of the help window that appears on hover</param>
        /// <param name="helpMessage">The message in the help window that appears on hover</param>
        /// <param name="isPrimary">Indicates how the button should be styled</param>
        public void AddButton(Image image, string? helpTitle, string? helpMessage, bool isPrimary)
        {
            image.Stretch = Stretch.None;
            AddButton(image as object, helpTitle, helpMessage, isPrimary);
        }

        /// <summary>
        /// Add a button segment to the control
        /// </summary>
        /// <param name="content">The content of the button, either text or image</param>
        /// <param name="helpTitle">The title of the help window that appears on hover</param>
        /// <param name="helpMessage">The message in the help window that appears on hover</param>
        /// <param name="isPrimary">Indicates how the button should be styled</param>
        private void AddButton(object content, string? helpTitle, string? helpMessage, bool isPrimary)
        {
            var button = new ActionButton();
            if (isPrimary)
            {
                button.Style = CreatePrimaryButtonStyle();
            }
            else
            {
                button.Style = CreateSecondaryButtonStyle();
            }
            button.Content = content;
            button.HelpTitle = helpTitle;
            button.HelpMessage = helpMessage;
            button.Click += Button_Click;
            ActionStack.Children.Add(button);
        }

        /// <summary>
        /// Called when any action button segment is clicked, will dispatch an <code>Action</code> event.
        /// </summary>
        /// <param name="sender">The button that was clicked</param>
        /// <param name="e">The event arguments</param>
        private void Button_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is ActionButton button)
            {
                var index = ActionStack.Children.IndexOf(button);
                var args = new ActionEventArgs(index);
                Action?.Invoke(this, args);
            }
        }

        /// <summary>
        /// The event arguments for <code>Action</code> events that are sent when a segment is clicked
        /// </summary>
        public class ActionEventArgs
        {
            /// <summary>
            /// The selected segment index, indicating which action button was clicked
            /// </summary>
            public int SelectedIndex { get; }

            /// <summary>
            /// Create a new args object with the given selected index
            /// </summary>
            /// <param name="selectedIndex">The selected segment index, indicating which action button was clicked</param>
            public ActionEventArgs(int selectedIndex)
            {
                SelectedIndex = selectedIndex;
            }
        }

        /// <summary>
        /// The <code>Action</code> event signature
        /// </summary>
        /// <param name="sender">The control that dispatched the event</param>
        /// <param name="e">The arguments indicating which button segment was clicked</param>
        public delegate void ActionHandler(object sender, ActionEventArgs e);

        /// <summary>
        /// The event that is dispatched when any action button segment is clicked
        /// </summary>
        public event ActionHandler? Action;

        /// <summary>
        /// A custom Button subclass that can show help text in a large window on hover
        /// </summary>
        private class ActionButton: Button
        {

            /// <summary>
            /// The title to show in the help window
            /// </summary>
            public string? HelpTitle { get; set; }

            /// <summary>
            /// The message to show in the help window
            /// </summary>
            public string? HelpMessage { get; set; }

            /// <summary>
            /// Indicates if the help window should be shown on hover
            /// </summary>
            public bool ShowsHelp { get; set; } = true;

            /// <summary>
            /// Create an action button
            /// </summary>
            public ActionButton(): base()
            {
                MouseEnter += OnMouseEnter;
                MouseLeave += OnMouseLeave;
            }

            /// <summary>
            /// Event handler for mouse enter
            /// </summary>
            /// <param name="sender"></param>
            /// <param name="e"></param>
            private void OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
            {
                if (ShowsHelp)
                {
                    if (HelpTitle is string title)
                    {
                        if (HelpMessage is string message)
                        {
                            QuickHelpWindow.Show(title, message);
                        }
                    }
                }
            }

            /// <summary>
            /// Event handler for mouse leave
            /// </summary>
            /// <param name="sender"></param>
            /// <param name="e"></param>
            private void OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
            {
                if (ShowsHelp)
                {
                    QuickHelpWindow.Dismiss();
                }
            }
        }
    }

    public static class BrushExtensions
    {
        public static Brush DarkenedByPercentage(this Brush brush, double percentage)
        {
            if (brush is SolidColorBrush colorBrush)
            {
                var color = colorBrush.Color;
                var darkenedColor = Color.FromRgb((byte)Math.Round((double)color.R * (1 - percentage)), (byte)Math.Round((double)color.G * (1 - percentage)), (byte)Math.Round((double)color.B * (1 - percentage)));
                return new SolidColorBrush(darkenedColor);
            }
            return brush;
        }

        public static Brush LightenedByPercentage(this Brush brush, double percentage)
        {
            if (brush is SolidColorBrush colorBrush)
            {
                var color = colorBrush.Color;
                var darkenedColor = Color.FromRgb((byte)(color.R + (byte)Math.Round((double)(255 - color.R) * (percentage))), (byte)(color.G + (byte)Math.Round((double)(255 - color.G) * (percentage))), (byte)(color.B + (byte)Math.Round((double)(255 - color.B) * (percentage))));
                return new SolidColorBrush(darkenedColor);
            }
            return brush;
        }
    }
}
