﻿// Copyright 2020-2025 Raising the Floor - US, Inc.
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
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UIAutomationClient;
using ComUIAC = UIAutomationClient;

// NOTE: for documentation on CUIAutomation8 (to COM UIAutomation interface introduced in Windows 8), see: https://learn.microsoft.com/en-us/previous-versions/windows/desktop/legacy/hh448746(v=vs.85)

namespace Morphic.WindowsNative.UIAutomation;

public class UIAutomationClient : IDisposable
{
    private bool disposedValue;

    private ComUIAC.CUIAutomation8 _comUIAutomationObject;

    public UIAutomationClient()
    {
        var comUIAutomationObject = new ComUIAC.CUIAutomation8();
        _comUIAutomationObject = comUIAutomationObject;
    }

    //

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
            }

            // free unmanaged resources (unmanaged objects) and override finalizer
            Marshal.ReleaseComObject(_comUIAutomationObject);

            // TODO: set large fields to null

            // mark our class as disposed
            disposedValue = true;
        }
    }

    ~UIAutomationClient()
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


}
