namespace Morphic.Client.Bar.Data.Actions
{
    using Microsoft.Extensions.Logging;
    using Morphic.Core;
    using Settings.SettingsHandlers;
    using Settings.SolutionsRegistry;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Automation;
    using System.Windows.Automation.Text;
    using UI;
    using Windows.Native.Input;
    using Windows.Native.Speech;

    [HasInternalFunctions]
    // ReSharper disable once UnusedType.Global - accessed via reflection.
    public class Functions
    {
        private readonly static SemaphoreSlim _captureTextSemaphore = new SemaphoreSlim(1, 1);

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

        private static async Task<IMorphicResult> ClearClipboardAsync(uint numberOfRetries, TimeSpan interval)
        {
            // NOTE from Microsoft documentation (something to think about when working on this in the future...and perhaps something we need to handle):
            /* "The Clipboard class can only be used in threads set to single thread apartment (STA) mode. 
             * To use this class, ensure that your Main method is marked with the STAThreadAttribute attribute." 
             * https://docs.microsoft.com/es-es/dotnet/api/system.windows.forms.clipboard.clear?view=net-5.0 
            */
            for (var i = 0; i < numberOfRetries; i++)
            {
                try
                {
					// NOTE: some developers have reported unhandled exceptions with this function call, even when inside a try...catch block.  If we experience that, we may need to look at our threading model, UWP alternatives, and Win32 API alternatives.
                    Clipboard.Clear();
                    return IMorphicResult.SuccessResult;
                }
                catch
                {
                    // failed to copy to clipboard; wait an interval and then try again
                    await Task.Delay(interval);
                }
            }

            App.Current.Logger.LogDebug("ReadAloud: Could not clear selected text from the clipboard.");
            return IMorphicResult.ErrorResult;
        }

