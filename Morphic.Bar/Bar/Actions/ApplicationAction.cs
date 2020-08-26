// ApplicationAction.cs: A bar action that starts an application.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

namespace Morphic.Bar.Bar.Actions
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Interop;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Extensions.Logging;
    using Microsoft.Win32;
    using Newtonsoft.Json;

    /// <summary>
    /// Action to start an application.
    /// </summary>
    [JsonTypeName("application")]
    public class ApplicationAction : BarAction
    {
        private string? exeNameValue;

        /// <summary>
        /// The actual path to the executable.
        /// </summary>
        public string? AppPath { get; set; }

        public override ImageSource? DefaultImageSource
        {
            get
            {
                if (this.AppPath != null)
                {
                    return Imaging.CreateBitmapSourceFromHIcon(
                        System.Drawing.Icon.ExtractAssociatedIcon(this.AppPath).Handle,
                        Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                }
                else
                {
                    return null;
                }
            }
        }

        [JsonProperty("default")]
        public string? DefaultAppName { get; set; }

        public BarAction? DefaultApp { get; private set; }

        /// <summary>
        /// Executable name, or the full path to it. If also providing arguments, surround the executable path with quotes.
        /// </summary>
        [JsonProperty("exe", Required = Required.Always)]
        public string ExeName
        {
            get => this.exeNameValue ?? string.Empty;
            set
            {
                this.exeNameValue = value;
                if (this.exeNameValue.Length == 0)
                {
                    this.AppPath = null;
                }
                else
                {
                    if (this.ExeName.StartsWith('"'))
                    {
                        int nextQuote = this.exeNameValue.IndexOf('"', 1);
                        if (nextQuote < 0)
                        {
                            App.Current.Logger.LogWarning($"Executable path [{this.ExeName}] has mismatching quote");
                            this.AppPath = this.ExeName.Substring(1);
                        }
                        else
                        {
                            this.AppPath = this.ExeName.Substring(1, nextQuote - 1);
                            this.ArgumentsString = this.ExeName.Substring(nextQuote + 1).Trim();
                        }
                    }

                    this.AppPath = this.ResolveAppPath(this.exeNameValue);
                    App.Current.Logger.LogDebug($"Resolved exe file '{this.exeNameValue}' to '{this.AppPath ?? "(null)"}'");
                }

                this.IsAvailable = this.AppPath != null;
            }
        }

        /// <summary>
        /// Array of arguments.
        /// </summary>
        [JsonProperty("args")]
        public List<string> Arguments { get; set; } = new List<string>();

        /// <summary>
        /// The arguments, if they're passed after the exe name.
        /// </summary>
        public string? ArgumentsString { get; set; }

        /// <summary>
        /// Environment variables to set
        /// </summary>
        [JsonProperty("env")]
        public Dictionary<string, string> EnvironmentVariables { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Resolves the path of an executable, by looking in the "App Paths" registry key or the PATH environment.
        /// If a full path is provided, and it doesn't exist, then the path for the file name alone is resolved.
        ///
        /// Environment variables in the file path are also resolved.
        /// </summary>
        /// <param name="exeName">The `exeName` input value.</param>
        /// <returns>Full path to the executable if found, or null.</returns>
        private string? ResolveAppPath(string exeName)
        {
            string file = Environment.ExpandEnvironmentVariables(exeName);
            string? ext = Path.GetExtension(file).ToLower();
            string withExe, withoutExe;

            // Try with the .exe extension first, then without, but if the file ends with a '.', then try that first.
            // (similar to CreateProcess)
            if (file.EndsWith("."))
            {
                string? result1 = this.ResolveAppPathAsIs(file);
                if (result1 != null)
                {
                    return result1;
                }

                withExe = Path.ChangeExtension(file, ".exe");
                withoutExe = Path.ChangeExtension(file, null);
            }
            else if (ext == ".exe")
            {
                withExe = file;
                withoutExe = Path.ChangeExtension(file, null);
            }
            else
            {
                withExe = file + ".exe";
                withoutExe = file;
            }

            string? result = this.ResolveAppPathAsIs(withExe);
            if (result != null)
            {
                return result;
            }
            else
            {
                return this.ResolveAppPathAsIs(withoutExe);
            }
        }

        /// <summary>
        /// Called by ResolveAppPath to perform the actual resolving work.
        /// </summary>
        /// <param name="file"></param>
        /// <returns>Full path to the executable if found, or null.</returns>
        private string? ResolveAppPathAsIs(string file)
        {
            string? fullPath = null;

            if (Path.IsPathRooted(file))
            {
                if (File.Exists(file))
                {
                    fullPath = file;
                }

                file = Path.GetFileName(file);
            }

            return fullPath ?? this.SearchAppPaths(file) ?? this.SearchPathEnv(file);
        }

        /// <summary>
        /// Searches the directories in the PATH environment variable.
        /// </summary>
        /// <param name="file"></param>
        /// <returns>null if not found.</returns>
        private string? SearchPathEnv(string file)
        {
            // Alternative: https://docs.microsoft.com/en-us/windows/win32/api/shlwapi/nf-shlwapi-pathfindonpathw
            return Environment.GetEnvironmentVariable("PATH")?
                    .Split(Path.PathSeparator)
                    .Select(p => Path.Combine(p, file))
                    .FirstOrDefault(File.Exists);
        }

        /// <summary>
        /// Searches SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths (in both HKCU and HKLM) for an executable.
        /// </summary>
        /// <param name="file"></param>
        /// <returns>null if not found.</returns>
        private string? SearchAppPaths(string file)
        {
            string? fullPath = null;

            // Look in *\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths
            foreach (RegistryKey rootKey in new[] {Registry.CurrentUser, Registry.LocalMachine})
            {
                RegistryKey? key =
                    rootKey.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{file}");
                if (key != null)
                {
                    fullPath = key.GetValue(string.Empty) as string;
                    if (fullPath != null)
                    {
                        break;
                    }
                }
            }

            return fullPath;
        }

        protected override Task<bool> InvokeImpl(string? source = null)
        {
            if (this.DefaultApp != null && string.IsNullOrEmpty(this.ExeName))
            {
                return this.DefaultApp.Invoke(source);
            }

            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = this.AppPath,
                // This is required to start taskmgr (the UAC prompt)
                UseShellExecute = true
            };

            if (this.Arguments.Count > 0)
            {
                foreach (string argument in this.Arguments)
                {
                    startInfo.ArgumentList.Add(this.ResolveString(argument, source));
                }
            }
            else
            {
                startInfo.Arguments = this.ResolveString(this.ArgumentsString, source);
            }

            foreach (var (key, value) in this.EnvironmentVariables)
            {
                startInfo.EnvironmentVariables.Add(key, this.ResolveString(value, source));
            }

            Process? process = Process.Start(startInfo);

            return Task.FromResult(process != null);
        }

        public override void Deserialized()
        {
            base.Deserialized();
            if (!string.IsNullOrEmpty(this.DefaultAppName))
            {
                this.DefaultApp = DefaultApps.GetDefaultApp(this.DefaultAppName);
            }
        }
    }
}
