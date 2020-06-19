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
        public Task<bool> IsRunning(string appPathKey)
        {
            foreach (var process in GetRunningProcesses(appPathKey))
            {
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<bool> Start(string appPathKey)
        {
            if (GetExecutablePath(appPathKey) is string exePath)
            {
                var process = System.Diagnostics.Process.Start(exePath);
                return Task.FromResult(!process.HasExited);
            }
            return Task.FromResult(false);
        }

        public Task<bool> Stop(string appPathKey)
        {
            var processes = GetRunningProcesses(appPathKey);
            foreach (var process in processes)
            {
                process.CloseMainWindow();
                process.Close();
            }
            return Task.FromResult(true);
        }

        private string? GetExecutablePath(string appPathKey)
        {
            var registryKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + appPathKey;
            return Microsoft.Win32.Registry.GetValue(appPathKey, "(Default)", null) as string;
        }

        private IEnumerable<System.Diagnostics.Process> GetRunningProcesses(string appPathKey)
        {
            if (GetExecutablePath(appPathKey) is string exePath)
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
