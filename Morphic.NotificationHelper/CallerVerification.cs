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

namespace Morphic.NotificationHelper;

// Verifies that the process that launched THIS helper instance is signed with the SAME
// Authenticode publisher cert (by thumbprint) as the helper itself. Used to gate helper
// modes that expose sensitive capability (currently --show; will extend to --listen-push
// for push notifications) so that arbitrary other programs on the machine cannot silently 
// spawn the helper to emit Morphic-branded toasts or harvest Morphic's push payloads.
//
// What is NOT gated:
//   * COM activation (Windows Shell launches helper for toast click). Parent is a Shell
//     process, not Morphic. Nothing user-controllable flows through that path.
//   * --unregister (MSI uninstall custom action). Parent is msiexec.exe (Microsoft-signed).
//     If someone wants to maliciously call --unregister they just remove our HKCU activator
//     registration; low impact.
//
// Why thumbprint matching (vs. subject-string matching or generic "is signed"):
//   * Subject-string matching ("does the cert contain 'Raising the Floor'") is trivially
//     bypassed by a self-signed cert with that subject (any user can create one in
//     PowerShell). NOT used.
//   * Generic "is signed" provides no provenance information at all. NOT used.
//   * Thumbprint matching means the caller must have been signed with the EXACT private
//     key that signed this helper. Attackers can't bypass without obtaining our actual
//     signing key.
//   * Robust to cert renewals as long as releases are atomic (helper + Morphic.exe are
//     signed together with the same cert each release).
//
// What thumbprint matching does NOT catch:
//   * In-place tampering of a legitimately-signed caller EXE (signature blob's cert is
//     unchanged, so thumbprint still matches; the actual signature would be invalid due
//     to hash mismatch, but we are not verifying signature validity).
//
// Why we are NOT adding WinVerifyTrust here even though it would catch the above case:
// the check would be circular. WinVerifyTrust would let us assert that the CALLER EXE
// is byte-intact relative to its signature. But the only thing running that check is
// THIS helper, whose own bytes are equally susceptible to in-place tampering by the
// same admin attacker. A modified helper could simply skip the IsCallerSignedBySameAuthenticodeCertificate
// call entirely, or hard-code a `return true`. So caller-integrity verification from a
// possibly-modified helper is security theater unless the helper also self-verifies,
// and self-verification is a chicken-and-egg problem with no clean solution from
// user-mode code (the modified helper would skip the self-verification too).
//
// The way to defend against in-place admin tampering is at a different layer entirely
// (Windows Defender Application Control / AppLocker enterprise policy enforcing that
// only valid-signed binaries run; the OS itself runs the verification before
// CreateProcess, so a modified binary simply won't launch). That's out of scope for
// what this helper can do from user mode. If WinVerifyTrust value emerges later, the
// place to add it is around the GetFileSignerThumbprint call below.
internal static class CallerVerification
{
    // Cache the helper's own cert thumbprint so we don't re-extract it on every call. The
    // helper exits quickly after one operation in v1, but a future persistent-listener
    // mode would call IsCallerSignedBySameAuthenticodeCertificate many times; the cache supports both.
    // Initialized lazily on first use.
    private static string? s_helperThumbprintCache;
    private static readonly object s_helperThumbprintLock = new();

