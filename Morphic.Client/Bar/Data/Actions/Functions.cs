namespace Morphic.Client.Bar.Data.Actions
{
    using Microsoft.Extensions.Logging;
    using Morphic.Core;
    using Morphic.WindowsNative.Input;
    using Morphic.WindowsNative.Speech;
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

    [HasInternalFunctions]
    // ReSharper disable once UnusedType.Global - accessed via reflection.
    public class Functions
    {
        private readonly static SemaphoreSlim s_captureTextSemaphore = new SemaphoreSlim(1, 1);

        [InternalFunction("snip")]
        public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> ScreenSnipAsync(FunctionArgs args)
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

                //// method 1: hold down the windows key while pressing shift + s
                //// NOTE: this method does not seem to work when we have uiAccess set to true in our manifest (oddly)
                //const uint windowsKey = 0x5b; // VK_LWIN
                //Keyboard.PressKey(windowsKey, true);
                //System.Windows.Forms.SendKeys.SendWait("+s");
                //Keyboard.PressKey(windowsKey, false);

                // method 2: open up the special windows URI of ms-screenclip:
                var openPath = "ms-screenclip:";
                Process.Start(new ProcessStartInfo(openPath)
                {
                    UseShellExecute = true
                });
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

            return MorphicResult.OkResult();
        }

        [InternalFunction("menu", "key=Morphic")]
        public async static Task<MorphicResult<MorphicUnit, MorphicUnit>> ShowMenuAsync(FunctionArgs args)
        {
            // NOTE: this internal function is only called by the MorphicBar's Morphie menu button
            await App.Current.ShowMenuAsync(null, Morphic.Client.Menu.MorphicMenu.MenuOpenedSource.morphicBarIcon);
            return MorphicResult.OkResult();
        }

        [InternalFunction("volumeUp")]
        public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> VolumeUpAsync(FunctionArgs args)
        {
            args.Arguments.Add("direction", "up");
            args.Arguments.Add("amount", "6");
            return await SetVolumeAsync(args);
        }

        [InternalFunction("volumeDown")]
        public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> VolumeDownAsync(FunctionArgs args)
        {
            args.Arguments.Add("direction", "down");
            args.Arguments.Add("amount", "6");
            return await SetVolumeAsync(args);
        }

        internal static MorphicResult<bool, MorphicUnit> GetMuteState()
        {
            try
            {
                var getDefaultAudioOutputEndpointResult = Morphic.WindowsNative.Audio.AudioEndpoint.GetDefaultAudioOutputEndpoint();
                if (getDefaultAudioOutputEndpointResult.IsError == true)
                {
                    return MorphicResult.ErrorResult();
                }
                var audioEndpoint = getDefaultAudioOutputEndpointResult.Value!;

                // if we didn't get a state in the request, try to reverse the state
                var getMasterMuteStateResult = audioEndpoint.GetMasterMuteState();
                if (getMasterMuteStateResult.IsError == true)
                {
                    return MorphicResult.ErrorResult();
                }
                var state = getMasterMuteStateResult.Value!;

                return MorphicResult.OkResult(state);
            }
            catch
            {
                return MorphicResult.ErrorResult();
            }
        }

        [InternalFunction("volumeMute")]
        public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> VolumeMuteAsync(FunctionArgs args)
        {
            bool newState;
            if (args.Arguments.Keys.Contains("state"))
            {
                newState = (args["state"] == "on");
            }
            else
            {
                var getMuteStateResult = Functions.GetMuteState();
                if (getMuteStateResult.IsSuccess == true)
                {
                    newState = getMuteStateResult.Value!;
                }
                else
                {
                    // if we cannot get the current value, gracefully degrade (i.e. assume that the volume is not muted)
                    newState = false;
                }
            }

            // set the mute state to the new state value
            var getDefaultAudioOutputEndpointResult = Morphic.WindowsNative.Audio.AudioEndpoint.GetDefaultAudioOutputEndpoint();
            if (getDefaultAudioOutputEndpointResult.IsError == true)
            {
                return MorphicResult.ErrorResult();
            }
            var audioEndpoint = getDefaultAudioOutputEndpointResult.Value!;

            var setMasterMuteStateResult = audioEndpoint.SetMasterMuteState(newState);
            if (setMasterMuteStateResult.IsError == true)
            {
                return MorphicResult.ErrorResult();
            }

            return MorphicResult.OkResult();
        }

        /// <summary>
        /// Lowers or raises the volume.
        /// </summary>
        /// <param name="args">direction: "up"/"down", amount: number of 1/100 to move</param>
        /// <returns></returns>
        [InternalFunction("volume", "direction", "amount=6")]
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> SetVolumeAsync(FunctionArgs args)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            // NOTE: ideally we should switch this functionality to use AudioEndpoint.SetMasterVolumeLevel instead
            //       [it may not be practical to do that, however, if AudioEndpoint.SetMasterVolumeLevel doesn't activate to on-screen volume change dialog in Windows 10/11; to be tested...]

            var percent = Convert.ToUInt32(args["amount"]);
            // clean up the percent argument (if it's not even, is out of range, etc.)
            if (percent % 2 != 0)
            {
                percent += 1;
            }
            percent = System.Math.Clamp(percent, 0, 100);

            if (args["direction"] == "up")
            {
                var sendCommandResult = Morphic.WindowsNative.Audio.Utils.VolumeUtils.SendVolumeUpCommand(percent);
                if (sendCommandResult.IsError == true)
                {
                    return MorphicResult.ErrorResult();
                }
            }
            else
            {
                var sendCommandResult = Morphic.WindowsNative.Audio.Utils.VolumeUtils.SendVolumeDownCommand(percent);
                if (sendCommandResult.IsError == true)
                {
                    return MorphicResult.ErrorResult();
                }
            }

            return MorphicResult.OkResult();
        }

        private static async Task<MorphicResult<MorphicUnit, MorphicUnit>> ClearClipboardAsync(uint numberOfRetries, TimeSpan interval)
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
                    return MorphicResult.OkResult();
                }
                catch
                {
                    // failed to copy to clipboard; wait an interval and then try again
                    await Task.Delay(interval);
                }
            }

            App.Current.Logger.LogDebug("ReadAloud: Could not clear selected text from the clipboard.");
            return MorphicResult.ErrorResult();
        }

        /// <summary>
        /// Reads the selected text.
        /// </summary>
        /// <param name="args">action: "play", "pause", or "stop"</param>
        /// <returns></returns>
        [InternalFunction("readAloud", "action")]
        public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> ReadAloudAsync(FunctionArgs args)
        {
            string action = args["action"];
            switch (action)
            {
                case "pause":
                    App.Current.Logger.LogError("ReadAloud: pause not supported.");

                    return MorphicResult.ErrorResult();

                case "stop":
                    App.Current.Logger.LogDebug("ReadAloud: Stop reading selected text.");
                    TextToSpeechHelper.Instance.Stop();

                    return MorphicResult.OkResult();

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
                        await s_captureTextSemaphore.WaitAsync();
                        //
                        try
                        {
                            var focusedElement = AutomationElement.FocusedElement;
                            if (focusedElement is not null)
                            {
                                object? pattern = null;
                                if (focusedElement.TryGetCurrentPattern(TextPattern.Pattern, out pattern))
                                {
                                    if ((pattern is not null) && (pattern is TextPattern textPattern))
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
                            s_captureTextSemaphore.Release();
                        }
                        //
                        // if we just captured a text range collection (i.e. were able to copy the current selection), convert that capture into a string now
                        StringBuilder? selectedTextBuilder = null;
                        if (textRangeCollection is not null)
                        {
                            // we have captured a range (presumably either an empty or non-empty selection)
                            selectedTextBuilder = new StringBuilder();

                            // append each text range
                            foreach (var textRange in textRangeCollection)
                            {
                                if (textRange is not null)
                                {
                                    selectedTextBuilder.Append(textRange.GetText(-1 /* maximumRange */));
                                }
                            }

                            //if (selectedTextBuilder is not null /* && stringBuilder.Length > 0 */)
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
                            await s_captureTextSemaphore.WaitAsync();
                            //
                            try
                            {
                                // App.Current.Logger.LogDebug("ReadAloud: Attempting to back up current clipboard.");

                                Dictionary<String, object?> clipboardContentsToRestore = new Dictionary<string, object?>();

                                var previousClipboardData = Clipboard.GetDataObject();
                                if (previousClipboardData is not null)
                                {
                                    // App.Current.Logger.LogDebug("ReadAloud: Current clipboard has contents; attempting to capture format(s) of contents.");
                                    string[]? previousClipboardFormats = previousClipboardData.GetFormats();
                                    if (previousClipboardFormats is not null)
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
                                                
                                                return MorphicResult.ErrorResult();
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
                                if (textCopiedToClipboard is not null)
                                {
                                    selectionWasCopiedToClipboard = true;

                                    // we now have our selected text
                                    selectedText = textCopiedToClipboard;

                                    if (selectedText is not null)
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
                                    if (copiedDataFormats is not null)
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
                                    if (data is not null)
                                    {
                                        Clipboard.SetData(format, data);
                                    }
                                }
                                //
                                App.Current.Logger.LogDebug("ReadAloud: Clipboard restoration complete");
                            }
                            finally
                            {
                                s_captureTextSemaphore.Release();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Current.Logger.LogError(ex, "ReadAloud: Error reading selected text.");

                        return MorphicResult.ErrorResult();
                    }

                    if (selectedText is not null)
                    {
                        if (selectedText != String.Empty)
                        {
                            try
                            {
                                App.Current.Logger.LogDebug("ReadAloud: Saying selected text.");

                                var sayResult = await TextToSpeechHelper.Instance.Say(selectedText);
                                if (sayResult.IsError == true)
                                {
                                    App.Current.Logger.LogError("ReadAloud: Error saying selected text.");

                                    return MorphicResult.ErrorResult();
                                }

                                return MorphicResult.OkResult();
                            }
                            catch (Exception ex)
                            {
                                App.Current.Logger.LogError(ex, "ReadAloud: Error reading selected text.");

                                return MorphicResult.ErrorResult();
                            }
                        }
                        else
                        {
                            App.Current.Logger.LogDebug("ReadAloud: No text to say; skipping 'say' command.");

                            return MorphicResult.OkResult();
                        }
                    } else {
                        // could not capture any text
                        // App.Current.Logger.LogError("ReadAloud: Could not capture any selected text; this may or may not be an error.");

                        return MorphicResult.ErrorResult();
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
        public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> SendKeysAsync(FunctionArgs args)
        {
            await SelectionReader.Default.ActivateLastActiveWindow();
            System.Windows.Forms.SendKeys.SendWait(args["keys"]);
            return MorphicResult.OkResult();
        }

        [InternalFunction("signOut")]
        public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> SignOutAsync(FunctionArgs args)
        {
            var success = Morphic.WindowsNative.WindowsSession.WindowsSession.LogOff();
            return success ? MorphicResult.OkResult() : MorphicResult.ErrorResult();
        }

        [InternalFunction("openAllUsbDrives")]
        public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> OpenAllUsbDrivesAsync(FunctionArgs args)
        {
            App.Current.Logger.LogError("OpenAllUsbDrives");

            var getRemovableDisksAndDrivesResult = await Functions.GetRemovableDisksAndDrivesAsync();
            if (getRemovableDisksAndDrivesResult.IsError == true)
            {
                Debug.Assert(false, "Could not get list of removable drives");
                App.Current.Logger.LogError("Could not get list of removable drives");
                return MorphicResult.ErrorResult();
            }
            var removableDrives = getRemovableDisksAndDrivesResult.Value!.RemovableDrives;

            // as we only want to open usb drives which are mounted (i.e. not USB drives which have had their "media" ejected but who still have drive letters assigned)...
            var mountedRemovableDrives = new List<Morphic.WindowsNative.Devices.Drive>();
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

            return MorphicResult.OkResult();
        }

        [InternalFunction("ejectAllUsbDrives")]
        public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> EjectAllUsbDrivesAsync(FunctionArgs args)
        {
            App.Current.Logger.LogError("EjectAllUsbDrives");

            var getRemovableDisksAndDrivesResult = await Functions.GetRemovableDisksAndDrivesAsync();
            if (getRemovableDisksAndDrivesResult.IsError == true)
            {
                Debug.Assert(false, "Could not get list of removable disks");
                App.Current.Logger.LogError("Could not get list of removable disks");
                return MorphicResult.ErrorResult();
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
                return MorphicResult.ErrorResult();
            }

            return allDisksRemoved ? MorphicResult.OkResult() : MorphicResult.ErrorResult();
        }

        private struct GetRemovableDisksAndDrivesResult 
        {
            public List<Morphic.WindowsNative.Devices.Disk> AllDisks;
            public List<Morphic.WindowsNative.Devices.Disk> RemovableDisks; // physical volumes
            public List<Morphic.WindowsNative.Devices.Drive> RemovableDrives; // logical volumes (media / partition); these can have drive letters
        }
        //
        private static async Task<MorphicResult<GetRemovableDisksAndDrivesResult, MorphicUnit>> GetRemovableDisksAndDrivesAsync()
        {
            // get a list of all disks (but not non-disks such as CD-ROM drives)
            var getAllDisksResult = await Morphic.WindowsNative.Devices.Disk.GetAllDisksAsync();
            if (getAllDisksResult.IsError == true)
            {
                Debug.Assert(false, "Cannot get list of disks");
                return MorphicResult.ErrorResult();
            }

            // filter out all disks which are not removable
            var allDisks = getAllDisksResult.Value!;
            var removableDisks = new List<Morphic.WindowsNative.Devices.Disk>();
            foreach (var disk in allDisks)
            {
                var getIsRemovableResult = disk.GetIsRemovable();
                if (getIsRemovableResult.IsError == true)
                {
                    Debug.Assert(false, "Cannot determine if disk is removable");
                    return MorphicResult.ErrorResult();
                }
                var diskIsRemovable = getIsRemovableResult.Value!;
                if (diskIsRemovable)
                {
                    removableDisks.Add(disk);
                }
            }

            // now get all the drives associated with our removable disks
            var removableDrives = new List<Morphic.WindowsNative.Devices.Drive>();
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

            return MorphicResult.OkResult(result);
        }

        internal async static Task<MorphicResult<bool, MorphicUnit>> GetDarkModeStateAsync()
        {
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
                    return MorphicResult.ErrorResult();
                }
                var personalizeKey = openPersonalizeKeyResult.Value!;

                // get the current setting
                bool appsUseLightThemeAsBool;
                var getAppsUseLightThemeResult = personalizeKey.GetValue<uint>("AppsUseLightTheme");
                if (getAppsUseLightThemeResult.IsError == true)
                {
                    if (getAppsUseLightThemeResult.Error == Morphic.WindowsNative.Registry.RegistryKey.RegistryValueError.ValueDoesNotExist)
                    {
                        // default AppsUseLightTheme (inverse of dark mode state) on Windows 10 v1809 is true
                        appsUseLightThemeAsBool = true;
                    }
                    else
                    {
                        return MorphicResult.ErrorResult();
                    }
                }
                else
                {
                    var appsUseLightThemeAsUInt32 = getAppsUseLightThemeResult.Value!;
                    appsUseLightThemeAsBool = (appsUseLightThemeAsUInt32 != 0) ? true : false;
                }

                // dark theme state is the inverse of AppsUseLightTheme
                var darkThemeState = !appsUseLightThemeAsBool;

                return MorphicResult.OkResult(darkThemeState);
            }
            else if (osVersion is null)
            {
                // error
                return MorphicResult.ErrorResult();
            }
            else
            {
                // Windows 10 v1903+

                // get system dark/light theme
                Setting systemThemeSetting = App.Current.MorphicSession.Solutions.GetSetting(SettingId.LightThemeSystem);
                var getSystemThemeValueResult = await systemThemeSetting.GetValueAsync();
                if (getSystemThemeValueResult.IsError == true)
                {
                    return MorphicResult.ErrorResult();
                }
                var lightThemeSystemAsObject = getSystemThemeValueResult.Value!;
                var lightThemeSystemAsBool = (bool)lightThemeSystemAsObject;

                // set apps dark/light theme
                Setting appsThemeSetting = App.Current.MorphicSession.Solutions.GetSetting(SettingId.LightThemeApps);
                var getAppsThemeValueResult = await appsThemeSetting.GetValueAsync();
                if (getAppsThemeValueResult.IsError == true)
                {
                    return MorphicResult.ErrorResult();
                }
                var lightThemeAppsAsObject = getAppsThemeValueResult.Value!;
                var lightThemeAppsAsBool = (bool)lightThemeSystemAsObject;

                // if either apps or system theme is set to "not light", then return true 
                var darkModeIsEnabled = ((lightThemeSystemAsBool == false) || (lightThemeAppsAsBool == false));
                return MorphicResult.OkResult(darkModeIsEnabled);
            }
        }

        internal async static Task<MorphicResult<MorphicUnit, MorphicUnit>> SetDarkModeStateAsync(bool state)
        {
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
                    return MorphicResult.ErrorResult();
                }
                var personalizeKey = openPersonalizeKeyResult.Value!;

                // set apps dark/light theme
                //
                uint newAppsUseLightThemeAsUInt32 = state ? (uint)0 : (uint)1; // NOTE: these are inverted (because we are setting "light state" using the inverse of the "dark state" parameter
                //
                // set the setting to the inverted state
                var setAppsUseLightThemeResult = personalizeKey.SetValue<uint>("AppsUseLightTheme", newAppsUseLightThemeAsUInt32);
                if (setAppsUseLightThemeResult.IsError == true)
                {
                    return MorphicResult.ErrorResult();
                }

                // see: https://docs.microsoft.com/en-us/windows/win32/winmsg/wm-wininichange
                var pointerToImmersiveColorSetString = Marshal.StringToHGlobalUni("ImmersiveColorSet");
                try
                {
                    // notify all windows that we have changed a setting in the "win ini" settings
                    _ = PInvoke.User32.SendMessage(PInvoke.User32.HWND_BROADCAST, PInvoke.User32.WindowMessage.WM_WININICHANGE, IntPtr.Zero, pointerToImmersiveColorSetString);
                }
                finally
                {
                    Marshal.FreeHGlobal(pointerToImmersiveColorSetString);
                }
            }
            else if (osVersion is null)
            {
                // error
                return MorphicResult.ErrorResult();
            }
            else
            {
                // Windows 10 v1903+

                /*
                 * NOTE: in addition to the SPI implementation (in code, below), we could also turn on/off the dark theme (via powershell...or possibly via direct registry access); here are the corresponding PowerShell commands
                 * NOTE: we use registry access to get/set dark mode under Windows 10 <=v1809 (see code above); the "system dark theme" was introduced in Windows 10 v1903
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
                await systemThemeSetting.SetValueAsync(!state);

                // set apps dark/light theme
                Setting appsThemeSetting = App.Current.MorphicSession.Solutions.GetSetting(SettingId.LightThemeApps);
                await appsThemeSetting.SetValueAsync(!state);
            }

            return MorphicResult.OkResult();
        }

        [InternalFunction("darkMode")]
        public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> DarkModeAsync(FunctionArgs args)
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

            var setDarkModeStateResult = await Functions.SetDarkModeStateAsync(on);
            if (setDarkModeStateResult.IsError == true)
            {
                return MorphicResult.ErrorResult();
            }

            return MorphicResult.OkResult();
        }

        //

        const string WORD_RUNNING_MESSAGE = "You need to exit Word in order to use the Word Simplify buttons.\n\n(1) Quit Word.\n(2) Use the Word Simplify buttons to add or remove the simplified ribbon(s) you want.\n(3) Re-launch Word.";

        private static bool IsSafeToModifyRibbonFile_WarnUser()
        {
            // make sure Word is not running before attempting to change the word ribbon enable/disable state
            var isWordRunningResult = Morphic.Integrations.Office.WordRibbon.IsWordRunning();
            if (isWordRunningResult.IsError == true)
            {
                // NOTE: realistically, we might not want to create a modal message box during an async function. 
                MessageBox.Show("Sorry, we cannot detect if Word is running.\n\nThis feature is currently unavailable.");
            }
            var wordIsRunning = isWordRunningResult.Value!;
            //
            if (wordIsRunning == true)
            {
                MessageBox.Show(Functions.WORD_RUNNING_MESSAGE);
                return false;
            }

            // if Word is not running, it's safe to proceed
            return true;
        }

        [InternalFunction("basicWordRibbon")]
        public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> ToggleBasicWordRibbonAsync(FunctionArgs args)
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
                System.Diagnostics.Debug.Assert(false, "Function 'basicWordRibbon' did not receive a new state");
                on = false;
            }

            if (Functions.IsSafeToModifyRibbonFile_WarnUser() == false)
            {
                // Word is running, so we are choosing not to execute this function
                return MorphicResult.ErrorResult();
            }

            if (on == true)
            {
                var enableRibbonResult = Morphic.Integrations.Office.WordRibbon.EnableBasicSimplifyRibbon();
                return enableRibbonResult.IsSuccess ? MorphicResult.OkResult() : MorphicResult.ErrorResult();
            }
            else
            {
                var disableRibbonResult = Morphic.Integrations.Office.WordRibbon.DisableBasicSimplifyRibbon();
                return disableRibbonResult.IsSuccess ? MorphicResult.OkResult() : MorphicResult.ErrorResult();
            }
        }

        [InternalFunction("essentialsWordRibbon")]
        public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> ToggleEssentialsWordRibbonAsync(FunctionArgs args)
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
                System.Diagnostics.Debug.Assert(false, "Function 'essentialsWordRibbon' did not receive a new state");
                on = false;
            }

            if (Functions.IsSafeToModifyRibbonFile_WarnUser() == false)
            {
                // Word is running, so we are choosing not to execute this function
                return MorphicResult.ErrorResult();
            }

            if (on == true)
            {
                var enableRibbonResult = Morphic.Integrations.Office.WordRibbon.EnableEssentialsSimplifyRibbon();
                return enableRibbonResult.IsSuccess ? MorphicResult.OkResult() : MorphicResult.ErrorResult();
            }
            else
            {
                var disableRibbonResult = Morphic.Integrations.Office.WordRibbon.DisableEssentialsSimplifyRibbon();
                return disableRibbonResult.IsSuccess ? MorphicResult.OkResult() : MorphicResult.ErrorResult();
            }
        }
    }
}
