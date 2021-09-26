using System;
using System.Threading.Tasks;

namespace Morphic.InstallerService.Client
{
    public static class ConsoleUtils
    {
        public static async Task BusyIndicator(string content, Func<Task> action)
        {
            using var spinner = new Spinner();

            spinner.Start(content);

            await action();

            spinner.Stop();
        }
    }
}
