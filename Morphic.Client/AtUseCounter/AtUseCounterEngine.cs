// Copyright 2020-2023 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-windows/blob/master/LICENSE.txt
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

using Morphic.Core;
using Morphic.TelemetryClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Display.Core;

namespace Morphic.Client.AtUseCounter;

internal class AtUseCounterEngine
{
     // useful debug constants
     private static bool SHOW_EVENT_HANDLER_CALLS = false;

     // telemetry objects
     private static MorphicTelemetryClient _telemetryClient;
     private static Guid? _telemetrySessionId = null;
     private static Timer? _telemetryHeartbeatTimer = null;

     private static System.Diagnostics.Stopwatch _stopwatch;

     // state variables
     private static bool _highContrastIsOn;
     //
     private static List<DisplayState> _displayStates;
     private static object _displayStateLockObject = new();
     //
     private static uint _mouseCursorSize;
     private static double? _ignoreMouseCursorSizeChangesUntilTimestamp;
     private static object _ignoreMouseCursorSizeChangesUntilTimestampLock = new();
     //
     // NOTE: the first item in the _darkThemeStateHistory list is the oldest history item
     private static List<DarkThemeState> _darkThemeStateHistory;
     private static object _darkThemeStateHistoryLockObject = new();
     //
     private static bool _colorFiltersAreActive;
     //
     private static bool _nightLightIsOn;

     private static Morphic.Core.MorphicSequentialTaskScheduler _sequentialTaskScheduler;
     private static TaskFactory _sequentialTaskFactory;

     private static Morphic.WindowsNative.Process.ProcessWatcher _processWatcher;

     static public async Task ConfigureAndStartAtUseCounterAsync(string mqttServerHostname, string appName, string appKey, Utils.TelemetryUtils.TelemetryIdComponents telemetryIds)
     {
          // start a stopwatch; we'll use this to help understand when rapid sequences of changes are really just one change (e.g. mouse cursor size changes)
          _stopwatch = Stopwatch.StartNew();

          _sequentialTaskScheduler = new Morphic.Core.MorphicSequentialTaskScheduler();
          _sequentialTaskFactory = new TaskFactory(_sequentialTaskScheduler);

          // set up and start telemetry
          await AtUseCounterEngine.ConfigureAndStartTelemetryAsync(mqttServerHostname, appName, appKey, telemetryIds);

          // retrieve the initial state of the AT features
          await AtUseCounterEngine.GetInitialStateAsync();

          // capture display events (e.g. changes in display scale)
          //
          // NOTE: to avoid needing to handle exceptions when setting up our DisplaySettingsChanged handler, we first manually start up the display settings listener; if our process was
          //       running without the ability or permissions to start up the appropriate system-/session-level listeners, we'd otherwise get an exception when wiring up the event(s).
          var startListeningForDisplaySettingsEventsResult = Morphic.WindowsNative.Display.DisplaySettingsListener.Shared.StartListening();
          if (startListeningForDisplaySettingsEventsResult.IsError == true)
          {
               // NOTE: in the future, we may want to log or otherwise capture the knowledge that we are unable to capture Win32 display settings changes
               Debug.Assert(false, "Could not listen for Win32 display settings changes");
          }
          else
          {
               // NOTE: wiring up this event will result in an exception if the display settings listener could not be started successfully (see note immediately above)
               Morphic.WindowsNative.Display.DisplaySettingsListener.Shared.DisplaySettingsChanged += AtUseCounterEngine.OnDisplaySettingsChanged;
          }

          // capture user preference events (e.g. changes in: high contrast mode (on/off); dark mode on/off; pointer size; etc.)
          //
          // NOTE: to avoid needing to handle exceptions when setting up our UserPreferenceChanged handler, we first manually start up the user preference listener; if our process was
          //       running without the ability or permissions to start up the appropriate system-/session-level listeners, we'd otherwise get an exception when wiring up the event(s).
          var startListeningForUserPreferenceEventsResult = Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.StartListening();
          if (startListeningForUserPreferenceEventsResult.IsError == true)
          {
               // NOTE: in the future, we may want to log or otherwise capture the knowledge that we are unable to capture Win32 user preference changes
               Debug.Assert(false, "Could not listen for Win32 user preference changes");
          }
          else
          {
               // NOTE: wiring up this event will result in an exception if the user preference listener could not be started successfully (see note immediately above)
               // NOTE: all of these events were effectively wired up in Morphic v1.6 and earlier; we are wiring them all up out of an abundance of caution, but realistically we should only
               //       wire up the display-related user preference change notifications
               //
               // triggered when changed: high contrast mode on/off; 
               Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.AccessibilityUserPreferenceChanged += AtUseCounterEngine.OnAccessibilityUserPreferenceChanged;
               //
               // triggered when changed: high contrast mode on/off; 
               Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.ColorUserPreferenceChanged += AtUseCounterEngine.OnColorUserPreferenceChanged;
               //
               Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.DesktopUserPreferenceChanged += AtUseCounterEngine.OnDesktopUserPreferenceChanged;
               //
               // triggered when changed: high contrast mode on/off; dark mode on/off; pointer size;
               Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.GeneralUserPreferenceChanged += AtUseCounterEngine.OnGeneralUserPreferenceChanged;
               //
               Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.IconUserPreferenceChanged += AtUseCounterEngine.OnIconUserPreferenceChanged;
               //
               Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.KeyboardUserPreferenceChanged += AtUseCounterEngine.OnKeyboardUserPreferenceChanged;
               //
               Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.LocaleUserPreferenceChanged += AtUseCounterEngine.OnLocaleUserPreferenceChanged;
               //
               Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.MenuUserPreferenceChanged += AtUseCounterEngine.OnMenuUserPreferenceChanged;
               //
               Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.MouseUserPreferenceChanged += AtUseCounterEngine.OnMouseUserPreferenceChanged;
               //
               Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.PolicyUserPreferenceChanged += AtUseCounterEngine.OnPolicyUserPreferenceChanged;
               //
               Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.PowerUserPreferenceChanged += AtUseCounterEngine.OnPowerUserPreferenceChanged;
               //
               Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.ScreensaverUserPreferenceChanged += AtUseCounterEngine.OnScreensaverUserPreferenceChanged;
               //
               // triggered when changed: high contrast mode on/off; 
               Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.VisualStyleUserPreferenceChanged += AtUseCounterEngine.OnVisualStyleUserPreferenceChanged;
               //
               // triggered when changed: high contrast mode on/off; 
               Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.WindowUserPreferenceChanged += AtUseCounterEngine.OnWindowUserPreferenceChanged;
          }

          // triggered when color filter IsActive is changed
          Morphic.WindowsNative.Accessibility.ColorFilters.IsActiveChanged += AtUseCounterEngine.ColorFilters_IsActiveChanged;

          // triggered when changed: dark mode on/off
          Morphic.WindowsNative.Theme.DarkMode.AppsUseDarkModeChanged += AtUseCounterEngine.DarkMode_DarkModeChanged;
          Morphic.WindowsNative.Theme.DarkMode.SystemUsesDarkModeChanged += AtUseCounterEngine.DarkMode_DarkModeChanged;

          // triggered when night light IsEnabled is changed
          Morphic.WindowsNative.Display.NightLight.IsOnChanged += AtUseCounterEngine.NightLight_IsOnChanged;

          // triggered when mouse cursor registry key is changed
          var openCursorsSubKeyResult = Morphic.WindowsNative.Registry.CurrentUser.OpenSubKey(@"Control Panel\Cursors");
          if (openCursorsSubKeyResult.IsError == true)
          {
               // NOTE: since we can capture the cursor size changes (albeit perhaps belatedly) otherwise, this is maybe not so critical
               // NOTE: if this fails, then we could just set up a poll every 30 seconds instead (or whatever half of our "changes are done for the mouse cursor size" window is)
               var win32ApiError = openCursorsSubKeyResult.Error!;
               Debug.Assert(false, "Could not watch mouse cursor registry key; win32 error: " + win32ApiError.ToString());
          }
          else
          {
               var cursorsSubKey = openCursorsSubKeyResult.Value!;
               cursorsSubKey.RegistryKeyChangedEvent += AtUseCounterEngine.CursorsSubKey_RegistryKeyChangedEvent;
          }

          // triggered when watched processes are started/stopped
          var processWatcher = Morphic.WindowsNative.Process.ProcessWatcher.CreateNew();
          processWatcher.ProcessNamesWatchFilter = new(new string[] { "Magnify.exe", "Magnify", "ScreenClippingHost.exe", "ScreenClippingHost", "SystemSettings.exe", "SystemSettings" });
          processWatcher.ProcessStarted += AtUseCounterEngine.ProcessWatcher_ProcessStarted;
          processWatcher.ProcessStopped += AtUseCounterEngine.ProcessWatcher_ProcessStopped;
          _processWatcher = processWatcher;
          //
          Morphic.WindowsNative.Process.ProcessWatcher.Start(new TimeSpan(0, 0, 1));
     }

