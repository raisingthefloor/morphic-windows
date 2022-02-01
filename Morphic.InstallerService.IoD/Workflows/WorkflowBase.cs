using IoDCLI.Cryptography;
using Microsoft.Extensions.Logging;
using Morphic.Core;
using Morphic.InstallerService.Contracts;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace IoDCLI.Workflows
{
    public abstract class WorkflowBase<TError> : IWorkflow<TError> where TError : new()
    {
        public Package Package { get; }
        public ILogger Logger { get; }

        public string LocalFilePath { get; internal set; }

        public EventHandler<ProgressEventArgs> ProgressHandler { get; }

        public WorkflowBase(Package package, EventHandler<ProgressEventArgs> progressHandler, ILogger logger)
        {
            Package = package;
            Logger = logger;

            LocalFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.{GetFileExtension()}");
            ProgressHandler = progressHandler;
        }

        public abstract Task<MorphicResult<bool, TError>> Install();
        public abstract Task<MorphicResult<bool, TError>> Uninstall();

        public abstract string GetFileExtension();

        public void Download()
        {
            using var client = new WebClient();

            if (!new Uri(Package.Url).IsFile)
            {
                Logger.LogInformation($"Downloading {Package.Url} to {LocalFilePath}.");

                client.DownloadFile(Package.Url, LocalFilePath);
            }
            else if (File.Exists(Package.Url))
            {
                LocalFilePath = Package.Url;
            }
        }

        public bool Validate()
        {
            Logger.LogInformation($"Validating '{LocalFilePath}'...");

            var hashProvider = new SHA256HashProvider();
            var fileHash = hashProvider.Hash(LocalFilePath);
            var packageHash = Package.Hash;

            Logger.LogInformation($"Got: {fileHash}, expected: {packageHash}");

            return VerifyHash(packageHash, fileHash);
        }

        public void Cleanup()
        {
            //File.Delete(LocalFilePath);
        }

        private static bool VerifyHash(string hash1, string hash2)
        {
            var comparer = StringComparer.OrdinalIgnoreCase;

            return comparer.Compare(hash1, hash2) == 0;
        }
    }
}
