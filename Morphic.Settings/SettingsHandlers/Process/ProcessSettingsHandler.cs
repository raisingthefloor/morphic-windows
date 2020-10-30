namespace Morphic.Settings.SettingsHandlers.Process
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;
    using SolutionsRegistry;

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
                    foreach (Process process in processes)
                    {
                        process.Kill(true);
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
