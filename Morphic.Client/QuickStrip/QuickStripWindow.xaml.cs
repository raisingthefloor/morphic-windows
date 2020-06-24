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
            Countly.RecordEvent("Main Menu");
            LogoButton.ContextMenu.IsOpen = true;
        }

        /// <summary>
        /// Event handler for when the user selects Hide Quick Strip from the logo button's menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HideQuickStrip(object sender, RoutedEventArgs e)
        {
            Countly.RecordEvent("Hide MorphicBar");
            App.Shared.HideQuickStrip();
        }

        /// <summary>
        /// Event handler for when the user selects Customize Quick Strip from the logo button's menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CustomizeQuickStrip(object sender, RoutedEventArgs e)
        {
            Countly.RecordEvent("Customize MorphicBar");
        }

        private void AboutMorphic(object sender, RoutedEventArgs e)
        {
            Countly.RecordEvent("About");
            App.Shared.OpenAboutWindow();
        }

        private void TravelWithSettings(object sender, RoutedEventArgs e)
        {
            Countly.RecordEvent("Travel");
            App.Shared.OpenTravelWindow();
        }

        private void ApplyMySettings(object sender, RoutedEventArgs e)
        {
            Countly.RecordEvent("Login");
            App.Shared.OpenLoginWindow();
        }

        private void Logout(object sender, RoutedEventArgs e)
        {
            Countly.RecordEvent("Logout");
            _ = session.Signout();
        }

        /// <summary>
        /// Event handler for when the user selects Quit from the logo button's menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Quit(object sender, RoutedEventArgs e)
        {
            Countly.RecordEvent("Quit");
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
                            var biggerHelp = new TextZoomInHelpControlBuilder(Display.Primary);
                            var smallerHelp = new TextZoomOutHelpControlBuilder(Display.Primary);
                            control.TitleLabel.Content = Properties.Resources.QuickStrip_Resolution_Title;
                            control.AddButton(new Image() { Source = new BitmapImage(new Uri("Plus.png", UriKind.Relative)) }, Properties.Resources.QuickStrip_Resolution_Bigger_HelpTitle, biggerHelp, isPrimary: true);
                            control.AddButton(new Image() { Source = new BitmapImage(new Uri("Minus.png", UriKind.Relative)) }, Properties.Resources.QuickStrip_Resolution_Smaller_HelpTitle, smallerHelp, isPrimary: false);
                            control.Action += quickStrip.Zoom;
                            return control;
                        }
                    case "magnifier":
                        {
                            var control = new QuickStripSegmentedButtonControl();
                            var showHelp = new QuickHelpTextControlBuilder(Properties.Resources.QuickStrip_Magnifier_Show_HelpTitle, Properties.Resources.QuickStrip_Magnifier_Show_HelpMessage);
                            var hideHelp = new QuickHelpTextControlBuilder(Properties.Resources.QuickStrip_Magnifier_Hide_HelpTitle, Properties.Resources.QuickStrip_Magnifier_Hide_HelpMessage);
                            control.TitleLabel.Content = Properties.Resources.QuickStrip_Magnifier_Title;
                            control.AddButton(Properties.Resources.QuickStrip_Magnifier_Show_Title, showHelp.Title, showHelp, isPrimary: true);
                            control.AddButton(Properties.Resources.QuickStrip_Magnifier_Hide_Title, hideHelp.Title, hideHelp, isPrimary: false);
                            control.Action += quickStrip.OnMagnify;
                            return control;
                        }
                    case "reader":
                        {
                            var control = new QuickStripSegmentedButtonControl();
                            var startHelp = new QuickHelpTextControlBuilder(Properties.Resources.QuickStrip_Reader_Start_HelpTitle, Properties.Resources.QuickStrip_Reader_Start_HelpMessage);
                            var stopHelp = new QuickHelpTextControlBuilder(Properties.Resources.QuickStrip_Reader_Stop_HelpTitle, Properties.Resources.QuickStrip_Reader_Stop_HelpMessage);
                            var pauseHelp = new QuickHelpTextControlBuilder(Properties.Resources.QuickStrip_Reader_Pause_HelpTitle, Properties.Resources.QuickStrip_Reader_Pause_HelpMessage);
                            control.TitleLabel.Content = Properties.Resources.QuickStrip_Reader_Title;
                            control.AddButton("\u25b6", startHelp.Title, startHelp, isPrimary: true);
                            control.AddButton("||", pauseHelp.Title, pauseHelp, isPrimary: false);
                            control.AddButton("\u25a0", stopHelp.Title, stopHelp, isPrimary: false);
                            control.Action += quickStrip.OnReader;
                            return control;
                        }
                    case "volume":
                        {
                            var control = new QuickStripSegmentedButtonControl();
                            var upHelp = new VolumeUpHelpControlBuilder(Audio.DefaultOutputEndpoint);
                            var downHelp = new VolumeDownHelpControlBuilder(Audio.DefaultOutputEndpoint);
                            var muteHelp = new VolumeMuteHelpControlBuilder(Audio.DefaultOutputEndpoint);
                            control.TitleLabel.Content = Properties.Resources.QuickStrip_Volume_Title;
                            control.AddButton(new Image() { Source = new BitmapImage(new Uri("Plus.png", UriKind.Relative)) }, Properties.Resources.QuickStrip_Volume_Up_HelpTitle, upHelp, isPrimary: true);
                            control.AddButton(new Image() { Source = new BitmapImage(new Uri("Minus.png", UriKind.Relative)) }, Properties.Resources.QuickStrip_Volume_Down_HelpTitle, downHelp, isPrimary: false);
                            control.AddButton(Properties.Resources.QuickStrip_Volume_Mute_Title, Properties.Resources.QuickStrip_Volume_Mute_HelpTitle, muteHelp, isPrimary: true);
                            control.Action += quickStrip.OnVolume;
                            return control;
                        }
                    case "contrast":
                        {
                            var control = new QuickStripSegmentedButtonControl();
                            var onHelp = new QuickHelpTextControlBuilder(Properties.Resources.QuickStrip_Contrast_On_HelpTitle, Properties.Resources.QuickStrip_Contrast_On_HelpMessage);
                            var offHelp = new QuickHelpTextControlBuilder(Properties.Resources.QuickStrip_Contrast_Off_HelpTitle, Properties.Resources.QuickStrip_Contrast_Off_HelpMessage);
                            control.TitleLabel.Content = Properties.Resources.QuickStrip_Contrast_Title;
                            control.AddButton(Properties.Resources.QuickStrip_Contrast_On_Title, onHelp.Title, onHelp, isPrimary: true);
                            control.AddButton(Properties.Resources.QuickStrip_Contrast_Off_Title, offHelp.Title, offHelp, isPrimary: false);
                            control.Action += quickStrip.OnContrast;
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
            double percentage = 1.0;
            if (e.SelectedIndex == 0)
            {
                Countly.RecordEvent("Text Larger");
                percentage = Display.Primary.PercentageForZoomingIn;
            }
            else
            {
                Countly.RecordEvent("Text Smaller");
                percentage = Display.Primary.PercentageForZoomingOut;
            }
            _ = session.Apply(SettingsManager.Keys.WindowsDisplayZoom, percentage);
        }

        /// <summary>Original magnifier settings.</summary>
        private Dictionary<Preferences.Key, object?> magnifyCapture;
        
        private async void OnMagnify(object sender, QuickStripSegmentedButtonControl.ActionEventArgs e)
        {
            if (e.SelectedIndex == 0)
            {
                _ = Countly.RecordEvent("Show Magnifier");
                // Enable lens mode at 200%
                Dictionary<Preferences.Key, object?> settings = new Dictionary<Preferences.Key, object?>
                {
                    {SettingsManager.Keys.WindowsMagnifierMode, (long)3},
                    {SettingsManager.Keys.WindowsMagnifierMagnification, (long)200},
                    {SettingsManager.Keys.WindowsMagnifierEnabled, true},
                };

                if (this.magnifyCapture == null)
                {
                    // capture the current settings
                    this.magnifyCapture = await this.session.SettingsManager.Capture(settings.Keys);
                }

                await session.Apply(settings);
            }
            else if (e.SelectedIndex == 1)
            {
                _ = Countly.RecordEvent("Hide Magnifier");
                // restore settings
                await session.Apply(SettingsManager.Keys.WindowsMagnifierEnabled, false);
                if (this.magnifyCapture != null)
                {
                    await this.session.Apply(this.magnifyCapture);
                    this.magnifyCapture = null;
                }
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
                    _ = Countly.RecordEvent("Read Selection");
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
                    _ = Countly.RecordEvent("Pause Reading");
                    speech.TogglePause();
                    break;
                // Stop
                case 2 when speech.Active:
                    _ = Countly.RecordEvent("Stop Reading");
                    speech.StopSpeaking();
                    break;
            }
        }

        private void OnVolume(object sender, QuickStripSegmentedButtonControl.ActionEventArgs e)
        {
            var endpoint = Audio.DefaultOutputEndpoint;
            if (e.SelectedIndex == 0)
            {
                _ = Countly.RecordEvent("Volume Up");
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
                _ = Countly.RecordEvent("Volume Down");
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
            if (e.SelectedIndex == 0)
            {
                Countly.RecordEvent("Contrast On");
                _ = session.Apply(SettingsManager.Keys.WindowsDisplayContrastEnabled, true);
            }
            else if (e.SelectedIndex == 1)
            {
                Countly.RecordEvent("Contrast Off");
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
                    left = Math.Max(ScreenEdgeInset, screenSize.Width - Width - ScreenEdgeInset);
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
                    left = Math.Max(ScreenEdgeInset, screenSize.Width - Width - ScreenEdgeInset);
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
            Countly.RecordEvent("Move MorphicBar");
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                Position = NearestPosition;
            }
        }

        #endregion

        private void LogoButton_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var help = new QuickHelpTextControlBuilder(Properties.Resources.QuickStrip_Advanced_HelpTitle, Properties.Resources.QuickStrip_Advanced_HelpMessage);
            QuickHelpWindow.Show(help);
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
