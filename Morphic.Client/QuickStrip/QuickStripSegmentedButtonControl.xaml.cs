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
using System.Windows.Automation.Peers;
using System.Windows.Automation;
using System.Linq;
using Morphic.Client.QuickStrip;

namespace Morphic.Client
{
    using System.Diagnostics;
    using System.Windows.Controls.Primitives;
    using System.Windows.Input;
    using Core;
    using Service;
    using Binding = System.Windows.Data.Binding;
    using Button = System.Windows.Controls.Button;
    using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;
    using Control = System.Windows.Controls.Control;
    using HorizontalAlignment = System.Windows.HorizontalAlignment;
    using VerticalAlignment = System.Windows.VerticalAlignment;

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

        public int ItemCount = 2;

        private Style CreateBaseButtonStyle()
        {
            var style = new Style();
            var template = new ControlTemplate(typeof(ButtonBase));
            var factory = new FrameworkElementFactory(typeof(Border));
            factory.SetBinding(Border.BackgroundProperty, new Binding { RelativeSource = RelativeSource.TemplatedParent, Path = new PropertyPath("Background") });
            factory.SetBinding(Border.PaddingProperty, new Binding { RelativeSource = RelativeSource.TemplatedParent, Path = new PropertyPath("Padding") });
            template.VisualTree = factory;
            factory.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
            style.Setters.Add(new Setter { Property = ButtonBase.TemplateProperty, Value = template });
            style.Setters.Add(new Setter { Property = ButtonBase.SnapsToDevicePixelsProperty, Value = true });
            style.Setters.Add(new Setter { Property = ButtonBase.PaddingProperty, Value = new Thickness(12, 4, 12, 4) });
            style.Setters.Add(new Setter { Property = ButtonBase.VerticalContentAlignmentProperty, Value = VerticalAlignment.Center });
            style.Setters.Add(new Setter { Property = ButtonBase.HorizontalContentAlignmentProperty, Value = HorizontalAlignment.Center });
            style.Setters.Add(new Setter { Property = ButtonBase.ForegroundProperty, Value = new SolidColorBrush(Color.FromRgb(255, 255, 255)) });
            style.Setters.Add(new Setter { Property = ButtonBase.FontFamilyProperty, Value = SystemFonts.MessageFontFamily });
            style.Setters.Add(new Setter { Property = ButtonBase.FontSizeProperty, Value = 14.0 });
            style.Setters.Add(new Setter { Property = ButtonBase.FontWeightProperty, Value = FontWeight.FromOpenTypeWeight(700) });
            
            // Fade the button if it's disabled.
            var trigger = new Trigger { Property = ButtonBase.IsEnabledProperty, Value = false };
            trigger.Setters.Add(new Setter{ Property =  ButtonBase.OpacityProperty, Value = 0.5});
            style.Triggers.Add(trigger);

            // Colour the button if it's checked.
            var triggerChecked = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
            triggerChecked.Setters.Add(new Setter{ Property =  ButtonBase.BackgroundProperty, Value = new SolidColorBrush(Colors.Black) });
            style.Triggers.Add(triggerChecked);

            CornerRadius radius = new CornerRadius(0);
            double radiusSize = 3;
            if (this.ActionStack.Children.Count == 0)
            {
                radius.TopLeft = radius.BottomLeft = radiusSize;
            }
            if (this.ActionStack.Children.Count == this.ItemCount - 1)
            {
                radius.TopRight = radius.BottomRight = radiusSize;
            }

            factory.SetValue(Border.CornerRadiusProperty, radius);

            return style;
        }

