// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt
//
// The R&D leading to these results received funding from the:
// * Rehabilitation Services Administration, US Dept. of Education under 
//   grant H421A150006 (APCP)
// * National Institute on Disability, Independent Living, and 
//   Rehabilitation Research (NIDILRR)
// * Administration for Independent Living & Dept. of Education under grants 
//   H133E080022 (RERC-IT) and H133E130028/90RE5003-01-00 (UIITA-RERC)
// * European Union's Seventh Framework Programme (FP7/2007-2013) grant 
//   agreement nos. 289016 (Cloud4all) and 610510 (Prosperity4All)
// * William and Flora Hewlett Foundation
// * Ontario Ministry of Research and Innovation
// * Canadian Foundation for Innovation
// * Adobe Foundation
// * Consumer Electronics Association Foundation

using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Morphic.Core;
using Morphic.Service;
using Morphic.Settings;
using System.Windows.Media.Animation;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using CountlySDK;
using Morphic.Windows.Native;
using Display = Morphic.Settings.Display;

namespace Morphic.Client.QuickStrip
{
    using System.Windows.Forms;
    using Clipboard = System.Windows.Clipboard;
    using IDataObject = System.Windows.IDataObject;
    using Speech = Windows.Native.Speech;

    /// <summary>
    /// Interaction logic for QuickStripWindow.xaml
    /// </summary>
    public partial class QuickStripWindow : Window
    {

        #region Initialization

        public QuickStripWindow(Session session)
        {
            this.session = session;
            session.UserChanged += Session_UserChanged;
            InitializeComponent();
            Deactivated += OnDeactivated;
            SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
        }

        private void Session_UserChanged(object? sender, EventArgs e)
        {
            Update();
        }

        private void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e)
        {
            Reposition(animated: false);
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            Update();
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            Reposition(animated: false);

            // Start monitoring the active window.
            WindowInteropHelper nativeWindow = new WindowInteropHelper(this);
            HwndSource hwndSource = HwndSource.FromHwnd(nativeWindow.Handle);
            SelectionReader.Default.Initialise(nativeWindow.Handle);
            hwndSource?.AddHook(SelectionReader.Default.WindowProc);
        }

        private readonly Session session;

        private void OnDeactivated(object? sender, EventArgs e)
        {
        }

        private void Update()
        {
            if (Enum.TryParse<FixedPosition>(session.GetString(PreferenceKeys.Position), out var position))
            {
                this.position = position;
            }
            if (session.GetArray(PreferenceKeys.Items) is object?[] items)
            {
                Items = Item.CreateItems(items);
            }
            if (session.User == null)
            {
                logoutItem.Visibility = Visibility.Collapsed;
            }
            else
            {
                logoutItem.Visibility = Visibility.Visible;
            }
        }

        #endregion

        #region Logo Button & Menu

        /// <summary>
        /// Event handler for when the user clicks on the logo button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LogoButtonClicked(object sender, RoutedEventArgs e)
        {
            LogoButton.ContextMenu.IsOpen = true;
        }

        /// <summary>
        /// Event handler for when the user selects Hide Quick Strip from the logo button's menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HideQuickStrip(object sender, RoutedEventArgs e)
        {
            App.Shared.HideQuickStrip();
        }

        /// <summary>
        /// Event handler for when the user selects Customize Quick Strip from the logo button's menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CustomizeQuickStrip(object sender, RoutedEventArgs e)
        {
            Countly.RecordEvent("customize-quickstrip");
        }

        private void AboutMorphic(object sender, RoutedEventArgs e)
        {
            Countly.RecordEvent("about-morphic");
            App.Shared.OpenAboutWindow();
        }

        private void TravelWithSettings(object sender, RoutedEventArgs e)
        {
            Countly.RecordEvent("travel-with-settings");
            App.Shared.OpenTravelWindow();
        }

        private void ApplyMySettings(object sender, RoutedEventArgs e)
        {
            Countly.RecordEvent("apply-my-settings");
            App.Shared.OpenLoginWindow();
        }

        private void Logout(object sender, RoutedEventArgs e)
        {
            _ = session.Signout();
        }

        /// <summary>
        /// Event handler for when the user selects Quit from the logo button's menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Quit(object sender, RoutedEventArgs e)
        {
            App.Shared.Shutdown();
        }

        #endregion