     // NOTE: this function waits up to two seconds for the telemetry client to close
     static public async Task ShutdownAtUseCounterAsync()
     {
          // send the final telemetry message (@session end)
          var eventData = new SessionTelemetryEventData()
          {
               SessionId = _telemetrySessionId,
               State = "end"
          };
          _telemetryClient.EnqueueEvent("@session", eventData);

          // wait up to two seconds for the event (and any other outstanding events) to be sent
          var waitTimeSpan = TimeSpan.FromSeconds(2);
          // NOTE: PrepareForDisposalAsync will attempt to finish sending the current message(s); this will not necessarily send the message we just enqueued, but if not
          //       then that message will be saved for the next telemetry server link-up and in the interim the telemetry server will count our last-sent event as the
          //       end of the session instead.  It will also attempt to flush any remaining queued items out to the on-disk persistant log (so they can sent on the next run)
          var cancellationTokenSource = new CancellationTokenSource();
          var cancellationToken = cancellationTokenSource.Token;
          var task = Task.Run(() =>
          {
               // NOTE: MorphicTelemetryClient.PrepareForDisposalAsync(...) may only be called once in the current implementation, so it's appropriate to call it here before shutdown
               _telemetryClient.PrepareForDisposalAsync(waitTimeSpan).GetAwaiter().GetResult();
               cancellationTokenSource.Cancel();
          });
          try
          {
               await Task.Delay(waitTimeSpan, cancellationToken);
               // NOTE: if we reach here, the function did not return on time
          }
          catch (TaskCanceledException)
          {
               // task was ended before timeout, which is the expected behavior if we ended before timeout
          }

          // dispose of the telemetry client; note that this may take up to 250ms (as the dispose function waits up to 250ms for the in-memory logs to be flushed to disk)
          _telemetryClient.Dispose();
     }

     //

