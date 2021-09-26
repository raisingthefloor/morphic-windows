using JKang.IpcServiceFramework.Client;
using Microsoft.Extensions.DependencyInjection;
using Morphic.InstallerService.Contracts;
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

            if (args[0] == "install")
            {
                await ConsoleUtils.BusyIndicator("Installing please wait.", async () =>
                {
                    await client.InvokeAsync(x => x.Install(package));
                });
            }
            else if (args[0] == "uninstall")
            {
                await ConsoleUtils.BusyIndicator("Uninstalling please wait.", async () =>
                {
                    await client.InvokeAsync(x => x.Uninstall(package));
                });
            }
        }
    }
}
