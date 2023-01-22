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

namespace Morphic.WindowsNative.Display;

using Morphic.Core;
using System;
using System.Collections.Generic;
using System.Text;

public class DisplaySettingsListener : IDisposable
{
    public static DisplaySettingsListener Shared { get; private set; }

    private bool _isListening = false;

    private bool disposedValue;

    static DisplaySettingsListener()
    {
        DisplaySettingsListener.Shared = new DisplaySettingsListener();
    }

    private DisplaySettingsListener()
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
            // NOTE: per Microsoft, we must unsubscribe from DisplaySettingsChanged upon application exit (or earlier, if the events are no longer needed)
            // see: https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.systemevents.displaysettingschanged?view=dotnet-plat-ext-6.0
            try
            {
                Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= this.DisplaySettingsChangedTrampoline;
            }
            catch { }

            // NOTE: set large fields to null

            disposedValue = true;
        }
    }

    ~DisplaySettingsListener()
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

    private void DisplaySettingsChangedTrampoline(object? sender, EventArgs e)
    {
        _displaySettingsChanged?.Invoke(sender, e);
    }

    private event EventHandler? _displaySettingsChanged;
    public event EventHandler? DisplaySettingsChanged
    {
        add
        {
            if (_isListening == false)
            {
                var startListeningResult = this.StartListening();
                if (startListeningResult.IsError == true)
                {
                    throw startListeningResult.Error!;
                }
            }
            //
            _displaySettingsChanged += value;
        }
        remove
        {
            _displaySettingsChanged -= value;
        }
    }

    // NOTE: callers who want to avoid getting exceptions when wiring up the display settings listener (i.e. if we're running in a process which doesn't allow such listening)
    //       can call this function to learn if they were or were not able to start up the display settings listener; it is not, however, a required call.
    // see: https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.systemevents.displaysettingschanged?view=dotnet-plat-ext-6.0
    public MorphicResult<MorphicUnit, Exception> StartListening()
    {
        if (_isListening == false)
        {
            try
            {
                // see: https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.systemevents.displaysettingschanged?view=dotnet-plat-ext-6.0
                Microsoft.Win32.SystemEvents.DisplaySettingsChanged += this.DisplaySettingsChangedTrampoline;
            }
            catch (Exception ex)
            {
                return MorphicResult.ErrorResult(ex);
            }
            _isListening = true;
        }

        return MorphicResult.OkResult();
    }

    // NOTE: callers who want to shut down the display settings listener early (out of an abundance of caution, during application exit or otherwise) can call this method
    // see: https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.systemevents.displaysettingschanged?view=dotnet-plat-ext-6.0
    public void StopListening()
    {
        if (_isListening == true)
        {
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= this.DisplaySettingsChangedTrampoline;
            _isListening = false;
        }
    }
}