     static private async Task ConfigureAndStartTelemetryAsync(string mqttServerHostname, string appName, string appKey, Utils.TelemetryUtils.TelemetryIdComponents telemetryIds)
     {
          var telemetryCompositeId = telemetryIds.CompositeId;
          var telemetrySiteId = telemetryIds.SiteId;
          var telemetryDeviceUuid = telemetryIds.DeviceUuid;

          // configure our telemetry uplink
          var mqttHostname = mqttServerHostname;
          var mqttClientId = telemetryDeviceUuid;
          var mqttUsername = appName;
          var mqttAnonymousPassword = appKey;

          // configure our telemetry client; it will automatically cache event records and connect to the server to send them on demand
          var telemetryClientConfig = new MorphicTelemetryClient.WebsocketTelemetryClientConfig(
              hostname: mqttHostname,
              port: 443,
              path: "/ws",
              clientId: mqttClientId,
              username: mqttUsername,
              password: mqttAnonymousPassword,
              useTls: true
          );
          MorphicTelemetryClient? telemetryClient = null;
          //
          string? userLocalAppDirectory = null;
          try
          {
               userLocalAppDirectory = Morphic.Client.Config.AppPaths.UserLocalConfigDir;
               if (System.IO.Directory.Exists(userLocalAppDirectory) == false)
               {
                    System.IO.Directory.CreateDirectory(userLocalAppDirectory);
               }
          }
          catch { }
          //
          if (userLocalAppDirectory is not null)
          {
               var pathToOnDiskTransactionLog = Path.Combine(userLocalAppDirectory, "atusecounter.log");
               var createTelemetryClientResult = await MorphicTelemetryClient.CreateUsingOnDiskTransactionLogAsync(telemetryClientConfig, pathToOnDiskTransactionLog);
               if (createTelemetryClientResult.IsSuccess == true)
               {
                    // we were able to read in the on-disk telemetry log (or create it); proceed with the newly-instantiated telemetry client
                    telemetryClient = createTelemetryClientResult.Value!;
               }
               else // createTelemetryClientResult.IsError == true
               {
                    // if we could not open the on-disk transaction log, attempt to delete the log and try to create a new file instead
                    try
                    {
                         // try to delete the existing file
                         System.IO.File.Delete(pathToOnDiskTransactionLog);

                         // try to create a new telemetry file at the specified path
                         createTelemetryClientResult = await MorphicTelemetryClient.CreateUsingOnDiskTransactionLogAsync(telemetryClientConfig, pathToOnDiskTransactionLog);
                         if (createTelemetryClientResult.IsSuccess == true)
                         {
                              telemetryClient = createTelemetryClientResult.Value!;
                         }
                    }
                    catch { }
               }
          }
          if (telemetryClient is null)
          {
               // if we could not create a telemetry file at the specified path, simply create a telemetry client (without on-disk persistance)
               telemetryClient = MorphicTelemetryClient.Create(telemetryClientConfig);
          }
          //
          if (telemetrySiteId is not null)
          {
               // if a site id is provided, remove any disallowed characters; if no characters remain, set the siteid to null
               var sanitizedTelemetrySiteId = Utils.TelemetryUtils.SanitizeSiteId(telemetrySiteId!);
               telemetrySiteId = sanitizedTelemetrySiteId != "" ? sanitizedTelemetrySiteId : null;
          }
          telemetryClient.SetSiteId(telemetrySiteId);
          _telemetryClient = telemetryClient;

          // create random session id
          _telemetrySessionId = Guid.NewGuid();

          //

          // send the first telemetry message (@session begin)
          // NOTE: we enqueue this message as soon as we create the telemetry client object
          var eventData = new SessionTelemetryEventData()
          {
               SessionId = _telemetrySessionId,
               State = "begin"
          };
          telemetryClient.EnqueueEvent("@session", eventData);

          // initialize (and start) our heartbeat timer; it should send the heartbeat message every 12 hours (i.e. twice a day, so that at least one event is recorded per active session per day)
          _telemetryHeartbeatTimer = new System.Threading.Timer(AtUseCounterEngine.SendTelemetryHeartbeat, null, new TimeSpan(12, 0, 0), new TimeSpan(12, 0, 0));
     }

     internal record SessionTelemetryEventData
     {
          [JsonPropertyName("session_id")]
          public Guid? SessionId { get; set; }
          //
          [JsonPropertyName("state")]
          public string? State { get; set; }
     }

     private static void SendTelemetryHeartbeat(object? state)
     {
          // send a ping/heartbeat telemetry message (@session heartbeat)
          var eventData = new SessionTelemetryEventData()
          {
               SessionId = _telemetrySessionId,
               State = "heartbeat"
          };
          _telemetryClient?.EnqueueEvent("@session", eventData);
     }

     //

