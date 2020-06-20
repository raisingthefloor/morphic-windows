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

        public Task<bool> Start(string exe)
        {
            if (GetExecutablePath(exe) is string exePath)
            {
                var process = System.Diagnostics.Process.Start(exePath);
                return Task.FromResult(!process.HasExited);
            }
            return Task.FromResult(false);
        }

        public Task<bool> Stop(string exe)
        {
            var processes = GetRunningProcesses(exe);
            foreach (var process in processes)
            {
                process.CloseMainWindow();
                process.Close();
            }
            return Task.FromResult(true);
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
            return GetAppPathRegistryKey(exe)?.GetValue("(Default)") as string;
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
                        if (process.MainModule.FileName == exePath)
                        {
                            yield return process;
                        }
                    }
                }
            }
        }
    }
}
