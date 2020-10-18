namespace Morphic.Client.Bar.Data.Actions
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using System.Media;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using global::Windows.Media.SpeechSynthesis;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Win32;
    using Settings.SystemSettings;
    using Settings = Settings;

    [HasInternalFunctions]
    public class Personal
    {
        /// <summary>
        /// Increases or decreases the resolution.
        /// </summary>
        /// <param name="args">direction: "in" (zoom in), "out" (zoom out)</param>
        /// <returns></returns>
        [InternalFunction("screenZoom", "direction")]
        public static Task<bool> ScreenZoom(FunctionArgs args)
        {
            double amount = args["direction"] switch
            {
                "in" => Settings.Display.Primary.PercentageForZoomingIn,
                "out" => Settings.Display.Primary.PercentageForZoomingOut,
                _ => double.NaN
            };

            if (!double.IsNaN(amount))
            {
                Settings.Display.Primary.Zoom(amount);
            }

            return Task.FromResult(true);
        }

        /// <summary>
        /// Lowers or raises the volume.
        /// </summary>
        /// <param name="args">direction: "up"/"down", amount: number of 1/100 to move</param>
        /// <returns></returns>
        [InternalFunction("volume", "direction", "amount=10")]
        public static Task<bool> Volume(FunctionArgs args)
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

            return Task.FromResult(true);
        }

        /// <summary>
        /// Start/stop the full screen magnifier.
        /// </summary>
        /// <param name="args">state: "on"/"off"</param>
        /// <returns></returns>
        [InternalFunction("magnifier", "state")]
        public static Task<bool> Magnifier(FunctionArgs args)
        {
            bool success = true;
            if (args["state"] == "on")
            {
                Registry.SetValue("HKEY_CURRENT_USER\\Software\\Microsoft\\ScreenMagnifier", "Magnification", 200);
                Process? process = Process.Start(new ProcessStartInfo("magnify.exe", "/lens")
                {
                    UseShellExecute = true
                });

                success = process != null;
            }
            else
            {
                foreach (Process process in Process.GetProcessesByName("magnify"))
                {
                    process.Kill();
                }
            }

            return Task.FromResult(success);
        }

        /// <summary>
        /// Enables night-mode (blue-light reduction)
        /// </summary>
        /// <param name="args">state: "on"/"off"</param>
        /// <returns></returns>
        [InternalFunction("nightMode", "state")]
        public static async Task<bool> NightMode(FunctionArgs args)
        {
            SystemSetting systemSetting = new Settings.SystemSettings.SystemSetting(
                "SystemSettings_Display_BlueLight_ManualToggleQuickAction",
                App.Current.ServiceProvider.GetRequiredService<ILogger<Settings.SystemSettings.SystemSetting>>());

            await systemSetting.SetValue(args["state"] == "on");
            return true;
        }

        // Plays the speech sound.
        private static SoundPlayer? speechPlayer;

        /// <summary>
        /// Reads the selected text.
        /// </summary>
        /// <param name="args">action: "play", "pause", or "stop"</param>
        /// <returns></returns>
        [InternalFunction("readAloud", "action")]
        public static async Task<bool> ReadAloud(FunctionArgs args)
        {
            string action = args["action"];
            switch (action)
            {
                case "pause":
                    App.Current.Logger.LogError("ReadAloud: pause not supported");
                    break;

                case "stop":
                case "play":
                    Personal.speechPlayer?.Stop();
                    Personal.speechPlayer?.Dispose();
                    Personal.speechPlayer = null;

                    if (action == "stop")
                    {
                        break;
                    }

                    App.Current.Logger.LogDebug("ReadAloud: Storing clipboard");
                    IDataObject? clipboardData = Clipboard.GetDataObject();
                    Dictionary<string, object?>? dataStored = null;
                    if (clipboardData != null)
                    {
                        dataStored = clipboardData.GetFormats()
                            .ToDictionary(format => format, format => (object?)clipboardData.GetData(format, false));
                    }

                    Clipboard.Clear();

                    // Get the selection
                    App.Current.Logger.LogDebug("ReadAloud: Getting selected text");
                    await SelectionReader.Default.GetSelectedText(System.Windows.Forms.SendKeys.SendWait);
                    string text = Clipboard.GetText();

                    // Restore the clipboard
                    App.Current.Logger.LogDebug("ReadAloud: Restoring clipboard");
                    Clipboard.Clear();
                    dataStored?.Where(kv => kv.Value != null).ToList()
                        .ForEach(kv => Clipboard.SetData(kv.Key, kv.Value));

                    // Talk the talk
                    SpeechSynthesizer synth = new SpeechSynthesizer();
                    SpeechSynthesisStream stream = await synth.SynthesizeTextToStreamAsync(text);
                    speechPlayer = new SoundPlayer(stream.AsStream());
                    speechPlayer.LoadCompleted += (o, args) =>
                    {
                        speechPlayer.Play();
                    };

                    speechPlayer.LoadAsync();

                    break;

            }

            return true;
        }

        /// <summary>
        /// Sends key strokes to the active application.
        /// </summary>
        /// <param name="args">keys: the keys (see MSDN for SendKeys.Send())</param>
        /// <returns></returns>
        [InternalFunction("sendKeys", "keys")]
        public static async Task<bool> SendKeys(FunctionArgs args)
        {
            await SelectionReader.Default.ActivateLastActiveWindow();
            System.Windows.Forms.SendKeys.SendWait(args["keys"]);
            return true;
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
