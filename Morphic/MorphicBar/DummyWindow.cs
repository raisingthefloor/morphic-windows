// Copyright 2020-2026 Raising the Floor - US, Inc.
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

using Microsoft.UI.Xaml;
using Morphic.Core;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Morphic.MorphicBar;

internal class DummyWindow : IDisposable
{
    private bool disposedValue;

    private Windows.Win32.Foundation.HWND _hwnd;

    public DummyWindow()
    {
        unsafe
        {
            this._hwnd = Windows.Win32.PInvoke.CreateWindowEx(
                  (Windows.Win32.UI.WindowsAndMessaging.WINDOW_EX_STYLE)0,
                  "Static",  // built-in window class, no need to register
                  "",
                  (Windows.Win32.UI.WindowsAndMessaging.WINDOW_STYLE)0,
                  0, 0, 0, 0,
                  Windows.Win32.Foundation.HWND.Null,
                  null,
                  null,
                  null
              );
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // dispose managed state (managed objects)
                // [none]
            }

            // free unmanaged resources (unmanaged objects) and override finalizer
            _ = Windows.Win32.PInvoke.DestroyWindow(_hwnd);
            _hwnd = Windows.Win32.Foundation.HWND.Null;

            // set large fields to null
            // [none]

            disposedValue = true;
        }
    }

    // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    ~DummyWindow()
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

    public Windows.Win32.Foundation.HWND hwnd => _hwnd;

    public MorphicResult<MorphicUnit, Morphic.WindowsNative.IWin32ApiError> SetAsParentHwnd(Windows.Win32.Foundation.HWND childHwnd)
    {
        // NOTE: SetWindowLongPtr can return 0 even if there is no error; see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowlongptrw
        System.Runtime.InteropServices.Marshal.SetLastPInvokeError(0);
        var setWindowLongPtrResult = Windows.Win32.PInvoke.SetWindowLongPtr(childHwnd, Windows.Win32.UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX.GWLP_HWNDPARENT, this._hwnd);
        if (setWindowLongPtrResult == 0)
        {
            var win32ErrorCode = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            if (win32ErrorCode != 0)
            {
                return MorphicResult.ErrorResult<Morphic.WindowsNative.IWin32ApiError>(new Morphic.WindowsNative.IWin32ApiError.Win32Error((uint)win32ErrorCode));
            }
        }

        return MorphicResult.OkResult();
    }
}