    // Returns true when the parent process's executable is signed with the same publisher
    // cert (by thumbprint) as this helper. Returns false on any failure path (parent gone,
    // either file unsigned, mismatch, etc.) -- caller treats false as "deny the operation."
    //
    // In Debug builds this always returns true so dev F5 iteration works without a signed
    // Morphic.exe (or a signed helper.exe). Production Release builds enforce the real
    // check. To exercise the real check during dev testing, sign both EXEs with the
    // Authenticate digital signing certificate and build as Release.
    public static bool IsCallerSignedBySameAuthenticodeCertificate()
    {
#if DEBUG
        System.Diagnostics.Debug.WriteLine(
            "[CallerVerification] Debug build: bypassing caller-signature check (would have run real check in Release).");
        return true;
#else
        var expectedThumbprint = CallerVerification.GetHelperThumbprint();
        if (expectedThumbprint is null)
        {
            System.Diagnostics.Debug.WriteLine(
                "[CallerVerification] Caller rejected: could not read helper's own cert thumbprint (helper unsigned, or cert read failed).");
            return false;
        }

        var callerExePath = CallerVerification.GetParentProcessExecutablePath();
        if (callerExePath is null)
        {
            System.Diagnostics.Debug.WriteLine(
                "[CallerVerification] Caller rejected: could not determine parent process exe path.");
            return false;
        }

        var callerThumbprint = CallerVerification.GetFileSignerThumbprint(callerExePath);
        if (callerThumbprint is null)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[CallerVerification] Caller rejected: parent '{callerExePath}' is unsigned or cert could not be extracted.");
            return false;
        }

        var match = string.Equals(callerThumbprint, expectedThumbprint, System.StringComparison.OrdinalIgnoreCase);
        if (match == false)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[CallerVerification] Caller rejected: parent cert thumbprint does not match helper's. Parent: '{callerExePath}'.");
        }
        return match;