        #region Items

        /// <summary>
        /// The definition of an item to show on the quick strip
        /// </summary>
        public class Item
        {

            /// <summary>
            /// The raw preference data, stored in generic form to prevent loss of data
            /// </summary>
            public Dictionary<string, object?> Payload;

            /// <summary>
            /// Create a new item from generic data
            /// </summary>
            /// <param name="payload"></param>
            public Item(Dictionary<string, object?> payload)
            {
                Payload = payload;
            }

            /// <summary>
            /// Get the control that should appear on the quick strip for this item, or <code>null</code> if no valid control exists
            /// </summary>
            /// <returns></returns>
            virtual public QuickStripItemControl? GetControl(QuickStripWindow quickStrip)
            {
                return null;
            }

            /// <summary>
            /// Create a list of items from the item descriptions
            /// </summary>
            /// <param name="descriptions"></param>
            /// <returns></returns>
            public static List<Item> CreateItems(object?[] descriptions)
            {
                var items = new List<Item>();
                foreach (var obj in descriptions)
                {
                    if (obj is Dictionary<string, object?> payload)
                    {
                        if (CreateItem(payload) is Item item)
                        {
                            items.Add(item);
                        }
                    }
                }
                return items;
            }

            /// <summary>
            /// Create an item from the given description
            /// </summary>
            /// <param name="description"></param>
            /// <returns></returns>
            public static Item? CreateItem(Dictionary<string, object?> payload)
            {
                if (payload.TryGetValue("type", out var typeValue))
                {
                    if (typeValue is string type)
                    {
                        switch (type)
                        {
                            case "control":
                                return new ControlItem(payload);
                        }
                    }
                }
                return new Item(payload);
            }

        }

        /// <summary>
        /// An item representing a known setting control
        /// </summary>
        private class ControlItem : Item
        {

            /// <summary>
            /// Create a control item from a generic payload, extracting the <code>Feature</code> if present
            /// </summary>
            /// <param name="payload"></param>
            public ControlItem(Dictionary<string, object?> payload): base(payload)
            {
                if (payload.TryGetValue("feature", out var obj))
                {
                    if (obj is string feature)
                    {
                        Feature = feature;
                    }
                }
            }

            public string Feature { get; private set; } = "";

