using System.Windows;

namespace Morphic.Client.Bar.UI
{
    using BarControls;
    using Data;
    using Dialogs.Elements;
    using Morphic.Client.Bar.Data.Actions;
    using Settings.SettingsHandlers;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Windows.Controls;
    using System.Windows.Media.Animation;

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
                new RoutedEventHandler(async (sender, args) =>
                {
                    if (args.Source is FrameworkElement element)
                    {
                        if (Window.GetWindow(element) is BarWindow barWindow)
                        {
                            var singleInstance = QuickHelpWindow.singleInstance;
                            if (singleInstance is not null) {
                                await singleInstance.ShowHelpAsync(barWindow, element);
                            }
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
                        if (element.ToolTip is not null && !(Window.GetWindow(element) is BarWindow))
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

        private (string? header, string? text, string? error) GetHelpText(BarItem? barItem, string? controlTooltip)
        {
            // Tooltip from the control is like "header|text|error", "header|text" or "text"
            string[] parts = controlTooltip?.Split('|', 3) ?? new string[0];
            string? controlHeader = null, controlText = null, errorText = null;
            if (parts.Length == 1)
            {
                controlText = parts[0];
            }
            else if (parts.Length >= 2)
            {
                controlHeader = parts[0];
                controlText = parts[1];
				//
	            if (parts.Length >= 3)
    	        {
                	errorText = parts[2];
	            }
            }

            if (string.IsNullOrEmpty(controlHeader))
            {
                controlHeader = null;
            }

            if (string.IsNullOrEmpty(controlText))
            {
                controlText = null;
            }

            if (string.IsNullOrEmpty(errorText))
            {
                errorText = null;
            }

            string? header = controlHeader ?? barItem?.ToolTipHeader;
            string? text = controlText ?? barItem?.ToolTip;
            string? error = errorText; // OBSERVATION: this "errorText" is generally a limit message (e.g. "cannot go further")

            if (header == text)
            {
                text = null;
            }

            if (header is null && text is not null)
            {
                header = barItem?.Text;
                if (header is null)
                {
                    header = text;
                    text = null;
                }
            }

            return (header, text, error);
        }

        private bool wanted;

        /// <summary>
        /// Show the quick help for an item within a bar window.
        /// </summary>
        /// <param name="barWindow">The bar window.</param>
        /// <param name="element">The element.</param>
        private async Task ShowHelpAsync(BarWindow barWindow, FrameworkElement element)
        {
            BarItemControl? control = element as BarItemControl ?? element.FindVisualParent<BarItemControl>();

            if (control is null)
            {
                this.HideHelp();
                return;
            }

            BarItem barItem = control.BarItem;

            (string? header, string? text, string? error) = this.GetHelpText(barItem, element.ToolTip?.ToString());

            if (string.IsNullOrEmpty(header))
            {
                this.HideHelp();
                return;
            }

            // NOTE: by default, we'll show the default tooltip text; but if our control is at its limit (or we otherwise use the "error" message for this scenario) then we will show the error instead.
            var showErrorMessage = false;

            BarMultiButton.ButtonInfo? buttonInfo = null;
            if (control is MultiButtonBarControl multiButtonBarControl)
            {
                // find our multi-button's segment (button) info
                foreach (var button in multiButtonBarControl.Buttons)
                {
                    if (button.Control == element)
                    {
                        buttonInfo = button.Button;
                    }
                }

                // determine if we should show the error/limit message
                if (buttonInfo is not null)
                {
                    if (buttonInfo.Action is SettingAction settingAction)
                    {
                        if ((await settingAction.CanExecute(buttonInfo.Id)) == false)
                        {
                            showErrorMessage = true;
                        }
                    }
                }
            }
            // OBSERVATION: we don't currently have a method for getting the buttonInfo of a single-segment control; since the sub-controls could be broken out into individual controls in the future, we should add this ability

            if (showErrorMessage == true)
            {
                this.HeaderText = error ?? string.Empty;
                this.MessageText = string.Empty;
            }
            else
            {
                this.HeaderText = header ?? string.Empty;
                this.MessageText = text ?? string.Empty;
            }

            this.ShowRangeControl(barItem);

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

        private Setting? GetSetting(BarItem barItem)
        {
            Setting? setting = null;

            if (barItem is BarSettingItem settingItem && settingItem.SettingId is not null)
            {
                try
                {
                    setting = settingItem.Solutions.GetSetting(settingItem.SettingId);
                }
                catch (KeyNotFoundException)
                {
                    // ignore
                }
            }

            return setting;
        }

        private async void ShowRangeControl(BarItem barItem)
        {
            this.RangeContainer.Children.Clear();

            var setting = this.GetSetting(barItem);
            if (setting?.Range is not null)
            {
                var control = await this.GetRangeControl(setting, setting.Range);
                this.RangeContainer.Children.Add(control);
            }
        }

        private async Task<Control> GetRangeControl(Setting setting, SettingRange range)
        {
            PagerControl pager = new PagerControl();

            pager.Height = 15;

            void SettingChanged(object? sender, SettingEventArgs e)
            {
                this.UpdatePager(pager, setting, range);
            }

            setting.Changed += SettingChanged;
            pager.Unloaded += (sender, args) => setting.Changed -= SettingChanged;
            
            // OBSERVATION: we are not checking for success/failure from the GetValueAsync method (or even capturing the 'gotten' value); this is presumably being called for its side-effects (which are presumably caching)
            await setting.GetValueAsync()
                .ContinueWith(_ => this.Dispatcher.Invoke(() => this.UpdatePager(pager, setting, range)));

            return pager;
        }

        private async void UpdatePager(PagerControl pager, Setting setting, SettingRange range)
        {
            // NOTE: we should add a property to the solutions registry which indicates that this needs to be refreshed every time
            //       _and/or_ we need to have the native handler itself send us a message when it needs updated (such as after a display 
            //       resolution change)
            var idRequiresCountRefresh = QuickHelpWindow.SettingRequiresCountRefresh(setting);

            if ((idRequiresCountRefresh == true) || (pager.CurrentPage == -1))
            {
                var min = range.GetMin(0, idRequiresCountRefresh);
                var max = range.GetMax(0, idRequiresCountRefresh);
                pager.Offset = await min;
                pager.NumberOfPages = await max;
            }

            int value = setting.CurrentValue as int? ?? default;
            pager.CurrentPage = value;
        }

        private static bool SettingRequiresCountRefresh(Setting setting)
        {
            switch (setting.Id)
            {
                case "zoom":
                    return true;
                default:
                    return false;
            }
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
                if (barRect.Top - workArea.Top > workArea.Bottom - barRect.Bottom)
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

