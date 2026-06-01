// Copyright 2026 Raising the Floor - US, Inc.
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

namespace Morphic.WindowsNative.Process;

// Queries whether the current process is running with uiAccess=true (manifested
// requestedExecutionLevel uiAccess="true"). Wraps the Win32 GetTokenInformation API on
// the current process's primary token, querying the TokenUIAccess information class.
//
// Why this matters for Morphic: uiAccess processes run at Medium+IL (UIPI-bypass capable)
// rather than plain Medium IL. Several Windows subsystems treat Medium+IL as effectively
// "elevated" for CreateProcess purposes; specifically Explorer.exe cannot CreateProcess
// a Medium+IL executable (returns error 740). That breaks COM-out-of-process activation
// for AppNotificationManager toast clicks, etc.
public static class UiAccess
{
    // Returns true if the current process token has TokenUIAccess set (i.e. this process
    // was launched from a manifest declaring uiAccess="true" AND Windows honored that
    // declaration; the latter requires the EXE to be signed, in a secure location, etc.
    public static MorphicResult<bool, MorphicUnit> IsCurrentProcessUiAccess()
    {
        // Use System.Diagnostics.Process.GetCurrentProcess().SafeHandle to obtain a
        // managed SafeProcessHandle for the current process. CsWin32's OpenProcessToken
        // signature requires a SafeHandle for the process parameter (not a raw HANDLE),
        // so the System.Diagnostics path gets us there cleanly. The Process object owns
        // its SafeHandle; dispose the Process via `using` so the handle lifetime is
        // bounded by this method's scope.
        using var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
        var processSafeHandle = currentProcess.SafeHandle;

        Microsoft.Win32.SafeHandles.SafeFileHandle? tokenHandle = null;
        try
        {
            var openResult = Windows.Win32.PInvoke.OpenProcessToken(
                processSafeHandle,
                Windows.Win32.Security.TOKEN_ACCESS_MASK.TOKEN_QUERY,
                out tokenHandle);
            if (openResult == false)
            {
                return MorphicResult.ErrorResult();
            }

            // CsWin32 generates GetTokenInformation with a raw HANDLE parameter for the
            // token (no SafeHandle overload as of CsWin32 0.3.275). DangerousGetHandle()
            // borrows the underlying value; the SafeFileHandle retains ownership and the
            // finally block disposes it.
            var rawTokenHandle = new Windows.Win32.Foundation.HANDLE(tokenHandle.DangerousGetHandle());

            uint uiAccessFlag = 0;
            uint returnLength = 0;
            unsafe
            {
                // ReturnLength is LPDWORD (pointer to DWORD) in the native signature.
                // CsWin32 surfaces it as uint*, not out uint, so we pass an address rather
                // than `out`. We do not actually consume returnLength (the value class
                // we're querying is fixed-size DWORD), but the parameter is required.
                var queryResult = Windows.Win32.PInvoke.GetTokenInformation(
                    rawTokenHandle,
                    Windows.Win32.Security.TOKEN_INFORMATION_CLASS.TokenUIAccess,
                    &uiAccessFlag,
                    sizeof(uint),
                    &returnLength);
                if (queryResult == false)
                {
                    return MorphicResult.ErrorResult();
                }
            }
            return MorphicResult.OkResult(uiAccessFlag != 0);
        }
        finally
        {
            tokenHandle?.Dispose();
        }
    }
}
