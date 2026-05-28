// Copyright 2026 Raising the Floor - US, Inc.
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

using System;
using Microsoft.Win32;
using WixToolset.Dtf.WindowsInstaller;

namespace MorphicSetup.Actions;

public static class OsCustomActions
{
    [CustomAction]
    public static ActionResult ReadWindowsUbr(Session session)
    {
        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key is null)
            {
                session.Log("ReadWindowsUbr: could not open HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion; setting WINDOWSUBR=#0");
                session["WINDOWSUBR"] = "#0";
                return ActionResult.Success;
            }

            object? ubrValue = key.GetValue("UBR");
            if (ubrValue is null)
            {
                session.Log("ReadWindowsUbr: UBR value not present in registry; setting WINDOWSUBR=#0");
                session["WINDOWSUBR"] = "#0";
                return ActionResult.Success;
            }

            // UBR is REG_DWORD on all supported Windows versions; .NET surfaces it as a
            // boxed Int32. Convert.ToInt32 also handles the (unexpected but harmless) case
            // where the value type ever shifts to something string-convertible in the future.
            int ubr = Convert.ToInt32(ubrValue, System.Globalization.CultureInfo.InvariantCulture);
            string formattedUbr = "#" + ubr.ToString(System.Globalization.CultureInfo.InvariantCulture);
            session["WINDOWSUBR"] = formattedUbr;
            session.Log("ReadWindowsUbr: set WINDOWSUBR=" + formattedUbr);
            return ActionResult.Success;
        }
        catch (Exception ex)
        {
            // Don't propagate exceptions out of a CA; doing so triggers MSI rollback. Log
            // and fail closed via the same "#0" path the explicit error branches use.
            session.Log("ReadWindowsUbr: exception while reading UBR; setting WINDOWSUBR=#0. Details: " + ex.GetType().FullName + ": " + ex.Message);
            session["WINDOWSUBR"] = "#0";
            return ActionResult.Success;
        }
    }
}
