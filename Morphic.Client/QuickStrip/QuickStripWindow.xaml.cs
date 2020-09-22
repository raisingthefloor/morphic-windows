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


namespace Morphic.Client.QuickStrip
{
    using System;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using Morphic.Core;
    using Morphic.Service;
    using Morphic.Settings;
    using System.Windows.Media.Animation;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Windows.Interop;
    using System.Windows.Media.Imaging;
    using Microsoft.Win32;
    using CountlySDK;
    using System.IO;
    using System.Media;
    using System.Runtime.InteropServices;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Windows.Forms;
    using System.Windows.Input;
    using Windows.Native;
    using global::Windows.Media.SpeechSynthesis;
    using Clipboard = System.Windows.Clipboard;
    using Control = System.Windows.Controls.Control;
    using IDataObject = System.Windows.IDataObject;
    using Keyboard = System.Windows.Input.Keyboard;
    using KeyEventArgs = System.Windows.Input.KeyEventArgs;
    using Display = Morphic.Settings.Display;

    /// <summary>
    /// Interaction logic for QuickStripWindow.xaml
    /// </summary>
    public partial class QuickStripWindow : Window, IMessageHook
    {

        public WindowMessageHook Messages { get; }

        #region Initialization

        public QuickStripWindow(Session session)
        {
            this.Messages = new WindowMessageHook(this);
            this.session = session;
            session.UserChanged += Session_UserChanged;
            InitializeComponent();
            Deactivated += OnDeactivated;
            SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
            App.Shared.SystemSettingChanged += (sender, args) => { this.UpdateState(); };
            this.MouseEnter += (sender, args) => this.UpdateState();
            this.IsVisibleChanged += (sender, args) =>
            {
                this.HideHelp();
            };

            this.Closing += (sender, args) =>
            {
                this.HideHelp();
                this.speechPlayer.Stop();
            };
            this.speechPlayer.LoadCompleted += (o, args) =>
            {
                this.speechPlayer.Play();
            };
        }

        /// <summary>
        /// Makes the controls update their state, to reflect an external change.
        /// </summary>
        public void UpdateState()
        {
            foreach (QuickStripSegmentedButtonControl control in this.ControlStack.Children
                .OfType<QuickStripSegmentedButtonControl>())
            {
                control.UpdateStates();
            }
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
            this.HideHelp();
        }

        private void Update()
        {
            if (Enum.TryParse<FixedPosition>(session.GetString(PreferenceKeys.Position), out var position))
            {
                this.position = position;
            }

            QuickStripJson qs = QuickStripJson.FromFile("quickstrip.json");
            Items = Item.CreateItems(qs.Items);
        }

        #endregion

        #region Logo Button & Menu

        /// <summary>
        /// Event handler for when the user clicks on the logo button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LogoButton_MouseUp(object sender, RoutedEventArgs e)
        {
            Countly.RecordEvent("Main Menu");
            App.Shared.ShowMenu(sender as Control);
        }