#endif
    }

    // Extracts THIS helper's own Authenticode signer cert thumbprint. Cached after first
    // successful read. Returns null if the helper is unsigned or the cert can't be read.
    private static string? GetHelperThumbprint()
    {
        if (CallerVerification.s_helperThumbprintCache is not null)
        {
            return CallerVerification.s_helperThumbprintCache;
        }
        lock (CallerVerification.s_helperThumbprintLock)
        {
            if (CallerVerification.s_helperThumbprintCache is not null)
            {
                return CallerVerification.s_helperThumbprintCache;
            }
            try
            {
                using var helperProcess = System.Diagnostics.Process.GetCurrentProcess();
                var helperExePath = helperProcess.MainModule?.FileName;
                if (helperExePath is null)
                {
                    return null;
                }
                var thumbprint = CallerVerification.GetFileSignerThumbprint(helperExePath);
                CallerVerification.s_helperThumbprintCache = thumbprint;
                return thumbprint;
            }
            catch (System.Exception)
            {
                return null;
            }
        }
    }

    // Returns the absolute path to the parent process's executable, or null if the
    // parent has exited or is otherwise inaccessible.
    //
    // Uses OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION) + QueryFullProcessImageName
    // rather than System.Diagnostics.Process.GetProcessById(pid).MainModule.FileName.
    // The .NET MainModule path requests PROCESS_VM_READ to enumerate modules, which UIPI
    // denies when the target process is at a HIGHER integrity tier than the calling
    // process (specifically: Morphic.exe is uiAccess=true / Medium+IL in production; this
    // helper is asInvoker / plain Medium IL by design). PROCESS_QUERY_LIMITED_INFORMATION
    // was added in Vista specifically to enable cross-IL inspection of basic process info
    // (exe path, name, start time, etc.) without VM_READ.
    private static string? GetParentProcessExecutablePath()
    {
        var parentPid = CallerVerification.GetParentProcessId();
        if (parentPid is null || parentPid.Value <= 0)
        {
            return null;
        }

        using var processHandle = Windows.Win32.PInvoke.OpenProcess_SafeHandle(
            Windows.Win32.System.Threading.PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION,
            bInheritHandle: false,
            (uint)parentPid.Value);
        if (processHandle.IsInvalid)
        {
            return null;
        }

        System.Span<char> buffer = stackalloc char[1024];
        uint size = (uint)buffer.Length;
        // dwFlags = 0 selects the Win32-format path (PROCESS_NAME_WIN32); the alternative
        // value 1 selects the NT-format path (\Device\HarddiskVolumeN\...). We want the
        // Win32 form to compare with X509Certificate2.CreateFromSignedFile, which expects
        // a Win32 path.
        if (Windows.Win32.PInvoke.QueryFullProcessImageName(processHandle, /* PROCESS_NAME_WIN32 */ 0, buffer, ref size) == false)
        {
            return null;
        }
        return buffer.Slice(0, (int)size).ToString();
    }

    private static int? GetParentProcessId()
    {
        // NtQueryInformationProcess + PROCESS_BASIC_INFORMATION are NT-subsystem APIs
        // in ntdll.dll. CsWin32 does not generate them (they're not in the win32metadata
        // package's coverage), so we keep the raw DllImport + struct definition for this
        // one query. All other Win32 calls in this file go through CsWin32 wrappers.
        var info = default(ProcessBasicInformation);
        try
        {
            using var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            var status = CallerVerification.NtQueryInformationProcess(
                currentProcess.Handle,
                /* ProcessBasicInformation = 0 */ 0,
                ref info,
                System.Runtime.InteropServices.Marshal.SizeOf<ProcessBasicInformation>(),
                out _);
            if (status != 0)
            {
                return null;
            }
            // InheritedFromUniqueProcessId is the PID of the process that called
            // CreateProcess to spawn us. It is NOT updated if our actual parent exits
            // and we get re-parented to System (PID 4) or another orphan-adopter.
            // GetParentProcessExecutablePath handles the orphaned-parent case via
            // IsInvalid on the OpenProcess SafeHandle result.
            return info.InheritedFromUniqueProcessId.ToInt32();
        }
        catch (System.Exception)
        {
            return null;
        }
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct ProcessBasicInformation
    {
        public System.IntPtr Reserved1;
        public System.IntPtr PebBaseAddress;
        public System.IntPtr Reserved2_1;
        public System.IntPtr Reserved2_2;
        public System.IntPtr UniqueProcessId;
        public System.IntPtr InheritedFromUniqueProcessId;
    }

    [System.Runtime.InteropServices.DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(
        System.IntPtr ProcessHandle,
        int ProcessInformationClass,
        ref ProcessBasicInformation ProcessInformation,
        int ProcessInformationLength,
        out int ReturnLength);

    // Reads the Authenticode signer cert from a signed PE file and returns its thumbprint
    // (uppercase hex). Returns null if the file is unsigned or the cert can't be parsed.
    //
    // X509Certificate2.CreateFromSignedFile is the documented API for extracting the
    // Authenticode signer cert from a signed PE. The byte-loading constructors it uses
    // internally are marked obsolete in .NET 10 (SYSLIB0057); the recommended replacement
    // (X509CertificateLoader) has no Authenticode-extraction overload, so migrating would
    // require WinVerifyTrust + CryptQueryObject + SignedCms (~100 lines of P/Invoke).
    // Suppressing the warning pending an upstream replacement for this specific use case,
	// as the deprecated code is already tested code.
    //
    // We extract only the signer cert; we DO NOT verify signature validity (file
    // integrity / cert chain trust / revocation). Thumbprint matching alone is sufficient
    // for the threat model documented in IsCallerSignedBySameAuthenticodeCertificate.
    private static string? GetFileSignerThumbprint(string filePath)
    {
        try
        {
#pragma warning disable SYSLIB0057
            // CreateFromSignedFile is inherited from the base X509Certificate class
            // and returns X509Certificate (not X509Certificate2), so we can't read
            // .Thumbprint directly. Wrap in X509Certificate2 to upgrade.
            var rawCert = System.Security.Cryptography.X509Certificates.X509Certificate.CreateFromSignedFile(filePath);
            using var cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(rawCert);
#pragma warning restore SYSLIB0057
            return cert.Thumbprint;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            // Unsigned file or signature parse failure.
            return null;
        }
        catch (System.Exception)
        {
            // Any other failure (file not found, access denied, etc.) -- treat as
            // unsigned rather than throwing.
            return null;
        }
    }
}
