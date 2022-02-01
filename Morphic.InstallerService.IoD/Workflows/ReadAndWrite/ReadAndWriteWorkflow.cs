﻿using IoDCLI;
using IoDCLI.Workflows;
using IoDCLI.Workflows.Msi;
using Microsoft.Extensions.Logging;
using Morphic.Core;
using Morphic.InstallerService.Contracts;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Morphic.InstallerService.IoD.Workflows.ReadAndWrite
{
    public class ReadAndWriteWorkflow : WorkflowBase<ReadAndWriteError>
    {
        private const string FileExtension = "zip";

        public string SourcePath { get; set; }

        private static readonly Package[] _msiToInstall =
        {
            new Package { Url = @"Setup.msi", Hash = "2BF379B1BF06C1536450274DEC9FBCAC6CF35EA8BFFDD2398067D1EBF9DCB2CC", HashType = "SHA256", CommandArguments = "" },
        };

        public ReadAndWriteWorkflow(Package package, EventHandler<ProgressEventArgs> progressHandler, ILogger logger) : base(package, progressHandler, logger)
        {
            SourcePath = @"C:\read&write";
        }

        public override string GetFileExtension()
        {
            return FileExtension;
        }

        public override async Task<IMorphicResult<bool, ReadAndWriteError>> Install()
        {
            try
            {
                foreach (var msiToInstall in _msiToInstall)
                {
                    msiToInstall.Url = Path.Combine(SourcePath, msiToInstall.Url);

                    var workflow = new MsiWorkflow(msiToInstall, true, (sender, args) => HandleProgress(args), Logger);

                    await workflow.Install();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An error has occuered.");
            }
            finally
            {
                Cleanup();
            }

            return IMorphicResult<bool, ReadAndWriteError>.SuccessResult(true);
        }

        private void HandleProgress(ProgressEventArgs progressEventArgs)
        {
            //Logger.LogInformation($"Progress: {progressEventArgs.Value}");
            ProgressHandler?.Invoke(this, progressEventArgs);
        }

        public override async Task<IMorphicResult<bool, ReadAndWriteError>> Uninstall()
        {
            try
            {
                await UninstallReadAndWrite();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An error has occured.");
            }

            return IMorphicResult<bool, ReadAndWriteError>.SuccessResult(true);
        }

        private async Task UninstallReadAndWrite()
        {
            foreach (var msiToInstall in _msiToInstall)
            {
                msiToInstall.Url = Path.Combine(SourcePath, msiToInstall.Url);

                var workflow = new MsiWorkflow(msiToInstall, true, (sender, args) => HandleProgress(args), Logger);

                var result = await workflow.Uninstall();

                if (result.IsError)
                {
                    Logger.LogError($"An error has occured while attempting to uninstall read&write. '{result.Error}'.");
                }
            }
        }
    }
}
