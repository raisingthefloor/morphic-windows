namespace Morphic.Client.Bar.Data.Actions
{
    using Microsoft.Extensions.Logging;
    using Morphic.Core;
    using Settings.SettingsHandlers;
    using Settings.SolutionsRegistry;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Automation;
    using UI;
    using Windows.Native.Input;
    using Windows.Native.Speech;

    [HasInternalFunctions]
    // ReSharper disable once UnusedType.Global - accessed via reflection.
    public class Functions
    {
        private readonly static SemaphoreSlim _readAloudSemaphore = new SemaphoreSlim(1, 1);

        [InternalFunction("screenshot")]
        public static async Task<IMorphicResult> ScreenshotAsync(FunctionArgs args)
        {
            // Hide all application windows
            Dictionary<Window, double> opacity = new Dictionary<Window, double>();
            HashSet<Window> visible = new HashSet<Window>();
            try
            {
                foreach (Window window in App.Current.Windows)
                {
                    if (window is BarWindow || window is QuickHelpWindow)
                    {
                        if (window.AllowsTransparency)
                        {
                            opacity[window] = window.Opacity;
                            window.Opacity = 0;
                        }
                        else
                        {
                            visible.Add(window);
                            window.Visibility = Visibility.Collapsed;
                        }
                    }
                }

                // Give enough time for the windows to disappear
                await Task.Delay(500);

                // Hold down the windows key while pressing shift + s
                const uint windowsKey = 0x5b; // VK_LWIN
                Keyboard.PressKey(windowsKey, true);
                System.Windows.Forms.SendKeys.SendWait("+s");
                Keyboard.PressKey(windowsKey, false);

            }
            finally
            {
                // Give enough time for snip tool to grab the screen without the morphic UI.
                await Task.Delay(3000);

                // Restore the windows
                foreach ((Window window, double o) in opacity)
                {
                    window.Opacity = o;
                }

                foreach (Window window in visible)
                {
                    window.Visibility = Visibility.Visible;
                }
            }

            return IMorphicResult.SuccessResult;
        }

        [InternalFunction("menu", "key=Morphic")]
        public async static Task<IMorphicResult> ShowMenuAsync(FunctionArgs args)
        {
            // NOTE: this internal function is only called by the MorphicBar's Morphie menu button
            await App.Current.ShowMenuAsync(null, Morphic.Client.Menu.MorphicMenu.MenuOpenedSource.morphicBarIcon);
            return IMorphicResult.SuccessResult;
        }

        /// <summary>
        /// Lowers or raises the volume.
        /// </summary>
        /// <param name="args">direction: "up"/"down", amount: number of 1/100 to move</param>
        /// <returns></returns>
        [InternalFunction("volume", "direction", "amount=10")]
        public static async Task<IMorphicResult> SetVolumeAsync(FunctionArgs args)
        {
            IntPtr taskTray = WinApi.FindWindow("Shell_TrayWnd", IntPtr.Zero);
            if (taskTray != IntPtr.Zero)
            {
                int action = args["direction"] == "up"
                    ? WinApi.APPCOMMAND_VOLUME_UP
                    : WinApi.APPCOMMAND_VOLUME_DOWN;

                // Each command moves the volume by 2 notches.
                int times = Math.Clamp(Convert.ToInt32(args["amount"]), 1, 20) / 2;
                for (int n = 0; n < times; n++)
                {
                    WinApi.SendMessage(taskTray, WinApi.WM_APPCOMMAND, IntPtr.Zero,
                        (IntPtr)WinApi.MakeLong(0, (short)action));
                }
            }

            return IMorphicResult.SuccessResult;
        }

        /// <summary>
        /// Reads the selected text.
        /// </summary>
        /// <param name="args">action: "play", "pause", or "stop"</param>
        /// <returns></returns>
        [InternalFunction("readAloud", "action")]
        public static async Task<IMorphicResult> ReadAloudAsync(FunctionArgs args)
        {
            var result = IMorphicResult.SuccessResult;

            string action = args["action"];
            switch (action)
            {
                case "pause":
                    App.Current.Logger.LogError("ReadAloud: pause not supported.");
                    result = IMorphicResult.ErrorResult;
                    break;

                case "stop":
                    App.Current.Logger.LogDebug("ReadAloud: Stop reading selected text.");
                    TextToSpeechHelper.Instance.Stop();
                    break;

                case "play":
                    await _readAloudSemaphore.WaitAsync();

                    try
                    {
                        App.Current.Logger.LogDebug("ReadAloud: Getting selected text.");

                        await SelectionReader.Default.ActivateLastActiveWindow();
                        var focusedElement = AutomationElement.FocusedElement;

                        if (focusedElement != null &&  focusedElement.TryGetCurrentPattern(TextPattern.Pattern, out var pattern) && pattern is TextPattern textPattern)
                        {
                            var stringBuilder = textPattern.GetSelection()?.Aggregate(new StringBuilder(), (sb, selection) => {
                                var selectedText = selection.GetText(-1);

                                if (selectedText.Length > 0)
                                    sb.AppendLine(selectedText);

                                return sb;
                            });

                            if (stringBuilder != null && stringBuilder.Length > 0)
                            {
                                App.Current.Logger.LogDebug("ReadAloud: Saying selected text.");

                                await TextToSpeechHelper.Instance.Say(stringBuilder.ToString());
                            }
                            else
                            {
                                App.Current.Logger.LogDebug("ReadAloud: Could not find any selected text.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Current.Logger.LogError(ex, "ReadAloud: Error reading selected text.");

                        result = IMorphicResult.ErrorResult;
                    }
                    finally
                    {
                        _readAloudSemaphore.Release();
                    }
                    break;
            }

            return result;
        }

        /// <summary>
        /// Sends key strokes to the active application.
        /// </summary>
        /// <param name="args">keys: the keys (see MSDN for SendKeys.Send())</param>
        /// <returns></returns>
        [InternalFunction("sendKeys", "keys")]
        public static async Task<IMorphicResult> SendKeysAsync(FunctionArgs args)
        {
            await SelectionReader.Default.ActivateLastActiveWindow();
            System.Windows.Forms.SendKeys.SendWait(args["keys"]);
            return IMorphicResult.SuccessResult;
        }

        [InternalFunction("signOut")]
        public static async Task<IMorphicResult> SignOutAsync(FunctionArgs args)
        {
            var success = Morphic.Windows.Native.WindowsSession.WindowsSession.LogOff();
            return success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult;
        }

        [InternalFunction("darkMode")]
        public static async Task<IMorphicResult> DarkModeAsync(FunctionArgs args)
        {
            // if we have a "value" property, this is a multi-segmented button and we should use "value" instead of "state"
            bool on;
            if (args["value"] != null)
            {
                on = (args["value"] == "on");
            }
            else if (args["state"] != null)
            {
                on = (args["state"] == "on");
            }
            else
            {
                System.Diagnostics.Debug.Assert(false, "Function 'darkMode' did not receive a new state");
                on = false;
            }

            /*
             * NOTE: in addition to the SPI implementation (in code, below), we could also turn on/off the dark theme (via powershell...or possibly via direct registry access); here are the corresponding PowerShell commands
             * 
             * SWITCH TO LIGHT MODE:
             * New-ItemProperty -Path HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize -Name SystemUsesLightTheme -Value 1 -Type Dword -Force
             * New-ItemProperty -Path HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize -Name AppsUseLightTheme -Value 1 -Type Dword -Force
             * 
             * SWITCH TO DARK MODE:
             * New-ItemProperty -Path HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize -Name SystemUsesLightTheme -Value 0 -Type Dword -Force
             * New-ItemProperty -Path HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize -Name AppsUseLightTheme -Value 0 -Type Dword -Force
            */

            // set system dark/light theme
            Setting systemThemeSetting = App.Current.MorphicSession.Solutions.GetSetting(SettingId.LightThemeSystem);
            await systemThemeSetting.SetValueAsync(!on);

            // set apps dark/light theme
            Setting appsThemeSetting = App.Current.MorphicSession.Solutions.GetSetting(SettingId.LightThemeApps);
            await appsThemeSetting.SetValueAsync(!on);
            return IMorphicResult.SuccessResult;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Windows API naming")]
        [SuppressMessage("ReSharper", "IdentifierTypo", Justification = "Windows API naming")]
        private static class WinApi
        {
            public const int APPCOMMAND_VOLUME_DOWN = 9;
            public const int APPCOMMAND_VOLUME_UP = 10;
            public const int WM_APPCOMMAND = 0x319;

            [DllImport("user32.dll")]
            public static extern IntPtr FindWindow(string lpClassName, IntPtr lpWindowName);

            [DllImport("user32.dll")]
            public static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

            public static int MakeLong(short low, short high)
            {
                return (low & 0xffff) | ((high & 0xffff) << 16);
            }
        }
    }
}