            public override QuickStripItemControl? GetControl(QuickStripWindow quickStrip)
            {
                switch (Feature)
                {
                    case "resolution":
                        {
                            var control = new QuickStripSegmentedButtonControl();
                            control.TitleLabel.Content = Properties.Resources.QuickStrip_Resolution_Title;
                            control.AddButton(new Image() { Source = new BitmapImage(new Uri("Plus.png", UriKind.Relative)) }, Properties.Resources.QuickStrip_Resolution_Bigger_HelpTitle, Properties.Resources.QuickStrip_Resolution_Bigger_HelpMessage, isPrimary: true);
                            control.AddButton(new Image() { Source = new BitmapImage(new Uri("Minus.png", UriKind.Relative)) }, Properties.Resources.QuickStrip_Resolution_Smaller_HelpTitle, Properties.Resources.QuickStrip_Resolution_Smaller_HelpMessage, isPrimary: false);
                            control.Action += quickStrip.Zoom;
                            return control;
                        }
                    case "magnifier":
                        {
                            var control = new QuickStripSegmentedButtonControl();
                            control.TitleLabel.Content = Properties.Resources.QuickStrip_Magnifier_Title;
                            control.AddButton(Properties.Resources.QuickStrip_Magnifier_Show_Title, Properties.Resources.QuickStrip_Magnifier_Show_HelpTitle, Properties.Resources.QuickStrip_Magnifier_Show_HelpMessage, isPrimary: true);
                            control.AddButton(Properties.Resources.QuickStrip_Magnifier_Hide_Title, Properties.Resources.QuickStrip_Magnifier_Hide_HelpTitle, Properties.Resources.QuickStrip_Magnifier_Hide_HelpMessage, isPrimary: false);
                            control.Action += quickStrip.OnMagnify;
                            return control;
                        }
                    case "reader":
                        {
                            var control = new QuickStripSegmentedButtonControl();
                            control.TitleLabel.Content = "Read text";
                            control.AddButton("\u25b6", "Speak the selected text", "Select some text, and click the button to read it aloud.", isPrimary: true);
                            control.AddButton("||", "Pause speech", "Pause or resume the current speech.", isPrimary: false);
                            control.AddButton("\u25a0", "Stop speech", "Stop the current speech.", isPrimary: true);
                            control.EnableButton(1, false);
                            control.EnableButton(2, false);
                            control.Action += quickStrip.OnReader;
                            Speech.Default.StateChanged += (sender, active) =>
                            {
                                control.Dispatcher.Invoke(() =>
                                {
                                    control.EnableButton(1, active);
                                    control.EnableButton(2, active);
                                });
                            };
                            return control;
                        }
                    case "volume":
                        {
                            var control = new QuickStripSegmentedButtonControl();
                            control.TitleLabel.Content = Properties.Resources.QuickStrip_Volume_Title;
                            control.AddButton(new Image() { Source = new BitmapImage(new Uri("Plus.png", UriKind.Relative)) }, Properties.Resources.QuickStrip_Volume_Up_HelpTitle, Properties.Resources.QuickStrip_Volume_Up_HelpMessage, isPrimary: true);
                            control.AddButton(new Image() { Source = new BitmapImage(new Uri("Minus.png", UriKind.Relative)) }, Properties.Resources.QuickStrip_Volume_Down_HelpTitle, Properties.Resources.QuickStrip_Volume_Down_HelpMessage, isPrimary: false);
                            control.AddButton(Properties.Resources.QuickStrip_Volume_Mute_Title, Properties.Resources.QuickStrip_Volume_Mute_HelpTitle, Properties.Resources.QuickStrip_Volume_Mute_HelpMessage, isPrimary: true);
                            control.Action += quickStrip.OnVolume;
                            return control;
                        }
                    case "contrast":
                        {
                            var control = new QuickStripSegmentedButtonControl();
                            control.TitleLabel.Content = Properties.Resources.QuickStrip_Contrast_Title;
                            control.AddButton(Properties.Resources.QuickStrip_Contrast_On_Title, Properties.Resources.QuickStrip_Contrast_On_HelpTitle, Properties.Resources.QuickStrip_Contrast_On_HelpMessage, isPrimary: true);
                            control.AddButton(Properties.Resources.QuickStrip_Contrast_Off_Title, Properties.Resources.QuickStrip_Contrast_Off_HelpTitle, Properties.Resources.QuickStrip_Contrast_Off_HelpMessage, isPrimary: false);
                            control.Action += quickStrip.OnContrast;
                            return control;
                        }
                    case "cursor":
                        {
                            var control = new QuickStripSegmentedButtonControl();
                            control.TitleLabel.Content = "Cursor";
                            control.AddButton("White", "Make the cursor white", "White is the default for your computer", isPrimary: true);
                            control.AddButton("Black", "Make the cursor black", "Black can be easier to see for some people", isPrimary: false);
                            control.Action += quickStrip.OnCursorColor;
                            return control;
                        }
                    default:
                        return null;
                }
            }
        }

        private List<Item> items = new List<Item>();
        public List<Item> Items
        {
            get
            {
                return items;
            }
            set
            {
                items = value;
                UpdateControls();
            }
        }

        #endregion

        #region Item Actions

        private void Zoom(object sender, QuickStripSegmentedButtonControl.ActionEventArgs e)
        {
            Countly.RecordEvent("change-zoom");
            double percentage = 1.0;
            if (e.SelectedIndex == 0)
            {
                percentage = Display.Primary.PercentageForZoomingIn;
            }
            else
            {
                percentage = Display.Primary.PercentageForZoomingOut;
            }
            _ = session.Apply(SettingsManager.Keys.WindowsDisplayZoom, percentage);
        }

        private void OnMagnify(object sender, QuickStripSegmentedButtonControl.ActionEventArgs e)
        {
            Countly.RecordEvent("toggle-magnify");
            if (e.SelectedIndex == 0)
            {
                _ = session.Apply(SettingsManager.Keys.WindowsMagnifierEnabled, true);
            }
            else if (e.SelectedIndex == 1)
            {
                _ = session.Apply(SettingsManager.Keys.WindowsMagnifierEnabled, false);
            }
        }

