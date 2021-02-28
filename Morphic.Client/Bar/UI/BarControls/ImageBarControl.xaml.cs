// BarImageControl.xaml.cs: Control for Bar images.
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
    using System.Windows;
    using Data;

    /// <summary>
    /// The control for Button bar items.
    /// </summary>
    public partial class ImageBarControl : BarItemControl
    {
        public ImageBarControl(BarImage barItem) : base(barItem)
        {
            this.InitializeComponent();
        }

        public new BarButton BarItem => (BarButton) base.BarItem;

        private async void Button_OnClick(object sender, RoutedEventArgs e)
        {
            if (this.BarItem.Action != null)
            {
                await this.BarItem.Action.InvokeAsync();
            }
        }
    }
    
}
