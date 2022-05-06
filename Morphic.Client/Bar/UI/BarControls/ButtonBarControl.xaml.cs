﻿// BarButtonControl.xaml.cs: Control for Bar buttons.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

namespace Morphic.Client.Bar.UI.BarControls
{
    using System.ComponentModel;
    using System.Windows;
    using Data;

    /// <summary>
    /// The control for Button bar items.
    /// </summary>
    public partial class ButtonBarControl : BarItemControl
    {
        public ButtonBarControl(BarButton barItem) : base(barItem)
        {
            this.PropertyChanged += this.OnPropertyChanged;
            this.InitializeComponent();
            this.BarItem.PropertyChanged += (sender, args) =>
            {
                this.OnPropertyChanged(nameof(this.ButtonResource));
            };
        }

        public new BarButton BarItem => (BarButton) base.BarItem;

        /// <summary>
        /// The control in the resource library to use for the specific size of button.
        /// </summary>
        public object ButtonResource
        {
            get
            {
                BarItemSize size = this.ItemSize;
                if ((this.BarItem.ImageSource is null) && (this.BarItem.XamlContent is null))
                {
                    size = BarItemSize.TextOnly;
                }
                string resourceName = size + "Button";
                object resource = this.Resources[resourceName];
                return resource;
            }
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Update the button resource when the item size changes.
            if (e.PropertyName == nameof(this.ItemSize))
            {
                this.OnPropertyChanged(nameof(this.ButtonResource));
            }
        }

        private async void Button_OnClick(object sender, RoutedEventArgs e)
        {
            if (this.BarItem.Action is not null)
            {
                await this.BarItem.Action.InvokeAsync();
            }
        }
    }
    
}
