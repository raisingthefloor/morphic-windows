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
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using Bar;
    using UserControl = System.Windows.Controls.UserControl;

    /// <summary>
    /// A bar item control.
    /// </summary>
    public class BarItemControl : UserControl, INotifyPropertyChanged
    {
        private Theme activeTheme = null!;
        private bool isMouseDown;
        private BarItemSize maxItemSize = BarItemSize.Large;

        public BarItemControl() : this(new BarItem())
        {
        }

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

            this.Loaded += this.OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs args)
        {
            // Override the apparent behaviour of ContentControl elements, where they make the control focusable.
            foreach (UIElement elem in this.GetAllChildren().OfType<UIElement>())
            {
                elem.Focusable = elem.Focusable && elem is Button;
            }

            foreach (Image image in this.GetAllChildren().OfType<Image>())
            {
                image.SourceUpdated += this.ImageOnSourceUpdated;
                this.ImageOnSourceUpdated(image, null);
            }
        }

        private void ImageOnSourceUpdated(object? sender, EventArgs? e)
        {
            if (sender is Image image)
            {
                if (image.Source is DrawingImage drawingImage)
                {
                    this.ChangeDrawingColor(drawingImage.Drawing);
                }
            }
        }

        private IEnumerable<DependencyObject> GetAllChildren(DependencyObject? parent = null)
        {
            return LogicalTreeHelper.GetChildren(parent ?? this)
                .OfType<DependencyObject>()
                .SelectMany(this.GetAllChildren)
                .Append(parent ?? this);
        }

        /// <summary>
        /// The bar item represented by this control.
        /// </summary>
        public BarItem BarItem { get; }

        /// <summary>
        /// The bar that contains the bar item.
        /// </summary>
        public BarData Bar => this.BarItem.Bar;

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

            // Update the brush used by a mono drawing image.
            if (this.DrawingBrush != null && this.ActiveTheme.Background.HasValue)
            {
                this.DrawingBrush.Color = this.ActiveTheme.Background.Value;
            }
        }

        /// <summary>
        /// The brush used for monochrome svg images.
        /// </summary>
        public SolidColorBrush? DrawingBrush { get; protected set; }

        /// <summary>
        /// Replaces the brushes used in a monochrome drawing with a new one, which can be set to a specific colour.
        /// </summary>
        /// <param name="drawing"></param>
        private void ChangeDrawingColor(Drawing drawing)
        {
            List<GeometryDrawing>? geometryDrawings;

            // Get all the geometries in the drawing.
            if (drawing is DrawingGroup drawingGroup)
            {
                geometryDrawings = this.GetDrawings(drawingGroup)
                    .OfType<GeometryDrawing>()
                    .ToList();
            }
            else
            {
                geometryDrawings = new List<GeometryDrawing>();
                if (drawing is GeometryDrawing gd)
                {
                    geometryDrawings.Add(gd);
                }
            }

            bool mono = geometryDrawings.Count > 0
                && geometryDrawings
                    .Select(gd => gd.Brush)
                    .OfType<SolidColorBrush>()
                    .All(b => b.Color == Colors.Black);

            if (mono)
            {
                this.DrawingBrush ??= new SolidColorBrush(this.BarItem.Color);
                geometryDrawings.ForEach(gd =>
                {
                    if (gd.Brush is SolidColorBrush)
                    {
                        gd.Brush = this.DrawingBrush;
                    }
                });
            }
        }

        /// <summary>
        /// Gets all drawings within a drawing group.
        /// </summary>
        /// <param name="drawingGroup"></param>
        /// <returns></returns>
        private IEnumerable<Drawing> GetDrawings(DrawingGroup drawingGroup)
        {
            return drawingGroup.Children.OfType<DrawingGroup>()
                .SelectMany(this.GetDrawings)
                .Concat(drawingGroup.Children.OfType<GeometryDrawing>());
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
