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
using System.IO;

namespace Morphic.Controls.Windowing;

// Temporary diagnostic for the "MorphicBar appears in the taskbar in production
// (signed + uiaccess=true + Program Files install location)" investigation.
// Writes timestamped lines to %TEMP%\morphic-taskbar-diag.log so we can capture
// the SetAsParentHwnd outcome, the DummyWindow HWND validity, and whether the
// bar's owner relationship survives the first Show in production.
//
// Should be removed once the root cause is confirmed and the fix proven.
public static class TaskbarDiag
{
    private static readonly string _logPath = Path.Combine(
        Path.GetTempPath(), "morphic-taskbar-diag.log");

    private static readonly object _writeLock = new object();

    public static void Log(string message)
    {
        try
        {
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [pid={Environment.ProcessId}] {message}{Environment.NewLine}";
            lock (_writeLock)
            {
                File.AppendAllText(_logPath, line);
            }
        }
        catch
        {
            // diagnostic logging must never throw and must never block production code
        }
    }
}
