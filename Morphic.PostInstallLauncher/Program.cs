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

// PostInstallLauncher.exe is a tiny unprivileged sibling of Morphic.exe whose sole purpose is
// to launch Morphic.exe with the run-after-install flag at the very end of an MSI install.
//
// Why it exists: Morphic.exe ships with uiAccess=true in production. A uiAccess (Medium+ IL)
// executable CANNOT be launched by CreateProcess from the msiexec context (it fails, typically
// with ERROR_ELEVATION_REQUIRED / 740), so a type-18 EXE custom action can't start it. The path
// that DOES start a uiAccess app is ShellExecute. The MSI's built-in WixShellExec custom action
// launches via ShellExecute, but it cannot pass command-line arguments, and we need to pass the
// run-after-install flag (so the post-install launch respects the user's persisted MorphicBar
// visibility instead of force-showing). This launcher resolves that: WixShellExec launches THIS
// exe (no arguments needed), and this exe ShellExecutes Morphic.exe with the fixed
// run-after-install argument baked in. In effect it is "WixShellExec with one hardcoded argument."
//
// It runs in the installing user's session (WixShellExec impersonates that user), finds
// Morphic.exe next to itself in the install directory, launches it, and exits immediately.
// Everything is best-effort: any failure is swallowed so it can never wedge an install.

namespace Morphic.PostInstallLauncher;

internal static class Program
{
    // The fixed argument this launcher always passes to Morphic.exe. Morphic reads it at startup
    // (via Environment.GetCommandLineArgs) and treats its presence as "an automatic start, so
    // respect the persisted bar visibility" rather than "the user launched me manually, force-show
    // the bar." Must match the flag string Morphic looks for (App.RUN_AFTER_INSTALL_COMMAND_LINE_FLAG).
    private const string RUN_AFTER_INSTALL_COMMAND_LINE_FLAG = "--run-after-install";

    // Morphic.exe always ships in the same directory as this launcher (Program Files\Morphic\),
    // so we resolve it relative to our own base directory rather than assuming an absolute path.
    private const string MORPHIC_EXECUTABLE_FILE_NAME = "Morphic.exe";

    private static int Main()
    {
        try
        {
            string launcherDirectory = System.AppContext.BaseDirectory;
            string morphicExecutablePath = System.IO.Path.Combine(launcherDirectory, Program.MORPHIC_EXECUTABLE_FILE_NAME);

            // UseShellExecute=true is mandatory here: ShellExecute is the launch path that grants
            // a uiAccess application its uiAccess token. A plain CreateProcess (UseShellExecute=false)
            // would fail to start uiAccess Morphic.exe the same way the msiexec context does.
            System.Diagnostics.ProcessStartInfo startInfo = new()
            {
                FileName = morphicExecutablePath,
                Arguments = Program.RUN_AFTER_INSTALL_COMMAND_LINE_FLAG,
                UseShellExecute = true,
                WorkingDirectory = launcherDirectory,
            };

            using (System.Diagnostics.Process.Start(startInfo))
            {
                // Fire and forget: we do not wait for Morphic to exit (it runs for the whole
                // session). The using block just disposes the returned Process handle.
            }

            return 0;
        }
        catch (System.Exception ex)
        {
            // Best-effort: log for an attached debugger and exit non-zero, but never throw. The
            // install has already fully committed by the time this runs; a launch failure here
            // must not surface as an install failure (the WixShellExec CA is Return="ignore" too).
            System.Diagnostics.Debug.WriteLine($"[PostInstallLauncher] Failed to launch Morphic: {ex}");
            return 1;
        }
    }
}
