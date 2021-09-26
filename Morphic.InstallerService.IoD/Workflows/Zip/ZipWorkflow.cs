using Microsoft.Extensions.Logging;
using Morphic.Core;
using Morphic.InstallerService.Contracts;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace IoDCLI.Workflows.Zip
{
    public class ZipWorkflow : WorkflowBase<ZipError>
    {
        private const string FileExtension = "zip";

        public ZipWorkflow(Package package, EventHandler<ProgressEventArgs> progressHandler, ILogger logger) : base(package, progressHandler, logger)
        {
        }

        public override string GetFileExtension()
        {
            return FileExtension;
        }

        public override async Task<IMorphicResult<bool, ZipError>> Install()
        {
            try
            {
                Download();

                if (Validate())
                {
                    await Execute(LocalFilePath, Package.CommandArguments);
                }
            }
            catch(Exception ex)
            {
                Logger.LogError(ex, "An error has occuered.");
            }
            finally
            {
                Cleanup();
            }

            return IMorphicResult<bool, ZipError>.SuccessResult(true);
        }

        private async Task Execute(string path, string arguments)
        {
            ZipFile.ExtractToDirectory(path, Path.Combine(Platform.DownloadsFolder, Package.Name));

            await Task.CompletedTask;
        }

        public override async Task<IMorphicResult<bool, ZipError>> Uninstall()
        {
            await Task.CompletedTask;

            return IMorphicResult<bool, ZipError>.SuccessResult(true);
        }
    }
}