        private Style CreatePrimaryButtonStyle()
        {
            var style = CreateBaseButtonStyle();
            style.Setters.Add(new Setter { Property = ButtonBase.BackgroundProperty, Value = PrimaryButtonBackground });
            var trigger = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
            trigger.Setters.Add(new Setter { Property = ButtonBase.BackgroundProperty, Value = PrimaryButtonBackground.DarkenedByPercentage(0.4) });
            style.Triggers.Add(trigger);
            return style;
        }
        private Style CreatePrimaryToggleStyle()
        {
            var style = CreateBaseButtonStyle();
            style.Setters.Add(new Setter { Property = ToggleButton.BackgroundProperty, Value = PrimaryButtonBackground });
            var trigger = new Trigger { Property = ToggleButton.IsPressedProperty, Value = true };
            trigger.Setters.Add(new Setter { Property = ToggleButton.BackgroundProperty, Value = PrimaryButtonBackground.DarkenedByPercentage(0.4) });
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
                    if (element is IActionControl button)
                    {
                        button.Helper.ShowsHelp = showsHelp;
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
        /// Updates the state of the buttons.
        /// </summary>
        public void UpdateStates()
        {
            foreach (QsToggleButton toggleButton in this.ActionStack.Children.OfType<QsToggleButton>())
            {
                toggleButton.UpdateState();
            }
        }

        public QsToggleButton AddToggle(string title, string automationName, IQuickHelpControlBuilder? helpBuilder)
        {
            return (QsToggleButton)this.AddButton(title as object, automationName, helpBuilder, true);
        }

        /// <summary>
        /// Add a text button segment to the control
        /// </summary>
        /// <param name="title">The title of the button</param>
        /// <param name="helpTitle">The title of the help window that appears on hover</param>
        /// <param name="helpMessage">The message in the help window that appears on hover</param>
        /// <param name="isPrimary">Indicates how the button should be styled</param>
        public ActionButton AddButton(string title, string automationName, IQuickHelpControlBuilder? helpBuilder, bool isPrimary)
        {
            return (ActionButton)this.AddButton(title as object, automationName, helpBuilder);
        }


        /// <summary>
        /// Add an image button segment to the control
        /// </summary>
        /// <param name="image">The image of the button</param>
        /// <param name="helpTitle">The title of the help window that appears on hover</param>
        /// <param name="helpMessage">The message in the help window that appears on hover</param>
        /// <param name="isPrimary">Indicates how the button should be styled</param>
        public ActionButton AddButton(Image image, string automationName, IQuickHelpControlBuilder? helpBuilder, bool isPrimary)
        {
            image.Stretch = Stretch.None;
            return (ActionButton)this.AddButton(image as object, automationName, helpBuilder);
        }

        /// <summary>
        /// Add a button segment to the control
        /// </summary>
        /// <param name="content">The content of the button, either text or image</param>
        /// <param name="helpTitle">The title of the help window that appears on hover</param>
        /// <param name="helpMessage">The message in the help window that appears on hover</param>
        /// <param name="isToggle">true if this button is a toggle button.</param>
        private ButtonBase AddButton(object content, string automationName, IQuickHelpControlBuilder? helpBuilder, bool isToggle = false)
        {
            ButtonBase button = isToggle ? (ButtonBase)new QsToggleButton(helpBuilder) : new ActionButton(helpBuilder);

            // We started with a design that had two styles of buttons: primary and secondary
            // They featured different background shades, but the lighter shade was too low of
            // contrast wit the white text for users who need high contrast.  So we're now
            // using only the primary style and adding a space in between the buttons 
            button.Style = isToggle ? CreatePrimaryToggleStyle() : CreatePrimaryButtonStyle();
            button.FocusVisualStyle = this.Resources["ButtonFocusStyle"] as Style;
            button.Content = content;
            AutomationProperties.SetName(button, automationName);
            button.Click += Button_Click;

            if (button is ToggleButton toggleButton)
            {
                toggleButton.Checked += Button_Toggled;
                toggleButton.Unchecked += Button_Toggled;
            }

            if (ActionStack.Children.Count > 0)
            {
                button.Margin = new Thickness(1, 0, 0, 0);
            }
            ActionStack.Children.Add(button);

            ((IActionControl)button).Helper.SetContextItems(this.Settings, this.LearnMore, this.Demo);
            return button;
        }

        private void Button_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton button)
            {
                if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                {
                    // ignore shift-click
                    button.IsChecked = !button.IsChecked;
                }
                else
                {
                    var index = ActionStack.Children.IndexOf(button);
                    var args = new ActionEventArgs(index, button.IsChecked == true);
                    Toggled?.Invoke(this, args);
                }
            }
        }

