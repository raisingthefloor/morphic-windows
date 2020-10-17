using System.Windows;

namespace Morphic.Client.Bar.UI
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media.Animation;
    using AppBarWindow;
    using BarControls;
    using Data;
    using global::Windows.Security.Authentication.Web.Provider;

    public partial class QuickHelpWindow : Window
    {
        public QuickHelpWindow()
        {
            this.DataContext = this;
            this.InitializeComponent();
        }

        static QuickHelpWindow()
        {
            QuickHelpWindow.ConfigureTooltips();
        }

        private static QuickHelpWindow? singleInstance;

        public static readonly DependencyProperty HeaderTextProperty = DependencyProperty.Register(nameof(HeaderText),
            typeof(string), typeof(QuickHelpWindow), new PropertyMetadata(default(string)));
        public static readonly DependencyProperty MessageTextProperty = DependencyProperty.Register(nameof(MessageText),
            typeof(string), typeof(QuickHelpWindow), new PropertyMetadata(default(string)));

        public string HeaderText
        {
            get => (string)this.GetValue(HeaderTextProperty);
            set => this.SetValue(HeaderTextProperty, value);
        }

        public string MessageText
        {
            get => (string)this.GetValue(MessageTextProperty);
            set => this.SetValue(MessageTextProperty, value);
        }

        /// <summary>Gets the singleton instance of the quick help window.</summary>
        public static QuickHelpWindow AddBar(BarWindow barWindow)
        {
            return singleInstance ??= new QuickHelpWindow();
        }

        /// <summary>
        /// Sets the tooltips to show instantly, for elements on a bar.
        /// </summary>
        internal static void ConfigureTooltips()
        {
            // Globally set the delay to zero.
            ToolTipService.InitialShowDelayProperty.OverrideMetadata(typeof(FrameworkElement),
                new FrameworkPropertyMetadata(
                    0,
                    FrameworkPropertyMetadataOptions.Inherits));

            // Handle the tooltip shown events
            EventManager.RegisterClassHandler(typeof(FrameworkElement),
                FrameworkElement.ToolTipOpeningEvent,
                new RoutedEventHandler((sender, args) =>
                {
                    if (args.Source is FrameworkElement element)
                    {
                        if (Window.GetWindow(element) is BarWindow barWindow)
                        {
                            QuickHelpWindow.singleInstance?.ShowHelp(barWindow, element);
                            args.Handled = true;
                        }
                    }
                }));

            // Set the tooltip delay to normal, for all controls not in a bar.
            EventManager.RegisterClassHandler(typeof(FrameworkElement),
                FrameworkElement.MouseEnterEvent,
                new RoutedEventHandler((sender, args) =>
                {
                    if (args.Source is FrameworkElement element)
                    {
                        if (element.ToolTip != null && !(Window.GetWindow(element) is BarWindow))
                        {
                            if (ToolTipService.GetInitialShowDelay(element) == 0)
                            {
                                ToolTipService.SetInitialShowDelay(element,
                                    (int)ToolTipService.InitialShowDelayProperty.DefaultMetadata.DefaultValue);
                            }
                        }
                    }
                }));

            EventManager.RegisterClassHandler(typeof(BarItemControl),
                FrameworkElement.MouseLeaveEvent,
                new RoutedEventHandler((sender, args) =>
                {
                    if (args.Source is BarItemControl control)
                    {
                        singleInstance?.ControlOnMouseLeave(sender, args);
                    }
                }));
        }

        private (string? header, string? text) GetHelpText(BarItem? barItem, string? controlTooltip)
        {
            // Tooltip from the control is like "header|text" or "text"
            string[] parts = controlTooltip?.Split('|', 2) ?? new string[0];
            string? controlHeader = null, controlText = null;
            if (parts.Length == 1)
            {
                controlText = parts[0];
            }
            else
            {
                controlHeader = parts[0];
                controlText = parts[1];
            }

            if (string.IsNullOrEmpty(controlHeader))
            {
                controlHeader = null;
            }

            if (string.IsNullOrEmpty(controlText))
            {
                controlText = null;
            }

            string? header = controlHeader ?? barItem?.ToolTipHeader ?? barItem?.Text;
            string? text = controlText ?? barItem?.ToolTip;

            if (header == text)
            {
                text = null;
            }

            if (header == null)
            {
                header = text;
                text = null;
            }

            return (header, text);
        }

        private bool wanted;

        /// <summary>
        /// Show the quick help for an item within a bar window.
        /// </summary>
        /// <param name="barWindow">The bar window.</param>
        /// <param name="element">The element.</param>
        private void ShowHelp(BarWindow barWindow, FrameworkElement element)
        {
            BarItemControl? control = element as BarItemControl ?? element.FindVisualParent<BarItemControl>();

            if (control == null)
            {
                this.HideHelp();
                return;
            }

            BarItem barItem = control.BarItem;

            (string? header, string? text) = this.GetHelpText(barItem, element.ToolTip?.ToString());
            this.HeaderText = header ?? string.Empty;
            this.MessageText = text ?? string.Empty;

            Console.WriteLine($"{this.HeaderText} # {this.MessageText}");

            this.wanted = true;
            this.BeginAnimation(Window.OpacityProperty, null);

            if (this.Visibility != Visibility.Visible)
            {
                this.SetPosition(barWindow, control, false);
                this.Show();
            }
            else
            {
                this.SetPosition(barWindow, control, true);
            }

            this.Opacity = 1;
        }

        /// <summary>
        /// Moves the window so it's adjacent to the bar, and centred over the control.
        /// </summary>
        /// <param name="barWindow"></param>
        /// <param name="control"></param>
        /// <param name="animate"></param>
        private void SetPosition(BarWindow barWindow, BarItemControl control, bool animate)
        {
            // Centre the window over the control.
            Point pos = control.PointToScreen(new Point(control.RenderSize.Width / 2, control.RenderSize.Height / 2));
            Rect rect = new Rect(pos.X - this.Width / 2, pos.Y - this.Height / 2, this.Width, this.Height);
            Rect barRect = barWindow.GetRect();

            Rect workArea = barWindow.GetWorkArea();

            // Make sure it's within the bar
            rect.X = rect.Width < barRect.Width
                ? Math.Clamp(rect.Left, barRect.Left, barRect.Right - rect.Width)
                : barRect.Left;
            rect.Y = rect.Height < barRect.Height
                ? Math.Clamp(rect.Top, barRect.Top, barRect.Bottom - rect.Height)
                : barRect.Top;

            // Make sure it's on the screen.
            rect.X = rect.Width < workArea.Width
                ? Math.Clamp(rect.Left, workArea.Left, workArea.Right - rect.Width)
                : workArea.Left;
            rect.Y = rect.Height < workArea.Height
                ? Math.Clamp(rect.Top, workArea.Top, workArea.Bottom - rect.Height)
                : workArea.Top;


            // Move the it next to the bar (the side that has most room)
            if (barWindow.Orientation == Orientation.Horizontal)
            {
                if (barRect.Top - workArea.Top  > workArea.Bottom - barRect.Bottom)
                {
                    rect.Y = barRect.Top - rect.Height;
                }
                else
                {
                    rect.Y = barRect.Bottom;
                }
            }
            else
            {
                if (barRect.Left - workArea.Left > workArea.Right - barRect.Bottom)
                {
                    rect.X = barRect.Left - rect.Width;
                }
                else
                {
                    rect.Y = barRect.Right;
                }
            }

            if (animate && !double.IsNaN(this.Left) && !double.IsNaN(this.Top))
            {

                DoubleAnimation xAnim = new DoubleAnimation()
                {
                    From = this.Left,
                    To = rect.Left,
                    Duration = new Duration(TimeSpan.FromMilliseconds(100)),
                    FillBehavior = FillBehavior.Stop
                };
                DoubleAnimation yAnim = new DoubleAnimation()
                {
                    From = this.Top,
                    To = rect.Top,
                    Duration = new Duration(TimeSpan.FromMilliseconds(100)),
                    FillBehavior = FillBehavior.Stop
                };

                this.BeginAnimation(Window.LeftProperty, xAnim);
                this.BeginAnimation(Window.TopProperty, yAnim);
            }
            else
            {
                this.Left = rect.Left;
                this.Top = rect.Top;
            }

        }

        private void ControlOnMouseLeave(object sender, RoutedEventArgs e)
        {
            this.HideHelp();
        }

        private void HideHelp(bool immediate = false)
        {
            this.wanted = false;
            if (immediate)
            {
                this.HeaderText = this.MessageText = string.Empty;
                this.Hide();
                this.Opacity = 1;
            }
            else
            {
                DoubleAnimation hideAnim = new DoubleAnimation()
                {
                    From = this.Opacity,
                    To = 0,
                    Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                    FillBehavior = FillBehavior.Stop,
                };

                hideAnim.Completed += (sender, args) =>
                {
                    if (!this.wanted)
                    {
                        this.HideHelp(true);
                    }
                };

                Task.Delay(200).ContinueWith(t =>
                {
                    if (!this.wanted)
                    {
                        this.Dispatcher.InvokeAsync(() => this.BeginAnimation(Window.OpacityProperty, hideAnim));
                    }
                });
            }
        }
    }
}

