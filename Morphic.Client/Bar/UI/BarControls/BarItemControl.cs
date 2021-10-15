// BarItemControl.cs: Base control for bar items.
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
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using System.Windows.Media;
    using Data;

    /// <summary>
    /// A bar item control.
    /// </summary>
    public class BarItemControl : ContentControl, INotifyPropertyChanged
    {
        private BarItemSize maxItemSize = BarItemSize.Large;

        /// <summary>
        /// The bar item represented by this control.
        /// </summary>
        public BarItem BarItem { get; }

        /// <summary>
        /// The bar that contains the bar item.
        /// </summary>
        public BarData Bar => this.BarItem.Bar;

        /// <summary>Tool tip header.</summary>
        public string? ToolTipHeader => this.BarItem.ToolTipHeader;

        /// <summary>Tool tip text.</summary>
        public string? ToolTipText => this.BarItem.ToolTip;

        protected ThemeHandler ThemeHandler { get; }

        /// <summary>
        /// The maximum size of an item.
        /// </summary>
        public BarItemSize MaxItemSize
        {
            get => this.maxItemSize;
            set
            {
                this.maxItemSize = value;
                this.OnPropertyChanged();
                this.OnPropertyChanged(nameof(this.ItemSize));
            }
        }

        /// <summary>
        /// The size of this item.
        /// </summary>
        public BarItemSize ItemSize => (BarItemSize)Math.Min((int)this.BarItem.Size, (int)this.maxItemSize);

        /// <summary>true if the last focus was performed by the keyboard.</summary>
        public bool FocusedByKeyboard { get; set; }

        public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register("Orientation",
            typeof(Orientation), typeof(MultiButtonBarControl), new PropertyMetadata(default(Orientation), (o, args) =>
            {
                if (o is BarItemControl control)
                {
                    control.OnOrientationChanged();
                }
            }));

        public static readonly DependencyProperty ActiveThemeProperty = DependencyProperty.Register("ActiveTheme", typeof(Theme), typeof(ButtonBarControl), new PropertyMetadata(default(Theme)));

        public Orientation Orientation
        {
            get => (Orientation)this.GetValue(OrientationProperty);
            set => this.SetValue(OrientationProperty, value);
        }

        public event EventHandler? OrientationChanged;

        /// <summary>
        /// Create an instance of this class, using the given bar item.
        /// </summary>
        /// <param name="barItem">The bar item that this control displays.</param>
        public BarItemControl(BarItem barItem)
        {
            this.DataContext = this;
            this.BarItem = barItem;
            this.ThemeHandler = new ThemeHandler(this, this.BarItem.Theme);

            this.ToolTip = $"{this.ToolTipHeader}|{this.ToolTipText}";

            this.Loaded += this.OnLoaded;
            this.MouseRightButtonUp += (sender, args) =>
            {
                args.Handled = this.OpenContextMenu(sender);
            };

            this.ThemeHandler.ThemeStateChanged += (sender, args) =>
            {
                this.ActiveTheme = args.ActiveTheme;
            };
            this.ActiveTheme = this.BarItem.Theme;
        }

        /// <summary>
        /// Open a context menu.
        /// </summary>
        /// <param name="control"></param>
        /// <param name="items"></param>
        /// <returns></returns>
        protected bool OpenContextMenu(object? control, Dictionary<string,string>? items = null, string? telemetryCategory = null)
        {
            ContextMenu? menu = BarContextMenu.CreateContextMenu(items ?? this.BarItem.Menu, telemetryCategory ?? this.BarItem.TelemetryCategory);
            if (menu is not null)
            {
                menu.Placement = PlacementMode.Top;
                menu.PlacementTarget = control as UIElement ?? this;
                menu.IsOpen = true;
                return true;
            }

            return false;
        }

        private void OnLoaded(object sender, RoutedEventArgs args)
        {
            // Override the apparent behaviour of ContentControl elements, where they make the control focusable.
            foreach (UIElement elem in this.GetAllChildren().OfType<UIElement>())
            {
                elem.Focusable = elem.Focusable && elem is Button || elem == this;
            }

            foreach (BitmapOrXamlImage bitmapOrXamlImage in this.GetAllChildren().OfType<BitmapOrXamlImage>())
            {
                bitmapOrXamlImage.ImageSourceChanged += BitmapOrXamlImage_ImageSourceChanged;
                this.BitmapOrXamlImage_ImageSourceChanged(bitmapOrXamlImage, new PropertyChangedEventArgs("ImageSource"));
            }
        }

        private void BitmapOrXamlImage_ImageSourceChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is BitmapOrXamlImage image)
            {
                if (image.ImageSource is DrawingImage drawingImage)
                {
                    this.DrawingBrush =
                        BarImages.ChangeDrawingColor(drawingImage.Drawing, this.BarItem.Color, this.DrawingBrush);
                }
            }
        }

        public IEnumerable<DependencyObject> GetAllChildren(DependencyObject? parent = null)
        {
            return LogicalTreeHelper.GetChildren(parent ?? this)
                .OfType<DependencyObject>()
                .SelectMany(this.GetAllChildren)
                .Append(parent ?? this);
        }

        /// <summary>
        /// Creates a control for the given bar item.
        /// </summary>
        /// <param name="item"></param>
        /// <returns>The control for the item, the type depends on the item.</returns>
        public static BarItemControl FromItem(BarItem item)
        {
            return (Activator.CreateInstance(item.ControlType, item) as BarItemControl)!;
        }

        /// <summary>
        /// The brush used for monochrome images.
        /// </summary>
        public SolidColorBrush? DrawingBrush { get; protected set; }

        public Theme ActiveTheme
        {
            get => (Theme)this.GetValue(ActiveThemeProperty);
            set => this.SetValue(ActiveThemeProperty, value);
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        protected virtual void OnOrientationChanged()
        {
            this.OnPropertyChanged(nameof(this.Orientation));
            this.OrientationChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
