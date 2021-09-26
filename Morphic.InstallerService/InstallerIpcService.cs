using IoDCLI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Morphic.InstallerService.Contracts;
using System;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;

namespace Morphic.InstallerService
{
    public class InstallerIpcService : IInstallerService
    {
        private ILogger<InstallerIpcService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public InstallerIpcService(ILogger<InstallerIpcService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task Install(Package package)
        {
            _logger.LogInformation("Install called.");

            var user = WindowsIdentityHelper.GetLoggedOnUsers().First();

            await WindowsIdentity.RunImpersonated(user.AccessToken, async () =>
            {
                var service = _serviceProvider.GetService<PackageManagerService>();

                if (service != null)
                {
                    await service.ProcessPackages();
                }
                else
                {
                    _logger.LogError("Unable to resolve package manager service.");
                }
            });
        }

        public async Task Uninstall(Package package)
        {
            _logger.LogInformation("Uninstall called.");

            var user = WindowsIdentityHelper.GetLoggedOnUsers().First();

            await WindowsIdentity.RunImpersonated(user.AccessToken, async () =>
            {
                var service = _serviceProvider.GetService<PackageManagerService>();

                if (service != null)
                {
                    await service.RemovePackages();
                }
                else
                {
                    _logger.LogError("Unable to resolve package manager service.");
                }
            });
        }
    }
}
