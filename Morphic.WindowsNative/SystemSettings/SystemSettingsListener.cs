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
    // Serializes StartListening/StopListening, the EnsureListening probe inside event accessors,
    // and event accessor add/remove (the custom accessors below don't get the compiler's
    // free Interlocked-based thread safety that field-like events get).
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
    /// <summary>
    /// Fires when Windows broadcasts WM_SETTINGCHANGE with SPI_SETHIGHCONTRAST. Subscribe to
    /// react to the user toggling High Contrast in Settings, Ease of Access, or via the
    /// keyboard shortcut.
    /// </summary>
    /// <remarks>
    /// Subscriber callbacks run on a single ThreadPool task per broadcast -- NOT on the UI
    /// thread that owns the HiddenMessageWindow. The Trampoline returns to the message pump
    /// immediately and fans out via Task.Run with per-handler try/catch isolation, so a slow
    /// or throwing subscriber can't block the UI thread, suppress other subscribers, or
    /// propagate an exception back through the WndProc and crash the pump. Subscribers that
    /// need to update UI should dispatch back to the UI thread themselves.
    ///
    /// Subscription is soft-fail. The accessor calls <see cref="StartListening"/> internally;
    /// if that returns an error (<see cref="IStartListeningError.WrongThread"/> when the call
    /// site is not on a UI/STA message-pump thread, or
    /// <see cref="IStartListeningError.InitializationFailed"/> when the underlying
    /// HiddenMessageWindow cannot be created), the handler is still wired but will not fire
    /// unless listening is later started successfully -- e.g. by a subsequent subscribe from
    /// the UI/STA thread, or by an explicit successful <see cref="StartListening"/> call. The
    /// handler list survives across failed-then-successful start attempts and the previously
    /// wired handler will fire from the moment listening starts.
    /// A <see cref="Debug.Assert(bool, string)"/> and a <see cref="Trace.WriteLine(string)"/>
    /// describe the failure so it surfaces in dev tools, DebugView, or any wired-up production
    /// trace listener. To detect subscription failure explicitly, call
    /// <see cref="StartListening"/> yourself before subscribing and inspect the
    /// <see cref="MorphicResult{TSuccess, TFailure}"/>.
    /// </remarks>
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
                        // Soft failure path: log loudly so devs catch the bug, but DON'T throw.
                        // The handler still gets wired up below; it just won't fire until
                        // listening is later started successfully (a subsequent subscribe from
                        // the UI/STA thread, or an explicit StartListening call from the right
                        // thread). Choosing graceful degradation in production (high-contrast
                        // events temporarily silent) over a hard crash for users; the
                        // Debug.Assert + Trace.WriteLine surface the problem for anyone watching
                        // dev tools, DebugView, or a wired-up production trace listener.
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

    // Errors returned by StartListening. Public so explicit callers can pattern-match on
    // the specific failure mode rather than treating all start failures the same. Both
    // failure modes leave _isListening == false; future subscribes will retry.
    public interface IStartListeningError
    {
        public record WrongThread(System.Threading.ApartmentState Observed) : IStartListeningError;
        public record InitializationFailed : IStartListeningError;
    }

    // Initializes the underlying broadcast-catching window (HiddenMessageWindow) and subscribes
    // our trampoline to it. Idempotent.
    //
    // Must be called from a thread with a running Win32 message pump (the UI thread, in
    // practice). HiddenMessageWindow creates its window on the calling thread, and that
    // thread owns the message dispatch -- if the calling thread doesn't pump, WM_SETTINGCHANGE
    // broadcasts queue up at the OS level and never fire the trampoline. We enforce STA here
    // as a proxy for "pump-capable thread" (worker thread pool threads are MTA and would
    // silently break event delivery). The hole is "STA worker thread without a pump"
    // (e.g. Morphic.WindowsNative.SystemSettings.SettingItemDispatcher's worker), which is
    // technically STA but pumps its own DispatcherQueue rather than arbitrary HWND messages;
    // nothing in current Morphic touches SystemSettingsListener from that thread, so the
    // STA check is sufficient in practice.
    //
    // NOTE: exposed externally so callers can start listening explicitly (with success/failure)
    // before wiring up events.
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

        // String-based notifications: wParam is 0 (or unrecognized), lParam is a PWSTR naming the
        // setting (e.g. "ImmersiveColorSet", "WindowsThemeElement", "Policy", "intl"). When we
        // add events for these settings, dispatch them here based on the lParam string.
        //
        // (Currently no consumers need them. Left as the natural extension point.)
        // string? lParamString = (e.LParam != IntPtr.Zero) ? Marshal.PtrToStringUni(e.LParam) : null;
        // switch (lParamString) { ... }
    }

    // Single ThreadPool task that fans out to every current subscriber of `source`, isolating
    // each via try/catch. Returns control to the trampoline (and therefore to the UI thread's
    // message pump) immediately so a slow subscriber can't block the pump or delay the next
    // WM_SETTINGCHANGE broadcast; per-handler catch keeps one throwing subscriber from
    // suppressing the others, propagating back through the pump to crash the UI thread, or
    // surfacing as an unobserved-task exception. Snapshots the invocation list before
    // entering the task so a concurrent unsubscribe doesn't observe a torn delegate chain.
    // No-ops if there are no subscribers.
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
