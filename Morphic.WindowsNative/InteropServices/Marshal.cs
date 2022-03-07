// Copyright 2021 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt
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
using System.Runtime.InteropServices;
using System.Text;

namespace Morphic.WindowsNative.InteropServices
{
    // NOTE: this class exists to support "classic" .NET Framework functions which are no longer present in .NET Core or .NET 5+
    public class Marshal
    {
        // NOTE: this function throws a COMException if the object was not found
        public static object? GetActiveObject(string progId)
        {
            if (progId is null)
            {
                throw new ArgumentNullException(nameof(progId));
            }

            // get the CLSID for the supplied ProgId
            var getClsidResult = ExtendedPInvoke.CLSIDFromProgIDEx(progId, out var clsid);
            if (getClsidResult != (uint)ExtendedPInvoke.Win32ErrorCode.S_OK)
            {
                switch (getClsidResult)
                {
                    case (uint)ExtendedPInvoke.Win32ErrorCode.CO_E_CLASSSTRING:
                        throw new COMException("Invalid class string");
                    case (uint)ExtendedPInvoke.Win32ErrorCode.REGDB_E_WRITEREGDB:
                        throw new COMException("Could not write key to registry");
                }
            }

            var getActiveObjectResult = ExtendedPInvoke.GetActiveObject(ref clsid, IntPtr.Zero, out var activeObject);
            if (getActiveObjectResult != (uint)ExtendedPInvoke.Win32ErrorCode.S_OK)
            {
                // if we could not get the object, return null
                // NOTE: if we want to distinguish between error results (i.e. object not active vs. others), we could read the win32 "last error" code
                return null;
            }

            return activeObject;
        }

    }
}
