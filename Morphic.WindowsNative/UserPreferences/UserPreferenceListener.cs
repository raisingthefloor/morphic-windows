// Copyright 2022 Raising the Floor - US, Inc.
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

namespace Morphic.WindowsNative.UserPreferences;

using Morphic.Core;
using System;
using System.Collections.Generic;
using System.Text;

public class UserPreferenceListener : IDisposable
{
    public static UserPreferenceListener Shared { get; private set; }

    private bool _isListening = false;

    private bool disposedValue;

    static UserPreferenceListener()
    {
        UserPreferenceListener.Shared = new UserPreferenceListener();
    }

    private UserPreferenceListener()
    {
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // NOTE: dispose managed state (managed objects)
            }

            // free unmanaged resources (unmanaged objects)
            //
            // NOTE: per Microsoft, we must unsubscribe from UserPreferencesChanged upon application exit (or earlier, if the events are no longer needed)
            // see: https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.systemevents.userpreferencechanged?view=dotnet-plat-ext-6.0
            try
            {
                Microsoft.Win32.SystemEvents.UserPreferenceChanged -= this.UserPreferenceChangedTrampoline;
            }
            catch { }

            // NOTE: set large fields to null

            disposedValue = true;
        }
    }

    ~UserPreferenceListener()
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

    //

    private void UserPreferenceChangedTrampoline(object? sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        switch (e.Category)
        {
            case Microsoft.Win32.UserPreferenceCategory.Accessibility:
                _accessibilityUserPreferenceChanged?.Invoke(sender, e);
                break;
            case Microsoft.Win32.UserPreferenceCategory.Color:
                _colorUserPreferenceChanged?.Invoke(sender, e);
                break;
            case Microsoft.Win32.UserPreferenceCategory.Desktop:
                _desktopUserPreferenceChanged?.Invoke(sender, e);
                break;
            case Microsoft.Win32.UserPreferenceCategory.General:
                _generalUserPreferenceChanged?.Invoke(sender, e);
                break;
            case Microsoft.Win32.UserPreferenceCategory.Icon:
                _iconUserPreferenceChanged?.Invoke(sender, e);
                break;
            case Microsoft.Win32.UserPreferenceCategory.Keyboard:
                _keyboardUserPreferenceChanged?.Invoke(sender, e);
                break;
            case Microsoft.Win32.UserPreferenceCategory.Locale:
                _localeUserPreferenceChanged?.Invoke(sender, e);
                break;
            case Microsoft.Win32.UserPreferenceCategory.Menu:
                _menuUserPreferenceChanged?.Invoke(sender, e);
                break;
            case Microsoft.Win32.UserPreferenceCategory.Mouse:
                _mouseUserPreferenceChanged?.Invoke(sender, e);
                break;
            case Microsoft.Win32.UserPreferenceCategory.Policy:
                _policyUserPreferenceChanged?.Invoke(sender, e);
                break;
            case Microsoft.Win32.UserPreferenceCategory.Power:
                _powerUserPreferenceChanged?.Invoke(sender, e);
                break;
            case Microsoft.Win32.UserPreferenceCategory.Screensaver:
                _screensaverUserPreferenceChanged?.Invoke(sender, e);
                break;
            case Microsoft.Win32.UserPreferenceCategory.VisualStyle:
                _visualStyleUserPreferenceChanged?.Invoke(sender, e);
                break;
            case Microsoft.Win32.UserPreferenceCategory.Window:
                _windowUserPreferenceChanged?.Invoke(sender, e);
                break;
            default:
                System.Diagnostics.Debug.Assert(false, "Microsoft.Win32.UserPreferenceChangedEventHandler event was raised, providing an unknown category argument.");
                break;
        }
    }

    //

    private event EventHandler? _accessibilityUserPreferenceChanged;
    public event EventHandler? AccessibilityUserPreferenceChanged
    {
        add
        {
            EnsureListeningIsEnabled_ThrowExceptionOnError();
            _accessibilityUserPreferenceChanged += value;
        }
        remove
        {
            _accessibilityUserPreferenceChanged -= value;
        }
    }

    private event EventHandler? _colorUserPreferenceChanged;
    public event EventHandler? ColorUserPreferenceChanged
    {
        add
        {
            EnsureListeningIsEnabled_ThrowExceptionOnError();
            _colorUserPreferenceChanged += value;
        }
        remove
        {
            _colorUserPreferenceChanged -= value;
        }
    }

    private event EventHandler? _desktopUserPreferenceChanged;
    public event EventHandler? DesktopUserPreferenceChanged
    {
        add
        {
            EnsureListeningIsEnabled_ThrowExceptionOnError();
            _desktopUserPreferenceChanged += value;
        }
        remove
        {
            _desktopUserPreferenceChanged -= value;
        }
    }

    private event EventHandler? _generalUserPreferenceChanged;
    public event EventHandler? GeneralUserPreferenceChanged
    {
        add
        {
            EnsureListeningIsEnabled_ThrowExceptionOnError();
            _generalUserPreferenceChanged += value;
        }
        remove
        {
            _generalUserPreferenceChanged -= value;
        }
    }

    private event EventHandler? _iconUserPreferenceChanged;
    public event EventHandler? IconUserPreferenceChanged
    {
        add
        {
            EnsureListeningIsEnabled_ThrowExceptionOnError();
            _iconUserPreferenceChanged += value;
        }
        remove
        {
            _iconUserPreferenceChanged -= value;
        }
    }

    private event EventHandler? _keyboardUserPreferenceChanged;
    public event EventHandler? KeyboardUserPreferenceChanged
    {
        add
        {
            EnsureListeningIsEnabled_ThrowExceptionOnError();
            _keyboardUserPreferenceChanged += value;
        }
        remove
        {
            _keyboardUserPreferenceChanged -= value;
        }
    }

    private event EventHandler? _localeUserPreferenceChanged;
    public event EventHandler? LocaleUserPreferenceChanged
    {
        add
        {
            EnsureListeningIsEnabled_ThrowExceptionOnError();
            _localeUserPreferenceChanged += value;
        }
        remove
        {
            _localeUserPreferenceChanged -= value;
        }
    }

    private event EventHandler? _menuUserPreferenceChanged;
    public event EventHandler? MenuUserPreferenceChanged
    {
        add
        {
            EnsureListeningIsEnabled_ThrowExceptionOnError();
            _menuUserPreferenceChanged += value;
        }
        remove
        {
            _menuUserPreferenceChanged -= value;
        }
    }

    private event EventHandler? _mouseUserPreferenceChanged;
    public event EventHandler? MouseUserPreferenceChanged
    {
        add
        {
            EnsureListeningIsEnabled_ThrowExceptionOnError();
            _mouseUserPreferenceChanged += value;
        }
        remove
        {
            _mouseUserPreferenceChanged -= value;
        }
    }

    private event EventHandler? _policyUserPreferenceChanged;
    public event EventHandler? PolicyUserPreferenceChanged
    {
        add
        {
            EnsureListeningIsEnabled_ThrowExceptionOnError();
            _policyUserPreferenceChanged += value;
        }
        remove
        {
            _policyUserPreferenceChanged -= value;
        }
    }

    private event EventHandler? _powerUserPreferenceChanged;
    public event EventHandler? PowerUserPreferenceChanged
    {
        add
        {
            EnsureListeningIsEnabled_ThrowExceptionOnError();
            _powerUserPreferenceChanged += value;
        }
        remove
        {
            _powerUserPreferenceChanged -= value;
        }
    }

    private event EventHandler? _screensaverUserPreferenceChanged;
    public event EventHandler? ScreensaverUserPreferenceChanged
    {
        add
        {
            EnsureListeningIsEnabled_ThrowExceptionOnError();
            _screensaverUserPreferenceChanged += value;
        }
        remove
        {
            _screensaverUserPreferenceChanged -= value;
        }
    }

    private event EventHandler? _visualStyleUserPreferenceChanged;
    public event EventHandler? VisualStyleUserPreferenceChanged
    {
        add
        {
            EnsureListeningIsEnabled_ThrowExceptionOnError();
            _visualStyleUserPreferenceChanged += value;
        }
        remove
        {
            _visualStyleUserPreferenceChanged -= value;
        }
    }

    private event EventHandler? _windowUserPreferenceChanged;
    public event EventHandler? WindowUserPreferenceChanged
    {
        add
        {
            EnsureListeningIsEnabled_ThrowExceptionOnError();
            _windowUserPreferenceChanged += value;
        }
        remove
        {
            _windowUserPreferenceChanged -= value;
        }
    }


    //

    private void EnsureListeningIsEnabled_ThrowExceptionOnError()
    {
        if (_isListening == false)
        {
            var startListeningResult = this.StartListening();
            if (startListeningResult.IsError == true)
            {
                throw startListeningResult.Error!;
            }
        }
    }

    // NOTE: callers who want to avoid getting exceptions when wiring up the user preference listener (i.e. if we're running in a process which doesn't allow such listening)
    //       can call this function to learn if they were or were not able to start up the user preference listener; it is not, however, a required call.
    // see: https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.systemevents.userpreferencechanged?view=dotnet-plat-ext-6.0
    public MorphicResult<MorphicUnit, Exception> StartListening()
    {
        if (_isListening == false)
        {
            try
            {
                // see: https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.systemevents.userpreferencechanged?view=dotnet-plat-ext-6.0
                Microsoft.Win32.SystemEvents.UserPreferenceChanged += this.UserPreferenceChangedTrampoline;
            }
            catch (Exception ex)
            {
                return MorphicResult.ErrorResult(ex);
            }
            _isListening = true;
        }

        return MorphicResult.OkResult();
    }

    // NOTE: callers who want to shut down the user preference listener early (out of an abundance of caution, during application exit or otherwise) can call this method
    // see: https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.systemevents.userpreferencechanged?view=dotnet-plat-ext-6.0
    public void StopListening()
    {
        if (_isListening == true)
        {
            Microsoft.Win32.SystemEvents.UserPreferenceChanged -= this.UserPreferenceChangedTrampoline;
            _isListening = false;
        }
    }
}
