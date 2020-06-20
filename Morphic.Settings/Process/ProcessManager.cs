// Copyright 2020 Raising the Floor - International
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
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Reflection.Metadata.Ecma335;

namespace Morphic.Settings.Process
{
    public class ProcessManager : IProcessManager
    {

        public Task<bool> IsInstalled(string exe)
        {
            return Task.FromResult(GetAppPathRegistryKey(exe) != null);
        }

        public Task<bool> IsRunning(string exe)
        {
            foreach (var process in GetRunningProcesses(exe))
            {
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public async Task<bool> Start(string exe)
        {
            if (GetExecutablePath(exe) is string exePath)
            {
                var info = new ProcessStartInfo(exePath);
                info.UseShellExecute = true;
                using (var process = System.Diagnostics.Process.Start(info))
                {
                    return await Task.Run(() =>
                    {
                        try
                        {
                            return process.WaitForInputIdle(3000);
                        }
                        catch (Exception e)
                        {
                            return false;
                        }
                    });
                }
            }
            return false;
        }

        public async Task<bool> Stop(string exe)
        {
            var processes = GetRunningProcesses(exe);
            foreach (var process in processes)
            {
                var success = await process.MorphicStop();
                if (!success)
                {
                    return false;
                }
            }
            return true;
        }

        private RegistryKey? GetAppPathRegistryKey(string exe)
        {
            var local = Microsoft.Win32.Registry.LocalMachine;
            if (local.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths") is RegistryKey parent)
            {
                return parent.OpenSubKey(exe);
            }
            return null;
        }

        private string? GetExecutablePath(string exe)
        {
            if (GetAppPathRegistryKey(exe) is RegistryKey key)
            {
                var value = key.GetValue("");
                if (value is string path)
                {
                    if (path.StartsWith("\"") && path.EndsWith("\""))
                    {
                        path = path.Substring(1, path.Length - 2);
                    }
                    return path;
                }
            }
            return null;
        }

        private IEnumerable<System.Diagnostics.Process> GetRunningProcesses(string exe)
        {
            if (GetExecutablePath(exe) is string exePath)
            {
                if (Path.GetFileNameWithoutExtension(exePath) is string name)
                {
                    var processes = System.Diagnostics.Process.GetProcessesByName(name);
                    foreach (var process in processes)
                    {
                        if (process.GetProcessFilename()?.ToLower() == exePath.ToLower())
                        {
                            yield return process;
                        }
                    }
                }
            }
        }
    }

    internal static class ProcessExtensions
    {

        [Flags]
        private enum ProcessAccessFlags : uint
        {
            QueryLimitedInformation = 0x00001000
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool QueryFullProcessImageName([In] IntPtr hProcess, [In] int dwFlags, [Out] StringBuilder lpExeName, ref int lpdwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(ProcessAccessFlags processAccess, bool bInheritHandle, int processId);

        public static string? GetProcessFilename(this System.Diagnostics.Process process)
        {
            var size = 1014;
            var name = new StringBuilder(size);
            var ptr = OpenProcess(ProcessAccessFlags.QueryLimitedInformation, false, process.Id);
            if (QueryFullProcessImageName(ptr, 0, name, ref size))
            {
                return name.ToString();
            }
            return null;
        }

        public static async Task<bool> MorphicStop(this System.Diagnostics.Process process)
        {
            try
            {
                if (process.CloseMainWindow())
                {
                    return await Task.Run(() =>
                    {
                        try
                        {
                            if (process.WaitForExit(5000))
                            {
                                return true;
                            }
                            process.Kill(entireProcessTree: true);
                            return true;
                        }
                        catch (Exception e)
                        {
                            return false;
                        }
                    });
                }
                process.Kill(entireProcessTree: true);
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }
    }
}
