// ApplicationAction.cs: A bar action that starts an application.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

using Morphic.Windows.Native.WindowsCom;

namespace Morphic.Client.Bar.Data.Actions
{
    using Microsoft.Extensions.Logging;
    using Microsoft.Win32;
    using Morphic.Core;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Interop;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;

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
                if (this.ImageIsCollapsed == false && this.AppPath is not null)
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

        internal bool ImageIsCollapsed { get; set; } = false;

        /// <summary>
        /// Start a default application. This value will be mapped locally
        /// </summary>
        [JsonProperty("default")]
        public string? DefaultAppName { get; set; }

        /// <summary>
        /// Invoke the value in `exe` as-is, via the shell (explorer). Don't resolve the path.
        /// </summary>
        [JsonProperty("shell")]
        public bool Shell { get; set; }

        /// <summary>
        /// This is a windows store app. The value of `exe` is the Application User Model ID of the app.
        /// For example, `Microsoft.WindowsCalculator_8wekyb3d8bbwe!App`
        /// </summary>
        [JsonProperty("appx")]
        public bool AppX { get; set; }

        /// <summary>
        /// true to always start a new instance. false to activate an existing instance.
        /// </summary>
        [JsonProperty("newInstance")]
        public bool NewInstance { get; set; }

        /// <summary>
        /// The initial state of the window.
        /// </summary>
        [JsonProperty("windowStyle")]
        public ProcessWindowStyle WindowStyle { get; set; } = ProcessWindowStyle.Normal;

