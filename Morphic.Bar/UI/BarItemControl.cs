// BarItemControl.cs: Base control for bar items.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

namespace Morphic.Bar.UI
{
    using System;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using System.Windows.Input;
    using Config;
    using UserControl = System.Windows.Controls.UserControl;

    /// <summary>
    /// A bar item control.
    /// </summary>
    public class BarItemControl : UserControl, INotifyPropertyChanged
    {
        private Theme activeTheme = null!;
        private bool isMouseDown;

        /// <summary>
        /// Create an instance of this class, using the given bar item.
        /// </summary>
        /// <param name="barItem">The bar item that this control displays.</param>
        public BarItemControl(BarItem barItem)
        {
            this.DataContext = this;
            this.BarItem = barItem;
            this.ActiveTheme = barItem.Theme;

            // Some events to monitor the state.
            this.MouseEnter += (sender, args) => this.UpdateTheme();
            this.MouseLeave += (sender, args) =>
            {
                this.CheckMouseState(sender, args);
                this.UpdateTheme();
            };

            this.PreviewMouseDown += this.CheckMouseState;
            this.PreviewMouseUp += this.CheckMouseState;

            this.IsKeyboardFocusWithinChanged += (sender, args) =>
            {
                this.FocusedByKeyboard = this.IsKeyboardFocusWithin &&
                                         InputManager.Current.MostRecentInputDevice is KeyboardDevice;
                this.UpdateTheme();
            };
        }


        public BarItemControl() : this(new BarItem())
        {
        }

        /// <summary>
        /// The bar item represented by this control.
        /// </summary>
        public BarItem BarItem { get; }

        /// <summary>Tool tip header - the name of the item, if the tooltip info isn't specified.</summary>
        public string? ToolTipHeader
            => string.IsNullOrEmpty(this.BarItem.ToolTipInfo) ? this.BarItem.Text : this.BarItem.ToolTip;

        /// <summary>Tool tip text.</summary>
        public string? ToolTipText
            => string.IsNullOrEmpty(this.BarItem.ToolTipInfo) ? this.BarItem.ToolTip : this.BarItem.ToolTipInfo;

        /// <summary>
        /// Current theme to use, depending on the state (normal/hover/focus).
        /// </summary>
        public Theme ActiveTheme
        {
            get => this.activeTheme;
            set
            {
                this.activeTheme = value;
                this.OnPropertyChanged();
            }
        }

        public bool IsMouseDown
        {
            get => this.isMouseDown;
            private set
            {
                if (this.isMouseDown != value)
                {
                    this.isMouseDown = value;
                    this.UpdateTheme();
                }
            }
        }

        /// <summary>true if the last focus was performed by the keyboard.</summary>
        public bool FocusedByKeyboard { get; set; }

        private void CheckMouseState(object sender, MouseEventArgs mouseEventArgs)
        {
            this.IsMouseDown = mouseEventArgs.LeftButton == MouseButtonState.Pressed;
        }

        /// <summary>
        /// Creates a control for the given bar item.
        /// </summary>
        /// <param name="item"></param>
        /// <returns>The control for the item, the type depends on the item.</returns>
        public static BarItemControl From(BarItem item)
        {
            return (Activator.CreateInstance(item.ControlType, item) as BarItemControl)!;
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
                theme.Apply(this.BarItem.Theme.Active);
            }

            if (this.IsMouseOver)
            {
                theme.Apply(this.BarItem.Theme.Hover);
            }

            if (this.IsKeyboardFocusWithin && this.FocusedByKeyboard)
            {
                theme.Apply(this.BarItem.Theme.Focus);
            }

            this.ActiveTheme = theme.Apply(this.BarItem.Theme);
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
