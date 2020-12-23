// BarControl.cs: The control for a bar.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

namespace Morphic.Client.Bar.UI
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using BarControls;
    using Data;
    using Button = System.Windows.Controls.Button;
    using Control = System.Windows.Controls.Control;
    using HorizontalAlignment = System.Windows.HorizontalAlignment;
    using Orientation = System.Windows.Controls.Orientation;

    /// <summary>
    /// This is the thing that contains bar items.
    /// </summary>
    public class BarControl : WrapPanel, INotifyPropertyChanged
    {
        public static readonly DependencyProperty ItemSpacingProperty = DependencyProperty.Register("ItemSpacing", typeof(double), typeof(BarControl), new PropertyMetadata(default(double)));
        public new static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register("ItemWidth", typeof(double), typeof(BarControl), new PropertyMetadata(default(double)));
        public new static readonly DependencyProperty ItemHeightProperty = DependencyProperty.Register("ItemHeight", typeof(double), typeof(BarControl), new PropertyMetadata(default(double)));
        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register("ItemsSource", typeof(List<BarItem>), typeof(BarControl), new PropertyMetadata(default(List<BarItem>)));

        public BarControl()
        {
            this.Bar = new BarData();
        }

        public BarData Bar { get; set; }
        public bool IsPrimary { get; set; }

        /// <summary>
        /// Disable wrapping of the child controls.
        /// </summary>
        public bool NoWrap => this.IsPrimary && this.Bar.Overflow != BarOverflow.Wrap;

        public double Scale { get; set; }

        public void ApplyScale()
        {
            this.LayoutTransform = new ScaleTransform(this.Scale, this.Scale);
        }

        public double ScaledItemWidth => Math.Ceiling(this.ItemWidth * this.Scale);
        public double ScaledItemHeight => Math.Ceiling(this.ItemHeight * this.Scale);

        public double ItemSpacing
        {
            get => (double)this.GetValue(ItemSpacingProperty);
            set => this.SetValue(ItemSpacingProperty, value);
        }

        public List<BarItem> ItemsSource
        {
            get => (List<BarItem>)this.GetValue(ItemsSourceProperty);
            set => this.SetValue(ItemsSourceProperty, value);
        }

        public IEnumerable<BarItemControl> ItemControls => this.Children.OfType<BarItemControl>();

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raised when tabbing through items has gone beyond the last item.
        /// </summary>
        public event EventHandler? EndTab;

        private Size GetChildSize(UIElement child)
        {
            return this.GetChildSize(child.DesiredSize);
        }

        private Size GetChildSize(Size desiredSize)
        {
            return new Size(
                double.IsNaN(this.ItemWidth) ? desiredSize.Width : this.ItemWidth,
                double.IsNaN(this.ItemHeight) ? desiredSize.Height : this.ItemHeight
            );
        }

        /// <summary>
        /// Create the controls for the items.
        /// </summary>
        protected override UIElementCollection CreateUIElementCollection(FrameworkElement logicalParent)
        {
            UIElementCollection uiElementCollection = base.CreateUIElementCollection(logicalParent);
            foreach (BarItem item in this.ItemsSource)
            {
                uiElementCollection.Add(this.CreateItem(item));
            }

            uiElementCollection.Add(this.CreateEndTabControl());
            return uiElementCollection;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            return this.MeasureArrange(constraint, true);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            return this.MeasureArrange(finalSize, false);
        }

        /// <summary>
        /// Measure or arrange the child items - slightly different to the default implementation, where it allows
        /// wrapping to be turned off.
        /// </summary>
        /// <param name="finalSize">The suggested size.</param>
        /// <param name="measure">true to only measure, otherwise arrange then items.</param>
        /// <param name="orientationOverride">Override the controls orientation.</param>
        /// <returns>A size that fits the arranged items.</returns>
        public Size MeasureArrange(Size finalSize, bool measure, Orientation? orientationOverride = null)
        {
            Orientation orientation = orientationOverride ?? this.Orientation;

            double rowHeight = 0;

            CorrectedCoords pos = new CorrectedCoords(0, 0, orientation);
            CorrectedCoords size = new CorrectedCoords(finalSize, orientation);
            CorrectedCoords actualSize = new CorrectedCoords(0, 0, orientation);

            List<BarItemControl> children = this.InternalChildren.OfType<BarItemControl>().Where(c => c != null).ToList();
            if (!children.Any())
            {
                return finalSize;
            }

            if (measure)
            {
                Size childAvailableSize = this.GetChildSize(finalSize);
                children.ForEach(c => c.Measure(childAvailableSize));
            }

            // Get the widest
            double widest = children.Select(c => new CorrectedCoords(c.DesiredSize, orientation).Width).Max();

            size.Width = Math.Max(size.Width, widest);

            // first item of the row
            bool firstItem = true;

            foreach (BarItemControl child in children)
            {
                CorrectedCoords childSize = new CorrectedCoords(this.GetChildSize(child), orientation);

                if (pos.X + childSize.Width >= size.Width)
                {
                    if (!this.NoWrap)
                    {
                        // new row
                        firstItem = true;
                        pos.X = 0;
                        pos.Y += rowHeight + this.ItemSpacing;

                        rowHeight = 0;
                    }
                }

                if (!firstItem)
                {
                    pos.X += this.ItemSpacing;
                }

                if (!measure)
                {
                    child.Arrange(new Rect(pos.ToPoint(), childSize.ToSize()));
                }

                rowHeight = Math.Max(rowHeight, childSize.Height);
                pos.X += childSize.Width;

                actualSize.Width = Math.Max(actualSize.Width, pos.X);
                actualSize.Height = pos.Y + rowHeight;

                firstItem = false;
            }

            actualSize.Width = Math.Ceiling(actualSize.Width);
            actualSize.Height = Math.Ceiling(actualSize.Height);

            // if this is a horizontal bar, re-arrange any ButtonBarControl children to be vertically centered
            // NOTE: we should redesign/refactor our layout logic to handle all button types equally (so we don't have to special-case specific controls...nor do layout twice)
            if ((orientation == Orientation.Horizontal) && (measure == false))
            {
                foreach (BarItemControl child in children)
                {
                    if (child is ButtonBarControl) {
                        CorrectedCoords childSize = new CorrectedCoords(this.GetChildSize(child), orientation);
                        var offset = VisualTreeHelper.GetOffset(child);
                        var verticallyCenteredPosition = new Point(x: offset.X, y: offset.Y + ((actualSize.Y - childSize.Height) / 2));
                        child.Arrange(new Rect(verticallyCenteredPosition, childSize.ToSize()));
                    }
                }
            }

            return actualSize.ToSize();
        }

        public Size GetSize(Size size, Orientation? orientationOverride = null)
        {
            Size newSize = this.MeasureOverride(size);
            // this.MeasureArrange(size, true, orientationOverride);
            newSize.Width *= this.Scale;
            newSize.Height *= this.Scale;
            return newSize;
        }

        /// <summary>
        /// Add a bar item to the control.
        /// </summary>
        /// <param name="item"></param>
        /// <returns>The bar item control.</returns>
        public BarItemControl CreateItem(BarItem item)
        {
            BarItemControl control = BarItemControl.FromItem(item);
            control.Orientation = this.Orientation;
            control.Style = new Style(control.GetType(), this.Resources["BarItemStyle"] as Style);
            return control;
        }

        /// <summary>
        /// Add a secret control as the last item. This is to move focus to the other bar.
        /// </summary>
        private Control CreateEndTabControl()
        {
            Button tb = new Button()
            {
                Focusable = true,
                IsTabStop = true,
                TabIndex = int.MaxValue,
                Width = 10,
                Height = 10,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom
            };

            tb.GotFocus += (o, a) =>
            {
                this.OnEndTab();
            };

            return tb;
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void OnEndTab()
        {
            this.EndTab?.Invoke(this, EventArgs.Empty);
        }
    }
}
