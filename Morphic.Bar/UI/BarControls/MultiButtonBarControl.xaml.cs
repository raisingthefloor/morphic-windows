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
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
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

                action.Invoke(buttonInfo.Id);
            }
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

            private readonly MultiButtonBarControl itemControl;

            public ButtonWrapper(MultiButtonBarControl itemControl, BarMultiButton.ButtonInfo buttonInfo)
            {
                this.itemControl = itemControl;
                this.Button = buttonInfo;
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
