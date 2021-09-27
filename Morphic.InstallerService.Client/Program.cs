using JKang.IpcServiceFramework.Client;
using Microsoft.Extensions.DependencyInjection;
using Morphic.InstallerService.Contracts;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Morphic.InstallerService.Client
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length == 0)
                return;

            var package = new Package();

            var serviceProvider = new ServiceCollection()
                .AddNamedPipeIpcClient<IInstallerService>("client1", pipeName: "moprhicinstaller")
                .BuildServiceProvider();

            var clientFactory = serviceProvider.GetRequiredService<IIpcClientFactory<IInstallerService>>();

            var client = clientFactory.CreateClient("client1");

            var action = args[0];
            var application = args[1];
            var arguments = new List<string>();

            if (args.Length > 2)
            {
                for (var i = 2; i < args.Length; i++)
                {
                    arguments.Add(args[i]);
                }
            }

            if (action == "install")
            {
                await ConsoleUtils.BusyIndicator("Installing please wait.", async () =>
                {
                    await client.InvokeAsync(x => x.Install(application, arguments.ToArray()));
                });
            }
            else if (action == "uninstall")
            {
                await ConsoleUtils.BusyIndicator("Uninstalling please wait.", async () =>
                {
                    await client.InvokeAsync(x => x.Uninstall(application, arguments.ToArray()));
                });
            }
        }
    }
}