        private async void OnReader(object sender, QuickStripSegmentedButtonControl.ActionEventArgs e)
        {
            SelectionReader reader = SelectionReader.Default;
            Windows.Native.Speech speech = Windows.Native.Speech.Default;
            QuickStripSegmentedButtonControl itemControl = (QuickStripSegmentedButtonControl)sender;

            switch (e.SelectedIndex)
            {
                // Play
                case 0:
                    speech.StopSpeaking();
                    
                    // Store the clipboard
                    IDataObject clipboadData = Clipboard.GetDataObject();
                    Dictionary<string, object> dataStored = clipboadData.GetFormats()
                        .ToDictionary(format => format, format => clipboadData.GetData(format, false));
                    Clipboard.Clear();

                    // Get the selection
                    await reader.GetSelectedText(SendKeys.SendWait);
                    string text = Clipboard.GetText();

                    // Restore the clipboard
                    Clipboard.Clear();
                    dataStored.Where(kv => kv.Value != null).ToList()
                        .ForEach(kv => Clipboard.SetData(kv.Key, kv.Value));
                    Clipboard.Flush();

                    if (!string.IsNullOrEmpty(text))
                    {
                        await speech.SpeakText(text);
                    }

                    break;
                // Pause
                case 1 when speech.Active:
                    speech.TogglePause();
                    break;
                // Stop
                case 2 when speech.Active:
                    speech.StopSpeaking();
                    break;
            }
        }

        private void OnVolume(object sender, QuickStripSegmentedButtonControl.ActionEventArgs e)
        {
            Countly.RecordEvent("change-volume");
            var endpoint = Audio.DefaultOutputEndpoint;
            if (e.SelectedIndex == 0)
            {
                if (endpoint.GetMasterMuteState() == true)
                {
                    endpoint.SetMasterMuteState(false);
                }
                else
                {
                    endpoint.SetMasterVolumeLevel(Math.Min(1, Math.Max(0, endpoint.GetMasterVolumeLevel() + (float)0.1)));
                }
            }
            else if (e.SelectedIndex == 1)
            {
                if (endpoint.GetMasterMuteState() == true)
                {
                    endpoint.SetMasterMuteState(false);
                }
                else
                {
                    endpoint.SetMasterVolumeLevel(Math.Min(1, Math.Max(0, endpoint.GetMasterVolumeLevel() - (float)0.1)));
                }
            }
            else
            {
                endpoint.SetMasterMuteState(true);
            }
        }

        private void OnContrast(object sender, QuickStripSegmentedButtonControl.ActionEventArgs e)
        {
            Countly.RecordEvent("toggle-contrast");
            if (e.SelectedIndex == 0)
            {
                _ = session.Apply(SettingsManager.Keys.WindowsDisplayContrastEnabled, true);
            }
            else if (e.SelectedIndex == 1)
            {
                _ = session.Apply(SettingsManager.Keys.WindowsDisplayContrastEnabled, false);
            }
        }

        private void OnCursorColor(object sender, QuickStripSegmentedButtonControl.ActionEventArgs e)
        {
            // FIXME: incomplete, for demo purposes only
            if (e.SelectedIndex == 0)
            {
                _ = session.Apply(new Dictionary<Preferences.Key, object?>
                {
                    { SettingsManager.Keys.WindowsCursorArrow, "%SystemRoot%\\cursors\\aero_arrow.cur" },
                    { SettingsManager.Keys.WindowsCursorWait, "%SystemRoot%\\cursors\\aero_busy.ani" },
                });
            }
            else
            {
                _ = session.Apply(new Dictionary<Preferences.Key, object?>
                {
                    { SettingsManager.Keys.WindowsCursorArrow, "%SystemRoot%\\cursors\\arrow_r.cur" },
                    { SettingsManager.Keys.WindowsCursorWait, "%SystemRoot%\\cursors\\busy_r.cur" },
                });
            }
        }

        #endregion

        #region Item Controls

        private List<QuickStripItemControl> itemControls = new List<QuickStripItemControl>();

        private void UpdateControls()
        {
            ControlStack.Children.RemoveRange(0, ControlStack.Children.Count);
            itemControls.Clear();
            foreach (var item in items)
            {
                if (item.GetControl(this) is QuickStripItemControl control)
                {
                    control.Margin = new Thickness(0, 0, 18, 0);
                    itemControls.Add(control);
                    ControlStack.Children.Add(control);
                }
            }
        }

        #endregion

        #region Layout & Position