        internal struct MorphicExecutablePathInfo
        {
            public bool IsAppx;
            public string Path;
        }
        //
        // NOTE: we should consider returning error details (e.g. "executable not found", "unknown exeId", "win32 error") from this function
        private static MorphicResult<MorphicExecutablePathInfo, MorphicUnit> ConvertExeIdToExecutablePath(string exeId)
        {
            bool isAppX = false;
            string? appPath = null;

            switch (exeId)
            {
                case "calculator":
                    {
                        // option #1: calc.exe in system folder
                        //appPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "calc.exe");
                        //
                        // option #2: find calc.exe in paths
                        appPath = ApplicationAction.SearchPathEnv("calc.exe");
                    }
                    break;
                case "firefox":
                    {
                        appPath = ApplicationAction.SearchAppPaths("firefox.exe");
                    }
                    break;
                case "googleChrome":
                    {
                        appPath = ApplicationAction.SearchAppPaths("chrome.exe");
                    }
                    break;
                case "microsoftAccess":
                    {
                        appPath = ApplicationAction.SearchAppPaths("MSACCESS.EXE");
                    }
                    break;
                case "microsoftExcel":
                    {
                        appPath = ApplicationAction.SearchAppPaths("excel.exe");
                    }
                    break;
                case "microsoftEdge":
                    {
                        appPath = ApplicationAction.SearchAppPaths("msedge.exe");
                    }
                    break;
                case "microsoftOneNote":
                    {
                        appPath = ApplicationAction.SearchAppPaths("OneNote.exe");
                    }
                    break;
                case "microsoftOutlook":
                    {
                        appPath = ApplicationAction.SearchAppPaths("OUTLOOK.EXE");
                    }
                    break;
                case "microsoftPowerPoint":
                    {
                        appPath = ApplicationAction.SearchAppPaths("powerpnt.exe");
                    }
                    break;
                case "microsoftQuickAssist":
                    {
                        // option #1: exactly as written in Quick Assist shortcut (as of 18-Apr-2021): %WINDIR%\system32\quickassist.exe
                        //var appPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"system32\quickassist.exe");
                        //
                        // option #2: quickassist.exe in system folder
                        appPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "quickassist.exe");
                        //
                        // option #3: ms url shortcut (NOTE: we have intentionally not used this in case Quick Assist is removed from the system...as the URL does not give us a way to detect that scenario via "file does not exist" checks)
                        //var appPath = "ms-quick-assist:";
                    }
                    break;
                case "microsoftSkype":
                    {
                        appPath = ApplicationAction.SearchAppPaths("Skype.exe");
                    }
                    break;
                case "microsoftTeams":
                    {
                        // NOTE: this is a very odd path, and it's not in the "AppPaths"; if we can find another way to determine the proper launch path or launch programatically, we should consider another method
                        var userAppDataLocalPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                        var fixedAppPath = Path.Combine(new string[] { userAppDataLocalPath, "Microsoft", "Teams", "current", "Teams.exe" });

                        // NOTE: we could also probably launch Teams via the "ms-teams:" URI, but that wouldn't necessarily help us detect if it's installed

                        // NOTE: in the future, it would be better to launch Update.exe and have it then start Teams (as Microsoft does for their shortcut) -- but we'd probably want to
                        //       detect the Teams.exe file itself to detect if it was installed
                        //var fixedAppPath = Path.Combine(new string[] { userAppDataLocalPath, "Microsoft", "Teams", "Update.exe" });
                        //var params = new string[] { "--processStart", "Teams.exe" };

                        // NOTE: in Windows 11 (and perhaps in recent releases of Windows 10), Microsoft Teams is installed as an Appx package with the name:
                        // MicrosoftTeams_8wekyb3d8bbwe
                        // ...so we might refer to this as appx:MicrosoftTeams_8wekyb3d8bbwe!MicrosoftTeams
                        // NOTE: this path, including "!MicrosoftTeams" at the end, is the Application User Model ID (AUMID) of Microsoft Teams

                        if (File.Exists(fixedAppPath) == true)
                        {
                            // if the file exists, set appPath to the fixed path
                            appPath = fixedAppPath;
                        }
                        else
                        {
                            // if Teams was not installed as an EXE, check to see if it's installed as an APPX package
                            var packageFamilyName = "MicrosoftTeams_8wekyb3d8bbwe";
                            var isPackageInstalledResult = Appx.IsPackageInstalled(packageFamilyName);
                            if (isPackageInstalledResult.IsError == true) {
                                return MorphicResult.ErrorResult();
                            }
                            var isPackageInstalled = isPackageInstalledResult.Value!;

                            if (isPackageInstalled == true) 
                            {
                                appPath = packageFamilyName + "!MicrosoftTeams";
                                isAppX = true;
                            }
                        }
                    }
                    break;
                case "microsoftWord":
                    {
                        appPath = ApplicationAction.SearchAppPaths("Winword.exe");
                    }
                    break;
                case "opera":
                    {
                        appPath = ApplicationAction.SearchAppPaths("opera.exe");
                    }
                    break;
                default:
                    {
                        appPath = null;
                    }
                    break;
            }