        /// <summary>
        /// Reads the selected text.
        /// </summary>
        /// <param name="args">action: "play", "pause", or "stop"</param>
        /// <returns></returns>
        [InternalFunction("readAloud", "action")]
        public static async Task<IMorphicResult> ReadAloudAsync(FunctionArgs args)
        {
            string action = args["action"];
            switch (action)
            {
                case "pause":
                    App.Current.Logger.LogError("ReadAloud: pause not supported.");

                    return IMorphicResult.ErrorResult;

                case "stop":
                    App.Current.Logger.LogDebug("ReadAloud: Stop reading selected text.");
                    TextToSpeechHelper.Instance.Stop();

                    return IMorphicResult.SuccessResult;

                case "play":
                    string? selectedText = null;

                    try
                    {
                        App.Current.Logger.LogDebug("ReadAloud: Getting selected text.");

                        // activate the target window (i.e. topmost/last-active window, rather than the MorphicBar); we will then capture the current selection in that window
                        // NOTE: ideally we would activate the last window as part of our atomic operation, but we really have no control over whether or not another application
                        //       or the user changes the activated window (and our internal code is also not set up to block us from moving activation/focus temporarily).
                        await SelectionReader.Default.ActivateLastActiveWindow();

                        // as a primary strategy, try using the built-in Windows functionality for capturing the current selection via UI automation
                        // NOTE: this does not work with some apps (such as Internet Explorer...but also others)
                        bool captureTextViaAutomationSucceeded = false;
                        //
                        TextPatternRange[]? textRangeCollection = null;
                        //
                        // capture (or wait on) our "capture text" semaphore; we'll release this in the finally block
                        await _captureTextSemaphore.WaitAsync();
                        //
                        try
                        {
                            var focusedElement = AutomationElement.FocusedElement;
                            if (focusedElement != null)
                            {
                                object? pattern = null;
                                if (focusedElement.TryGetCurrentPattern(TextPattern.Pattern, out pattern))
                                {
                                    if ((pattern != null) && (pattern is TextPattern textPattern))
                                    {
                                        // App.Current.Logger.LogDebug("ReadAloud: Capturing select text range(s).");

                                        // get the collection of text ranges in the selection; note that this can be a disjoint collection if multiple disjoint items were selected
                                        textRangeCollection = textPattern.GetSelection();
                                    }
                                }
                                else
                                {
                                    App.Current.Logger.LogDebug("ReadAloud: Selected element is not text.");
                                }
                            }
                            else
                            {
                                App.Current.Logger.LogDebug("ReadAloud: No element is currently selected.");
                            }
                        }
                        finally
                        {
                            _captureTextSemaphore.Release();
                        }
                        //
                        // if we just captured a text range collection (i.e. were able to copy the current selection), convert that capture into a string now
                        StringBuilder? selectedTextBuilder = null;
                        if (textRangeCollection != null)
                        {
                            // we have captured a range (presumably either an empty or non-empty selection)
                            selectedTextBuilder = new StringBuilder();

                            // append each text range
                            foreach (var textRange in textRangeCollection)
                            {
                                if (textRange != null)
                                {
                                    selectedTextBuilder.Append(textRange.GetText(-1 /* maximumRange */));
                                }
                            }

                            //if (selectedTextBuilder != null /* && stringBuilder.Length > 0 */)
                            //{
                                selectedText = selectedTextBuilder.ToString();
                                captureTextViaAutomationSucceeded = true;

                                if (selectedText != String.Empty)
                                {
                                    App.Current.Logger.LogDebug("ReadAloud: Captured selected text.");
                                }
                                else
                                {
                                    App.Current.Logger.LogDebug("ReadAloud: Captured empty selection.");
                                }
                            //}
                        }

                        // as a backup strategy, use the clipboard and send ctrl+c to the target window to capture the text contents (while preserving as much of the previous
                        // clipboard's contents as possible); this is necessary in Internet Explorer and some other programs
                        if (captureTextViaAutomationSucceeded == false)
                        {
                            // capture (or wait on) our "capture text" semaphore; we'll release this in the finally block
                            await _captureTextSemaphore.WaitAsync();
                            //
                            try
                            {
                                // App.Current.Logger.LogDebug("ReadAloud: Attempting to back up current clipboard.");

                                Dictionary<String, object?> clipboardContentsToRestore = new Dictionary<string, object?>();

                                var previousClipboardData = Clipboard.GetDataObject();
                                if (previousClipboardData != null)
                                {
                                    // App.Current.Logger.LogDebug("ReadAloud: Current clipboard has contents; attempting to capture format(s) of contents.");
                                    string[]? previousClipboardFormats = previousClipboardData.GetFormats();
                                    if (previousClipboardFormats != null)
                                    {
                                        // App.Current.Logger.LogDebug("ReadAloud: Current clipboard has contents; attempting to back up current clipboard.");

                                        foreach (var format in previousClipboardFormats)
                                        {
                                            object? dataObject;
                                            try 
                                            {
                                                dataObject = previousClipboardData.GetData(format, false /* autoConvert */);
                                            }
                                            catch
                                            {
                                                // NOTE: in the future, we should look at using Project Reunion to use the UWP APIs (if they can deal with this scenario better)
                                                // see: https://docs.microsoft.com/en-us/uwp/api/windows.applicationmodel.datatransfer.clipboard?view=winrt-19041
                                                // see: https://docs.microsoft.com/en-us/windows/apps/desktop/modernize/desktop-to-uwp-enhance
                                                App.Current.Logger.LogDebug("ReadAloud: Unable to back up clipboard contents; this can happen with files copied to the clipboard, etc.");
                                                
                                                return IMorphicResult.ErrorResult;
                                            }
                                            clipboardContentsToRestore[format] = dataObject;
                                        }
                                    }
                                    else
                                    {
                                        App.Current.Logger.LogDebug("ReadAloud: Current clipboard has contents, but we were unable to obtain their formats.");
                                    }
                                }
                                else
                                {
                                    App.Current.Logger.LogDebug("ReadAloud: Current clipboard has no contents.");
                                }

                                // clear the current clipboard
                                App.Current.Logger.LogDebug("ReadAloud: Clearing the current clipboard.");
                                try
                                {
                                    // try to clear the clipboard for up to 500ms (4 delays of 125ms)
                                    await Functions.ClearClipboardAsync(5, new TimeSpan(0, 0, 0, 0, 125));
                                }
                                catch
                                {
                                    App.Current.Logger.LogDebug("ReadAloud: Could not clear the current clipboard.");
                                }

                                // copy the current selection to the clipboard
                                App.Current.Logger.LogDebug("ReadAloud: Sending Ctrl+C to copy the current selection to the clipboard.");
                                await SelectionReader.Default.GetSelectedTextAsync(System.Windows.Forms.SendKeys.SendWait);

                                // wait 100ms (an arbitrary amount of time, but in our testing some wait is necessary...even with the WM-triggered copy logic above)
                                // NOTE: perhaps, in the future, we should only do this if our first call to Clipboard.GetText() returns (null? or) an empty string;
								//       or perhaps we should wait up to a certain number of milliseconds to receive a SECOND WM (the one that GetSelectedTextAsync
								//       waited for).
								await Task.Delay(100);

                                // capture the current selection
                                var selectionWasCopiedToClipboard = false;
                                var textCopiedToClipboard = Clipboard.GetText();
                                if (textCopiedToClipboard != null)
                                {
                                    selectionWasCopiedToClipboard = true;

                                    // we now have our selected text
                                    selectedText = textCopiedToClipboard;

                                    if (selectedText != null)
                                    {
                                        App.Current.Logger.LogDebug("ReadAloud: Captured selected text.");
                                    }
                                    else
                                    {
                                        App.Current.Logger.LogDebug("ReadAloud: Captured empty selection.");
                                    }
                                }
                                else
                                {
                                    var copiedDataFormats = Clipboard.GetDataObject()?.GetFormats();
                                    if (copiedDataFormats != null)
                                    {
                                        selectionWasCopiedToClipboard = true;

                                        // var formatsCsvBuilder = new StringBuilder();
                                        // formatsCsvBuilder.Append("[");
                                        // if (copiedDataFormats.Length > 0)
                                        // {
                                        //     formatsCsvBuilder.Append("\"");
                                        //     formatsCsvBuilder.Append(String.Join("\", \"", copiedDataFormats));
                                        //     formatsCsvBuilder.Append("\"");
                                        // }
                                        // formatsCsvBuilder.Append("]");

                                        // App.Current.Logger.LogDebug("ReadAloud: Ctrl+C did not copy text; instead it copied data in these format(s): " + formatsCsvBuilder.ToString());
                                        App.Current.Logger.LogDebug("ReadAloud: Ctrl+C copied non-text (un-speakable) contents to the clipboard.");
                                    }
                                    else
                                    {
                                        App.Current.Logger.LogDebug("ReadAloud: Ctrl+C did not copy anything to the clipboard.");
                                    }
                                }

                                // restore the previous clipboard's contents
                                // App.Current.Logger.LogDebug("ReadAloud: Attempting to restore the previous clipboard's contents");
                                //
                                if (selectionWasCopiedToClipboard == true)
                                {
                                    // App.Current.Logger.LogDebug("ReadAloud: Clearing the selected text from the clipboard.");
                                    try 
                                    {
                                        // try to clear the clipboard for up to 500ms (4 delays of 125ms)
                                        await Functions.ClearClipboardAsync(5, new TimeSpan(0,0,0,0,125));
                                    } 
                                    catch
                                    {
                                        App.Current.Logger.LogDebug("ReadAloud: Could not clear selected text from the clipboard.");
                                    }
                                }
                                //
                                if (clipboardContentsToRestore.Count > 0)
                                {
                                    // App.Current.Logger.LogDebug("ReadAloud: Attempting to restore " + clipboardContentsToRestore.Count.ToString() + " item(s) to the clipboard.");
                                }
                                else
                                {
                                    // App.Current.Logger.LogDebug("ReadAloud: there is nothing to restore to the clipboard.");
                                }
                                //
                                foreach (var (format, data) in clipboardContentsToRestore)
                                {
                                    // NOTE: sometimes, data is null (which is not something that SetData can accept) so we have to just skip that element
                                    if (data != null)
                                    {
                                        Clipboard.SetData(format, data);
                                    }
                                }
                                //
                                App.Current.Logger.LogDebug("ReadAloud: Clipboard restoration complete");
                            }
                            finally
                            {
                                _captureTextSemaphore.Release();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Current.Logger.LogError(ex, "ReadAloud: Error reading selected text.");

                        return IMorphicResult.ErrorResult;
                    }

                    if (selectedText != null)
                    {
                        if (selectedText != String.Empty)
                        {
                            try
                            {
                                App.Current.Logger.LogDebug("ReadAloud: Saying selected text.");

                                await TextToSpeechHelper.Instance.Say(selectedText);

                                return IMorphicResult.SuccessResult;
                            }
                            catch (Exception ex)
                            {
                                App.Current.Logger.LogError(ex, "ReadAloud: Error reading selected text.");

                                return IMorphicResult.ErrorResult;
                            }
                        }
                        else
                        {
                            App.Current.Logger.LogDebug("ReadAloud: No text to say; skipping 'say' command.");

                            return IMorphicResult.SuccessResult;
                        }
                    } else {
                        // could not capture any text
                        // App.Current.Logger.LogError("ReadAloud: Could not capture any selected text; this may or may not be an error.");

                        return IMorphicResult.ErrorResult;
                    }
                default:
                    throw new Exception("invalid code path");
            }
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

        [InternalFunction("openAllUsbDrives")]
        public static async Task<IMorphicResult> OpenAllUsbDrivesAsync(FunctionArgs args)
        {
            App.Current.Logger.LogError("OpenAllUsbDrives");

            var getRemovableDisksAndDrivesResult = await Functions.GetRemovableDisksAndDrivesAsync();
            if (getRemovableDisksAndDrivesResult.IsError == true)
            {
                Debug.Assert(false, "Could not get list of removable drives");
                App.Current.Logger.LogError("Could not get list of removable drives");
                return IMorphicResult.ErrorResult;
            }
            var removableDrives = getRemovableDisksAndDrivesResult.Value!.RemovableDrives;

            // as we only want to open usb drives which are mounted (i.e. not USB drives which have had their "media" ejected but who still have drive letters assigned)...
            var mountedRemovableDrives = new List<Morphic.Windows.Native.Devices.Drive>();
            foreach (var drive in removableDrives)
            {
                var getIsMountedResult = await drive.GetIsMountedAsync();
                if (getIsMountedResult.IsError == true)
                {
                    Debug.Assert(false, "Could not determine if drive is mounted");
                    App.Current.Logger.LogError("Could not determine if drive is mounted");
                    // gracefully degrade; skip this disk
                    continue;
                }
                var driveIsMounted = getIsMountedResult.Value!;

                if (driveIsMounted)
                {
                    mountedRemovableDrives.Add(drive);
                }
            }

            // now open all the *mounted* removable disks
            foreach (var drive in mountedRemovableDrives)
            {
                // get the drive's root path (e.g. "E:\"); note that we intentionally get the root path WITH the backslash so that we don't launch autoplay, etc.
                var tryGetDriveRootPathResult = await drive.TryGetDriveRootPathAsync();
                if (tryGetDriveRootPathResult.IsError == true)
                {
                    // START TEMPORARY CODE TO DIAGNOSE BETA TESTER'S OPEN USB ISSUE
                    switch (tryGetDriveRootPathResult.Error!.Value)
                    {
                        case Windows.Native.Devices.Drive.TryGetDriveLetterError.Values.CouldNotRetrieveStorageDeviceNumbers:
                            App.Current.Logger.LogError("ERROR IN 'TryGetDriveRootPathAsync': CouldNotRetrieveStorageDeviceNumbers");
                            App.Current.Logger.LogError("Could not get removable drive's root path");
                            break;
                        case Windows.Native.Devices.Drive.TryGetDriveLetterError.Values.Win32Error:
                            App.Current.Logger.LogError("ERROR IN 'TryGetDriveRootPathAsync': Win32Error: " + tryGetDriveRootPathResult.Error!.Win32ErrorCode.ToString());
                            break;
                    }
                    // END TEMPORARY CODE TO DIAGNOSE BETA TESTER'S OPEN USB ISSUE

                    Debug.Assert(false, "Could not get removable drive's root path");
                    App.Current.Logger.LogError("Could not get removable drive's root path");
                    // gracefully degrade; skip this disk
                    continue;
                }
                var driveRootPath = tryGetDriveRootPathResult.Value!;

                // NOTE: there is also an API call which may be able to do this more directly
                // see: https://docs.microsoft.com/en-us/windows/win32/api/shlobj_core/nf-shlobj_core-shopenfolderandselectitems

                // NOTE: we might also consider getting the current process for Explorer.exe and then asking it to "explore" the drive

                App.Current.Logger.LogError("Opening USB drive");

                Process.Start(new ProcessStartInfo()
                {
                    FileName = driveRootPath,
                    UseShellExecute = true
                });
            }

            return IMorphicResult.SuccessResult;
        }

        [InternalFunction("ejectAllUsbDrives")]
        public static async Task<IMorphicResult> EjectAllUsbDrivesAsync(FunctionArgs args)
        {
            App.Current.Logger.LogError("EjectAllUsbDrives");

            var getRemovableDisksAndDrivesResult = await Functions.GetRemovableDisksAndDrivesAsync();
            if (getRemovableDisksAndDrivesResult.IsError == true)
            {
                Debug.Assert(false, "Could not get list of removable disks");
                App.Current.Logger.LogError("Could not get list of removable disks");
                return IMorphicResult.ErrorResult;
            }
            var removableDisks = getRemovableDisksAndDrivesResult.Value!.RemovableDisks;

            // now eject all the removable disks
            var allDisksRemoved = true;
            foreach (var disk in removableDisks)
            {
                App.Current.Logger.LogError("Safely ejecting drive");

                // NOTE: "safe eject" in this circumstance means to safely eject the usb device (removing it from the PnP system, not physically ejecting media)
                var safeEjectResult = disk.SafelyRemoveDevice();
                if (safeEjectResult.IsError == true)
                {
                    allDisksRemoved = false;
                }

                // wait 50ms between ejection
                await Task.Delay(50);
            }

            if (allDisksRemoved == false)
            {
                return IMorphicResult.ErrorResult;
            }

            return allDisksRemoved ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult;
        }

        private struct GetRemovableDisksAndDrivesResult 
        {
            public List<Morphic.Windows.Native.Devices.Disk> AllDisks;
            public List<Morphic.Windows.Native.Devices.Disk> RemovableDisks; // physical volumes
            public List<Morphic.Windows.Native.Devices.Drive> RemovableDrives; // logical volumes (media / partition); these can have drive letters
        }
        //
        private static async Task<IMorphicResult<GetRemovableDisksAndDrivesResult>> GetRemovableDisksAndDrivesAsync()
        {
            // get a list of all disks (but not non-disks such as CD-ROM drives)
            var getAllDisksResult = await Morphic.Windows.Native.Devices.Disk.GetAllDisksAsync();
            if (getAllDisksResult.IsError == true)
            {
                Debug.Assert(false, "Cannot get list of disks");
                return IMorphicResult<GetRemovableDisksAndDrivesResult>.ErrorResult();
            }

            // filter out all disks which are not removable
            var allDisks = getAllDisksResult.Value!;
            var removableDisks = new List<Morphic.Windows.Native.Devices.Disk>();
            foreach (var disk in allDisks)
            {
                var getIsRemovableResult = disk.GetIsRemovable();
                if (getIsRemovableResult.IsError == true)
                {
                    Debug.Assert(false, "Cannot determine if disk is removable");
                    return IMorphicResult<GetRemovableDisksAndDrivesResult>.ErrorResult();
                }
                var diskIsRemovable = getIsRemovableResult.Value!;
                if (diskIsRemovable)
                {
                    removableDisks.Add(disk);
                }
            }

            // now get all the drives associated with our removable disks
            var removableDrives = new List<Morphic.Windows.Native.Devices.Drive>();
            foreach (var removableDisk in removableDisks)
            {
                var getDrivesForDiskResult = await removableDisk.GetDrivesAsync();
                if (getDrivesForDiskResult.IsError == true)
                {
                    Debug.Assert(false, "Cannot get list of drives for removable disk");
                    // gracefully degrade; skip this disk
                    continue;
                }
                var drivesForRemovableDisk = getDrivesForDiskResult.Value!;

                removableDrives.AddRange(drivesForRemovableDisk);
            }

            var result = new GetRemovableDisksAndDrivesResult
            {
                AllDisks = allDisks,
                RemovableDisks = removableDisks,
                RemovableDrives = removableDrives
            };

            return IMorphicResult<GetRemovableDisksAndDrivesResult>.SuccessResult(result);
        }

        [InternalFunction("darkMode")]
        public static async Task<IMorphicResult> DarkModeAsync(FunctionArgs args)
        {
            // if we have a "value" property, this is a multi-segmented button and we should use "value" instead of "state"
            bool on;
            if (args.Arguments.Keys.Contains("value"))
            {
                on = (args["value"] == "on");
            }
            else if (args.Arguments.Keys.Contains("state"))
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