        /// <summary>
        /// Amount the Quick Strip should be inset from the edge of each screen
        /// </summary>
        public double ScreenEdgeInset = 4;

        /// <summary>
        /// The possible positions for the quick strip 
        /// </summary>
        public enum FixedPosition
        {
            BottomRight,
            BottomLeft,
            TopLeft,
            TopRight
        }

        /// <summary>
        /// The preferred position of the quick strip
        /// </summary>
        private FixedPosition position = QuickStripWindow.FixedPosition.BottomRight;

        /// <summary>
        /// The preferred position of the quick strip
        /// </summary>
        public FixedPosition Position
        {
            get
            {
                return position;
            }
            set
            {
                if (value != position)
                {
                    position = value;
                    session.SetPreference(PreferenceKeys.Position, position.ToString());
                }
                Reposition(animated: true);
            }
        }

        /// <summary>
        /// Reposition the Quick Strip to its current fixed position
        /// </summary>
        /// <param name="animated"></param>
        private void Reposition(bool animated)
        {
            var screenSize = SystemParameters.WorkArea;
            double top = Top;
            double left = Left;
            switch (position)
            {
                case FixedPosition.BottomRight:
                    top = screenSize.Height - Height - ScreenEdgeInset;
                    left = screenSize.Width - Width - ScreenEdgeInset;
                    break;
                case FixedPosition.BottomLeft:
                    top = screenSize.Height - Height - ScreenEdgeInset;
                    left = ScreenEdgeInset;
                    break;
                case FixedPosition.TopLeft:
                    top = ScreenEdgeInset;
                    left = ScreenEdgeInset;
                    break;
                case FixedPosition.TopRight:
                    top = ScreenEdgeInset;
                    left = screenSize.Width - Width - ScreenEdgeInset;
                    break;
            }
            if (animated)
            {
                var duration = new Duration(TimeSpan.FromSeconds(0.5));
                var topAnimation = new DoubleAnimation();
                topAnimation.From = Top;
                topAnimation.To = top;
                topAnimation.Duration = duration;
                topAnimation.FillBehavior = FillBehavior.Stop;

                var leftAnimation = new DoubleAnimation();
                leftAnimation.From = Left;
                leftAnimation.To = left;
                leftAnimation.Duration = duration;
                leftAnimation.FillBehavior = FillBehavior.Stop;

                BeginAnimation(Window.TopProperty, topAnimation);
                BeginAnimation(Window.LeftProperty, leftAnimation);
            }
            else
            {
                Top = top;
                Left = left;
            }
        }

        /// <summary>
        /// Get the nearest fixed position to the window's current position
        /// </summary>
        /// <remarks>
        /// Helpful for snapping into the correct place after the user moves the window
        /// </remarks>
        private FixedPosition NearestPosition
        {
            get
            {
                var screenSize = SystemParameters.WorkArea;
                if (Left < screenSize.Width / 2){
                    if (Top < screenSize.Height / 2)
                    {
                        return FixedPosition.TopLeft;
                    }
                    return FixedPosition.BottomLeft;
                }
                else
                {
                    if (Top < screenSize.Height / 2)
                    {
                        return FixedPosition.TopRight;
                    }
                    return FixedPosition.BottomRight;
                }
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Event handler for mouse down to move the window
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                DragMove();
            }
        }

        /// <summary>
        /// Event handler for mouse up to snap into place after moving the window
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Countly.RecordEvent("window-moved");
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                Position = NearestPosition;
            }
        }

        #endregion

        private void LogoButton_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            QuickHelpWindow.Show(Properties.Resources.QuickStrip_Advanced_HelpTitle, Properties.Resources.QuickStrip_Advanced_HelpMessage);
        }

        private void LogoButton_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            QuickHelpWindow.Dismiss();
        }

        private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Activate();
        }

        private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
        }

        public static class PreferenceKeys
        {
            public static Preferences.Key Visible = new Preferences.Key("org.raisingthefloor.morphic.quickstrip", "visible");
            public static Preferences.Key Position = new Preferences.Key("org.raisingthefloor.morphic.quickstrip", "position.win");
            public static Preferences.Key ShowsHelp = new Preferences.Key("org.raisingthefloor.morphic.quickstrip", "showsHelp");
            public static Preferences.Key Items = new Preferences.Key("org.raisingthefloor.morphic.quickstrip", "items");
        }
    }
}