        /// <summary>
        /// Event handler for when the user clicks on the logo button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LogoButton_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Apps:
                case Key.F10 when Keyboard.Modifiers == ModifierKeys.Shift:
                case Key.Space when Keyboard.Modifiers == ModifierKeys.Shift:
                case Key.Enter when Keyboard.Modifiers == ModifierKeys.Shift:
                    Countly.RecordEvent("Main Menu");
                    App.Shared.ShowMenu();
                    e.Handled = true;
                    break;
            }
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
            public static List<Item> CreateItems(IEnumerable<object?> descriptions)
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
                            control.AddButton(new Image() { Source = new BitmapImage(new Uri("Plus.png", UriKind.Relative)) }, Properties.Resources.QuickStrip_Resolution_Bigger_Name, biggerHelp, isPrimary: true);
                            control.AddButton(new Image() { Source = new BitmapImage(new Uri("Minus.png", UriKind.Relative)) }, Properties.Resources.QuickStrip_Resolution_Smaller_Name, smallerHelp, isPrimary: false);
                            control.Action += quickStrip.Zoom;
                            control.SetContextItems(new []
                            {
                                ("setting", "display"),
                                ("learn", "textsize"),
                                ("demo", "textsize")
                            });
                            return control;
                        }
                    case "magnifier":
                        {
                            var control = new QuickStripSegmentedButtonControl();
                            var showHelp = new QuickHelpTextControlBuilder(Properties.Resources.QuickStrip_Magnifier_Show_HelpTitle, Properties.Resources.QuickStrip_Magnifier_Show_HelpMessage);
                            var hideHelp = new QuickHelpTextControlBuilder(Properties.Resources.QuickStrip_Magnifier_Hide_HelpTitle, Properties.Resources.QuickStrip_Magnifier_Hide_HelpMessage);
                            control.TitleLabel.Content = Properties.Resources.QuickStrip_Magnifier_Title;
                            control.AddButton(Properties.Resources.QuickStrip_Magnifier_Show_Title, Properties.Resources.QuickStrip_Magnifier_Show_Name, showHelp, isPrimary: true);
                            control.AddButton(Properties.Resources.QuickStrip_Magnifier_Hide_Title, Properties.Resources.QuickStrip_Magnifier_Hide_Name, hideHelp, isPrimary: false);
                            control.Action += quickStrip.OnMagnify;
                            control.SetContextItems(new []
                            {
                                ("setting", "easeofaccess-magnifier"),
                                ("learn", "magnifier"),
                                ("demo", "magnifier")
                            });
                            return control;
                        }
                    case "reader":
                        {
                            var control = new QuickStripSegmentedButtonControl();
                            var startHelp = new QuickHelpTextControlBuilder(Properties.Resources.QuickStrip_Reader_Start_HelpTitle, Properties.Resources.QuickStrip_Reader_Start_HelpMessage);
                            var stopHelp = new QuickHelpTextControlBuilder(Properties.Resources.QuickStrip_Reader_Stop_HelpTitle, Properties.Resources.QuickStrip_Reader_Stop_HelpMessage);
                            control.TitleLabel.Content = Properties.Resources.QuickStrip_Reader_Title;
                            control.AddButton("\u25b6", Properties.Resources.QuickStrip_Reader_Start_Name, startHelp, isPrimary: true);
                            control.AddButton("\u25a0", Properties.Resources.QuickStrip_Reader_Stop_Name, stopHelp, isPrimary: false);
                            //control.EnableButton(1, false);
                            control.Action += quickStrip.OnReader;
                            control.SetContextItems(new []
                            {
                                ("setting", "speech"),
                                ("learn", "readsel-pc"),
                                ("demo", "readsel-pc")
                            });

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
                    case "nightmode":
                        {
                            var control = new QuickStripSegmentedButtonControl();
                            var onHelp = new QuickHelpTextControlBuilder(Properties.Resources.QuickStrip_NightMode_On_HelpTitle, Properties.Resources.QuickStrip_NightMode_On_HelpMessage);
                            var offHelp = new QuickHelpTextControlBuilder(Properties.Resources.QuickStrip_NightMode_Off_HelpTitle, Properties.Resources.QuickStrip_NightMode_Off_HelpMessage);
                            control.TitleLabel.Content = Properties.Resources.QuickStrip_NightMode_Title;
                            control.AddButton(Properties.Resources.QuickStrip_NightMode_On_Title, onHelp.Title, onHelp, isPrimary: true);
                            control.AddButton(Properties.Resources.QuickStrip_NightMode_Off_Title, offHelp.Title, offHelp, isPrimary: false);
                            control.Action += quickStrip.OnNightMode;
                            return control;
                        }
                    case "snip":
                        {
                            var control = new QuickStripSegmentedButtonControl();
                            control.ItemCount = 1;
                            var copyButtonHelp = new QuickHelpTextControlBuilder(Properties.Resources.QuickStrip_Snip_HelpTitle, Properties.Resources.QuickStrip_Snip_HelpMessage);
                            control.TitleLabel.Content = Properties.Resources.QuickStrip_Snip_Title;
                            control.AddButton(Properties.Resources.QuickStrip_Snip_Button_Title, Properties.Resources.QuickStrip_Snip_Name, copyButtonHelp, isPrimary: false);
                            control.Action += quickStrip.OnSnip;
                            control.SetContextItems(new[]
                            {
                                ("learn", "snip"),
                                ("demo", "snip")
                            });
                            return control;
                        }
                    case "colors":
                        {
                            var control = new QuickStripSegmentedButtonControl();
                            control.ItemCount = 4;

                            control.TitleLabel.Content = Properties.Resources.QuickStrip_Colors_Title;

                            var contrastHelp = new QuickHelpTextControlBuilder(Properties.Resources.QuickStrip_Contrast_On_HelpTitle, Properties.Resources.QuickStrip_Contrast_On_HelpMessage);
                            control.AddToggle(Properties.Resources.QuickStrip_Colors_Contrast_Title, Properties.Resources.QuickStrip_Colors_Contrast_Name, contrastHelp)
                                .Automate(quickStrip.session, SettingsManager.Keys.WindowsDisplayContrastEnabled, applySetting: false)
                                .Helper.SetContextItems(new []
                                {
                                    ("setting", "easeofaccess-highcontrast"),
                                    ("learn", "contrast"),
                                    ("demo", "contrast")
                                });

                            var colorHelp = new QuickHelpTextControlBuilder(Properties.Resources.QuickStrip_Colors_Color_HelpTitle, Properties.Resources.QuickStrip_Colors_Color_HelpMessage);
                            control.AddToggle(Properties.Resources.QuickStrip_Colors_Color_Title, Properties.Resources.QuickStrip_Colors_Color_Name, colorHelp)
                                .Automate(quickStrip.session, SettingsManager.Keys.WindowsDisplayColorFilterEnabled)
                                .Helper.SetContextItems(new []
                                {
                                    ("setting", "easeofaccess-colorfilter"),
                                    ("learn", "colorvision"),
                                    ("demo", "colorvision")
                                });

                            var darkHelp = new QuickHelpTextControlBuilder(Properties.Resources.QuickStrip_Colors_Dark_HelpTitle, Properties.Resources.QuickStrip_Colors_Dark_HelpMessage);
                            control.AddToggle(Properties.Resources.QuickStrip_Colors_Dark_Title, Properties.Resources.QuickStrip_Colors_Dark_Name, darkHelp)
                                .Automate(quickStrip.session, SettingsManager.Keys.WindowsDisplayLightAppsThemeEnabled,
                                    applySetting: false, onValue: false, offValue: true)
                                .Helper.SetContextItems(new []
                                {
                                    ("setting", "colors"),
                                    ("learn", "darkmode"),
                                    ("demo", "darkmode")
                                });

                            var nightHelp = new QuickHelpTextControlBuilder(Properties.Resources.QuickStrip_NightMode_On_HelpTitle, Properties.Resources.QuickStrip_NightMode_On_HelpMessage);
                            control.AddToggle(Properties.Resources.QuickStrip_Colors_Night_Title, Properties.Resources.QuickStrip_Colors_Night_Name, nightHelp)
                                .Automate(quickStrip.session, SettingsManager.Keys.WindowsDisplayNightModeEnabled, false)
                                .Helper.SetContextItems(new []
                                {
                                    ("setting", "nightlight"),
                                    ("learn", "nightmode"),
                                    ("demo", "nightmode")
                                });

                            //control.SpaceButtons();
                            control.Action += quickStrip.OnColors;

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
                Process? process = Process.Start(new ProcessStartInfo("magnify.exe", "/lens")
                {
                    UseShellExecute = true
                });
            }
            else if (e.SelectedIndex == 1)
            {
                _ = Countly.RecordEvent("Hide Magnifier");
                foreach (Process process in Process.GetProcessesByName("magnify"))
                {
                    process.Kill();
                }
            }
        }

        private readonly SoundPlayer speechPlayer = new SoundPlayer();

        private async void OnReader(object sender, QuickStripSegmentedButtonControl.ActionEventArgs e)
        {
            // Play/Pause
            if (e.SelectedIndex == 0)
            {
                _ = Countly.RecordEvent("Read Selection");
                string text = await QuickStripWindow.GetSelectedText(SelectionReader.Default);

                this.speechPlayer.Stop();

                using SpeechSynthesizer synth = new SpeechSynthesizer();
                using SpeechSynthesisStream stream = await synth.SynthesizeTextToStreamAsync(text);

                this.speechPlayer.Stream = stream.AsStream();
                this.speechPlayer.LoadAsync();
            }
            else
            {
                _ = Countly.RecordEvent("Stop Reading");
                this.speechPlayer.Stop();
            }
        }

        private static async Task<string> GetSelectedText(SelectionReader reader)
        {
            Dictionary<string, object?>? dataStored = null;
            try
            {
                // Store the clipboard
                IDataObject? clipboardData = Clipboard.GetDataObject();
                dataStored = clipboardData?.GetFormats()
                    .ToDictionary(format => format, format =>
                    {
                        try
                        {
                            return clipboardData.GetData(format, false);
                        }
                        catch (COMException)
                        {
                            return null;
                        }
                    });
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                // ignore
            }

            Clipboard.Clear();

            // Get the selection
            await reader.GetSelectedText(SendKeys.SendWait);
            string text = Clipboard.GetText();

            // Restore the clipboard
            Clipboard.Clear();
            try
            {
                dataStored?.Where(kv => kv.Value != null).ToList()
                    .ForEach(kv => Clipboard.SetData(kv.Key, kv.Value!));
                Clipboard.Flush();
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                // ignore
            }

            return text;
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

        private void OnNightMode(object sender, QuickStripSegmentedButtonControl.ActionEventArgs e)
        {
            if (e.SelectedIndex == 0)
            {
                Countly.RecordEvent("NightMode On");
                _ = session.Apply(SettingsManager.Keys.WindowsDisplayNightModeEnabled, true);
            }
            else if (e.SelectedIndex == 1)
            {
                Countly.RecordEvent("NightMode Off");
                _ = session.Apply(SettingsManager.Keys.WindowsDisplayNightModeEnabled, false);
            }
        }

        private bool? initialAppsLight;
        private bool? initialWindowsLight;
        private bool? initialDarkMode;

        private async void OnColors(object sender, QuickStripSegmentedButtonControl.ActionEventArgs e)
        {
            if (e.SelectedName == Properties.Resources.QuickStrip_Colors_Contrast_Name)
            {
                // Setting via the system settings method doesn't respect the high-contrast theme set during
                // first run - running sethc directly instead.
                ProcessStartInfo startInfo = new ProcessStartInfo("sethc.exe")
                {
                    UseShellExecute = true,
                };

                startInfo.ArgumentList.Add(e.ToggleState ? "100" : "1");

                Process.Start(startInfo);

                Countly.RecordEvent("Contrast toggle");

            } else if (e.SelectedName == Properties.Resources.QuickStrip_Colors_Dark_Name)
            {
                Countly.RecordEvent("Darkmode toggle");

                bool newValue = !e.ToggleState;

                Preferences.Key lightAppsSetting = SettingsManager.Keys.WindowsDisplayLightAppsThemeEnabled;
                Preferences.Key lightWindowsSetting = SettingsManager.Keys.WindowsDisplayLightWindowsThemeEnabled;

                this.initialDarkMode ??= !e.ToggleState;
                this.initialAppsLight ??= await this.session.SettingsManager.CaptureBool(lightAppsSetting)
                    ?? false;
                this.initialWindowsLight ??= await this.session.SettingsManager.CaptureBool(lightWindowsSetting)
                    ?? false;

                // Turning the toggle back to its initial state puts the settings back to their original value.
                if (e.ToggleState == this.initialDarkMode)
                {
                    _ = session.Apply(lightAppsSetting, this.initialAppsLight);
                    _ = session.Apply(lightWindowsSetting, this.initialWindowsLight);
                }
                else
                {
                    _ = session.Apply(lightAppsSetting, newValue);
                    _ = session.Apply(lightWindowsSetting, newValue);
                }
            }
        }

        private void OnSnip(object sender, QuickStripSegmentedButtonControl.ActionEventArgs e)
        {
            if (e.SelectedIndex == 0)
            {
                Countly.RecordEvent("Snip Copy");

                // Hide the qs while the snipper tool captures the screen.
                this.Opacity = 0;
                QuickHelpWindow.Dismiss(true);

                // Hold down the windows key while pressing shift + s
                const uint windowsKey = 0x5b; // VK_LWIN
                Morphic.Windows.Native.Keyboard.PressKey(windowsKey, true);
                SendKeys.SendWait("+s");
                Morphic.Windows.Native.Keyboard.PressKey(windowsKey, false);

                // Show the qs again
                Task.Delay(3000).ContinueWith(task => this.Dispatcher.Invoke(() => this.Opacity = 1));
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

        /// <summary>
        /// Hides the help window.
        /// </summary>
        public void HideHelp()
        {
            QuickHelpWindow.Dismiss();
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
                if (Left + Width / 2 < screenSize.Width / 2){
                    if (Top + Height / 2 < screenSize.Height / 2)
                    {
                        return FixedPosition.TopLeft;
                    }
                    return FixedPosition.BottomLeft;
                }
                else
                {
                    if (Top + Height / 2 < screenSize.Height / 2)
                    {
                        return FixedPosition.TopRight;
                    }
                    return FixedPosition.BottomRight;
                }
            }
        }

        #endregion

        #region Events

        private Point mouseDownPos;

        /// <summary>
        /// Event handler for mouse down to move the window
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.mouseDownPos = e.GetPosition(this);
            }
        }

        /// <summary>
        /// Move the window when the mouse moves enough for it to be a drag action.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point point = e.GetPosition(this);

                if (Math.Abs(point.X - this.mouseDownPos.X) >= SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(point.Y - this.mouseDownPos.Y) >= SystemParameters.MinimumVerticalDragDistance)
                {
                    this.DragMove();
                    Countly.RecordEvent("Move MorphicBar");
                    Position = NearestPosition;
                }
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

        private void LogoButton_GotFocus(object sender, EventArgs e)
        {
            if (InputManager.Current.MostRecentInputDevice is KeyboardDevice)
            {
                SystemSounds.Beep.Play();
            }
        }

        private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Activate();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                // Make the up and down arrows move focus like left/right
                case Key.Up:
                case Key.Down:
                    if (FocusManager.GetFocusedElement(this) is FrameworkElement elem)
                    {
                        FocusNavigationDirection direction = e.Key == Key.Up
                            ? FocusNavigationDirection.Left
                            : FocusNavigationDirection.Right;
                        elem.MoveFocus(new TraversalRequest(direction));
                    }

                    e.Handled = true;
                    break;
                case Key.System when e.SystemKey == Key.F4:
                    e.Handled = true;
                    App.Shared.HideQuickStrip();
                    break;

            }
        }

        public static class PreferenceKeys
        {
            public static Preferences.Key Visible = new Preferences.Key("org.raisingthefloor.morphic.quickstrip", "visible");
            public static Preferences.Key Position = new Preferences.Key("org.raisingthefloor.morphic.quickstrip", "position.win");
            public static Preferences.Key ShowsHelp = new Preferences.Key("org.raisingthefloor.morphic.quickstrip", "showsHelp");
            public static Preferences.Key Items = new Preferences.Key("org.raisingthefloor.morphic.quickstrip", "items");
        }

        /// <summary>
        /// Static configuration for the quick strip
        /// </summary>
        public class QuickStripJson
        {
            public static QuickStripJson FromFile(string file)
            {
                string json = File.ReadAllText(file);
                JsonSerializerOptions options = new JsonSerializerOptions();
                options.Converters.Add(new JsonElementInferredTypeConverter());
                return JsonSerializer.Deserialize<QuickStripJson>(json, options);
            }

            [JsonPropertyName("items")]
            public List<Dictionary<string, object>> Items { get; set; }
        }

        public void Dispose()
        {
            this.speechPlayer.Dispose();
            this.Messages.Dispose();
        }

        /// <summary>
        /// Ensure the first item is focused.
        /// </summary>
        /// <param name="keyboard"></param>
        public void FocusFirstItem(bool keyboard = false)
        {
            this.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            if (keyboard)
            {
                this.SetKeyboardFocus();
            }
        }

        public void SetKeyboardFocus()
        {
            this.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            this.MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous));
        }
    }
}
