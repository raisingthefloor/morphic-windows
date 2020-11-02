namespace Morphic.Client.Bar.UI.BarControls
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using System.Windows.Input;
    using System.Windows.Media;
    using Data;

    public class ThemeHandler
    {
        public Control Control { get; }
        public BarItemTheme Theme { get; }

        /// <summary>
        /// Current theme to use, depending on the state (normal/hover/focus).
        /// </summary>
        public Theme ActiveTheme { get; private set;}
        public bool IsMouseDown { get;set;}
        public bool FocusedByKeyboard { get;set;}
        public bool IsChecked { get; set; }

        public event EventHandler<ThemeEventArgs>? ThemeStateChanged;

        public ThemeHandler(Control control, BarItemTheme theme)
        {
            this.Control = control;
            this.Theme = theme;
            this.ActiveTheme = this.Theme;
            if (this.Control.IsLoaded)
            {
                this.ControlOnLoaded(this.Control, EventArgs.Empty);
            }
            else
            {
                this.Control.Loaded += this.ControlOnLoaded;
            }
        }

        private void ControlOnLoaded(object sender, EventArgs e)
        {
            this.ApplyTheme();
            this.UpdateTheme();
        }

        /// <summary>
        /// Apply theming to the control.
        /// </summary>
        /// <param name="control"></param>
        protected void ApplyTheme()
        {
            // Some events to monitor the state.
            this.Control.MouseEnter += (sender, args) => this.UpdateTheme();
            this.Control.MouseLeave += (sender, args) =>
            {
                this.CheckMouseState(sender, args);
                this.UpdateTheme();
            };

            this.Control.PreviewMouseDown += this.CheckMouseState;
            this.Control.PreviewMouseUp += this.CheckMouseState;

            this.Control.IsKeyboardFocusWithinChanged += (sender, args) =>
            {
                this.FocusedByKeyboard = this.Control.IsKeyboardFocusWithin &&
                    InputManager.Current.MostRecentInputDevice is KeyboardDevice;
                this.UpdateTheme();
            };

            if (this.Control is ToggleButton button)
            {
                this.IsChecked = button.IsChecked == true;
                button.Checked += this.ButtonCheckedChange;
                button.Unchecked += this.ButtonCheckedChange;
            }
        }

        private void ButtonCheckedChange (object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton button)
            {
                this.IsChecked = button.IsChecked ?? false;
                this.UpdateTheme();
            }
        }


        /// <summary>
        /// Update the theme depending on the current state of the control.
        /// </summary>
        public void UpdateTheme()
        {
            Theme theme = new Theme();

            // Apply the applicable states, most important first.
            if (this.IsMouseDown)
            {
                theme.Apply(this.Theme.Active);
            }

            if (this.Control.IsMouseOver)
            {
                theme.Apply(this.Theme.Hover);
            }

            if (this.Control.IsKeyboardFocusWithin && this.FocusedByKeyboard)
            {
                theme.Apply(this.Theme.Focus);
            }

            if (this.IsChecked)
            {
                theme.Apply(this.Theme.Checked);
            }

            //this.ActiveTheme = theme.Apply(this.Theme);
            this.ActiveTheme = theme.Apply(this.Theme);

            this.ThemeStateChanged?.Invoke(this, new ThemeEventArgs(this.ActiveTheme));

            // Update the brush used by a mono drawing image.
            if (this.DrawingBrush != null && this.ActiveTheme.Background.HasValue)
            {
                this.DrawingBrush.Color = this.ActiveTheme.Background.Value;
            }
        }

        public SolidColorBrush? DrawingBrush { get; }


        private void CheckMouseState(object sender, MouseEventArgs mouseEventArgs)
        {
            bool last = this.IsMouseDown;
            this.IsMouseDown = mouseEventArgs.LeftButton == MouseButtonState.Pressed;
            if (last != this.IsMouseDown)
            {
                this.UpdateTheme();
            }
        }

    }

    public class ThemeEventArgs : EventArgs
    {
        public Theme ActiveTheme { get; }

        public ThemeEventArgs(Theme activeTheme)
        {
            this.ActiveTheme = activeTheme;
        }
    }
}