        /// <summary>
        /// Called when any action button segment is clicked, will dispatch an <code>Action</code> event.
        /// </summary>
        /// <param name="sender">The button that was clicked</param>
        /// <param name="e">The event arguments</param>
        private void Button_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is ButtonBase button)
            {
                QsControlHelper? helper = (button is IActionControl qsControl)
                    ? qsControl.Helper
                    : null;

                if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                {
                    helper?.ShowContextMenu();
                }
                else
                {
                    var index = ActionStack.Children.IndexOf(button);
                    var args = new ActionEventArgs(index, (button as ToggleButton)?.IsChecked == true);
                    Action?.Invoke(this, args);
                    helper?.UpdateHelp();
                }
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
            /// The toggle state, for toggle buttons.
            /// </summary>
            public bool ToggleState { get; }

            /// <summary>
            /// Create a new args object with the given selected index
            /// </summary>
            /// <param name="selectedIndex">The selected segment index, indicating which action button was clicked</param>
            /// <param name="toggleState">true if the button is 'on'</param>
            public ActionEventArgs(int selectedIndex, bool toggleState = false)
            {
                SelectedIndex = selectedIndex;
                ToggleState = toggleState;
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
        /// The event that is dispatched when any toggle button segment is toggled
        /// </summary>
        public event ActionHandler? Toggled;

        /// <summary>
        /// A custom Button subclass that can show help text in a large window on hover
        /// </summary>
        public class ActionButton : Button, IActionControl
        {
            public ActionButton(IQuickHelpControlBuilder? helpBuilder)
            {
                this.Helper = new QsControlHelper(this, helpBuilder);
            }

            public QsControlHelper Helper { get; set; }
        }

        public class QsToggleButton : ToggleButton, IActionControl
        {
            /// <summary>The setting.</summary>
            public Preferences.Key PreferenceKey { get; set; }
            /// <summary>The session.</summary>
            public Session? Session { get; set; }
            public bool AutoUpdate { get; set; }

            /// <summary>The value to use when the button is checked.</summary>
            public object OnValue { get; set; }
            /// <summary>The value to use when the button is unchecked.</summary>
            public object OffValue { get; set; }

            public QsControlHelper Helper { get; set; }

            public QsToggleButton(IQuickHelpControlBuilder? helpBuilder)
            {
                this.Helper = new QsControlHelper(this, helpBuilder);
            }

            /// <summary>
            /// Update the value of the toggle button.
            /// </summary>
            public async void UpdateState()
            {
                if (this.AutoUpdate && this.Session != null)
                {
                    object value = await this.Session.SettingsManager.Capture(this.PreferenceKey) ?? false;

                    bool check = value.Equals(this.OnValue);

                    if (this.IsChecked != check)
                    {
                        this.IsChecked = check;
                    }
                }
            }

            /// <summary>
            /// Automatically set a setting for the toggle button.
            /// </summary>
            public QsToggleButton Automate(Session session, Preferences.Key pref, bool autoUpdate = true, object? onValue = null, object? offValue = null)
            {
                this.OnValue = onValue ?? true;
                this.OffValue = offValue ?? false;
                this.Session = session;
                this.PreferenceKey = pref;
                this.AutoUpdate = autoUpdate;

                this.Click += this.OnClick;
                this.UpdateState();

                return this;
            }

            private async void OnClick(object sender, RoutedEventArgs e)
            {
                if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                {
                    this.Helper.ShowContextMenu();
                }
                else if (this.Session != null)
                {
                    object value = this.IsChecked == true ? this.OnValue : this.OffValue;
                    await this.Session.Apply(this.PreferenceKey, value);
                    this.UpdateState();
                }
            }
        }

        public interface IActionControl
        {
            QsControlHelper Helper { get; }
        }

        /// <summary>
        /// Used by the qs controls to display the help pop-up, and context menu.
        /// </summary>
        public class QsControlHelper
        {

            /// <summary>
            /// The title to show in the help window
            /// </summary>
            public IQuickHelpControlBuilder? HelpBuilder { get; set; }

            /// <summary>
            /// Indicates if the help window should be shown on hover
            /// </summary>
            public bool ShowsHelp { get; set; } = true;
            public Control Control { get; set; }

            /// <summary>
            /// Create an action button
            /// </summary>
            public QsControlHelper(Control control, IQuickHelpControlBuilder? helpBuilder): base()
            {
                this.Control = control;
                this.HelpBuilder = helpBuilder;
                control.MouseEnter += OnMouseEnter;
                control.MouseLeave += OnMouseLeave;
                control.MouseUp += OnMouseUp;
                control.GotKeyboardFocus += OnGotKeyboardFocus;
                control.KeyDown += OnKeyDown;
            }

            private void OnKeyDown(object sender, KeyEventArgs e)
            {
                switch (e.Key)
                {
                    case Key.Apps:
                    case Key.F10 when Keyboard.Modifiers == ModifierKeys.Shift:
                    case Key.Space when Keyboard.Modifiers == ModifierKeys.Shift:
                    case Key.Enter when Keyboard.Modifiers == ModifierKeys.Shift:
                        this.ShowContextMenu();
                        e.Handled = true;
                        break;
                }
            }


            private void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
            {
                if (InputManager.Current.MostRecentInputDevice is KeyboardDevice)
                {
                    UpdateHelp();
                }
            }

            /// <summary>
            /// Event handler for mouse enter
            /// </summary>
            /// <param name="sender"></param>
            /// <param name="e"></param>
            private void OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
            {
                UpdateHelp();
            }

            internal void UpdateHelp()
            {
                if (ShowsHelp)
                {
                    if (HelpBuilder is IQuickHelpControlBuilder builder)
                    {
                        QuickHelpWindow.Show(builder);
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

            private void OnMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
            {
                if (e.ChangedButton == MouseButton.Right)
                {
                    this.ShowContextMenu();
                    e.Handled = true;
                }
            }

            #region Context menu

            public ContextMenu? ContextMenu { get; private set; }
            public string? Settings { get; set; }
            public string? Demo { get; set; }
            public string? LearnMore { get; set; }

            private static readonly string SettingsBase = "ms-settings:";
            private static readonly string DemoBase = "https://morphic.world/demo/";
            private static readonly string LearnMoreBase = "https://morphic.world/basics/learn-more-about-quickstrip#";

            public void SetContextItems(string? settings = null, string? learnMore = null, string? demo = null)
            {
                if (settings == null && learnMore == null && demo == null)
                {
                    this.ContextMenu = null;
                    return;
                }

                if (learnMore != null)
                {
                    this.LearnMore = learnMore;
                }
                if (demo != null)
                {
                    this.Demo = demo;
                }
                if (settings != null)
                {
                    this.Settings = settings;
                }

                ContextMenu menu = new ContextMenu();

                MenuItem NewItem(string header, string? value)
                {
                    MenuItem item = new MenuItem()
                    {
                        Header = header,
                        Tag = value
                    };

                    menu.Items.Add(item);
                    return item;
                };

                NewItem("Learn more", learnMore).Click += this.LearnMore_Click;
                NewItem("Quick demo video", demo).Click += this.Demo_Click;
                NewItem("Settings", settings).Click += this.Settings_Click;

                bool hasEnabled = false;

                foreach (MenuItem item in menu.Items.OfType<MenuItem>())
                {
                    item.IsEnabled = item.Tag != null;
                    if (item.IsEnabled)
                    {
                        hasEnabled = true;
                    }
                }
                
                if (hasEnabled)
                {
                    this.ContextMenu = menu;
                    this.ContextMenu.Placement = PlacementMode.Top;
                    this.ContextMenu.PlacementTarget = this.Control;
                }
                else
                {
                    this.ContextMenu = null;
                }
            }

            /// <summary>
            /// Show the context menu for this control.
            /// </summary>
            public void ShowContextMenu()
            {
                if (this.ContextMenu != null)
                {
                    this.ContextMenu.IsOpen = true;
                }
            }

            /// <summary>
            /// "Learn more" menu item click.
            /// </summary>
            private void LearnMore_Click(object sender, RoutedEventArgs e)
            {
                this.OpenUrl(QsControlHelper.LearnMoreBase, this.LearnMore);
            }

            /// <summary>
            /// "Quick demo video" menu item click.
            /// </summary>
            private void Demo_Click(object sender, RoutedEventArgs e)
            {
                this.OpenUrl(QsControlHelper.DemoBase, this.Demo);
            }

            /// <summary>
            /// "Settings" menu item click.
            /// </summary>
            private void Settings_Click(object sender, RoutedEventArgs e)
            {
                this.OpenUrl(QsControlHelper.SettingsBase, this.Settings);
            }

            /// <summary>
            /// Opens a url (by executing it via the shell)
            /// </summary>
            /// <param name="baseUrl"></param>
            /// <param name="extra"></param>
            private void OpenUrl(string baseUrl, string? extra)
            {
                if (!string.IsNullOrEmpty(extra))
                {
                    string finalUrl = extra.StartsWith('!')
                        ? extra.Substring(1)
                        : baseUrl + extra;
                    Process.Start(new ProcessStartInfo(finalUrl)
                    {
                        UseShellExecute = true
                    });
                }
            }

            #endregion
        }

        public string? Settings { get; set; }
        public string? Demo { get; set; }
        public string? LearnMore { get; set; }

        /// <summary>
        /// Sets the context items for the child controls
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="learnMore"></param>
        /// <param name="demo"></param>
        public void SetContextItems(string? settings = null, string? learnMore = null, string? demo = null)
        {
            this.LearnMore = learnMore;
            this.Demo = demo;
            this.Settings = settings;

            foreach (IActionControl control in this.ActionStack.Children.OfType<IActionControl>())
            {
                control.Helper.SetContextItems(settings, learnMore, demo);
            }

        }

        /// <summary>
        /// Add some space between the buttons
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public void SpaceButtons()
        {
            foreach (ButtonBase control in this.ActionStack.Children.OfType<ButtonBase>().Skip(1))
            {
                Thickness margin = control.Margin;
                margin.Left++;
                control.Margin = margin;
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
