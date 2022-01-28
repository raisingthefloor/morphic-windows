using IoDCLI.Workflows;
using IoDCLI.Workflows.Exe;
using IoDCLI.Workflows.Jaws;
using IoDCLI.Workflows.Msi;
using IoDCLI.Workflows.MsiX;
using IoDCLI.Workflows.Zip;
using Microsoft.Extensions.Logging;
using Morphic.InstallerService.Contracts;
using Morphic.InstallerService.IoD.Workflows.ReadAndWrite;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace IoDCLI
{
    public class PackageManagerService
    {
        private readonly ILogger _logger;

        public PackageManagerService(ILogger<PackageManagerService> logger)
        {
            _logger = logger;
        }

        public async Task RemovePackages()
        {
            _logger.LogInformation($"{Platform.Name}");

            var servicePath = Assembly.GetEntryAssembly().Location;
            var path = Path.GetDirectoryName(servicePath);

            var file = File.ReadAllText(Path.Combine(path, "packages.json"));

            var packages = JsonConvert.DeserializeObject<List<Package>>(file);

            foreach (var package in packages)
            {
                try
                {
                    if (package.Enabled)
                    {
                        if (package.Type == "msi")
                            await ProcessMsi(package, true);
                        else if (package.Type == "msix")
                            await ProcessMsiX(package, true);
                        else if (package.Type == "exe")
                            await ProcessExe(package, true);
                        else if (package.Type == "zip")
                            await ProcessZip(package, true);
                        else if (package.Type == "jaws")
                            await ProcessJaws(package, true);
                    }
                    else
                    {
                        _logger.LogInformation($"{package.Name} is disabled.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"An error has occured.");
                }
            }
        }

        public async Task InstallJaws(string[] arguments, EventHandler<ProgressEventArgs> progressHandler = null)
        {
            if (arguments.Length > 0)
            {
                JawsWorkflow workflow = null;

                if (progressHandler == null)
                {
                    workflow = new JawsWorkflow(null, (sender, args) => HandleProgress(args), _logger)
                    {
                        SourcePath = arguments[0]
                    };
                }
                else
                {
                    workflow = new JawsWorkflow(null, progressHandler, _logger)
                    {
                        SourcePath = arguments[0]
                    };
                }

                await workflow.Install();
            }

            await Task.CompletedTask;
        }

        public async Task UninstallJaws(string[] arguments)
        {
            if (arguments.Length > 0)
            {
                var workflow = new JawsWorkflow(null, (sender, args) => HandleProgress(args), _logger)
                {
                    SourcePath = arguments[0]
                };

                await workflow.Uninstall();
            }

            await Task.CompletedTask;
        }

        public async Task InstallReadAndWrite(string[] arguments, EventHandler<ProgressEventArgs> progressHandler = null, EventHandler completionHandler = null)
        {
            if (arguments.Length > 0)
            {
                ReadAndWriteWorkflow workflow = null;

                if (progressHandler == null)
                {
                    workflow = new ReadAndWriteWorkflow(null, (sender, args) => HandleProgress(args), _logger)
                    {
                        SourcePath = arguments[0]
                    };
                }
                else
                {
                    workflow = new ReadAndWriteWorkflow(null, progressHandler, _logger)
                    {
                        SourcePath = arguments[0]
                    };
                }

                await workflow.Install();
                completionHandler?.Invoke(this, new EventArgs());
            }

            await Task.CompletedTask;
        }

        public async Task UninstallReadAndWrite(string[] arguments, EventHandler<ProgressEventArgs> progressHandler = null, EventHandler completionHandler = null)
        {
            if (arguments.Length > 0)
            {
                ReadAndWriteWorkflow workflow = null;

                if (progressHandler == null)
                {
                    workflow = new ReadAndWriteWorkflow(null, (sender, args) => HandleProgress(args), _logger)
                    {
                        SourcePath = arguments[0]
                    };
                }
                else
                {
                    workflow = new ReadAndWriteWorkflow(null, progressHandler, _logger)
                    {
                        SourcePath = arguments[0]
                    };
                }

                await workflow.Uninstall();
                completionHandler?.Invoke(this, new EventArgs());
            }

            await Task.CompletedTask;
        }

        public async Task ProcessPackages()
        {
            _logger.LogInformation($"{Platform.Name}");

            var servicePath = Assembly.GetEntryAssembly().Location;
            var path = Path.GetDirectoryName(servicePath);

            var file = File.ReadAllText(Path.Combine(path, "packages.json"));

            var packages = JsonConvert.DeserializeObject<List<Package>>(file);

            foreach (var package in packages)
            {
                try
                {
                    if (package.Enabled)
                    {
                        if (package.Type == "msi")
                            await ProcessMsi(package, false);
                        else if (package.Type == "msix")
                            await ProcessMsiX(package, false);
                        else if (package.Type == "exe")
                            await ProcessExe(package, false);
                        else if (package.Type == "zip")
                            await ProcessZip(package, false);
                        else if (package.Type == "jaws")
                            await ProcessJaws(package, false);
                        else if (package.Type == "readandwrite")
                            await ProcessReadAndWrite(package, false);
                    }
                    else
                    {
                        _logger.LogInformation($"{package.Name} is disabled.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"An error has occured.");
                }
            }
        }

        private async Task ProcessMsi(Package package, bool remove)
        {
            var workflow = new MsiWorkflow(package, (sender, args) => HandleProgress(args), _logger);

            if (!remove)
                await workflow.Install();
            else
                await workflow.Uninstall();
        }

        private async Task ProcessMsiX(Package package, bool remove)
        {
            var workflow = new MsiXWorkflow(package, (sender, args) => HandleProgress(args), _logger);

            if (!remove)
                await workflow.Install();
            else
                await workflow.Uninstall();
        }

        private async Task ProcessExe(Package package, bool remove)
        {
            var workflow = new ExeWorkflow(package, (sender, args) => HandleProgress(args), _logger);

            if (!remove)
                await workflow.Install();
            else
                await workflow.Uninstall();
        }

        private async Task ProcessZip(Package package, bool remove)
        {
            var workflow = new ZipWorkflow(package, (sender, args) => HandleProgress(args), _logger);

            if (!remove)
                await workflow.Install();
            else
                await workflow.Uninstall();
        }

        private async Task ProcessJaws(Package package, bool remove)
        {
            var workflow = new JawsWorkflow(package, (sender, args) => HandleProgress(args), _logger);

            if(!remove)
                await workflow.Install();
            else
                await workflow.Uninstall();
        }

        private async Task ProcessReadAndWrite(Package package, bool remove)
        {
            var workflow = new ReadAndWriteWorkflow(package, (sender, args) => HandleProgress(args), _logger);

            if (!remove)
                await workflow.Install();
            else
                await workflow.Uninstall();
        }

        private void HandleProgress(ProgressEventArgs progressEventArgs)
        {
            _logger.LogInformation($"Progress: {progressEventArgs.Value}%");
        }
    }
}
