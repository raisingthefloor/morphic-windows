// BarButtonControl.xaml.cs: Control for Bar buttons.
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
    using System.Windows;
    using Bar;

    /// <summary>
    /// The control for Button bar items.
    /// </summary>
    public partial class BarButtonControl : BarItemControl
    {
        public BarButtonControl() : this(new BarButton())
        {
        }

        public BarButtonControl(BarButton barItem) : base(barItem)
        {
            this.InitializeComponent();
        }

        public new BarButton BarItem => (BarButton) base.BarItem;

        private void Button_OnClick(object sender, RoutedEventArgs e)
        {
            this.BarItem.Action.Invoke();
        }
    }
    
}
