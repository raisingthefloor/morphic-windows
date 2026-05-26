// Copyright 2026 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-controls-lib-cs/blob/main/LICENSE.txt
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

using System;
using System.Runtime.InteropServices;

namespace Morphic.Controls.Windowing;

// Wraps the Shell's ITaskbarList COM interface to let a window remove its own taskbar
// entry without affecting Alt+Tab inclusion. The owner-window technique (a top-level
// window with an explicit owner is hidden from the taskbar by default) is the cleaner
// way to achieve the same effect, but Morphic's MorphicBar's owner relationship gets
// silently dropped in some production environments (signed + uiaccess=true + "Program
// Files" install location). DeleteTab is the documented Shell API for the "hide from 
// taskbar but keep in Alt+Tab" shape, and it works regardless of integrity level, 
// signing state, or install location.
public static class TaskbarHelper
{
    private static readonly object _initLock = new object();
    private static ITaskbarList? _taskbarList;

    // Removes hwnd's entry from the Windows taskbar (if present). Idempotent. Safe to
    // call repeatedly: the Shell can re-add the entry on subsequent Show calls if the
    // owner-window relationship hasn't taken effect, so the typical pattern is to call
    // from each Window.Activated handler, not just the first.
    public static void RemoveFromTaskbar(IntPtr hwnd)
    {
        try
        {
            ITaskbarList taskbarList = EnsureTaskbarList();
            taskbarList.DeleteTab(hwnd);
        }
        catch (COMException)
        {
            // Shell COM call can fail during process startup / shutdown or in edge
            // environments; the taskbar bug we're fixing is a cosmetic one, not worth
            // surfacing a process-level error if the Shell isn't responding.
        }
    }

    private static ITaskbarList EnsureTaskbarList()
    {
        if (_taskbarList is not null)
        {
            return _taskbarList;
        }
        lock (_initLock)
        {
            if (_taskbarList is null)
            {
                ITaskbarList instance = (ITaskbarList)new TaskbarListCoClass();
                instance.HrInit();
                _taskbarList = instance;
            }
            return _taskbarList;
        }
    }

    // CoClass for ITaskbarList. CLSID 56FDF342-FD6D-11D0-958A-006097C9A090 is the
    // Shell's TaskbarList class; the ComImport empty-class pattern lets `new`
    // construct it via CoCreateInstance.
    [ComImport]
    [Guid("56FDF342-FD6D-11D0-958A-006097C9A090")]
    [ClassInterface(ClassInterfaceType.None)]
    private class TaskbarListCoClass
    {
    }

    // ITaskbarList interface (Shell, shobjidl.h). We only need DeleteTab and HrInit
    // (HrInit is the required first call before any other method). The four other
    // methods are declared for vtable order; we leave them unused. IID
    // 56FDF344-FD6D-11D0-958A-006097C9A090 (differs from the CoClass CLSID 56FDF342...
    // by the second-to-last hex digit, which is easy to confuse).
    [ComImport]
    [Guid("56FDF344-FD6D-11D0-958A-006097C9A090")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList
    {
        void HrInit();
        void AddTab(IntPtr hwnd);
        void DeleteTab(IntPtr hwnd);
        void ActivateTab(IntPtr hwnd);
        void SetActiveAlt(IntPtr hwnd);
    }
}
