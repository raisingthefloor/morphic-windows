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
public class SystemSettingsListener : IDisposable
{
    public static SystemSettingsListener Shared { get; } = new();

    private bool _isListening = false;
    private bool disposedValue;

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
            this.EnsureListeningIsEnabled_ThrowExceptionOnError();
            _highContrastChanged += value;
        }
        remove
        {
            _highContrastChanged -= value;
        }
    }

    //
    // Listening lifecycle
    //

    // Initializes the underlying broadcast-catching window (HiddenMessageWindow) and subscribes
    // our trampoline to it. Idempotent. Must be called from a thread with a running Win32
    // message pump (the UI thread) -- HiddenMessageWindow creates its window on the calling
    // thread and that thread owns the message dispatch. The first event-property subscription
    // (typically from the bar factory, on the UI thread) calls this automatically.
    //
    // NOTE: this function is exposed externally to enable callers to start listening (with success/failure) before wiring up events
    public MorphicResult<MorphicUnit, MorphicUnit> StartListening()
    {
        if (_isListening == false)
        {
            var initializeResult = Morphic.WindowsNative.Windowing.HiddenMessageWindow.Initialize();
            if (initializeResult.IsError)
            {
                Debug.Assert(false, "HiddenMessageWindow.Initialize() failed; system-settings notifications will not fire");
                return MorphicResult.ErrorResult();
            }
            Morphic.WindowsNative.Windowing.HiddenMessageWindow.MessageReceived += this.HiddenMessageWindowMessageReceivedTrampoline;
            _isListening = true;
        }
        return MorphicResult.OkResult();
    }

    // Stops dispatching events. The underlying HiddenMessageWindow stays alive (process-lifetime);
    // we just stop routing its broadcasts through us. Idempotent.
    public void StopListening()
    {
        if (_isListening == true)
        {
            Morphic.WindowsNative.Windowing.HiddenMessageWindow.MessageReceived -= this.HiddenMessageWindowMessageReceivedTrampoline;
            _isListening = false;
        }
    }

    private void EnsureListeningIsEnabled_ThrowExceptionOnError()
    {
        if (_isListening == false)
        {
            var startListeningResult = this.StartListening();
            if (startListeningResult.IsError == true)
            {
                throw new InvalidOperationException("SystemSettingsListener could not start listening (HiddenMessageWindow initialization failed)");
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
                _highContrastChanged?.Invoke(this, EventArgs.Empty);
                return;
        }
    }

    //
    // IDisposable
    //

    // Defensive only -- the singleton normally lives for the process lifetime. If a caller
    // explicitly disposes us, we unsubscribe from the underlying HiddenMessageWindow event so
    // our handler isn't kept alive by it.

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // NOTE: dispose managed state (managed objects)
            }

            try
            {
                Morphic.WindowsNative.Windowing.HiddenMessageWindow.MessageReceived -= this.HiddenMessageWindowMessageReceivedTrampoline;
            }
            catch
            {
            }

            disposedValue = true;
        }
    }

    ~SystemSettingsListener()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
