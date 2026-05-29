// Copyright 2026 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-windowsnative-lib-cs/blob/main/LICENSE
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
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Morphic.WindowsNative.SystemSettings;

// Listens for Windows broadcast notifications (WM_SETTINGCHANGE) and demuxes them into typed
// per-setting events that consumers can subscribe to without re-implementing the (wParam,
// lParam) classification themselves.
//
// Sits on top of Morphic.WindowsNative.Windowing.HiddenMessageWindow (a process-owned top-level
// invisible window). That layer exists because Microsoft.Win32.SystemEvents.UserPreferenceChanged
// silently fails to receive broadcasts in WinAppSDK processes; owning our own HWND on the UI
// thread sidesteps the issue.
//
// Add new events as we need them. Each event corresponds to a specific (wParam, lParam) signal
// Windows broadcasts on the toggle path -- so a subscriber for HighContrastChanged gets called
// only when high-contrast actually changes, not on every General/Locale/Mouse/... fire.
//
public sealed class SystemSettingsListener
{
    public static SystemSettingsListener Shared { get; } = new();

    private bool _isListening = false;
    private readonly object _listeningLock = new();

    private SystemSettingsListener()
    {
    }

    //
    // Events
    //
	
    // Each event fires when Windows broadcasts WM_SETTINGCHANGE with the matching (wParam,
    // lParam) signature. EventArgs is intentionally bare (EventArgs.Empty) -- consumers re-read
    // their setting's source-of-truth value (registry / SPI / WinRT) themselves, both because
    // some settings carry no value in the broadcast and to keep this class focused purely on
    // the "something changed" signal.

    private event EventHandler? _highContrastChanged;
    public event EventHandler? HighContrastChanged
    {
        add
        {
            lock (_listeningLock)
            {
                if (_isListening == false)
                {
                    var startResult = this.StartListening();
                    if (startResult.IsError == true)
                    {
                        var message = startResult.Error! switch
                        {
                            IStartListeningError.WrongThread(var observed) =>
                                $"SystemSettingsListener.HighContrastChanged += called from {observed} thread; must be the UI/STA thread with a Win32 message pump. HiddenMessageWindow would be created on the wrong thread and the broadcast trampoline would never fire. Subscribe from the UI thread or marshal to it. Events will not fire until listening starts successfully.",
                            IStartListeningError.InitializationFailed =>
                                "SystemSettingsListener.HighContrastChanged += could not initialize HiddenMessageWindow. Events will not fire until listening starts successfully.",
                            _ =>
                                "SystemSettingsListener.HighContrastChanged += could not start listening.",
                        };
                        Debug.Assert(false, message);
                        System.Diagnostics.Trace.WriteLine(message);
                    }
                }
                _highContrastChanged += value;
            }
        }
        remove
        {
            lock (_listeningLock)
            {
                _highContrastChanged -= value;
            }
        }
    }

    //
    // Listening lifecycle
    //

    public interface IStartListeningError
    {
        public record WrongThread(System.Threading.ApartmentState Observed) : IStartListeningError;
        public record InitializationFailed : IStartListeningError;
    }
    // NOTE: this function is exposed externally to enable callers to start listening (with success/failure) before wiring up events
    public MorphicResult<MorphicUnit, IStartListeningError> StartListening()
    {
        lock (_listeningLock)
        {
            if (_isListening == true)
            {
                return MorphicResult.OkResult();
            }

            var apartment = System.Threading.Thread.CurrentThread.GetApartmentState();
            if (apartment != System.Threading.ApartmentState.STA)
            {
                return MorphicResult.ErrorResult<IStartListeningError>(new IStartListeningError.WrongThread(apartment));
            }

            var initializeResult = Morphic.WindowsNative.Windowing.HiddenMessageWindow.Initialize();
            if (initializeResult.IsError)
            {
                Debug.Assert(false, "HiddenMessageWindow.Initialize() failed; system-settings notifications will not fire");
                return MorphicResult.ErrorResult<IStartListeningError>(new IStartListeningError.InitializationFailed());
            }
            Morphic.WindowsNative.Windowing.HiddenMessageWindow.MessageReceived += this.HiddenMessageWindowMessageReceivedTrampoline;
            _isListening = true;
            return MorphicResult.OkResult();
        }
    }

    // Stops dispatching events. The underlying HiddenMessageWindow stays alive (process-lifetime);
    // we just stop routing its broadcasts through us. Idempotent.
    public void StopListening()
    {
        lock (_listeningLock)
        {
            if (_isListening == true)
            {
                Morphic.WindowsNative.Windowing.HiddenMessageWindow.MessageReceived -= this.HiddenMessageWindowMessageReceivedTrampoline;
                _isListening = false;
            }
        }
    }

    //
    // Trampoline: maps incoming WM_SETTINGCHANGE broadcasts to specific events
    //

    private void HiddenMessageWindowMessageReceivedTrampoline(object? sender, Morphic.WindowsNative.Windowing.WindowMessageEventArgs e)
    {
        if (e.Msg != Windows.Win32.PInvoke.WM_SETTINGCHANGE)
        {
            return;
        }

        var wParam = (uint)(nuint)e.WParam;

        // SPI-based notifications: wParam carries the SPI_* action code, lParam is usually empty
        // for these (the caller used SystemParametersInfo with SPIF_SENDWININICHANGE). Each known
        // SPI code routes to its specific event.
        switch ((Windows.Win32.UI.WindowsAndMessaging.SYSTEM_PARAMETERS_INFO_ACTION)wParam)
        {
            case Windows.Win32.UI.WindowsAndMessaging.SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETHIGHCONTRAST:
                SystemSettingsListener.DispatchEventToSubscribers(_highContrastChanged, this);
                return;
        }

    }

    // Defensive only -- the singleton normally lives for the process lifetime. If a caller
    // explicitly disposes us, we unsubscribe from the underlying HiddenMessageWindow event so
    // our handler isn't kept alive by it.
    private static void DispatchEventToSubscribers(EventHandler? source, object sender)
    {
        var invocationList = source?.GetInvocationList();
        if (invocationList is null || invocationList.Length == 0)
        {
            return;
        }

        _ = Task.Run(() =>
        {
            foreach (EventHandler subscriber in invocationList)
            {
                try
                {
                    subscriber.Invoke(sender, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SystemSettingsListener] subscriber threw: {ex.Message}");
                }
            }
        });
    }

}
