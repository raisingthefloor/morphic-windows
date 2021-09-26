using Microsoft.Extensions.Logging;
using Morphic.Core;
using Morphic.InstallerService.Contracts;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace IoDCLI.Workflows.Exe
{
    public class ExeWorkflow : WorkflowBase<ExeInstallError>
    {
        private const string FileExtension = "exe";

        public ExeWorkflow(Package package, EventHandler<ProgressEventArgs> progressHandler, ILogger logger) : base(package, progressHandler, logger)
        {
        }

        public override string GetFileExtension()
        {
            return FileExtension;
        }

        public override async Task<IMorphicResult<bool, ExeInstallError>> Install()
        {
            try
            {
                Download();

                if (Validate())
                {
                    await Execute(LocalFilePath, Package.CommandArguments);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An error has occured.");
            }
            finally
            {
                Cleanup();
            }

            return IMorphicResult<bool, ExeInstallError>.SuccessResult(true);
        }

        private async Task Execute(string path, string arguments)
        {
            var processInfo = new ProcessStartInfo
            {
                UseShellExecute = true,
                Verb = "runas",

                FileName = path,
                Arguments = arguments
            };

            var process = Process.Start(processInfo);

            await process.WaitForExitAsync();
        }

        public override async Task<IMorphicResult<bool, ExeInstallError>> Uninstall()
        {
            try
            {
                await Execute(Package.UninstallCommand, Package.CommandArguments);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An error has occured.");
            }
            finally
            {
                Cleanup();
            }

            return IMorphicResult<bool, ExeInstallError>.SuccessResult(true);
        }
    }
}
