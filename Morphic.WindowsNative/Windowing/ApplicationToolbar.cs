// Copyright 2025 Raising the Floor - US, Inc.
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

using System;
using System.Runtime.InteropServices;

namespace Morphic.WindowsNative.Windowing;

public class ApplicationToolbar : IDisposable
{
    private bool disposedValue;

    private bool isRegistered;
    private Windows.Win32.Foundation.HWND hWnd;

    private ApplicationToolbar(Windows.Win32.Foundation.HWND hWnd)
    {
        this.hWnd = hWnd;
    }

    // NOTE: pass in the window's handle and a callback message # (to be used to capture and distinguish appbar messages sent to the window's message loop)
    public static ApplicationToolbar RegisterNew(IntPtr hWnd, uint callbackMsg)
    {
        var hWndAsHwnd = new Windows.Win32.Foundation.HWND(hWnd);

        Windows.Win32.UI.Shell.APPBARDATA appbardata = new()
        {
            hWnd = hWndAsHwnd,
            uCallbackMessage = callbackMsg,
            cbSize = (uint)Marshal.SizeOf(typeof(Windows.Win32.UI.Shell.APPBARDATA))
        };
        //
        Windows.Win32.PInvoke.SHAppBarMessage(Windows.Win32.PInvoke.ABM_REMOVE, ref appbardata);

        // create an instance of this object (now registered) and return it to the caller
        ApplicationToolbar applicationToolbar = new ApplicationToolbar(hWndAsHwnd);
        applicationToolbar.isRegistered = true;

        return applicationToolbar;
    }

    // NOTE: this function should be called by the client once before the window is closed
    public void Unregister()
    {
        if (this.isRegistered == false)
        {
            return;
        }

        Windows.Win32.UI.Shell.APPBARDATA appbardata = new()
        {
            hWnd = this.hWnd,
            cbSize = (uint)Marshal.SizeOf(typeof(Windows.Win32.UI.Shell.APPBARDATA))
        };
        //
        Windows.Win32.PInvoke.SHAppBarMessage(Windows.Win32.PInvoke.ABM_REMOVE, ref appbardata);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // dispose managed state (managed objects)
                // [nothing to do]
            }

            // free unmanaged resources (unmanaged objects) and override finalizer
            // NOTE: unregistering the window after its parent window has already been closed might not be helpful
            this.Unregister();

            // set large fields to null
            // [nothing to do]

            disposedValue = true;
        }
    }

    // NOTE: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    ~ApplicationToolbar()
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

    private static uint ConvertScreenEdgeToApiValue(ScreenEdge edge)
    {
        var result = edge switch
        {
            ScreenEdge.Bottom => Windows.Win32.PInvoke.ABE_BOTTOM,
            ScreenEdge.Left => Windows.Win32.PInvoke.ABE_LEFT,
            ScreenEdge.Right => Windows.Win32.PInvoke.ABE_RIGHT,
            ScreenEdge.Top => Windows.Win32.PInvoke.ABE_TOP,
            _ => throw new ArgumentOutOfRangeException(nameof(edge)),

        };
        return result;
    }
}