     static async Task GetInitialStateAsync()
     {
          var timeoutForCaptureInitialValue = new TimeSpan(0, 0, 0, 0, 250);


          // high contrast mode
          bool highContrastModeIsOn;
          var getHighContrastModeResult = Morphic.WindowsNative.Theme.HighContrast.GetIsOn();
          if (getHighContrastModeResult.IsError == true)
          {
               Debug.Assert(false, "Could not get initial high contrast mode state");

               // fail gracefully: set high contrast mode to off by default
               highContrastModeIsOn = false;
          }
          else
          {
               highContrastModeIsOn = getHighContrastModeResult.Value!;
               Debug.WriteLine("INITIAL | High contrast mode is on: " + highContrastModeIsOn.ToString());
          }
          _highContrastIsOn = highContrastModeIsOn;


          // display states
          //
          // initialize our _displayStates variable
          _displayStates = new();
          //
          // get (init) display states (which will populate our _displayStates variable)
          var (initDisplayStatesResult, initialDisplayStates) = AtUseCounterEngine.InitOrGetChangedDisplayStates();
          if (initDisplayStatesResult.IsError == true)
          {
               Debug.Assert(false, "Could not get full list of displays");
          }
          if (initialDisplayStates.Count > 0)
          {
               Debug.WriteLine("INITIAL | Display scale percentage(s):");
               foreach (var displayState in initialDisplayStates)
               {
                    Debug.WriteLine("          0x" + displayState.Display.AdapterId.HighPart.ToString("X") + displayState.Display.AdapterId.LowPart.ToString("X") + ": " + (displayState.ScalePercentage * 100).ToString() + "%");
               }
          }


          // mouse pointer size
          uint mouseCursorSize;
          var getMouseCursorSizeResult = Morphic.WindowsNative.Mouse.Mouse.GetCursorSize();
          if (getMouseCursorSizeResult.IsError == true)
          {
               Debug.Assert(false, "Could not get initial mouse cursor size");

               // default the mouseCursorSize to 32
               mouseCursorSize = 32;
          }
          else
          {
               if (getMouseCursorSizeResult.Value is null)
               {
                    Debug.Assert(false, "Could not find mouse cursor size in registry");

                    // default the mouseCursorSize to 32
                    mouseCursorSize = 32;
               }
               else
               {
                    mouseCursorSize = getMouseCursorSizeResult.Value!.Value;
                    Debug.WriteLine("INITIAL | Mouse cursor size: " + mouseCursorSize.ToString());
               }
          }
          _mouseCursorSize = mouseCursorSize;



          // dark theme state
          DarkThemeState darkModeState;
          var getDarkModeStateResult = AtUseCounterEngine.GetDarkThemeState();
          if (getDarkModeStateResult.IsError == true)
          {
               Debug.Assert(false, "Could not get initial dark mode state(s)");

               // set the dark mode state default to "light theme"
               darkModeState = new DarkThemeState() { AppsUseDarkTheme = false, SystemUsesDarkTheme = false };
          }
          else
          {
               darkModeState = getDarkModeStateResult.Value!;
               if (darkModeState.SystemUsesDarkTheme is null)
               {
                    Debug.WriteLine("INITIAL | Dark mode states: [apps: " + darkModeState.AppsUseDarkTheme.ToString() + "]");
               }
               else
               {
                    Debug.WriteLine("INITIAL | Dark mode states: [system: " + darkModeState.SystemUsesDarkTheme.ToString() + "; apps: " + darkModeState.AppsUseDarkTheme.ToString() + "]");
               }
          }
          lock (_darkThemeStateHistoryLockObject)
          {
               _darkThemeStateHistory = new();
               _darkThemeStateHistory.Add(darkModeState);
          }


          // color filters
          bool colorFiltersAreActive;
          var getColorFiltersAreActiveResult = Morphic.WindowsNative.Accessibility.ColorFilters.GetIsActive();
          if (getColorFiltersAreActiveResult.IsError == true)
          {
               Debug.Assert(false, "Could not get initial color filters active state");

               // set the color filters default to off
               colorFiltersAreActive = false;
          }
          else
          {
               if (getColorFiltersAreActiveResult.Value is not null)
               {
                    colorFiltersAreActive = getColorFiltersAreActiveResult.Value!.Value;
               }
               else
               {
                    Debug.Assert(false, "Could not get initial color filters active state (i.e. returned null)");

                    colorFiltersAreActive = false;
               }
               Debug.WriteLine("INITIAL | Color filters are active: " + colorFiltersAreActive.ToString());
          }
          _colorFiltersAreActive = colorFiltersAreActive;


          // night light
          bool nightLightIsEnabled;
          var getNightLightIsEnabledResult = await Morphic.WindowsNative.Display.NightLight.GetIsOnAsync(timeoutForCaptureInitialValue);
          if (getNightLightIsEnabledResult.IsError == true)
          {
               Debug.Assert(false, "Could not get initial night light enabled state");

               // set the night light default to off
               nightLightIsEnabled = false;
          }
          else
          {
               nightLightIsEnabled = getNightLightIsEnabledResult.Value!.Value;
               Debug.WriteLine("INITIAL | Night mode is enabled: " + nightLightIsEnabled.ToString());
          }
          _nightLightIsOn = nightLightIsEnabled;
     }

     //

     static void OnDisplaySettingsChanged(object? sender, EventArgs e)
     {
          if (SHOW_EVENT_HANDLER_CALLS == true)
          {
               Debug.WriteLine("OnDisplaySettingsChanged");
          }
          _sequentialTaskFactory.StartNew(() => AtUseCounterEngine.CheckForDisplayScalePercentageChange());
     }

     static void OnAccessibilityUserPreferenceChanged(object? sender, EventArgs e)
     {
          if (SHOW_EVENT_HANDLER_CALLS == true)
          {
               Debug.WriteLine("OnAccessibilityUserPreferenceChanged");
          }
          _sequentialTaskFactory.StartNew(() => AtUseCounterEngine.CheckForHighContrastIsOnChange());
     }

     static void OnColorUserPreferenceChanged(object? sender, EventArgs e)
     {
          if (SHOW_EVENT_HANDLER_CALLS == true)
          {
               Debug.WriteLine("OnColorUserPreferenceChanged");
          }
          _sequentialTaskFactory.StartNew(() => AtUseCounterEngine.CheckForHighContrastIsOnChange());
     }

     static void OnDesktopUserPreferenceChanged(object? sender, EventArgs e)
     {
          if (SHOW_EVENT_HANDLER_CALLS == true)
          {
               Debug.WriteLine("OnDesktopUserPreferenceChanged");
          }
          _sequentialTaskFactory.StartNew(() => { });
     }

     static void OnGeneralUserPreferenceChanged(object? sender, EventArgs e)
     {
          if (SHOW_EVENT_HANDLER_CALLS == true)
          {
               Debug.WriteLine("OnGeneralUserPreferenceChanged");
          }
          _sequentialTaskFactory.StartNew(() => {
               AtUseCounterEngine.CheckForHighContrastIsOnChange();
               AtUseCounterEngine.CheckForDarkModeChange();
               // NOTE: based on observations, Windows doesn't raise this event every time the mouse cursor size changes in the Windows settings panel UI; we should also capture the 
               //       changes by watching the corresponding registry key (and if that isn't effective enough, by polling or using delayed checks etc.)
               AtUseCounterEngine.CheckForMouseCursorSizeChange();
          });
     }

     static void OnIconUserPreferenceChanged(object? sender, EventArgs e)
     {
          if (SHOW_EVENT_HANDLER_CALLS == true)
          {
               Debug.WriteLine("OnIconUserPreferenceChanged");
          }
          _sequentialTaskFactory.StartNew(() => { });
     }

     static void OnKeyboardUserPreferenceChanged(object? sender, EventArgs e)
     {
          if (SHOW_EVENT_HANDLER_CALLS == true)
          {
               Debug.WriteLine("OnKeyboardUserPreferenceChanged");
          }
          _sequentialTaskFactory.StartNew(() => { });
     }