            if (appPath is not null)
            {
                var result = new MorphicExecutablePathInfo()
                {
                    IsAppx = isAppX,
                    Path = appPath
                };
                return MorphicResult.OkResult(result);
            }
            else
            {
                return MorphicResult.ErrorResult();
            }
        }

        /// <summary>
        /// Executable name, or the full path to it. If also providing arguments, surround the executable path with quotes.
        /// </summary>
        [JsonProperty("exe")]
        public string? ExeName
        {
            get => this.exeNameValue ?? null;
            set
            {
                this.exeNameValue = value;
                this.AppPath = null; // until we find the executable id'd by exeNameValue, AppPath should be null; it will be set to the actual executable path (or AppX identity)

                // OBSERVATION: we currently do not check if a package is installed; we simply assume that it is installed (i.e. available) if the EXE name beings with "appx:"
				// NOTE: the "exename" for an appx is "appx:" followed by the AUMID (which includes the package name, plus additional information after the package name)
                if (value is not null && value.StartsWith("appx:", StringComparison.InvariantCultureIgnoreCase))
                {
                    this.AppX = true;
                    this.AppPath = value.Substring(5);
                }

                if (value is not null && value.Length > 0)
                {
                    var convertExeToPathResult = ApplicationAction.ConvertExeIdToExecutablePath(value);
                    if (convertExeToPathResult.IsError == false)
                    {
                        this.AppX = convertExeToPathResult.Value!.IsAppx;
                        this.AppPath = convertExeToPathResult.Value!.Path;
                        App.Current.Logger.LogDebug($"Resolved exe file '{this.exeNameValue}' to '{this.AppPath ?? "(null)"}'");
                    }
                }

                this.IsAvailable = this.AppPath is not null;
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
        /// Searches the directories in the PATH environment variable.
        /// </summary>
        /// <param name="file"></param>
        /// <returns>null if not found.</returns>
        private static string? SearchPathEnv(string file)
        {
            // OBSERVATION: the noted alternative may search some standard system directories outside of the PATH if the path env var isn't explicitly provided as an array of strings
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
        private static string? SearchAppPaths(string file)
        {
            string? fullPath = null;

            // Look in *\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths (giving priority to the current user, in case an entry exists in both locations)
            foreach (RegistryKey rootKey in new[] {Registry.CurrentUser, Registry.LocalMachine})
            {
                RegistryKey? key =
                    rootKey.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{file}");
                if (key is not null)
                {
                    // capture the default key (which should be the full path with executable)
                    fullPath = key.GetValue(null) as string;
                    if (fullPath is not null)
                    {
                        break;
                    }
                }
            }

            return fullPath;
        }

        // NOTE: we should not use GetAppPathForProgId until we have dealt with removing command-line arguments (e.g. "C:\Program Files\Microsoft Office\Root\Office16\WINWORD.EXE /Automation")
        private static MorphicResult<string, MorphicUnit> GetAppPathForProgId(string progId)
        {
            var getClassIdResult = ApplicationAction.GetClassIdForProgId(progId);
            if (getClassIdResult.IsError == true)
            {
                return MorphicResult.ErrorResult();
            }
            var classId = getClassIdResult.Value!;

            var getPathResult = ApplicationAction.GetLocalServer32PathForClassId(classId);
            if (getPathResult.IsError == true)
            {
                return MorphicResult.ErrorResult();
            }
            var path = getPathResult.Value!;

            // if the command is enclosed by quotes, strip out the actual executable name
            if (path.Length > 0 && path.Substring(0, 1) == "\"")
            {
                var indexOfClosingQuote = path.IndexOf('\"', 1);
                if (indexOfClosingQuote > 0)
                {
                    path = path.Substring(1, indexOfClosingQuote - 1);
                }
            }

            // NOTE: we should check the actual path and remove any command-line arguments (in case the path includes the executable name and also command-line arguments)
            //       [this could be tricky]

            return MorphicResult.OkResult(path);
        }

        // NOTE: this function should return the path to run an executable, given its classId (which can be retrieved from FindClassIdForProgId)
        private static MorphicResult<string, MorphicUnit> GetLocalServer32PathForClassId(string classId)
        {
            string? result = null;

            bool is64Bit;
            switch (IntPtr.Size)
            {
                case 4:
                    is64Bit = false;
                    break;
                case 8:
                    is64Bit = true;
                    break;
                default:
                    Debug.Assert(false, "OS is not 32-bit or 64-bit");
                    return MorphicResult.ErrorResult();
            }

            RegistryKey? key;
            if (is64Bit == true)
            {
                key = Registry.ClassesRoot.OpenSubKey($@"Wow6432Node\CLSID\{classId}\LocalServer32");
            } 
            else
            {
                key = Registry.ClassesRoot.OpenSubKey($@"CLSID\{classId}\LocalServer32");
            }

            if (key is not null)
            {
                var localServer32 = key.GetValue(null) as string;
                if (localServer32 is not null)
                {
                    result = localServer32;
                }
            }

            if (result is not null)
            {
                return MorphicResult.OkResult(result);
            }
            else
            {
                // if we could not find the key or if its default value was null or invalid, return an error
                return MorphicResult.ErrorResult();
            }
        }

        private static MorphicResult<string, MorphicUnit> GetClassIdForProgId(string progId)
        {
            RegistryKey? key = Registry.ClassesRoot.OpenSubKey($@"{progId}\CLSID");
            if (key is not null)
            {
                var classId = key.GetValue(null) as string;
                if (classId is not null)
                {
                    return MorphicResult.OkResult(classId);
                }
            }

            // if we could not find the key or if its default value was null, return an error
            return MorphicResult.ErrorResult();
        }

        private static MorphicResult<string, MorphicUnit> StripExecutableFromCommand(string command)
        {
            // we need to split off the executable name from any arguments; it is either enclosed in quotes or it's everything before a space
            //
            // option 1: enclosed in quotes
            // NOTE: there may be other ways of addressing this (such as looking for a file extension on the executable file name)
            var indexOfFirstDoubleQuote = command.IndexOf('\"');
            if (indexOfFirstDoubleQuote == 0)
            {
                var indexOfSecondDoubleQuote = command.IndexOf('\"', indexOfFirstDoubleQuote + 1);
                if (indexOfSecondDoubleQuote > 1)
                {
                    command = command.Substring(indexOfFirstDoubleQuote + 1, indexOfSecondDoubleQuote - indexOfFirstDoubleQuote - 1);
                    return MorphicResult.OkResult(command);
                }
                else
                {
                    return MorphicResult.ErrorResult();
                }
            }
            //
            // option 2: everything before a space (or everything, if there are no spaces)
            var indexOfFirstSpace = command.IndexOf(' ');
            if (indexOfFirstSpace > 0)
            {
                command = command.Substring(0, indexOfFirstSpace);
                return MorphicResult.OkResult(command);
            }
            else
            {
                return MorphicResult.OkResult(command);
            }
        }

        private static MorphicResult<string, MorphicUnit> GetOpenCommandForProgIdClass(string progId) 
        {
            // look up the browser progId's actual executable path (e.g. path to Edge, instead of "MSEdgeHtm")
            var browserOpenCommandRegistryKey = Registry.ClassesRoot.OpenSubKey(progId + @"\shell\open\command");
            if (browserOpenCommandRegistryKey is not null)
            {
                // get the string to launch the browser (e.g. the default registry key value); this result may include arguments
                var browserOpenCommand = browserOpenCommandRegistryKey.GetValue(null) as string;
                if (browserOpenCommand is not null)
                {
                    var stripExecutableFromCommandResult = ApplicationAction.StripExecutableFromCommand(browserOpenCommand);
                    if (stripExecutableFromCommandResult.IsError == true)
                    {
                        return MorphicResult.ErrorResult();
                    }
                    browserOpenCommand = stripExecutableFromCommandResult.Value!;

                    return MorphicResult.OkResult(browserOpenCommand);
                }
            }

            // if we could not get the open command, return failure
            return MorphicResult.ErrorResult();
        }

        private static MorphicResult<string, MorphicUnit> GetPathToExecutableForUrlAssociation(string urlAssociation)
        {
            var userSelectedBrowserRegistryKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\Shell\Associations\UrlAssociations\" + urlAssociation + @"\UserChoice", false);
            if (userSelectedBrowserRegistryKey is not null)
            {
                var progId = userSelectedBrowserRegistryKey.GetValue("ProgId") as string;
                if (progId is not null)
                {
                    var getOpenCommandForProgIdClassResult = ApplicationAction.GetOpenCommandForProgIdClass(progId);
                    if (getOpenCommandForProgIdClassResult.IsError == false)
                    {
                        var browserOpenCommand = getOpenCommandForProgIdClassResult.Value!;
                        return MorphicResult.OkResult(browserOpenCommand);
                    }
                }
            }

            // if we could not get the open command, return failure
            return MorphicResult.ErrorResult();
        }

        private static MorphicResult<string, MorphicUnit> GetPathToExecutableForFileExtension(string fileExtension)
        {
            var userSelectedBrowserRegistryKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\" + fileExtension + @"\UserChoice", false);
            if (userSelectedBrowserRegistryKey is not null)
            {
                var progId = userSelectedBrowserRegistryKey.GetValue("ProgId") as string;
                if (progId is not null)
                {
                    var getOpenCommandForProgIdClassResult = ApplicationAction.GetOpenCommandForProgIdClass(progId);
                    if (getOpenCommandForProgIdClassResult.IsError == false)
                    {
                        var browserOpenCommand = getOpenCommandForProgIdClassResult.Value!;
                        return MorphicResult.OkResult(browserOpenCommand);
                    }
                }
            }

            // if we could not get the open command, return failure
            return MorphicResult.ErrorResult();
        }

        private static MorphicResult<string, MorphicUnit> GetDefaultBrowserPath()
        {
            var userSelectedBrowserRegistryKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice", false);
            if (userSelectedBrowserRegistryKey is not null)
            {
                var browserProgId = userSelectedBrowserRegistryKey.GetValue("ProgId") as string;
                if (browserProgId is not null)
                {
                    var getOpenCommandForProgIdClassResult = ApplicationAction.GetOpenCommandForProgIdClass(browserProgId);
                    if (getOpenCommandForProgIdClassResult.IsError == false)
                    {
                        var browserOpenCommand = getOpenCommandForProgIdClassResult.Value!;
                        return MorphicResult.OkResult(browserOpenCommand);
                    }
                }
            }

            // if we could not get the browser using the UserChoice registry key, try looking up the file association for an ".htm" file instead




            // if no path could be found, return failure
            return MorphicResult.ErrorResult();
        }

        protected override Task<MorphicResult<MorphicUnit, MorphicUnit>> InvokeAsyncImpl(string? source = null, bool? toggleState = null)
        {
            if (this.DefaultAppName is not null)
            {
                // use the default application for this type
                switch (this.DefaultAppName!)
                {
                    case "browser":
                        {
                            string? associatedExecutablePath = null;

                            // try to get the executable for https:// urls
                            var getAssociatedExecutableForHttpUrlsResult = ApplicationAction.GetPathToExecutableForUrlAssociation("https");
                            if (getAssociatedExecutableForHttpUrlsResult.IsError == false)
                            {
                                associatedExecutablePath = getAssociatedExecutableForHttpUrlsResult.Value!;
                            }

                            // if we haven't found the default browser yet, look for the default application to open ".htm" files
                            if (associatedExecutablePath is null)
                            {
                                var getAssociatedExecutableForHtmFilesResult = ApplicationAction.GetPathToExecutableForFileExtension(".htm");
                                if (getAssociatedExecutableForHtmFilesResult.IsError == false)
                                {
                                    associatedExecutablePath = getAssociatedExecutableForHtmFilesResult.Value!;
                                }
                            }

                            // if we still haven't found the default browser, gracefully degrade by trying to use the launch process executable shortcut "https:" instead
                            if (associatedExecutablePath is null)
                            {
                                associatedExecutablePath = "https:";
                            }

                            var launchBrowserProcessResult = ApplicationAction.LaunchProcess(associatedExecutablePath, new List<string>(), new Dictionary<string, string>(), this.WindowStyle);
                            return Task.FromResult(launchBrowserProcessResult);
                        }
                    case "email":
                        {
                            var launchTarget = "mailto:";

                            var launchMailProcessResult = ApplicationAction.LaunchProcess(launchTarget, new List<string>(), new Dictionary<string, string>(), this.WindowStyle, true /* useShellExecute should be true for all protocols (e.g. 'mailto:') */);
                            return Task.FromResult(launchMailProcessResult);
                        }
                    default:
                        {
                            // unknown
                            Debug.Assert(false, "Unknown 'default' application type: " + this.DefaultAppName!);
                            //
                            MorphicResult<MorphicUnit, MorphicUnit> result = MorphicResult.ErrorResult();
                            return Task.FromResult(result);
                        }
                }
            }

            // if we reach here, we need to launch the executable related to this "exe" ID
            if (string.IsNullOrEmpty(this.ExeName) || string.IsNullOrEmpty(this.AppPath))
            {
                // if we don't have an exeName ID tag, we have failed
                //
                MorphicResult<MorphicUnit, MorphicUnit> result = MorphicResult.ErrorResult();
                return Task.FromResult(result);
            }

            if (this.AppX)
            {
                var pid = Appx.Start(this.AppPath);
                //
                MorphicResult<MorphicUnit, MorphicUnit> result = pid > 0 ? MorphicResult.OkResult() : MorphicResult.ErrorResult();
                return Task.FromResult(result);
            }

            // for all other processes, launch the executable
            // pathToExecutable
            var pathToExecutable = this.AppPath;
            //
            // useShellExecute
            var useShellExecute = true; // default
            if (this.Shell)
            {
                useShellExecute = true;
            }
            // arguments
            List<string> arguments = new List<string>();
            if (this.Arguments.Count > 0)
            {
                foreach (string argument in this.Arguments)
                {
                    var resolvedString = this.ResolveString(argument, source);
                    if (resolvedString is not null)
                    {
                        arguments.Add(resolvedString);
                    }
                }
            }
            else
            {
                var resolvedString = this.ResolveString(this.ArgumentsString, source);
                if (resolvedString is not null)
                {
                    arguments.Add(resolvedString);
                }
            }
            //
            // environmentVariables
            Dictionary<string, string> environmentVariables = new Dictionary<string, string>();
            foreach (var (key, value) in this.EnvironmentVariables)
            {
                var resolvedString = this.ResolveString(value, source);
                if (resolvedString is not null)
                {
                    environmentVariables.Add(key, resolvedString);
                } 
                else
                {
                    Debug.Assert(false, "Could not resolve environment variable: key = " + key + ", value = '" + value + "'");
                }
            }
            //
            // windowStyle
            var windowStyle = this.WindowStyle;

            var launchProcessResult = ApplicationAction.LaunchProcess(pathToExecutable, arguments, environmentVariables, windowStyle, useShellExecute);
            return Task.FromResult(launchProcessResult);
        }

        private static MorphicResult<MorphicUnit, MorphicUnit> LaunchProcess(string pathToExecutable, List<string> arguments, Dictionary<string, string> environmentVariables, ProcessWindowStyle windowStyle, bool useShellExecute = true)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = pathToExecutable,
                ErrorDialog = true,
                // This is required to start taskmgr (the UAC prompt)
                UseShellExecute = useShellExecute,
                WindowStyle = windowStyle
            };

            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            foreach (var (key, value) in environmentVariables)
            {
                startInfo.EnvironmentVariables.Add(key, value);
            }

            Process? process = Process.Start(startInfo);
            if (process is not null)
            {
                return MorphicResult.OkResult();
            }
            else
            {
                return MorphicResult.ErrorResult();
            }
        }

        /// <summary>
        /// Activates a running instance of the application.
        /// </summary>
        /// <returns>false if it could not be done.</returns>
        /// <exception cref="NotImplementedException"></exception>
        private MorphicResult<MorphicUnit, MorphicUnit> ActivateInstance()
        {
            bool success = false;
            string? friendlyName = Path.GetFileNameWithoutExtension(this.AppPath);
            if (!string.IsNullOrEmpty(friendlyName))
            {
                success = Process.GetProcessesByName(friendlyName)
                    .Where(p => p.MainWindowHandle != IntPtr.Zero)
                    .OrderByDescending(p => p.StartTime)
                    .Any(process => WinApi.ActivateWindow(process.MainWindowHandle));
            }

            return success ? MorphicResult.OkResult() : MorphicResult.ErrorResult();
        }
    }
}
