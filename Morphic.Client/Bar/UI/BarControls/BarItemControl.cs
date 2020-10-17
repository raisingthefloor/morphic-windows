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
    using System.Windows.Input;
    using System.Windows.Media;
    using Data;

    /// <summary>
    /// A bar item control.
    /// </summary>
    public class BarItemControl : ContentControl, INotifyPropertyChanged
    {
        private BarItemSize maxItemSize = BarItemSize.Large;

        /// <summary>
        /// Theming info for child controls.
        /// </summary>
        protected Dictionary<Control, ControlThemeState> ControlTheme = new Dictionary<Control, ControlThemeState>();

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

        /// <summary>
        /// Current theme to use, depending on the state (normal/hover/focus).
        /// </summary>
        public Theme ActiveTheme
        {
            get =>
                this.ControlTheme.Count == 0
                    ? this.BarItem.Theme
                    : this.ControlTheme.FirstOrDefault().Value.ActiveTheme;
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

        public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register("Orientation",
            typeof(Orientation), typeof(MultiButtonBarControl), new PropertyMetadata(default(Orientation), (o, args) =>
            {
                if (o is BarItemControl control)
                {
                    control.OnOrientationChanged();
                }
            }));

        public Orientation Orientation
        {
            get => (Orientation)this.GetValue(OrientationProperty);
            set => this.SetValue(OrientationProperty, value);
        }

        public event EventHandler? OrientationChanged;

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

            this.ToolTip = $"{this.ToolTipHeader}|{this.ToolTipText}";

            this.Loaded += this.OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs args)
        {
            this.ApplyTheming();
            // Override the apparent behaviour of ContentControl elements, where they make the control focusable.
            foreach (UIElement elem in this.GetAllChildren().OfType<UIElement>())
            {
                elem.Focusable = elem.Focusable && elem is Button || elem == this;
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

        protected IEnumerable<DependencyObject> GetAllChildren(DependencyObject? parent = null)
        {
            return LogicalTreeHelper.GetChildren(parent ?? this)
                .OfType<DependencyObject>()
                .SelectMany(this.GetAllChildren)
                .Append(parent ?? this);
        }

        private void CheckMouseState(object sender, MouseEventArgs mouseEventArgs)
        {
            ControlThemeState state = this.ControlTheme[(Control)sender];
            bool last = state.IsMouseDown;
            state.IsMouseDown = mouseEventArgs.LeftButton == MouseButtonState.Pressed;
            if (last != state.IsMouseDown)
            {
                this.UpdateTheme((Control)sender);
            }
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
        /// Update the theme depending on the current state of the control.
        /// </summary>
        public void UpdateTheme(Control? control = null)
        {
            if (control == null)
            {
                foreach (Control ctrl in this.ControlTheme.Keys)
                {
                    this.UpdateTheme(ctrl);
                }
                return;
            }

            Theme theme = new Theme();

            ControlThemeState state = this.ControlTheme[control];

            // Apply the applicable states, most important first.
            if (state.IsMouseDown)
            {
                theme.Apply(this.BarItem.Theme.Active);
            }

            if (control.IsMouseOver)
            {
                theme.Apply(this.BarItem.Theme.Hover);
            }

            if (control.IsKeyboardFocusWithin && state.FocusedByKeyboard)
            {
                theme.Apply(this.BarItem.Theme.Focus);
            }

            //this.ActiveTheme = theme.Apply(this.BarItem.Theme);
            state.ActiveTheme = theme.Apply(this.BarItem.Theme);

            this.OnPropertyChanged(nameof(this.ActiveTheme));

            // Update the brush used by a mono drawing image.
            if (this.DrawingBrush != null && this.ActiveTheme.Background.HasValue)
            {
                this.DrawingBrush.Color = this.ActiveTheme.Background.Value;
            }
        }

        /// <summary>
        /// Apply the theming to all the buttons in this control.
        /// </summary>
        protected virtual void ApplyTheming()
        {
            foreach (Button button in this.GetAllChildren().OfType<Button>())
            {
                this.ApplyControlTheme(button);
            }
        }

        /// <summary>
        /// Apply theming to a control.
        /// </summary>
        /// <param name="control"></param>
        protected void ApplyControlTheme(Control control)
        {
            if (this.ControlTheme.ContainsKey(control))
            {
                return;
            }

            ControlThemeState state = new ControlThemeState(this.BarItem.Theme);
            this.ControlTheme.Add(control, state);

            // Some events to monitor the state.
            control.MouseEnter += (sender, args) => this.UpdateTheme(control);
            control.MouseLeave += (sender, args) =>
            {
                this.CheckMouseState(sender, args);
                this.UpdateTheme(control);
            };

            control.PreviewMouseDown += this.CheckMouseState;
            control.PreviewMouseUp += this.CheckMouseState;

            control.IsKeyboardFocusWithinChanged += (sender, args) =>
            {
                state.FocusedByKeyboard = control.IsKeyboardFocusWithin &&
                    InputManager.Current.MostRecentInputDevice is KeyboardDevice;
                this.UpdateTheme(control);
            };
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

            // If there's only 1 colour, it's mono.
            bool mono = geometryDrawings.Count > 0
                && geometryDrawings
                    .Select(gd => gd.Brush)
                    .OfType<SolidColorBrush>()
                    .Select(b => b.Color)
                    .Distinct()
                    .Count() == 1;

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

        public class ControlThemeState
        {
            public ControlThemeState(Theme activeTheme)
            {
                this.ActiveTheme = activeTheme;
            }

            public Theme ActiveTheme { get;set;}
            public bool IsMouseDown { get;set;}
            public bool FocusedByKeyboard { get;set;}

        }

        protected virtual void OnOrientationChanged()
        {
            this.OnPropertyChanged(nameof(this.Orientation));
            this.OrientationChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