     static void OnLocaleUserPreferenceChanged(object? sender, EventArgs e)
     {
          if (SHOW_EVENT_HANDLER_CALLS == true)
          {
               Debug.WriteLine("OnLocaleUserPreferenceChanged");
          }
          _sequentialTaskFactory.StartNew(() => { });
     }

     static void OnMenuUserPreferenceChanged(object? sender, EventArgs e)
     {
          if (SHOW_EVENT_HANDLER_CALLS == true)
          {
               Debug.WriteLine("OnMenuUserPreferenceChanged");
          }
          _sequentialTaskFactory.StartNew(() => { });
     }

     static void OnMouseUserPreferenceChanged(object? sender, EventArgs e)
     {
          if (SHOW_EVENT_HANDLER_CALLS == true)
          {
               Debug.WriteLine("OnMouseUserPreferenceChanged");
          }
          _sequentialTaskFactory.StartNew(() => { });
     }

     static void OnPolicyUserPreferenceChanged(object? sender, EventArgs e)
     {
          if (SHOW_EVENT_HANDLER_CALLS == true)
          {
               Debug.WriteLine("OnPolicyUserPreferenceChanged");
          }
          _sequentialTaskFactory.StartNew(() => { });
     }

     static void OnPowerUserPreferenceChanged(object? sender, EventArgs e)
     {
          if (SHOW_EVENT_HANDLER_CALLS == true)
          {
               Debug.WriteLine("OnPowerUserPreferenceChanged");
          }
          _sequentialTaskFactory.StartNew(() => { });
     }

     static void OnScreensaverUserPreferenceChanged(object? sender, EventArgs e)
     {
          if (SHOW_EVENT_HANDLER_CALLS == true)
          {
               Debug.WriteLine("OnScreensaverUserPreferenceChanged");
          }
          _sequentialTaskFactory.StartNew(() => { });
     }

     static void OnVisualStyleUserPreferenceChanged(object? sender, EventArgs e)
     {
          if (SHOW_EVENT_HANDLER_CALLS == true)
          {
               Debug.WriteLine("OnVisualStyleUserPreferenceChanged");
          }
          _sequentialTaskFactory.StartNew(() => AtUseCounterEngine.CheckForHighContrastIsOnChange());
     }

     static void OnWindowUserPreferenceChanged(object? sender, EventArgs e)
     {
          if (SHOW_EVENT_HANDLER_CALLS == true)
          {
               Debug.WriteLine("OnWindowUserPreferenceChanged");
          }
          _sequentialTaskFactory.StartNew(() => AtUseCounterEngine.CheckForHighContrastIsOnChange());
     }

     static void ColorFilters_IsActiveChanged(object? sender, EventArgs e)
     {
          if (SHOW_EVENT_HANDLER_CALLS == true)
          {
               Debug.WriteLine("ColorFilters_IsActiveChanged");
          }
          _sequentialTaskFactory.StartNew(() => AtUseCounterEngine.CheckForColorFiltersAreActiveChange());
     }

     static void DarkMode_DarkModeChanged(object? sender, EventArgs e)
     {
          if (SHOW_EVENT_HANDLER_CALLS == true)
          {
               Debug.WriteLine("DarkMode_DarkModeChanged");
          }
          _sequentialTaskFactory.StartNew(() => AtUseCounterEngine.CheckForDarkModeChange());
     }

     static void NightLight_IsOnChanged(object? sender, EventArgs e)
     {
          if (SHOW_EVENT_HANDLER_CALLS == true)
          {
               Debug.WriteLine("NightLight_IsEnabledChanged");
          }
          _sequentialTaskFactory.StartNew(() => AtUseCounterEngine.CheckForNightLightIsEnabledChange());
     }

     static void CursorsSubKey_RegistryKeyChangedEvent(Morphic.WindowsNative.Registry.RegistryKey sender, EventArgs e)
     {
          if (SHOW_EVENT_HANDLER_CALLS == true)
          {
               Debug.WriteLine("CursorsSubKey_RegistryKeyChangedEvent");
          }
          _sequentialTaskFactory.StartNew(() => AtUseCounterEngine.CheckForMouseCursorSizeChange());
     }

     static void ProcessWatcher_ProcessStarted(object? sender, Morphic.WindowsNative.Process.ProcessWatcher.ProcessUpdatedEventArgs e)
     {
          if (SHOW_EVENT_HANDLER_CALLS == true)
          {
               Debug.WriteLine("ProcessWatcher_ProcessStarted");
          }

          if (e.ProcessName.Contains("Magnify", StringComparison.InvariantCultureIgnoreCase) == true)
          {
               Debug.WriteLine("CHANGED | Magnifier shown");

               // submit telemetry event
               _telemetryClient?.EnqueueEvent("magnifierShow", null);
          }
          else if (e.ProcessName.Contains("ScreenClippingHost", StringComparison.InvariantCultureIgnoreCase) == true)
          {
               Debug.WriteLine("CHANGED | Screen clipping activated");

               // submit telemetry event
               _telemetryClient?.EnqueueEvent("screenSnip", null);
          }
          else if (e.ProcessName.Contains("SystemSettings", StringComparison.InvariantCultureIgnoreCase) == true)
          {
               Debug.WriteLine("CHANGED | System Settings app started");

               // submit telemetry event
               _telemetryClient?.EnqueueEvent("systemSettings", null);
          }
          else
          {
               Debug.Assert(false, "invalid code path; we are not watching any other process names");
               Debug.WriteLine("CHANGED | Process started: " + e.ProcessName);
          }
     }

