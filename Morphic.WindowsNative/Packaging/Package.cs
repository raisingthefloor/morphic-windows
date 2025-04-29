// Copyright 2022-2024 Raising the Floor - US, Inc.
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

namespace Morphic.WindowsNative.Packaging;

public class Package
{
    public static MorphicResult<bool, MorphicUnit> IsRunningAsPackagedApp()
    {
        // STEP 1: capture the length of the package full name
        uint packageFullNameLength = 0;
        //
        // capture the size of the package's full name
        // see: https://learn.microsoft.com/en-us/windows/win32/api/appmodel/nf-appmodel-getcurrentpackagefullname
        var getCurrentPackageFullNameResult = Windows.Win32.PInvoke.GetCurrentPackageFullName(ref packageFullNameLength, null);
        switch (getCurrentPackageFullNameResult)
        {
            case Windows.Win32.Foundation.WIN32_ERROR.APPMODEL_ERROR_NO_PACKAGE:
                // the process has no package identity
                return MorphicResult.OkResult(false);
            case Windows.Win32.Foundation.WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER:
                // this is the expected result (since we passed in a length of zero); the packageFullNameLength parameter should contain the correct length
                break;
            default:
                Debug.Assert(false, "Unknown error code: " + getCurrentPackageFullNameResult.ToString());
                return MorphicResult.ErrorResult();
        }

        // STEP 2: capture the package full name (using an appropriately-sized buffer)
        //
        // capture the package's full name
        Span<char> packageFullNameAsChars = new char[(int)packageFullNameLength];
        string packageFullName;
        //
        // resize the package to the length indicated in our initial call (via the return-by-reference variable 'packageFullNameLength')
        // see: https://learn.microsoft.com/en-us/windows/win32/api/appmodel/nf-appmodel-getcurrentpackagefullname
        getCurrentPackageFullNameResult = Windows.Win32.PInvoke.GetCurrentPackageFullName(ref packageFullNameLength, packageFullNameAsChars);
        switch (getCurrentPackageFullNameResult)
        {
            case Windows.Win32.Foundation.WIN32_ERROR.ERROR_SUCCESS:
                // extract the package name (which should presumably be the full string minus the null terminator)
                packageFullName = new string(packageFullNameAsChars.ToArray(), 0, (int)packageFullNameLength - 1 /* -1 for null terminator */);
                break;
            case Windows.Win32.Foundation.WIN32_ERROR.APPMODEL_ERROR_NO_PACKAGE:
                // the process has no package identity
                return MorphicResult.OkResult(false);
            case Windows.Win32.Foundation.WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER:
                // the provided buffer was not large enough
                Debug.Assert(false, "Buffer provided to GetCurrentPackageFullName API was not large enough; this probably represents a code bug.");
                return MorphicResult.ErrorResult(); // gracefully degrade
            default:
                Debug.Assert(false, "Unknown error code: " + getCurrentPackageFullNameResult.ToString());
                return MorphicResult.ErrorResult();
        }

        if (packageFullName != String.Empty)
        {
            return MorphicResult.OkResult(true);
        }
        else
        {
            // NOTE: just in case the package name returned is an empty string, we are returning false in that scenario; this would presumably never happen in production
            return MorphicResult.OkResult(false);
        }
    }
}
