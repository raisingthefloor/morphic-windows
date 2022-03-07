namespace Morphic.Settings.SettingsHandlers.Process
{
    using SolutionsRegistry;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;

    [SrService]
    public class ProcessSettingsHandler : FixedSettingsHandler
    {
        /// <summary>Gets the "friendly name" of a process. That is, without the path or extension.</summary>
        private static string GetProcessName(string path)
        {
            return Path.ChangeExtension(Path.GetFileName(path), null);
        }

        private static Process[] GetProcesses(string path)
        {
            string processName = GetProcessName(path);
            return Process.GetProcessesByName(processName);
        }

        [Setter("isRunning")]
        public Task<bool> SetRunning(Setting setting, object? newValue)
        {
            if (newValue is bool start)
            {
                Process[] processes = GetProcesses(setting.SettingGroup.Path);
                if (start)
                {
                    if (processes.Length == 0)
                    {
                        this.StartProcess(setting);
                    }
                }
                else if (processes.Length > 0)
                {
                    // try to close each process naturally (by closing all of its windows) and then, if that didn't exit the process naturally, force-terminate the process
                    foreach (Process process in processes)
                    {
                        int numberOfMillisecondsToWait;
#if DEBUG
                        // NOTE: in debug mode, we usually don't set the requestedExecutionLevel with uiAccess to asInvoker (in the application manifest, for example), unless we're specifically debugging this code
                        //       [so we need to just shut down the process (without closing all windwos for the process) instead]
                        numberOfMillisecondsToWait = 0;
#else
                        // NOTE: we ignore the success/failure of closing all windows for the process; this is a "reasonable effort" kind of function call
                        // NOTE: a response of "true" means that all windows closed (or are closing), whereas "false" means that some windows didn't accept our close call; we may want to tweak the numberOfMillisecondsToWait based on this response
                        _ = Morphic.WindowsNative.Process.Process.CloseAllWindowsForProcess(process.Id);

                        // give this process up to numberOfMillisecondsToWait milliseconds (in this case: 2 seconds) to exit before we intervene to terminate it
                        // OBSERVATION: we may want to wait for all processes in parallel (if there are multiple processes) so that we don't wait 2 seconds TIMES the number of processes (i.e. so that we want 2 seconds maximum)
                        numberOfMillisecondsToWait = 2000;
#endif

                        if (!process.WaitForExit(numberOfMillisecondsToWait))
                        {
                            // if the process doesn't exit after numberOfMillisecondsToWait, kill the process and all of its subchildren
                            process.Kill(true);
                        }
                    }
                }
            }

            return Task.FromResult(true);
        }

        [Getter("isRunning")]
        public Task<object?> GetRunning(Setting setting)
        {
            bool running = GetProcesses(setting.SettingGroup.Path).Length > 0;
            return Task.FromResult<object?>(running);
        }

        private void StartProcess(Setting setting)
        {
            ProcessSettingGroup group = (ProcessSettingGroup)setting.SettingGroup;

            ProcessStartInfo startInfo = new ProcessStartInfo(group.Path)
            {
                UseShellExecute = true
            };

            group.Arguments.ForEach(startInfo.ArgumentList.Add);
            foreach (KeyValuePair<string, string> item in group.Environment)
            {
                startInfo.Environment.Add(item);
            }

            Process.Start(startInfo);
        }
    }
}