     static void ProcessWatcher_ProcessStopped(object? sender, Morphic.WindowsNative.Process.ProcessWatcher.ProcessUpdatedEventArgs e)
     {
          if (SHOW_EVENT_HANDLER_CALLS == true)
          {
               Debug.WriteLine("ProcessWatcher_ProcessStopped");
          }

          if (e.ProcessName.Contains("Magnify", StringComparison.InvariantCultureIgnoreCase) == true)
          {
               Debug.WriteLine("CHANGED | Magnifier hidden");

               // submit telemetry event
               _telemetryClient?.EnqueueEvent("magnifierHide", null);
          }
          else if (e.ProcessName.Contains("ScreenClippingHost", StringComparison.InvariantCultureIgnoreCase) == true)
          {
               Debug.WriteLine("CHANGED | Screen clipping done");
          }
          else if (e.ProcessName.Contains("SystemSettings", StringComparison.InvariantCultureIgnoreCase) == true)
          {
               Debug.WriteLine("CHANGED | System Settings app closed");
          }
          else
          {
               Debug.Assert(false, "invalid code path; we are not watching any other process names");
               Debug.WriteLine("CHANGED | Process stopped: " + e.ProcessName);
          }
     }

     //

     static void CheckForHighContrastIsOnChange()
     {
          // high contrast mode
          var getHighContrastModeResult = Morphic.WindowsNative.Theme.HighContrast.GetIsOn();
          if (getHighContrastModeResult.IsError == true)
          {
               Debug.Assert(false, "Could not get high contrast mode state");
               return;
          }
          var highContrastModeIsOn = getHighContrastModeResult.Value!;
          if (_highContrastIsOn != highContrastModeIsOn)
          {
               _highContrastIsOn = highContrastModeIsOn;
               Debug.WriteLine("CHANGED | High contrast mode is on: " + highContrastModeIsOn.ToString());

               // submit telemetry event
               _telemetryClient?.EnqueueEvent(highContrastModeIsOn ? "highContrastOn" : "highContrastOff", null);
          }
     }

     static void CheckForDisplayScalePercentageChange()
     {
          // capture display states before checking for new states
          DisplayState[] previousDisplayStates;
          lock (_displayStateLockObject)
          {
               previousDisplayStates = new DisplayState[_displayStates.Count];
               _displayStates.CopyTo(previousDisplayStates);
          }

          var (getChangedDisplayStatesResult, changedDisplayStates) = AtUseCounterEngine.InitOrGetChangedDisplayStates();
          if (getChangedDisplayStatesResult.IsError == true)
          {
               Debug.Assert(false, "Could not get complete list of changed display states");
          }
          if (changedDisplayStates.Count > 0)
          {
               Debug.WriteLine("CHANGED | Display scale percentage(s):");
               foreach (var displayState in changedDisplayStates)
               {
                    Debug.WriteLine("          0x" + displayState.Display.AdapterId.HighPart.ToString("X") + displayState.Display.AdapterId.LowPart.ToString("X") + ": " + (displayState.ScalePercentage * 100).ToString() + "%");

                    // submit telemetry event
                    foreach (var previousDisplayState in previousDisplayStates)
                    {
                         if ((previousDisplayState.Display.AdapterId.HighPart == displayState.Display.AdapterId.HighPart) && (previousDisplayState.Display.AdapterId.LowPart == displayState.Display.AdapterId.LowPart))
                         {
                              var previousScalePercentage = previousDisplayState.ScalePercentage;
                              var newScalePercentage = displayState.ScalePercentage;

                              if (previousScalePercentage < newScalePercentage)
                              {
                                   _telemetryClient?.EnqueueEvent("textSizeIncrease", null);
                              }
                              else if (previousScalePercentage > newScalePercentage)
                              {
                                   _telemetryClient?.EnqueueEvent("textSizeDecrease", null);
                              }
                         }
                    }
               }
          }
     }

     static MorphicResult<DarkThemeState, MorphicUnit> GetDarkThemeState()
     {
          var getAppsUseDarkModeResult = Morphic.WindowsNative.Theme.DarkMode.GetAppsUseDarkMode();
          if (getAppsUseDarkModeResult.IsError == true)
          {
               return MorphicResult.ErrorResult();
          }
          var appsUseDarkMode = getAppsUseDarkModeResult.Value;

          var getSystemUsesDarkModeResult = Morphic.WindowsNative.Theme.DarkMode.GetSystemUsesDarkMode();
          if (getSystemUsesDarkModeResult.IsError == true)
          {
               return MorphicResult.ErrorResult();
          }
          var systemUsesDarkMode = getSystemUsesDarkModeResult.Value;

          var darkModeState = new DarkThemeState() { AppsUseDarkTheme = appsUseDarkMode, SystemUsesDarkTheme = systemUsesDarkMode };
          return MorphicResult.OkResult(darkModeState);
     }

