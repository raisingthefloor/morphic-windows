// Copyright 2020-2024 Raising the Floor - US, Inc.
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
using Morphic.WindowsNative.Theme;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Morphic.WindowsNative.SystemEvents;

internal class UserPreferenceEvents
{
    public class UserPreferenceChangedEventArgs(Microsoft.Win32.UserPreferenceCategory category) : EventArgs
    {
        public Microsoft.Win32.UserPreferenceCategory Category = category;
    }
    public delegate void UserPreferenceChangedEventHandler(object? sender, UserPreferenceChangedEventArgs e);
    //
    private static UserPreferenceChangedEventHandler? s_userPreferenceChanged = null;
    private static object s_userPreferenceChangedEventLock = new();
    private static bool s_UserPreferenceWatchEventIsActive = false;
    private static object s_UserPreferenceWatchEventLock = new();

    //

    // NOTE: it is the target event's responsibility to run any UI-related code on the main UI thread; this event should be considered to be fired from a background thread
    public static event UserPreferenceChangedEventHandler UserPreferenceChanged
    {
        add
        {
            var connectWatchEventResult = UserPreferenceEvents.ConnectUserPreferenceChangedWatchEventIfUninitialized();
            Debug.Assert(connectWatchEventResult.IsSuccess, "Could not wire up user preference changed notifications");

            lock (s_userPreferenceChangedEventLock)
            {
                s_userPreferenceChanged += value;
            }
        }
        remove
        {
            lock (s_userPreferenceChangedEventLock)
            {
                s_userPreferenceChanged -= value;

                if (s_userPreferenceChanged is null || s_userPreferenceChanged!.GetInvocationList().Length == 0)
                {
                    s_userPreferenceChanged = null;

                    UserPreferenceEvents.DestroyUserPreferenceChangedWatchEventIfUnused();
                }
            }
        }
    }

    //

    private static MorphicResult<MorphicUnit, MorphicUnit> ConnectUserPreferenceChangedWatchEventIfUninitialized()
    {
        lock (s_UserPreferenceWatchEventLock)
        {
            if (s_UserPreferenceWatchEventIsActive == false)
            {
                // see: https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.systemevents.userpreferencechanged?view=dotnet-plat-ext-6.0
                //      NOTE: this strategy will only work if the message pump is running; we may want to consider creating a hidden window to ensure that we capture messages
                // NOTE: if we use the UserPreferenceChanged event handler, we must also detach our event handler when the application is disposed (see: https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.systemevents.displaysettingschanged?view=dotnet-plat-ext-3.1#microsoft-win32-systemevents-displaysettingschanged)
                try
                {
                    Microsoft.Win32.SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
                }
                catch
                {
                    return MorphicResult.ErrorResult();
                }

                s_UserPreferenceWatchEventIsActive = true;
            }
        }

        return MorphicResult.OkResult();
    }

    private static void DestroyUserPreferenceChangedWatchEventIfUnused()
    {
        lock (s_UserPreferenceWatchEventLock)
        {
            if (s_userPreferenceChanged is null || s_userPreferenceChanged!.GetInvocationList().Length == 0)
            {
                Microsoft.Win32.SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;

                s_UserPreferenceWatchEventIsActive = false;
            }
        }
    }

    private static void SystemEvents_UserPreferenceChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        var userPreferenceChangedEventArgs = new UserPreferenceChangedEventArgs(e.Category);

        // NOTE: to ensure that each event handler runs (even if one throws an exception), we send an event to each window separately in parallel
        var invocationList = s_userPreferenceChanged?.GetInvocationList();
        if (invocationList is not null)
        {
            foreach (UserPreferenceChangedEventHandler element in invocationList!)
            {
                Task.Run(() =>
                {
                    // NOTE: it is the target event's responsibility to run any UI-related code on the main UI thread; this event should be considered to be fired from a background thread
                    element.Invoke(null /* static class, no so type instance */, userPreferenceChangedEventArgs);
                });
            }
        }
        //Task.Run(() =>
        //{
        //    s_userPreferenceChanged?.Invoke(null /* static class, no so type instance */, userPreferenceChangedEventArgs);
        //});
    }
}
