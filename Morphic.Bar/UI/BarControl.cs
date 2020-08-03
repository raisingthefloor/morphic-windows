// BarControl.cs: The control for a bar.
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
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using Bar;

    /// <summary>
    /// This is the thing that contains bar items.
    /// </summary>
    public class BarControl : WrapPanel, INotifyPropertyChanged
    {
        private double tallestItem;

        public BarControl()
        {
            this.Bar = new BarData();
            this.LayoutUpdated += this.OnLayoutUpdated;
        }

        public BarData Bar { get; private set; }
        public bool IsPrimary { get; set; }

        public bool FixedSize => this.IsPrimary && this.Bar.Columns != 0;
        public bool IsHorizontal { get; set; }

        public double Scale { get; set; }

        public void ApplyScale()
        {
            this.LayoutTransform = new ScaleTransform(this.Scale, this.Scale);
        }
        
        public double ScaledItemWidth => Math.Ceiling(this.ItemWidth * this.Scale);
        public double ScaledItemHeight => Math.Ceiling(this.ItemHeight * this.Scale);

        public event PropertyChangedEventHandler? PropertyChanged;

        public event EventHandler? BarLoaded;

        private void OnLayoutUpdated(object? sender, EventArgs e)
        {
            this.tallestItem = double.IsNaN(this.ItemHeight)
                ? this.Children.OfType<UIElement>()
                    .Select(child => child.RenderSize.Height)
                    .Max() * this.Scale
                : this.ScaledItemHeight;
        }

        /// <summary>Gets a width that fits all items with the given height.</summary>
        /// <param name="height"></param>
        /// <param name="orientation"></param>
        /// <returns></returns>
        public double GetWidthFromHeight(double height, Orientation orientation)
        {
            int itemCount = Math.Max(1, this.Children.Count);

            double width;
            if (this.FixedSize && orientation == Orientation.Vertical)
            {
                width = this.ScaledItemWidth * this.Bar.Columns;
            }
            else
            {
                double columns = this.FixedSize ? this.Bar.Columns : Math.Floor(height / this.tallestItem);
                width = Math.Ceiling(itemCount / columns) * this.ScaledItemWidth;
            }

            return  Math.Ceiling(Math.Clamp(width, this.ScaledItemWidth, this.ScaledItemWidth * itemCount));
        }

        /// <summary>Gets a height that fits all items with the given width.</summary>
        /// <param name="width"></param>
        /// <param name="orientation"></param>
        /// <returns></returns>
        public double GetHeightFromWidth(double width, Orientation orientation)
        {
            int itemCount = Math.Max(1, this.Children.Count);
            double height;

            if (this.FixedSize && orientation == Orientation.Horizontal)
            {
                height = this.ScaledItemHeight * this.Bar.Columns;
            }
            else
            {
                double rows = this.FixedSize ? this.Bar.Columns : Math.Floor(width / this.ScaledItemWidth);
                height = Math.Ceiling(itemCount / rows) * this.tallestItem;
            }

            return Math.Ceiling(Math.Clamp(height, this.tallestItem, this.tallestItem * itemCount)) + 1;
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            if (this.FixedSize)
            {
                this.Orientation = sizeInfo.NewSize.Width > this.ScaledItemWidth * 1.5
                    ? Orientation.Horizontal
                    : Orientation.Vertical;
            }
            base.OnRenderSizeChanged(sizeInfo);
        }

        public void LoadBar(BarData bar, bool isPrimary)
        {
            this.IsPrimary = isPrimary;
            this.RemoveItems();
            this.Bar = bar;
            
            this.LayoutTransform = new ScaleTransform(this.Scale, this.Scale);
            
            this.LoadItems(isPrimary ? this.Bar.PrimaryItems : this.Bar.SecondaryItems);
        }

        public void RemoveItems()
        {
            this.Children.Clear();
        }

        /// <summary>
        /// Load some items.
        /// </summary>
        /// <param name="items"></param>
        public void LoadItems(IEnumerable<BarItem> items)
        {
            foreach (BarItem item in items)
            {
                this.AddItem(item);
            }
            
            this.OnBarLoaded();
        }

        /// <summary>
        /// Add a bar item to the control.
        /// </summary>
        /// <param name="item"></param>
        /// <returns>The bar item control.</returns>
        public BarItemControl AddItem(BarItem item)
        {
            BarItemControl control = BarItemControl.From(item);
            control.Style = new Style(control.GetType(), this.Resources["BarItemStyle"] as Style);
            this.Children.Add(control);
            return control;
        }

        protected virtual void OnBarLoaded()
        {
            this.BarLoaded?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
