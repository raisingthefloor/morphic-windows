// BarMultiButtonControl.xaml.cs: Control for Bar images.
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
    using Data;
    using Data.Actions;
    using Settings.SettingsHandlers;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Automation.Peers;
    using System.Windows.Automation.Provider;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using System.Windows.Input;
    using System.Windows.Media;

    /// <summary>
    /// The control for Button bar items.
    /// </summary>
    public partial class MultiButtonBarControl : BarItemControl
    {
        public MultiButtonBarControl(BarMultiButton barItem) : base(barItem)
        {
            this.Buttons = this.BarItem.Buttons.Values.Select(b => new ButtonWrapper(this, b)).ToList();

            this.InitializeComponent();

            // Apply theming to the dynamic buttons when they're created.
            this.ButtonContainer.ItemContainerGenerator.StatusChanged += async (sender, args) =>
            {
                if (this.ButtonContainer.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
                {
                    foreach (ButtonWrapper b in this.ButtonContainer.ItemContainerGenerator.Items)
                    {
                        ContentPresenter content = (ContentPresenter)this.ButtonContainer.ItemContainerGenerator.ContainerFromItem(b);
                        content.ApplyTemplate();

                        if (content.ContentTemplate.FindName("ControlButton", content) is ButtonBase control)
                        {
                            await b.SetControlAsync(control);
                        }
                    }
                }
            };

            // Set the navigation modes, depending on its type.
            bool isPair = (this.BarItem.Type == MultiButtonType.Toggle
                || this.BarItem.Type == MultiButtonType.Additive);

#if CombinePairs
            // For keyboard navigation, paired buttons act as a single control
            this.Focusable = isPair;
            this.Panel.SetValue(FocusManager.IsFocusScopeProperty, isPair);
            this.Panel.SetValue(KeyboardNavigation.DirectionalNavigationProperty,
                isPair ? KeyboardNavigationMode.None : KeyboardNavigationMode.Continue);
            this.Panel.SetValue(KeyboardNavigation.TabNavigationProperty,
                isPair ? KeyboardNavigationMode.None : KeyboardNavigationMode.Continue);
#endif

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
                if (this.Buttons[clickIndex].Control is ButtonBase button)
                {
                    // // Make it look like it's being clicked.
                    // this.ControlTheme[button].IsMouseDown = true;
                    // this.UpdateTheme(button);

                    // Click it
                    FrameworkElementAutomationPeer automationPeer;
                    if (button is ToggleButton toggle)
                    {
                        automationPeer = new ToggleButtonAutomationPeer(toggle);
                    }
                    else
                    {
                        automationPeer = new ButtonAutomationPeer((Button)button);
                    }
                    ((IInvokeProvider?)automationPeer.GetPattern(PatternInterface.Invoke))?.Invoke();

                    // await Task.Delay(250);
                    // this.ControlTheme[button].IsMouseDown = false;
                    // this.UpdateTheme(button);
                }
            }
        }

        public new BarMultiButton BarItem => (BarMultiButton) base.BarItem;

        public List<ButtonWrapper> Buttons { get; set; }

        private void Button_Checked(object sender, RoutedEventArgs routedEventArgs)
        {
            if (sender is ToggleButton button)
            {
                BarMultiButton.ButtonInfo? buttonInfo =
                    this.Buttons.Where(b => b.Control == button)
                        .Select(b => b.Button)
                        .FirstOrDefault();

                if (buttonInfo is not null && !buttonInfo.Toggle)
                {
                    button.IsChecked = false;
                    routedEventArgs.Handled = true;
                }
            }
        }
        private void Button_OnRightClick(object sender, MouseEventArgs e)
        {
            BarMultiButton.ButtonInfo? buttonInfo =
                this.Buttons.Where(b => b.Control == sender)
                    .Select(b => b.Button)
                    .FirstOrDefault();

            if (buttonInfo is not null)
            {
                e.Handled = this.OpenContextMenu(sender, buttonInfo.Menu, buttonInfo.TelemetryCategory);
            }
        }

        private async void Button_OnClick(object sender, RoutedEventArgs e)
        {
            BarMultiButton.ButtonInfo? buttonInfo =
                this.Buttons.Where(b => b.Control == sender)
                    .Select(b => b.Button)
                    .FirstOrDefault();

            if (buttonInfo is not null)
            {
                MultiButtonBarControl.LastClickedControl = sender as Control;
                MultiButtonBarControl.LastClickedTime = DateTime.Now;

                // Call the button action.
                bool? state = (sender as ToggleButton)?.IsChecked;
                if (buttonInfo.Action is not null)
                {
                    var result = await buttonInfo.Action.InvokeAsync(buttonInfo.Value, state);

                    // if we get an error, deal with that error based on the type of button that was pressed
                    // NOTE: this is, unfortunately, a temporary solution; this code should be reworked to centrally handle these sorts of things
                    //       (and also to not TOGGLE the state of the button automatically via the GUI (so that it's not toggled unless the operation is successful)
                    if (result.IsError == true)
                    {
                        if (buttonInfo.Value.ToLowerInvariant() == "basicwordribbon")
                        {
                            var isBasicSimplifyRibbonEnabledResult = Morphic.Integrations.Office.WordRibbon.IsBasicSimplifyRibbonEnabled();
                            // NOTE: we do not handle the error condition (of not being able to capture this value); in the future, we may want to consider showing an error to the user and/or assuming a default toggle state
                            if (isBasicSimplifyRibbonEnabledResult.IsSuccess == true)
                            {
                                ((ToggleButton)sender).IsChecked = isBasicSimplifyRibbonEnabledResult.Value!;
                            }
                        }
                        else if (buttonInfo.Value.ToLowerInvariant() == "essentialswordribbon")
                        {
                            var isEssentialsSimplifyRibbonEnabledResult = Morphic.Integrations.Office.WordRibbon.IsEssentialsSimplifyRibbonEnabled();
                            // NOTE: we do not handle the error condition (of not being able to capture this value); in the future, we may want to consider showing an error to the user and/or assuming a default toggle state
                            if (isEssentialsSimplifyRibbonEnabledResult.IsSuccess == true)
                            {
                                ((ToggleButton)sender).IsChecked = isEssentialsSimplifyRibbonEnabledResult.Value!;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>When a button was last clicked.</summary>
        public static DateTime? LastClickedTime { get; private set; }
        /// <summary>The button that was last clicked.</summary>
        public static Control? LastClickedControl { get; private set; }

        /// <summary>
        /// Gets the text or icon to be displayed, based on the given text. This allows symbols to be easily expressed
        /// in the json.
        /// May also return a string prefixed with "icon:", where the rest of the text is the bar icon.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        protected string GetDisplayText(string text)
        {
            var (finalText, icon) = text switch
            {
                "+" => ("\u2795", "plus"),
                "-" => ("\u2796", "minus"),
                "||" => ("\u258e \u258e", ""),
                "|>" => ("\u25b6", ""),
                "[]" => ("\u25a0", ""),
                _ => (text, null)
            };

            string? iconPath = string.IsNullOrEmpty(icon)
                ? null
                : BarImages.GetBarIconFile(icon);

            return iconPath is not null
                ? $"icon:{iconPath}"
                : finalText;
        }

        /// <summary>
        /// Wraps a button with its theming info.
        /// </summary>
        public class ButtonWrapper : INotifyPropertyChanged
        {
            public BarMultiButton.ButtonInfo Button { get; set; }
            public BarData Bar => this.Button.BarItem.Bar;

            public Theme ActiveTheme { get; private set; }

            public Control? Control { get; set; }

            public string? Text { get; set; }
            public ImageSource? ImageSource { get; set; }
            public ThemeHandler? ThemeHandler { get; set; }

            private readonly MultiButtonBarControl itemControl;

            public ButtonWrapper(MultiButtonBarControl itemControl, BarMultiButton.ButtonInfo buttonInfo)
            {
                this.itemControl = itemControl;
                this.Button = buttonInfo;
                this.ActiveTheme = buttonInfo.BarItem.ControlTheme;
                string text = itemControl.GetDisplayText(this.Button.Text);
                SolidColorBrush? imageBrush = null;
                if (text.StartsWith("icon:"))
                {
                    string icon = text.Substring(text.IndexOf(':') + 1);
                    this.ImageSource = BarImages.CreateImageSource(icon);
                    if (this.ImageSource is DrawingImage di)
                    {
                        imageBrush = BarImages.ChangeDrawingColor(di.Drawing, this.ActiveTheme.TextColor ?? Colors.White);
                    }
                }
                else
                {
                    this.Text = text;
                }

                // Update the theme when the property changes.
                this.itemControl.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == nameof(this.itemControl.ActiveTheme))
                    {
                        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(args.PropertyName));
                        if (imageBrush is not null && this.ActiveTheme.TextColor.HasValue)
                        {
                            imageBrush.Color = this.ActiveTheme.TextColor.Value;
                        }
                    }
                };
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            public async Task SetControlAsync(ButtonBase control)
            {
                this.Control = control;
                this.ThemeHandler = new ThemeHandler(this.Control, this.Button.BarItem.ControlTheme);
                this.ActiveTheme = this.ThemeHandler.ActiveTheme;
                this.ThemeHandler.ThemeStateChanged += (sender, args) =>
                {
                    this.ActiveTheme = args.ActiveTheme;
                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.ActiveTheme)));
                };

                // For settings controls, get the current value and listen for a change.
                if (this.Button.Toggle && this.Button.Action is SettingAction settingAction)
                {
                    Setting setting = settingAction.Solutions.GetSetting(this.Button.Id);
                    //
                    var settingValue = await setting.GetValue<bool>();
                    ((ToggleButton)this.Control).IsChecked = settingValue;
                    //
                    setting.Changed += this.SettingOnChanged;
                    this.Control.Unloaded += (sender, args) => setting.Changed -= this.SettingOnChanged;
                }

                // For settings controls with internal actions, get the current value and listen for a change.
                if (this.Button.Toggle && this.Button.Action is InternalAction internalAction)
                {
                    // NOTE: these buttons are special-cased; we should ideally create logic which wraps this functionality and is referenced via the JSON files (so that these are not hard-coded)
                    switch (internalAction.FunctionName)
                    {
                        case "darkMode":
                            bool systemThemeIsLightTheme;

                            var getDarkModeStateResult = await Morphic.Client.Bar.Data.Actions.Functions.GetDarkModeStateAsync();
                            if (getDarkModeStateResult.IsError == true)
                            {
                                Debug.Assert(false, "Could not get dark mode state");
                                break;
                            }
                            var darkModeState = getDarkModeStateResult.Value!;

                            var osVersion = Morphic.WindowsNative.OsVersion.OsVersion.GetWindowsVersion();
                            if (osVersion == Morphic.WindowsNative.OsVersion.WindowsVersion.Win10_v1809)
                            {
                                // Windows 10 v1809

                                // NOTE: this is hard-coded, as a patch, because the solutions registry does not yet understand how to capture/apply settings across incompatible handlers
                                //       [and trying to call the Windows 10 v1903+ handlers for apps/system "light theme" will result in a memory access exception under v1809]
                                //       [also: only "AppsUseLightTheme" (and not "SystemUsesLightTheme") existed properly under Windows 10 v1809]

                                var openPersonalizeKeyResult = Morphic.WindowsNative.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize", true);
                                if (openPersonalizeKeyResult.IsError == true)
                                {
                                    Debug.Assert(false, "Could not get Personalize key from registry (so we cannot watch the dark mode state)");
                                }
                                else
                                {
                                    var personalizeKey = openPersonalizeKeyResult.Value!;

                                    var registerForChangesEvent = new Morphic.WindowsNative.Registry.RegistryKey.RegistryKeyChangedEvent(async (sender, e) =>
                                    {
                                        var getDarkModeStateResult = await Morphic.Client.Bar.Data.Actions.Functions.GetDarkModeStateAsync();
                                        if (getDarkModeStateResult.IsError == true)
                                        {
                                            Debug.Assert(false, "Could not get dark mode state");
                                            return;
                                        }
                                        var darkModeState = getDarkModeStateResult.Value!;

                                        Setting appsThemeSetting = App.Current.MorphicSession.Solutions.GetSetting(Settings.SolutionsRegistry.SettingId.LightThemeApps);
                                        Application.Current.Dispatcher.Invoke(new Action(() => {
                                            this.InverseSettingOnChanged(sender, new SettingEventArgs(appsThemeSetting, !darkModeState));
                                        }));
                                    });
                                    personalizeKey.RegisterForValueChangeNotification(registerForChangesEvent);

                                    this.Control.Unloaded += (sender, SettingEventArgs) =>
                                    {
										// dispose of the RegistryKey; this should terminate the notification registration as well
                                        personalizeKey.Dispose();
                                    };
                                }

                                // TODO: add a registry hook which watches for the value to change (and which calls "this.InverseSettingOnChanged")
                                //       [and unwire the handler when our control is unloaded]
                                // TODO: we need to make sure that we wire this up in a way that the registry key doesn't get GC'd prematurely
                                //appsThemeRegistryKey.Changed += this.InverseSettingOnChanged;
                                ////
                                //this.Control.Unloaded += (sender, args) => appsThemeRegistryKey.Changed -= this.InverseSettingOnChanged;
                            }
                            else if (osVersion is null)
                            {
                                // error
                                //break;
                            }
                            else
                            {
                                // Windows 10 v1903+

                                // capture changes to system dark theme (triggering our this.InverseSettingOnChanged event handler)
                                Setting systemThemeSetting = App.Current.MorphicSession.Solutions.GetSetting(Settings.SolutionsRegistry.SettingId.LightThemeSystem);
                                systemThemeSetting.Changed += this.InverseSettingOnChanged;
                                //
                                this.Control.Unloaded += (sender, args) => systemThemeSetting.Changed -= this.InverseSettingOnChanged;
                                
                                // capture changes to apps dark theme (triggering our this.InverseSettingOnChanged event handler)
                                Setting appsThemeSetting = App.Current.MorphicSession.Solutions.GetSetting(Settings.SolutionsRegistry.SettingId.LightThemeApps);
                                appsThemeSetting.Changed += this.InverseSettingOnChanged;
                                //
                                this.Control.Unloaded += (sender, args) => appsThemeSetting.Changed -= this.InverseSettingOnChanged;
                            }

                            ((ToggleButton)this.Control).IsChecked = darkModeState;
                            break;
                        case "volumeMute":
                            var getMuteStateResult = Morphic.Client.Bar.Data.Actions.Functions.GetMuteState();
                            if (getMuteStateResult.IsError == true)
                            {
                                Debug.Assert(false, "Could not get volume mute state");
                                break;
                            }
                            var volumeMuteState = getMuteStateResult.Value!;

                            // NOTE: this.Control.Unloaded should capture 'defaultAudioEndpoint' so we don't need to create a class-level reference to it here
                            var defaultAudioEndpoint = Morphic.WindowsNative.Audio.AudioEndpoint.GetDefaultAudioOutputEndpoint();
                            defaultAudioEndpoint.MasterMuteStateChangedEvent += this.MasterMuteStateOnChanged;
                            //
                            this.Control.Unloaded += (sender, args) => defaultAudioEndpoint.MasterMuteStateChangedEvent -= this.MasterMuteStateOnChanged;

                            ((ToggleButton)this.Control).IsChecked = volumeMuteState;

                            break;
                        case "basicWordRibbon":
                            var isBasicWordRibbonEnabledResult = Morphic.Integrations.Office.WordRibbon.IsBasicSimplifyRibbonEnabled();
                            var basicSimpifyRibbonIsEnabled = isBasicWordRibbonEnabledResult.IsSuccess ? isBasicWordRibbonEnabledResult.Value! : false;

                            ((ToggleButton)this.Control).IsChecked = basicSimpifyRibbonIsEnabled;

                            break;
                        case "essentialsWordRibbon":
                            var isEssentialsWordRibbonEnabledResult = Morphic.Integrations.Office.WordRibbon.IsEssentialsSimplifyRibbonEnabled();
                            var essentialsSimpifyRibbonIsEnabled = isEssentialsWordRibbonEnabledResult.IsSuccess ? isEssentialsWordRibbonEnabledResult.Value! : false;

                            ((ToggleButton)this.Control).IsChecked = essentialsSimpifyRibbonIsEnabled;
                            break;
                        default:
                            // unknown internal action
                            break;
                    }
                }
            }

            private void SettingOnChanged(object? sender, SettingEventArgs e)
            {
                if (this.Control is ToggleButton button)
                {
                    button.IsChecked = e.NewValue as bool? ?? false;
                }
            }

            private void InverseSettingOnChanged(object? sender, SettingEventArgs e)
            {
                if (this.Control is ToggleButton button)
                {
                    button.IsChecked = !(e.NewValue as bool? ?? false);
                }
            }

            private void MasterMuteStateOnChanged(object? sender, Morphic.WindowsNative.Audio.AudioEndpoint.MasterMuteStateChangedEventArgs e)
            {
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    if (this.Control is ToggleButton button)
                    {
                        button.IsChecked = e.MuteState;
                    }
                }));
            }
        }
    }

    /// <summary>
    /// Panel for the buttons of a <see cref="MultiButtonBarControl"/>. Child items can either have the same size (of
    /// the widest), or their own size.
    /// </summary>
    public class MultiButtonPanel : WrapPanel
    {
        public static readonly DependencyProperty VariableWidthProperty = DependencyProperty.Register("VariableWidth", typeof(bool), typeof(MultiButtonPanel), new PropertyMetadata(default(bool)));

        public static readonly DependencyProperty ParentOrientationProperty =
            DependencyProperty.Register("ParentOrientation", typeof(Orientation), typeof(MultiButtonPanel),
                new PropertyMetadata(default(Orientation), (o, args) => ((MultiButtonPanel)o).OnOrientationChanged()));

        public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register("CornerRadius", typeof(double), typeof(MultiButtonPanel), new PropertyMetadata(default(double)));

        public Orientation ParentOrientation
        {
            get => (Orientation)this.GetValue(ParentOrientationProperty);
            set => this.SetValue(ParentOrientationProperty, value);
        }

        public bool VariableWidth
        {
            get => (bool)this.GetValue(VariableWidthProperty);
            set => this.SetValue(VariableWidthProperty, value);
        }

        public double CornerRadius
        {
            get => (double)this.GetValue(CornerRadiusProperty);
            set => this.SetValue(CornerRadiusProperty, value);
        }

        protected void OnOrientationChanged()
        {
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (!this.VariableWidth || this.ParentOrientation == Orientation.Vertical)
            {
                // Make all child items the width of the largest.
                this.ItemWidth = this.Children.OfType<FrameworkElement>().Select(c =>
                {
                    c.Measure(availableSize);
                    return c.DesiredSize.Width;
                }).Max();
            }
            else
            {
                this.ItemWidth = double.NaN;
            }

            Size size = base.MeasureOverride(availableSize);
            return size;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            Size arrangeOverride = base.ArrangeOverride(finalSize);
            this.ApplyButtonBorders();
            return arrangeOverride;
        }

        /// <summary>
        /// Applies the rounded corners to the buttons, according to where they are positioned.
        /// </summary>
        private void ApplyButtonBorders()
        {
            if (this.CornerRadius > 0)
            {
                // Get the positions of the buttons.
                Dictionary<ContentPresenter, Point> positions = this.InternalChildren.OfType<ContentPresenter>()
                    .Where(c => c.Content is MultiButtonBarControl.ButtonWrapper)
                    .ToDictionary(c => c, c => c.TranslatePoint(default, this));

                // Sets the border of a button.
                static void SetBorder(ContentPresenter? presenter, Func<CornerRadius, CornerRadius> apply)
                {
                    if ((presenter?.Content as MultiButtonBarControl.ButtonWrapper)?
                        .Control?.FindName("ButtonBorder") is Border border)
                    {
                        border.CornerRadius = apply(border.CornerRadius);
                    }
                }

                // Reset all the borders.
                foreach (ContentPresenter contentPresenter in positions.Keys)
                {
                    SetBorder(contentPresenter, c => default);
                }

                // Top-left.
                SetBorder(positions
                    .OrderBy(p => p.Value.X)
                    .ThenBy(p => p.Value.Y)
                    .FirstOrDefault().Key, cr =>
                {
                    cr.TopLeft = this.CornerRadius;
                    return cr;
                });

                // Top-right.
                SetBorder(positions
                    .OrderByDescending(p => p.Value.X)
                    .ThenBy(p => p.Value.Y)
                    .FirstOrDefault().Key, cr =>
                {
                    cr.TopRight = this.CornerRadius;
                    return cr;
                });

                // Bottom-left.
                SetBorder(positions
                    .OrderBy(p => p.Value.X)
                    .ThenByDescending(p => p.Value.Y)
                    .FirstOrDefault().Key, cr =>
                {
                    cr.BottomLeft = this.CornerRadius;
                    return cr;
                });

                // Bottom-right.
                SetBorder(positions
                    .OrderByDescending(p => p.Value.X)
                    .ThenByDescending(p => p.Value.Y)
                    .FirstOrDefault().Key, cr =>
                {
                    cr.BottomRight = this.CornerRadius;
                    return cr;
                });
            }
        }
    }
}
