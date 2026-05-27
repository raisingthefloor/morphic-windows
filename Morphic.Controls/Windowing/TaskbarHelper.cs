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
        int hresult = unchecked((int)0x80004005);  // E_FAIL until proven otherwise
        string failureReason = "(not attempted)";
        try
        {
            ITaskbarList taskbarList = EnsureTaskbarList();
            hresult = taskbarList.DeleteTab(hwnd);
            failureReason = hresult == 0 ? "(none)" : $"HRESULT 0x{hresult:X8}";
        }
        catch (System.Exception ex)
        {
            failureReason = $"{ex.GetType().Name}: {ex.Message}";
        }
        Morphic.Controls.Windowing.TaskbarDiag.Log(
            $"TaskbarHelper.RemoveFromTaskbar: hwnd=0x{(long)hwnd:X} hresult=0x{hresult:X8} reason={failureReason}");
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

    // CoClass for ITaskbarList. CLSID 56FDF344-FD6D-11D0-958A-006097C9A090 is the
    // Shell's TaskbarList class (registered as "Task Bar Communication" served by
    // explorerframe.dll); the ComImport empty-class pattern lets `new` construct
    // it via CoCreateInstance.
    [ComImport]
    [Guid("56FDF344-FD6D-11D0-958A-006097C9A090")]
    [ClassInterface(ClassInterfaceType.None)]
    private class TaskbarListCoClass
    {
    }

    // ITaskbarList interface (Shell, shobjidl_core.h, MIDL_INTERFACE).
    [ComImport]
    [Guid("56FDF342-FD6D-11D0-958A-006097C9A090")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList
    {
        // PreserveSig=true (the attribute below) returns the raw HRESULT instead of
        // throwing on non-success; we want to log the HRESULT, not swallow it via
        // the marshaler's automatic exception. Without PreserveSig the marshaler
        // throws COMException on any non-S_OK result, which prevents us from seeing
        // S_FALSE or HRESULTs the Shell may return for "did nothing" outcomes.
        [PreserveSig] int HrInit();
        [PreserveSig] int AddTab(IntPtr hwnd);
        [PreserveSig] int DeleteTab(IntPtr hwnd);
        [PreserveSig] int ActivateTab(IntPtr hwnd);
        [PreserveSig] int SetActiveAlt(IntPtr hwnd);
    }
}
