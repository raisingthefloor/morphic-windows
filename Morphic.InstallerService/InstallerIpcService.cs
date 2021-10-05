using IoDCLI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Morphic.InstallerService.Contracts;
using System;
using System.Linq;
using System.Runtime.InteropServices;
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
                LoadUserProfile(user);

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

        private bool LoadUserProfile(WindowsIdentity user)
        {
            var profileInfo = new WindowsIdentityHelper.ProfileInfo();
            profileInfo.dwSize = Marshal.SizeOf(profileInfo);
            profileInfo.lpUserName = user.Name;
            profileInfo.dwFlags = 1;
            return WindowsIdentityHelper.LoadUserProfile(user.Token, ref profileInfo);
        }

        public async Task Uninstall(Package package)
        {
            _logger.LogInformation("Uninstall called.");

            var user = WindowsIdentityHelper.GetLoggedOnUsers().First();

            await WindowsIdentity.RunImpersonated(user.AccessToken, async () =>
            {
                LoadUserProfile(user);

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

        public async Task Install(string application, string[] arguments)
        {
            _logger.LogInformation($"Install called for application '{application}'.");

            var user = WindowsIdentityHelper.GetLoggedOnUsers().First();

            await WindowsIdentity.RunImpersonated(user.AccessToken, async () =>
            {
                LoadUserProfile(user);

                var service = _serviceProvider.GetService<PackageManagerService>();

                if (service != null)
                {
                    await service.InstallJaws(arguments);
                }
                else
                {
                    _logger.LogError("Unable to resolve package manager service.");
                }
            });
        }

        public async Task Uninstall(string application, string[] arguments)
        {
            _logger.LogInformation($"Uninstall called for application '{application}'.");

            var user = WindowsIdentityHelper.GetLoggedOnUsers().First();

            await WindowsIdentity.RunImpersonated(user.AccessToken, async () =>
            {
                LoadUserProfile(user);

                var service = _serviceProvider.GetService<PackageManagerService>();

                if (service != null)
                {
                    await service.UninstallJaws(arguments);
                }
                else
                {
                    _logger.LogError("Unable to resolve package manager service.");
                }
            });
        }
    }
}
