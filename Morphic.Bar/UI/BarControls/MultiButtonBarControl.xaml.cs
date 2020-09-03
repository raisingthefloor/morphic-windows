// BarMultiButtonControl.xaml.cs: Control for Bar images.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

namespace Morphic.Bar.UI.BarControls
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Automation.Peers;
    using System.Windows.Automation.Provider;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using System.Windows.Input;
    using Bar;
    using Bar.Actions;

    /// <summary>
    /// The control for Button bar items.
    /// </summary>
    public partial class MultiButtonBarControl : BarItemControl
    {
        public MultiButtonBarControl() : this(new BarMultiButton())
        {
        }

        public MultiButtonBarControl(BarMultiButton barItem) : base(barItem)
        {
            this.ApplyControlTheme(this);
            this.Buttons = this.BarItem.Buttons.Values.Select(b => new ButtonWrapper(this, b)).ToList();

            this.InitializeComponent();

            // Apply theming to the dynamic buttons when they're created.
            this.ButtonContainer.ItemContainerGenerator.StatusChanged += (sender, args) =>
            {
                if (this.ButtonContainer.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
                {
                    foreach (ButtonWrapper b in this.ButtonContainer.ItemContainerGenerator.Items)
                    {
                        ContentPresenter content = (ContentPresenter)this.ButtonContainer.ItemContainerGenerator.ContainerFromItem(b);
                        content.ApplyTemplate();

                        if (content.ContentTemplate.FindName("ControlButton", content) is Button control)
                        {
                            b.Control = control;
                            this.ApplyControlTheme(b.Control);
                        }
                    }
                }
            };

            // Set the navigation modes, depending on its type.
            bool isPair = (this.BarItem.Type == MultiButtonType.Toggle
                || this.BarItem.Type == MultiButtonType.Additive);

            // For keyboard navigation, paired buttons act as a single control
            this.Focusable = isPair;
            this.Panel.SetValue(FocusManager.IsFocusScopeProperty, isPair);
            this.Panel.SetValue(KeyboardNavigation.DirectionalNavigationProperty,
                isPair ? KeyboardNavigationMode.None : KeyboardNavigationMode.Continue);
            this.Panel.SetValue(KeyboardNavigation.TabNavigationProperty,
                isPair ? KeyboardNavigationMode.None : KeyboardNavigationMode.Continue);

            if (isPair)
            {
                this.KeyDown += this.OnKeyDown_ButtonPair;
            }

        }

        /// <summary>
        /// Activate one of the buttons of a pair, if -/+ is pressed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void OnKeyDown_ButtonPair(object sender, KeyEventArgs e)
        {
            int clickIndex = -1;

            switch (e.Key)
            {
                case Key.Subtract:
                case Key.OemMinus:
                    clickIndex = 0;
                    break;
                case Key.Add:
                case Key.OemPlus:
                    clickIndex = 1;
                    break;
            }

            if (clickIndex > -1)
            {
                if (this.Buttons[clickIndex].Control is Button button)
                {
                    // Make it look like it's being clicked.
                    this.ControlTheme[button].IsMouseDown = true;
                    this.UpdateTheme(button);

                    // Click it
                    ButtonAutomationPeer peer = new ButtonAutomationPeer(button);
                    ((IInvokeProvider?)peer.GetPattern(PatternInterface.Invoke))?.Invoke();

                    await Task.Delay(250);
                    this.ControlTheme[button].IsMouseDown = false;
                    this.UpdateTheme(button);
                }
            }
        }

        public new BarMultiButton BarItem => (BarMultiButton) base.BarItem;

        public List<ButtonWrapper> Buttons { get; set; }

        private void Button_OnClick(object sender, RoutedEventArgs e)
        {
            BarMultiButton.ButtonInfo? buttonInfo =
                this.Buttons.Where(b => b.Control == sender)
                    .Select(b => b.Button)
                    .FirstOrDefault();

            if (buttonInfo != null)
            {
                // Call either the bar item's action, or the button's own action if it has one.
                BarAction action = (buttonInfo.Action is NoOpAction)
                    ? this.BarItem.Action
                    : buttonInfo.Action;

                action.Invoke(buttonInfo.Value);
            }
        }

        /// <summary>
        /// Gets the text to be displayed, based on the given text.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        protected string GetDisplayText(string text)
        {
            return text switch
            {
                "+" => "\u2795",
                "-" => "\u2796",
                "||" => "\u258e \u258e",
                "|>" => "\u25b6",
                "[]" => "\u25a0",
                _ => text
            };
        }

        /// <summary>
        /// Wraps a button with its theming info.
        /// </summary>
        public class ButtonWrapper : INotifyPropertyChanged
        {
            public BarMultiButton.ButtonInfo Button { get; set; }

            public Theme ActiveTheme =>
                this.Control == null
                    ? this.itemControl.ActiveTheme
                    : this.itemControl.ControlTheme[this.Control].ActiveTheme;

            public Control? Control { get; set; }

            public string Text { get; set; }

            private readonly MultiButtonBarControl itemControl;

            public ButtonWrapper(MultiButtonBarControl itemControl, BarMultiButton.ButtonInfo buttonInfo)
            {
                this.itemControl = itemControl;
                this.Button = buttonInfo;
                this.Text = itemControl.GetDisplayText(this.Button.Text);
                // Update the theme when the property changes.
                this.itemControl.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == nameof(this.itemControl.ActiveTheme))
                    {
                        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(args.PropertyName));
                    }
                };
            }

            public event PropertyChangedEventHandler? PropertyChanged;
        }
    }
}