     static void CheckForDarkModeChange()
     {
          var getDarkModeStateResult = GetDarkThemeState();
          if (getDarkModeStateResult.IsError == true)
          {
               Debug.Assert(false, "Could not get dark mode state(s)");
               return;
          }
          var darkModeState = getDarkModeStateResult.Value;

          if (darkModeState.AppsUseDarkTheme is null && darkModeState.SystemUsesDarkTheme is null)
          {
               Debug.Assert(false, "Could not get system or app dark mode.");
               return;
          }

          DarkThemeState oldDarkThemeState;
          DarkThemeState? olderDarkThemeState;

          lock (_darkThemeStateHistory)
          {
               var darkThemeStateHistoryCount = _darkThemeStateHistory.Count;
               if (darkThemeStateHistoryCount == 0)
               {
                    throw new Exception("No dark theme state history; this should already be populated.");
               }

               oldDarkThemeState = _darkThemeStateHistory[0];
               if (darkThemeStateHistoryCount >= 2)
               {
                    olderDarkThemeState = _darkThemeStateHistory[1];
               }
               else
               {
                    olderDarkThemeState = null;
               }
               Debug.Assert(darkThemeStateHistoryCount < 3, "Invalid dark theme state history stack: too many elements");
          }

          bool atLeastOneDarkModeStateHasChanged = false;
          if (oldDarkThemeState.SystemUsesDarkTheme != darkModeState.SystemUsesDarkTheme || oldDarkThemeState.AppsUseDarkTheme != darkModeState.AppsUseDarkTheme)
          {
               atLeastOneDarkModeStateHasChanged = true;
          }
          //
          if (atLeastOneDarkModeStateHasChanged == true)
          {
               if (darkModeState.SystemUsesDarkTheme is null)
               {
                    Debug.WriteLine("CHANGED | Dark mode states: [apps: " + darkModeState.AppsUseDarkTheme.ToString() + "]");
               }
               else
               {
                    Debug.WriteLine("CHANGED | Dark mode states: [system: " + darkModeState.SystemUsesDarkTheme.ToString() + "; apps: " + darkModeState.AppsUseDarkTheme.ToString() + "]");
               }
          }

          // determine if this is a complete change from dark to light mode (or just the second half of an in-process change)
          bool darkModeStateHasChanged = false;
          if (atLeastOneDarkModeStateHasChanged == true)
          {
               if (olderDarkThemeState is null)
               {
                    // if at least one state value has changed and there were not 2 history records, then our state has indeed changed
                    darkModeStateHasChanged = true;
               }
               else if (darkModeState.SystemUsesDarkTheme is null /* || oldDarkThemeState.SystemUsesDarkTheme is null || olderDarkThemeState?.SystemUsesDarkTheme is null <-- unnecessary, unless we have a code bug */)
               {
                    // if only apps dark theme is being tracked, then any change _is_ a dark mode state change
                    darkModeStateHasChanged = true;
               }
               else
               {
                    // if both apps and system dark mode states are populated--and if we have two history records...
                    // - if the oldest record shows both states on or both states off and the newest record shows the opposite, then only count the "old" record as the state change
                    //   [and don't count the new record as a state change, as it is just the _final part_ of the state change
                    //
                    // assume that the dark mode state has changed, unless we want to suppress it
                    darkModeStateHasChanged = true;
                    //
                    if ((olderDarkThemeState.Value.SystemUsesDarkTheme == true && olderDarkThemeState.Value.AppsUseDarkTheme == true) ||
                        (olderDarkThemeState.Value.SystemUsesDarkTheme == false && olderDarkThemeState.Value.AppsUseDarkTheme == false))
                    {
                         if (darkModeState.SystemUsesDarkTheme == darkModeState.AppsUseDarkTheme)
                         {
                              // is the new state opposite of the old state?  [NOTE: we only need to check one value, since we already know apps and system values are the _same_ at this point]
                              if (darkModeState.AppsUseDarkTheme != olderDarkThemeState.Value.AppsUseDarkTheme)
                              {
                                   // it appears that our dark mode state change was in motion with the previous state change; consider the dark mode state as NOT having changed
                                   darkModeStateHasChanged = false;
                              }
                         }
                    }
               }
          }

          if (atLeastOneDarkModeStateHasChanged == true && darkModeStateHasChanged == false)
          {
               Debug.WriteLine("   NOTE | Dark mode state change is a completion of a previous change");
          }

          if (atLeastOneDarkModeStateHasChanged == true)
          {
               // update the dark theme state history stack
               lock (_darkThemeStateHistoryLockObject)
               {
                    if (_darkThemeStateHistory.Count > 1)
                    {
                         // remove the oldest state record
                         _darkThemeStateHistory.RemoveAt(_darkThemeStateHistory.Count - 1);
                    }
                    //
                    // add the new state record (in the front of the history)
                    _darkThemeStateHistory.Insert(0, darkModeState);
               }
          }

          // submit telemetry event
          // NOTE: we wait until here to send the telemetry event so that our "dark mode completion" logic can filter out scenarios where we were just completing the change from all-light to all-dark or vice-versa
          if (atLeastOneDarkModeStateHasChanged == true && darkModeStateHasChanged == true)
          {
               if ((oldDarkThemeState.SystemUsesDarkTheme == true && darkModeState.SystemUsesDarkTheme == false) ||
                   (oldDarkThemeState.AppsUseDarkTheme == true && darkModeState.AppsUseDarkTheme == false))
               {
                    _telemetryClient?.EnqueueEvent("darkModeOff", null);
               }
               else if ((oldDarkThemeState.SystemUsesDarkTheme == false && darkModeState.SystemUsesDarkTheme == true) ||
                        (oldDarkThemeState.AppsUseDarkTheme == false && darkModeState.AppsUseDarkTheme == true))
               {
                    _telemetryClient?.EnqueueEvent("darkModeOn", null);
               }
          }
     }

     static void CheckForMouseCursorSizeChange()
     {
          var getMouseCursorSizeResult = Morphic.WindowsNative.Mouse.Mouse.GetCursorSize();
          if (getMouseCursorSizeResult.IsError == true)
          {
               Debug.Assert(false, "Could not get mouse cursor size");
               return;
          }
          var mouseCursorSize = getMouseCursorSizeResult.Value;
          if (mouseCursorSize is null)
          {
               Debug.Assert(false, "Could not find mouse cursor size in registry");
               return;
          }

          if (_mouseCursorSize != mouseCursorSize)
          {
               bool mouseCursorSizeChangeIsPartOfExistingSequence;
               lock (_ignoreMouseCursorSizeChangesUntilTimestampLock)
               {
                    if (_ignoreMouseCursorSizeChangesUntilTimestamp is null)
                    {
                         // this is the first in a potential sequence of mouse cursor size changes; ignore any changes beyond this in regards to telemetry reporting (since the user may just be sliding the mouse cursor size bar)
                         mouseCursorSizeChangeIsPartOfExistingSequence = false;
                    }
                    else
                    {
                         // this is part of an existing sequence of mouse cursor changes -- or it is the first of a NEW sequence
                         if (_ignoreMouseCursorSizeChangesUntilTimestamp < _stopwatch.ElapsedMilliseconds)
                         {
                              // start of a NEW sequence
                              mouseCursorSizeChangeIsPartOfExistingSequence = false;
                         }
                         else
                         {
                              // part of an existing sequence (i.e. be sure to filter this out for telemetry purposes)
                              mouseCursorSizeChangeIsPartOfExistingSequence = true;
                         }
                    }

                    // if this change is not part of an existing sequence, set a timestamp to indicate when we should stop considering mouse cursor changes to no longer be part of the same sequence
                    if (mouseCursorSizeChangeIsPartOfExistingSequence == false)
                    {
                         const long NUMBER_OF_MILLISECONDS_TO_FILTER_OUT_SUBSEQUENT_CHANGES = 10_000; // 10 sec (NOTE: adjust if/as appropriate)
                         _ignoreMouseCursorSizeChangesUntilTimestamp = _stopwatch.ElapsedMilliseconds + NUMBER_OF_MILLISECONDS_TO_FILTER_OUT_SUBSEQUENT_CHANGES;
                    }
               }

               _mouseCursorSize = mouseCursorSize.Value;

               Debug.WriteLine("CHANGED | Mouse cursor size: " + mouseCursorSize.Value.ToString());

               if (mouseCursorSizeChangeIsPartOfExistingSequence == true)
               {
                    Debug.WriteLine("   NOTE | Mouse cursor size change is considered part of a moving change (e.g. 'mouse cursor size' slider)");
               }

               if (mouseCursorSizeChangeIsPartOfExistingSequence == false)
               {
                    // submit telemetry event
                    _telemetryClient?.EnqueueEvent("pointerSize", null);
               }
          }
     }

