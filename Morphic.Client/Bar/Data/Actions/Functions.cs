namespace Morphic.Client.Bar.Data.Actions
{
    using Microsoft.Extensions.Logging;
    using Morphic.Core;
    using Morphic.WindowsNative.Speech;
    using Settings.SettingsHandlers;
    using Settings.SolutionsRegistry;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
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
            await App.Current.ShowMenuAsync(null, Morphic.Client.MainMenu.MorphicMainMenu.MenuOpenedSource.morphicBarIcon);
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

     //   NOTE: this function is deprecated as of Morphic v1.6
     //   private static async Task<MorphicResult<MorphicUnit, MorphicUnit>> ClearClipboardAsync(uint numberOfRetries, TimeSpan interval)
     //   {
     //       // NOTE from Microsoft documentation (something to think about when working on this in the future...and perhaps something we need to handle):
     //       /* "The Clipboard class can only be used in threads set to single thread apartment (STA) mode. 
     //        * To use this class, ensure that your Main method is marked with the STAThreadAttribute attribute." 
     //        * https://docs.microsoft.com/es-es/dotnet/api/system.windows.forms.clipboard.clear?view=net-5.0 
     //       */
     //       for (var i = 0; i < numberOfRetries; i++)
     //       {
     //           try
     //           {
					//// NOTE: some developers have reported unhandled exceptions with this function call, even when inside a try...catch block.  If we experience that, we may need to look at our threading model, UWP alternatives, and Win32 API alternatives.
     //               Clipboard.Clear();
     //               return MorphicResult.OkResult();
     //           }
     //           catch
     //           {
     //               // failed to copy to clipboard; wait an interval and then try again
     //               await Task.Delay(interval);
     //           }
     //       }

     //       App.Current.Logger.LogDebug("ReadAloud: Could not clear selected text from the clipboard.");
     //       return MorphicResult.ErrorResult();
     //   }

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
                        /* Capture strategy #1 (preferred strategy): capture text via UI automation */

                        App.Current.Logger.LogDebug("ReadAloud: Capturing selected text via UI automation.");

                        // activate the target window (i.e. topmost/last-active window, rather than the MorphicBar); we will then capture the current selection in that window
                        // NOTE: ideally we would activate the last window as part of our atomic operation, but we really have no control over whether or not another application
                        //       or the user changes the activated window (and our internal code is also not set up to block us from moving activation/focus temporarily).
                        await SelectionReader.Default.ActivateLastActiveWindow();

                        // as a primary strategy, try using the built-in Windows functionality for capturing the current selection via UI automation
                        // NOTE: this does not work with some apps (such as Internet Explorer...but also others)
                        bool captureTextViaAutomationSucceeded = false;
                        //
                        // capture (or wait on) our "capture text" semaphore; we'll release this in the finally block
                        await s_captureTextSemaphore.WaitAsync();
                        try
                        {
                            var getSelectedTextResult = Morphic.WindowsNative.UIAutomation.UIAutomationClient.GetSelectedText();
                            if (getSelectedTextResult.IsSuccess == true)
                            {
                                selectedText = getSelectedTextResult.Value;

                                if (selectedText is null)
                                {
                                    // NOTE: we don't mark captureTextViaAutomationSucceeded as true here because programs like Internet Explorer require using the backup option (i.e. ctrl+c)
                                    App.Current.Logger.LogDebug("ReadAloud: Focused element does not support selected text via UI automation.");
                                }
                                else
                                {
                                    captureTextViaAutomationSucceeded = true;

                                    if (selectedText == String.Empty)
                                    {
                                        App.Current.Logger.LogDebug("ReadAloud: Captured empty selection via UI automation.");
                                    }
                                    else
                                    {
                                        App.Current.Logger.LogDebug("ReadAloud: Captured selected (non-empty) text via UI automation.");
                                    }
                                }
                            }
                            else
                            {
                                // NOTE: we only log errors here, rather than returning an error condition to our caller; we don't return immediately here because we have a backup strategy (i.e. ctrl+c) which we employ if this strategy fails
                                switch (getSelectedTextResult.Error!.Value)
                                {
                                    case WindowsNative.UIAutomation.UIAutomationClient.CaptureSelectedTextError.Values.ComInterfaceInstantiationFailed:
                                        App.Current.Logger.LogDebug("ReadAloud: Capture selected text via UI automation failed (com interface could not be instantiated)");
                                        break;
                                    case WindowsNative.UIAutomation.UIAutomationClient.CaptureSelectedTextError.Values.TextRangeIsNull:
                                        App.Current.Logger.LogDebug("ReadAloud: Capture selected text via UI automation returned a null text range; this is an unexpected error condition");
                                        break;
                                    case WindowsNative.UIAutomation.UIAutomationClient.CaptureSelectedTextError.Values.Win32Error:
                                        App.Current.Logger.LogDebug("ReadAloud: Capture selected text via UI automation resulted in win32 error code: " + getSelectedTextResult.Error!.Win32ErrorCode.ToString());
                                        break;
                                    default:
                                        throw new MorphicUnhandledErrorException();
                                }
                            }
                        }
                        finally
                        {
                            s_captureTextSemaphore.Release();
                        }

                        /* Capture strategy #2 (backup strategy): capture text via copy key sequence (i.e. send "ctrl+c" to foreground/last-active window) */

                        // as a backup strategy, use the clipboard and send ctrl+c to the target window to capture the text contents (while preserving as much of the previous
                        // clipboard's contents as possible); this is necessary in Internet Explorer and some other programs
                        bool captureTextViaCopyKeySequenceSucceeded = false;
                        if (captureTextViaAutomationSucceeded == false)
                        {
                            App.Current.Logger.LogDebug("ReadAloud: Capturing selected text via copy key sequence.");

                            // capture (or wait on) our "capture text" semaphore; we'll release this in the finally block
                            await s_captureTextSemaphore.WaitAsync();
                            //
                            try
                            {
                                // capture the selected text from the last active (foreground) window
                                var getSelectedTextFromForegroundWindowResult = await Functions.GetSelectedTextFromForegroundWindowAsync();
                                if (getSelectedTextFromForegroundWindowResult.IsError == true)
                                {
                                    App.Current.Logger.LogDebug("ReadAloud: Capture selected text via ctrl+c failed.");
                                    captureTextViaCopyKeySequenceSucceeded = false;
                                }
                                else
                                {
                                    captureTextViaCopyKeySequenceSucceeded = true;
                                    selectedText = getSelectedTextFromForegroundWindowResult.Value;

                                    if (selectedText is not null)
                                    {
                                        App.Current.Logger.LogDebug("ReadAloud: Captured selected text via ctrl+c.");
                                    }
                                    else
                                    {
                                        App.Current.Logger.LogDebug("ReadAloud: Captured empty selection via ctrl+c.");
                                    }

                                }
                            }
                            finally
                            {
                                s_captureTextSemaphore.Release();
                            }
                        }

                        if (captureTextViaAutomationSucceeded == false && captureTextViaCopyKeySequenceSucceeded == false)
                        {
                            App.Current.Logger.LogDebug("ReadAloud: Could not capture selected text by automation; could not capture selected text via ctrl+c.");

                            // if we were unable to capture the text via either automation or emulating a "copy key sequence (ctrl+c)", then return failure
                            return MorphicResult.ErrorResult();
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Current.Logger.LogError(ex, "ReadAloud: Error reading selected text.");

                        return MorphicResult.ErrorResult();
                    }

                    // NOTE: if we reach here, we were able to capture the selected text (either 

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
                        // no text was selected
                        App.Current.Logger.LogDebug("ReadAloud: No text was selected (or the selected text didn't support capture via UI automation or copying via ctrl+c).");

                        return MorphicResult.OkResult();
                    }
                default:
                    throw new Exception("invalid code path");
            }
        }

        //

        private static async Task<MorphicResult<string?, MorphicUnit>> GetSelectedTextFromForegroundWindowAsync()
        {
            string? selectedText = null;
            var isSuccess = true;

            /* PHASE 1: capture the newest item in the windows clipboard history (so we can rollback upon completion); we will also _first_ try to directly capture the current clipboard contents as a fallback position (in case of enterprise-disabled clipboard history or failure during clipboard history restoration) */

            // capture the clipboard's current content directly; ideally we'd use the clipboard history, but sometimes clipboard history isn't available
            //
            // this variable is used to indicate that we captured the clipboard content directly (in case we need to try to manually restore back to the clipboard content)
            bool clipboardContentIsCaptured;
            // this variable is used to store the clipboard content
            List<(string, object)>? clipboardContent = null;
            //
            var backupContentResult = await Morphic.WindowsNative.Clipboard.Clipboard.BackupContentAsync();
            if (backupContentResult.IsError == true)
            {
                App.Current.Logger.LogDebug("GetSelectedTextFromForegroundWindowAsync: Unable to back up clipboard content; this can happen when the clipboard content is a file (i.e. filestream), etc.");
                clipboardContentIsCaptured = false;
            }
            else
            {
                App.Current.Logger.LogDebug("GetSelectedTextFromForegroundWindowAsync: Captured current clipboard content");
                clipboardContent = backupContentResult.Value;
                clipboardContentIsCaptured = true;
            }
            //
            // NOTE: clipboard content can technically be empty
            if (clipboardContent is not null && clipboardContent!.Count == 0)
            {
                clipboardContent = null;
            }
            if (clipboardContent is null)
            {
                App.Current.Logger.LogDebug("GetSelectedTextFromForegroundWindowAsync: Current clipboard had no content.");
            }
            //

            // NOTE: if clipboard content capture fails, we would ideally turn on clipboard history temporarily to use that; unfortunately, Windows does _not_ copy the current clipboard contents to the clipboard history at the time
            //       that clipboard history is enabled...so we would not be able to roll back to the previous entry using clipboard history

            // capture the last item in the windows clipboard history
            //
            // this variable is used to indicate that we captured the newest clipboard history item (and should therefore restore back to THAT item)
            bool clipboardHistoryIsCaptured;
            // this variable is used to store the newest clipboard history item (if the clipboard history was not empty)
            Windows.ApplicationModel.DataTransfer.ClipboardHistoryItem? newestClipboardHistoryItem = null;
            //
            // NOTE: if clipboard history is disabled, we'll capture the "ClipboardHistoryDisabled" error here and fall back to the manual backup of the clipboard content
            var getNewestClipboardClipboardHistoryItemResult = await Functions.GetNewestClipboardHistoryItemAsync();
            if (getNewestClipboardClipboardHistoryItemResult.IsError == true)
            {
                clipboardHistoryIsCaptured = false;
                //
                switch (getNewestClipboardClipboardHistoryItemResult.Error!.Value)
                {
                    case Morphic.WindowsNative.Clipboard.Clipboard.GetHistoryItemsError.Values.AccessDenied:
                        App.Current.Logger.LogDebug("GetSelectedTextFromForegroundWindowAsync: Could not get clipboard history (error: access denied).");
                        break;
                    case Morphic.WindowsNative.Clipboard.Clipboard.GetHistoryItemsError.Values.ClipboardHistoryDisabled:
                        App.Current.Logger.LogDebug("GetSelectedTextFromForegroundWindowAsync: Clipboard history is disabled.");
                        break;
                    default:
                        throw new MorphicUnhandledErrorException();
                }
            }
            else
            {
                clipboardHistoryIsCaptured = true;
                //
                App.Current.Logger.LogDebug("GetSelectedTextFromForegroundWindowAsync: Captured clipboard history.");
                newestClipboardHistoryItem = getNewestClipboardClipboardHistoryItemResult.Value!;
            }

            if (clipboardContentIsCaptured == false && clipboardHistoryIsCaptured == false)
            {
                // NOTE: ideally, we would return this problem scenario to the caller so that they could alert the user; unfortunately there aren't any particularly good workarounds (other than perhaps re-implementing the win32 wrappers around clipboard APIs to try to capture the clipboard state perfectly)
                //       we might also want to create an option in the arguments for this function which allowed the caller to request that we FAIL if the clipboard couldn't be backed up.
                // NOTE: clipboard history doesn't appear to backup all "copy" operations (such as file copies); for that reason, clipboard history may be useless to us in most/all scenarios.  However we need to support it so that users with clipboard history enabled don't get history entries (i.e. so that Morphic doesn't add each read-aloud text to their clipboard history).
                App.Current.Logger.LogDebug("GetSelectedTextFromForegroundWindowAsync: Clipboard content could not be backed up nor a clipboard history snapshot created; current clipboard content will be cleared.");
            }

            // NOTE: we use a try...finally block here to make sure that we restore the clipboard history after our operation is complete
            try
            {
                /* PHASE 2: capture the selected text in the last active ("foreground") window */

                // capture the selected text
                var copySelectedTextFromLastActiveWindowResult = await Functions.CopySelectedTextFromLastActiveWindowAsync();
                if (copySelectedTextFromLastActiveWindowResult.IsError == true) 
                {
                    // NOTE: in the future, we may want to consider reporting more robust error information
                    return MorphicResult.ErrorResult();
                }
                selectedText = copySelectedTextFromLastActiveWindowResult.Value;
            }
            finally
            {
                /* PHASE 3: restore the original clipboard contents */

                // if we captured the clipboard history, restore it now
                bool clipboardHistoryWasRestored = false;
                if (clipboardHistoryIsCaptured == true)
                {
                    if (newestClipboardHistoryItem is not null)
                    {
                        App.Current.Logger.LogDebug("GetSelectedTextFromForegroundWindowAsync: Attempting to restore the clipboard history.");
                        var restoreClipboardToHistoryItemResult = await Functions.RollbackClipboardToHistoryItemAsync(newestClipboardHistoryItem);
                        if (restoreClipboardToHistoryItemResult.IsError == true)
                        {
                            switch (restoreClipboardToHistoryItemResult.Error!.Value)
                            {
                                case RollbackClipboardToHistoryItemError.Values.AccessDenied:
                                    App.Current.Logger.LogDebug("GetSelectedTextFromForegroundWindowAsync: Could not restore (rollback) clipboard history: access denied.");
                                    break;
                                case RollbackClipboardToHistoryItemError.Values.ClipboardHistoryDisabled:
                                    App.Current.Logger.LogDebug("GetSelectedTextFromForegroundWindowAsync: Could not restore (rollback) clipboard history: clipboard history disabled (although it was enabled at start of capture).");
                                    break;
                                case RollbackClipboardToHistoryItemError.Values.CouldNotDeleteItem:
                                    App.Current.Logger.LogDebug("GetSelectedTextFromForegroundWindowAsync: Could not restore (rollback) clipboard history: could not delete newer items.");
                                    break;
                                case RollbackClipboardToHistoryItemError.Values.ItemWasDeleted:
                                    App.Current.Logger.LogDebug("GetSelectedTextFromForegroundWindowAsync: Could not restore (rollback) clipboard history: rollback point item was already deleted (although it existed at start of capture).");
                                    break;
                                case RollbackClipboardToHistoryItemError.Values.NoItemsInClipboardHistory:
                                    App.Current.Logger.LogDebug("GetSelectedTextFromForegroundWindowAsync: Could not restore (rollback) clipboard history: clipboard history is empty (and therefore the rollback point item is missing).");
                                    break;
                                default:
                                    throw new MorphicUnhandledErrorException();
                            }
                            //
                            // NOTE: in the future, we may want to return an error of "could not roll back history" so that the caller can try to fix the clipboard in another manner.
                            clipboardHistoryWasRestored = false;
                        }
                        else
                        {
                            var numberOfClipboardEntriesErased = restoreClipboardToHistoryItemResult.Value!;
#if DEBUG
                            if (numberOfClipboardEntriesErased != 1)
                            {
                                App.Current.Logger.LogDebug("GetSelectedTextFromForegroundWindowAsync: During clipboard history restore, expected one new history item; found " + numberOfClipboardEntriesErased.ToString() + " items instead.");
                            }
#endif // DEBUG

                            App.Current.Logger.LogDebug("GetSelectedTextFromForegroundWindowAsync: Clipboard history restoration complete");
                            clipboardHistoryWasRestored = true;
                        }
                    }
                    else
                    {
                        // as there were no entries in the clipboard history, simply clear the history after our copy operation
                        var clearHistoryResult = Morphic.WindowsNative.Clipboard.Clipboard.ClearHistory();
                        if (clearHistoryResult.IsError == true)
                        {
                            App.Current.Logger.LogDebug("GetSelectedTextFromForegroundWindowAsync: Could not clear clipboard history during clipboard history restore; clearing current contents instead.");

                            // in an attempt to gracefully degrade, clear the current contents of the clipboard instead
                            Morphic.WindowsNative.Clipboard.Clipboard.ClearContent();

                            clipboardHistoryWasRestored = true;
                        }
                        else
                        {
                            App.Current.Logger.LogDebug("GetSelectedTextFromForegroundWindowAsync: there is no clipboard history to restore.");
                            clipboardHistoryWasRestored = true;
                        }
                    }
                }

                var clipboardContentWasRestored = false;
                if (clipboardHistoryWasRestored == false)
                {
                    // if the clipboard history was not restored (either because there was no support for clipboard history or because clipboard history restoration failed), try to restore the backed-up clipboard content instead

                    if (clipboardContentIsCaptured == true)
                    {
                        // if the clipboard content was backed up (instead of the history), restore that content now
                        if (clipboardContent is not null)
                        {
                            // if the clipboard was not empty, restore its content
                            App.Current.Logger.LogDebug("GetSelectedTextFromForegroundWindowAsync: Attempting to restore " + clipboardContent.Count.ToString() + " item component(s) to the clipboard.");
                            Morphic.WindowsNative.Clipboard.Clipboard.RestoreContent(clipboardContent);
                        }
                        else
                        {
                            // if the clipboard was empty, clear it
                            App.Current.Logger.LogDebug("GetSelectedTextFromForegroundWindowAsync: there is nothing to restore to the clipboard.");
                            Morphic.WindowsNative.Clipboard.Clipboard.ClearContent();
                        }
                        clipboardContentWasRestored = true;
                    }
                }

                var clipboardContentWasClearedAsBackupPlan = false;
                if (clipboardHistoryIsCaptured == false && clipboardContentIsCaptured == false)
                {
                    // NOTE: in this scenario, there is nothing we can do to restore the clipboard, so we just clear it.
                    App.Current.Logger.LogDebug("GetSelectedTextFromForegroundWindowAsync: the clipboard was not backed up, so we are clearing it.");
                    Morphic.WindowsNative.Clipboard.Clipboard.ClearContent();

                    clipboardContentWasClearedAsBackupPlan = true;
                }

                // if we couldn't restore the clipboard history OR the clipboard content (and we didn't execute an alternate backup plan), then return a failure condition
                if (clipboardHistoryWasRestored == false && clipboardContentWasRestored == false && clipboardContentWasClearedAsBackupPlan == false)
                {
                    // NOTE: in the future, we may want to find a way to return this as a warning (while still returning the captured text) rather than simply indicating that an error occurred
                    isSuccess = false;
                }
            }

            return isSuccess ? MorphicResult.OkResult(selectedText) : MorphicResult.ErrorResult();
        }

        // NOTE: this function returns null if the clipboard history is empty
        private static async Task<MorphicResult<Windows.ApplicationModel.DataTransfer.ClipboardHistoryItem?, Morphic.WindowsNative.Clipboard.Clipboard.GetHistoryItemsError>> GetNewestClipboardHistoryItemAsync()
        {
            var isClipboardHistoryEnabled = Morphic.WindowsNative.Clipboard.Clipboard.IsHistoryEnabled();
            if (isClipboardHistoryEnabled == false)
            {
                return MorphicResult.ErrorResult(Morphic.WindowsNative.Clipboard.Clipboard.GetHistoryItemsError.ClipboardHistoryDisabled);
            }

            // NOTE: if we reach here, clipboard history is enabled

            // capture clipboard history
            var getHistoryItemsResult = await Morphic.WindowsNative.Clipboard.Clipboard.GetHistoryItemsAsync();
            if (getHistoryItemsResult.IsError == true)
            {
                switch (getHistoryItemsResult.Error!.Value)
                {
                    case Morphic.WindowsNative.Clipboard.Clipboard.GetHistoryItemsError.Values.AccessDenied:
                        return MorphicResult.ErrorResult(getHistoryItemsResult.Error!);
                    case Morphic.WindowsNative.Clipboard.Clipboard.GetHistoryItemsError.Values.ClipboardHistoryDisabled:
                        return MorphicResult.ErrorResult(getHistoryItemsResult.Error!);
                    default:
                        throw new MorphicUnhandledErrorException();
                }
            }
            var clipboardHistoryItems = getHistoryItemsResult.Value!;

            Windows.ApplicationModel.DataTransfer.ClipboardHistoryItem? newestClipboardHistoryItem = null;
            if (clipboardHistoryItems.Count > 0)
            {
                newestClipboardHistoryItem = clipboardHistoryItems.OrderBy(item => item.Timestamp).ToList().Last();
            }
            else
            {
                newestClipboardHistoryItem = null;
            }

            return MorphicResult.OkResult(newestClipboardHistoryItem);
        }

        public static async Task<MorphicResult<int, MorphicUnit>> WaitForClipboardHistoryToIncludeNewItems(DateTimeOffset afterTimestamp, int minimumCount = 1)
        {
            const int NUMBER_OF_ATTEMPTS = 5;
            const int MILLISECONDS_BETWEEN_ATTEMPTS = 50;

            for (var iAttempt = 0; iAttempt < NUMBER_OF_ATTEMPTS; iAttempt += 1)
            {
                var getHistoryItemsResult = await Morphic.WindowsNative.Clipboard.Clipboard.GetHistoryItemsAsync();
                if (getHistoryItemsResult.IsError == false)
                {
                    var clipboardHistoryItems = getHistoryItemsResult.Value!;

                    // get a list of any clipboard history items added since 'afterDateTimeOffset'
                    var newClipboardHistoryItems = clipboardHistoryItems.OrderBy(x => x.Timestamp).Where(x => x.Timestamp > afterTimestamp).ToList();
                    if (newClipboardHistoryItems.Count >= minimumCount) 
                    {
                        return MorphicResult.OkResult(newClipboardHistoryItems.Count);
                    }
                }

                // if we have not encountered any new items yet, wait MILLISECONDS_BETWEEN_ATTEMPTS and try again
                if (iAttempt < NUMBER_OF_ATTEMPTS - 1)
                {
                    await Task.Delay(MILLISECONDS_BETWEEN_ATTEMPTS);
                }
            }

            // if we did not find any new items before timing out completely, return an error condition
            return MorphicResult.ErrorResult();
        }

        public record RollbackClipboardToHistoryItemError : MorphicAssociatedValueEnum<RollbackClipboardToHistoryItemError.Values>
        {
            // enum members
            public enum Values
            {
                AccessDenied,
                ClipboardHistoryDisabled,
                CouldNotDeleteItem,
                ItemWasDeleted,
                NoItemsInClipboardHistory,
            }

            // functions to create member instances
            public static RollbackClipboardToHistoryItemError AccessDenied => new RollbackClipboardToHistoryItemError(Values.AccessDenied);
            public static RollbackClipboardToHistoryItemError ClipboardHistoryDisabled => new RollbackClipboardToHistoryItemError(Values.ClipboardHistoryDisabled);
            public static RollbackClipboardToHistoryItemError CouldNotDeleteItem => new RollbackClipboardToHistoryItemError(Values.CouldNotDeleteItem);
            public static RollbackClipboardToHistoryItemError ItemWasDeleted => new RollbackClipboardToHistoryItemError(Values.ItemWasDeleted);
            public static RollbackClipboardToHistoryItemError NoItemsInClipboardHistory => new RollbackClipboardToHistoryItemError(Values.NoItemsInClipboardHistory);

            // associated values

            // verbatim required constructor implementation for MorphicAssociatedValueEnums
            private RollbackClipboardToHistoryItemError(Values value) : base(value) { }
        }
        //
        // NOTE: this function returns the number of items removed from the clipboard history while rolling back to the specified history item
        private static async Task<MorphicResult<int, RollbackClipboardToHistoryItemError>> RollbackClipboardToHistoryItemAsync(Windows.ApplicationModel.DataTransfer.ClipboardHistoryItem item)
        {
            // wait for the clipboard history to include the new items; sometimes it takes the operating system a few moments to catch up
            // NOTE: this function uses a somewhat arbitrary countdown time; it may or may not be enough in practice
            var waitForClipboardHistoryToIncludeNewItemsResult = await Functions.WaitForClipboardHistoryToIncludeNewItems(item.Timestamp, 1);
            Debug.Assert(waitForClipboardHistoryToIncludeNewItemsResult.IsSuccess, "Clipboard history does not yet include the expected new items");
            // NOTE: for now, we ignore the result value
            // var numberOfNewClipboardHistoryItems = waitForClipboardHistoryToIncludeNewItemsResult.Value!;

            // delete all entries newer than the supplied item
            var getHistoryItemsResult = await Morphic.WindowsNative.Clipboard.Clipboard.GetHistoryItemsAsync();
            if (getHistoryItemsResult.IsError == true)
            {
                switch (getHistoryItemsResult.Error!.Value)
                {
                    case Morphic.WindowsNative.Clipboard.Clipboard.GetHistoryItemsError.Values.AccessDenied:
                        App.Current.Logger.LogDebug("RollbackClipboardToHistoryItemAsync: Could not get clipboard history during clipboard history restore (error: access denied).");
                        return MorphicResult.ErrorResult(RollbackClipboardToHistoryItemError.AccessDenied);
                    case Morphic.WindowsNative.Clipboard.Clipboard.GetHistoryItemsError.Values.ClipboardHistoryDisabled:
                        App.Current.Logger.LogDebug("RollbackClipboardToHistoryItemAsync: Could not get clipboard history during clipboard history restore (error: clipboard history disabled).");
                        return MorphicResult.ErrorResult(RollbackClipboardToHistoryItemError.ClipboardHistoryDisabled);
                    default:
                        throw new MorphicUnhandledErrorException();
                }
            }
            var clipboardHistoryItems = getHistoryItemsResult.Value!;

            int numberOfHistoryItemsRemoved;
            if (clipboardHistoryItems.Count > 0)
            {
                // get a list of any clipboard history items added since our operation continued; this should just include the clipboard history item we created
                var newClipboardHistoryItems = clipboardHistoryItems.OrderBy(x => x.Timestamp).Where(x => x.Timestamp > item.Timestamp).ToList();
                numberOfHistoryItemsRemoved = newClipboardHistoryItems.Count;

                foreach (var newClipboardHistoryItem in newClipboardHistoryItems)
                {
                    var deleteItemFromHistoryResult = Morphic.WindowsNative.Clipboard.Clipboard.DeleteItemFromHistory(newClipboardHistoryItem);
                    if (deleteItemFromHistoryResult.IsError == true)
                    {
                        App.Current.Logger.LogDebug("RollbackClipboardToHistoryItemAsync: Could not delete new history item during clipboard history restore (error: could not delete item from history).");
                        return MorphicResult.ErrorResult(RollbackClipboardToHistoryItemError.CouldNotDeleteItem);
                    }
                }
            }
            else
            {
                return MorphicResult.ErrorResult(RollbackClipboardToHistoryItemError.NoItemsInClipboardHistory);
            }

            // restore the previously-newest clipboard entry
            var setHistoryItemAsContentResult = Morphic.WindowsNative.Clipboard.Clipboard.SetHistoryItemAsContent(item);
            if (setHistoryItemAsContentResult.IsError == true)
            {
                switch (setHistoryItemAsContentResult.Error!.Value)
                {
                    case WindowsNative.Clipboard.Clipboard.SetHistoryItemAsContentError.Values.AccessDenied:
                        App.Current.Logger.LogDebug("RollbackClipboardToHistoryItemAsync: Could not restore history setpoint during clipboard history restore (error: access denied).");
                        return MorphicResult.ErrorResult(RollbackClipboardToHistoryItemError.AccessDenied);
                    case WindowsNative.Clipboard.Clipboard.SetHistoryItemAsContentError.Values.ItemDeleted:
                        App.Current.Logger.LogDebug("RollbackClipboardToHistoryItemAsync: Could not restore history setpoint during clipboard history restore (error: item deleted).");
                        return MorphicResult.ErrorResult(RollbackClipboardToHistoryItemError.ItemWasDeleted);
                    default:
                        throw new MorphicUnhandledErrorException();
                }
            }

            return MorphicResult.OkResult(numberOfHistoryItemsRemoved);
        }

        private static async Task<MorphicResult<string?, MorphicUnit>> CopySelectedTextFromLastActiveWindowAsync()
        {
            // clear the clipboard content before copying the text (in case the target application didn't have anything to copy)
            Morphic.WindowsNative.Clipboard.Clipboard.ClearContent();

            // NOTE: previously, we called ClearClipboardAsync and got an error back if the clipboard couldn't be cleared; in our current implementation, we assume that the ClearContent function will always succeed synchronously
            //try
            //{
            //    // try to clear the clipboard for up to 500ms (4 delays of 125ms)
            //    await Functions.ClearClipboardAsync(5, new TimeSpan(0,0,0,0,125));
            //} 
            //catch
            //{
            //    App.Current.Logger.LogDebug("CopySelectedTextFromLastActiveWindowAsync: Could not clear selected text from the clipboard.");
            //}

            // copy the current selection to the clipboard
            App.Current.Logger.LogDebug("CopySelectedTextFromLastActiveWindow: Sending Ctrl+C to copy the current selection to the clipboard.");
            await SelectionReader.Default.CopySelectedTextToClipboardAsync(System.Windows.Forms.SendKeys.SendWait);

            // wait 100ms (an arbitrary amount of time, but in our testing some wait is necessary...even with the WM-triggered copy logic above)
            // NOTE: perhaps, in the future, we should only do this if our first call to Clipboard.GetText() returns (null? or) an empty string;
            //       or perhaps we should wait up to a certain number of milliseconds to receive a SECOND WM (the one that GetSelectedTextAsync
            //       waited for).
            await Task.Delay(100);

            // capture the current selection
            var textCopiedToClipboard = await Morphic.WindowsNative.Clipboard.Clipboard.GetTextAsync();
            if (textCopiedToClipboard is not null)
            {
                // we now have our selected text
                string? selectedText = textCopiedToClipboard;

                if (selectedText is not null)
                {
                    App.Current.Logger.LogDebug("CopySelectedTextFromLastActiveWindow: Captured selected text.");
                }
                else
                {
                    App.Current.Logger.LogDebug("CopySelectedTextFromLastActiveWindow: Captured empty selection.");
                }

                return MorphicResult.OkResult(selectedText);
            }
            else
            {
                // write out diagnostics information if we were unable to copy contents via ctrl+c

                var copiedDataFormats = Morphic.WindowsNative.Clipboard.Clipboard.GetContentFormats();
                if (copiedDataFormats is not null && copiedDataFormats.Count > 0)
                {
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
                    App.Current.Logger.LogDebug("CopySelectedTextFromLastActiveWindow: Ctrl+C copied non-text (nonspeakable) contents to the clipboard.");
                }
                else
                {
                    App.Current.Logger.LogDebug("CopySelectedTextFromLastActiveWindow: Ctrl+C did not copy anything to the clipboard.");

                    // NOTE: we are making an assumption here that nothing was selected if we executed "copy" and the clipboard was subsequently empty
                    return MorphicResult.OkResult<string?>(null);
                }

                return MorphicResult.ErrorResult();
            }
        }

        //

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

        [InternalFunction("allUsbAction")]
        public static async Task<MorphicResult<MorphicUnit, MorphicUnit>> AllUsbActionAsync(FunctionArgs args)
        {
            var value = args["value"];

            switch (value)
            {
                case "openallusb":
                    var openAllUsbDrivesResult = await Functions.OpenAllUsbDrivesAsync(args);
                    if (openAllUsbDrivesResult.IsError == true)
                    {
                        Debug.Assert(false, "Could not open mounted drives");
                        App.Current.Logger.LogError("Could not open mounted drives");
                    }
                    break;
                case "ejectallusb":
                    var ejectAllUsbDrivesResult = await Functions.EjectAllUsbDrivesAsync(args);
                    if (ejectAllUsbDrivesResult.IsError == true)
                    {
                        Debug.Assert(false, "Could not eject mounted drives");
                        App.Current.Logger.LogError("Could not eject mounted drives");
                    }
                    break;
            }

            return MorphicResult.OkResult();
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
            if (osVersion is null)
            {
                // error
                return MorphicResult.ErrorResult();
            }
            else
            {
                // Windows 10 v1903+

                // get system dark/light theme
                //
                //Setting systemThemeSetting = App.Current.MorphicSession.Solutions.GetSetting(SettingId.LightThemeSystem);
                //var getSystemThemeValueResult = await systemThemeSetting.GetValueAsync();
                //if (getSystemThemeValueResult.IsError == true)
                //{
                //    return MorphicResult.ErrorResult();
                //}
                //var lightThemeSystemAsObject = getSystemThemeValueResult.Value!;
                //var lightThemeSystemAsBool = (bool)lightThemeSystemAsObject;
                //
                var getSystemUsesLightThemeSettingResult = Morphic.WindowsNative.Theme.LightTheme.GetSystemUsesLightThemeSetting();
                if (getSystemUsesLightThemeSettingResult.IsError == true)
                {
                    return MorphicResult.ErrorResult();
                }
                var lightThemeSystemAsBool = getSystemUsesLightThemeSettingResult.Value!;

                // get apps dark/light theme
                //
                //Setting appsThemeSetting = App.Current.MorphicSession.Solutions.GetSetting(SettingId.LightThemeApps);
                //var getAppsThemeValueResult = await appsThemeSetting.GetValueAsync();
                //if (getAppsThemeValueResult.IsError == true)
                //{
                //    return MorphicResult.ErrorResult();
                //}
                //var lightThemeAppsAsObject = getAppsThemeValueResult.Value!;
                //var lightThemeAppsAsBool = (bool)lightThemeAppsAsObject;
				//
                var getAppsUseLightThemeSettingResult = Morphic.WindowsNative.Theme.LightTheme.GetAppsUseLightThemeSetting();
                if (getAppsUseLightThemeSettingResult.IsError == true)
                {
                    return MorphicResult.ErrorResult();
                }
                var lightThemeAppsAsBool = getAppsUseLightThemeSettingResult.Value!;

                // if either apps or system theme is set to "not light", then return true 
                var darkModeIsEnabled = ((lightThemeSystemAsBool == false) || (lightThemeAppsAsBool == false));
                return MorphicResult.OkResult(darkModeIsEnabled);
            }
        }

        internal async static Task<MorphicResult<MorphicUnit, MorphicUnit>> SetDarkModeStateAsync(bool state)
        {
            var osVersion = Morphic.WindowsNative.OsVersion.OsVersion.GetWindowsVersion();
            if (osVersion is null)
            {
                // error
                return MorphicResult.ErrorResult();
            }
            else
            {
                // Windows 10 v1903+

                /*
                 * NOTE: in addition to the SPI implementation (in code, below), we could also turn on/off the dark theme (via powershell...or possibly via direct registry access); here are the corresponding PowerShell commands
                 * NOTE: we used registry access to get/set dark mode prior to  Windows 10 v1903
                 * NOTE: the "system dark theme" was introduced in Windows 10 v1903
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
                //
                // implementation option 1: use SystemSettings
                _ = await Morphic.WindowsNative.Theme.DarkMode.SetSystemUsesDarkModeAsync(state);
                //
                // implementation option 2: use registry setting (NOTE: may require follow-up theme change notification broadcast message)
                //Setting systemThemeSetting = App.Current.MorphicSession.Solutions.GetSetting(SettingId.LightThemeSystem);
                //_ = await systemThemeSetting.SetValueAsync(!state);

                // set apps dark/light theme
                //
                // implementation option 1: use SystemSettings
                _ = await Morphic.WindowsNative.Theme.DarkMode.SetAppsUseDarkModeAsync(state);
                //
                // implementation option 2: use registry setting (NOTE: may require follow-up theme change notification broadcast message)
                //Setting appsThemeSetting = App.Current.MorphicSession.Solutions.GetSetting(SettingId.LightThemeApps);
                //_ = await appsThemeSetting.SetValueAsync(!state);
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