     static void CheckForColorFiltersAreActiveChange()
     {
          var getColorFiltersAreActiveResult = Morphic.WindowsNative.Accessibility.ColorFilters.GetIsActive();
          if (getColorFiltersAreActiveResult.IsError == true)
          {
               Debug.Assert(false, "Could not get the current color filters active state");
               return;
          }
          bool colorFiltersAreActive;
          if (getColorFiltersAreActiveResult.Value is not null)
          {
               colorFiltersAreActive = getColorFiltersAreActiveResult.Value!.Value;
          }
          else
          {
               Debug.Assert(false, "Could not get the current color filters active state (i.e. returned null)");
               return;
          }
          if (_colorFiltersAreActive != colorFiltersAreActive)
          {
               _colorFiltersAreActive = colorFiltersAreActive;
               Debug.WriteLine("CHANGED | Color filters are active: " + colorFiltersAreActive.ToString());

               // submit telemetry event
               _telemetryClient?.EnqueueEvent(colorFiltersAreActive ? "colorFiltersOn" : "colorFiltersOff", null);
          }
     }

     static async void CheckForNightLightIsEnabledChange()
     {
          var getNightLightIsEnabledResult = await Morphic.WindowsNative.Display.NightLight.GetIsOnAsync();
          if (getNightLightIsEnabledResult.IsError == true)
          {
               Debug.Assert(false, "Could not get the current night light enabled state");
               return;
          }
          var nightLightIsEnabled = getNightLightIsEnabledResult.Value!.Value;
          if (_nightLightIsOn != nightLightIsEnabled)
          {
               _nightLightIsOn = nightLightIsEnabled;
               Debug.WriteLine("CHANGED | Night mode is enabled: " + nightLightIsEnabled.ToString());

               // submit telemetry event
               _telemetryClient?.EnqueueEvent(nightLightIsEnabled ? "nightModeOn" : "nightModeOff", null);
          }
     }

     //

     // NOTE: this function returns success/failure; in the case of failure, it will still try to return a list of display states which it detected had changed values
     static (MorphicResult<MorphicUnit, MorphicUnit> /* success/error */, List<DisplayState> /* partial/complete result */) InitOrGetChangedDisplayStates()
     {
          MorphicResult<MorphicUnit, MorphicUnit> successErrorResult = MorphicResult.OkResult();

          var getAllDisplaysResult = Morphic.WindowsNative.Display.Display.GetAllDisplays();
          if (getAllDisplaysResult.IsError == true)
          {
               Debug.Assert(false, "Could not get list of displays");
               successErrorResult = MorphicResult.ErrorResult();
               return (successErrorResult, new());
          }
          var allDisplays = getAllDisplaysResult.Value!;
          //
          List<DisplayState> updatedDisplayStates = new();
          foreach (var display in allDisplays)
          {
               var getScalePercentageResult = display.GetScalePercentage();
               if (getScalePercentageResult.IsError == true)
               {
                    Debug.Assert(false, "Could not get scale percentage of a display");
                    successErrorResult = MorphicResult.ErrorResult();
                    continue;
               }
               var scalePercentage = getScalePercentageResult.Value!;

               var displayState = new DisplayState()
               {
                    Display = display,
                    ScalePercentage = scalePercentage
               };

               // determine if this display state already exists in our list of display states
               var displayIsKnown = false;
               lock (_displayStateLockObject)
               {
                    for (var index = 0; index < _displayStates.Count; index += 1)
                    {
                         var knownDisplayState = _displayStates[index];

                         if (knownDisplayState.Display.AdapterId.Equals(displayState.Display.AdapterId) &&
                             knownDisplayState.Display.SourceId == displayState.Display.SourceId)
                         {
                              // known display state; see if it has changed
                              if (knownDisplayState.ScalePercentage != displayState.ScalePercentage)
                              {
                                   // scale percentage has changed
                                   //
                                   // update our stored display state
                                   _displayStates[index] = displayState;
                                   //
                                   // add the updated display state to our result list
                                   updatedDisplayStates.Add(displayState);
                              }

                              displayIsKnown = true;
                              break;
                         }
                    }
               }

               if (displayIsKnown == false)
               {
                    // if the display was not known, add it to our list of known displays
                    lock (_displayStateLockObject)
                    {
                         _displayStates.Add(displayState);
                    }

                    // if the display was not known, add it to our list of "updated" results
                    updatedDisplayStates.Add(displayState);
               }
          }

          // return success/error...plus our partial/complete list of updated display states; note that the list may be a partial list if our success/error result is "error"
          return (successErrorResult, updatedDisplayStates);
     }
}
