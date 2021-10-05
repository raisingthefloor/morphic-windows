using Microsoft.Deployment.WindowsInstaller;
using Microsoft.Extensions.Logging;
using Morphic.Core;
using Morphic.InstallerService.Contracts;
using System;
using System.Threading.Tasks;

namespace IoDCLI.Workflows.Msi
{
    public class MsiWorkflow : WorkflowBase<InstallError>
    {
        private const string FileExtension = "msi";

        private bool _active;

        int _progressTotal = 0; // total ticks on progress bar
        int _progress = 0;      // amount of progress
        int _curPos = 0;
        bool _firstTime = true;
        bool _forwardProgress = true; //TRUE if the progress bar control should be incremented in a forward direction
        bool _scriptInProgress = false;
        bool _enableActionData; //TRUE if INSTALLOGMODE_ACTIONDATA messages are sending progress information
        bool _cancelInstall = false; //Should be set to TRUE if the user clicks Cancel button.
        bool _localPackage = false;

        private ExternalUIRecordHandler _handler;
        private EventHandler<ProgressEventArgs> _progressHandler;

        public MsiWorkflow(Package package, EventHandler<ProgressEventArgs> progressHandler, ILogger logger) : base(package, progressHandler, logger)
        {
            _active = false;
            _handler = UIMonitor;
        }

        public MsiWorkflow(Package package, bool localPackage, EventHandler<ProgressEventArgs> progressHandler, ILogger logger) : base(package, progressHandler, logger)
        {
            _active = false;
            _handler = UIMonitor;
            _localPackage = localPackage;

            if (localPackage)
            {
                Logger.LogInformation($"Installing from {package.Url}");
                LocalFilePath = package.Url;
            }
        }

        public override Task<IMorphicResult<bool, InstallError>> Install()
        {
            try
            {
                if (!_localPackage)
                    Download();

                if (Validate())
                {
                    Logger.LogInformation($"Installing {Package.Name}");
                    Installer.SetInternalUI(InstallUIOptions.Silent);
                    Installer.SetExternalUI(_handler,
                        InstallLogModes.ActionData |
                        InstallLogModes.ActionStart |
                        InstallLogModes.CommonData |
                        InstallLogModes.Error |
                        InstallLogModes.ExtraDebug |
                        InstallLogModes.FatalExit |
                        InstallLogModes.FilesInUse |
                        InstallLogModes.Info |
                        InstallLogModes.Initialize |
                        InstallLogModes.OutOfDiskSpace |
                        InstallLogModes.Progress |
                        InstallLogModes.PropertyDump |
                        InstallLogModes.ResolveSource |
                        InstallLogModes.RMFilesInUse |
                        InstallLogModes.ShowDialog |
                        InstallLogModes.Terminate |
                        InstallLogModes.User |
                        InstallLogModes.Verbose |
                        InstallLogModes.Warning
                        );

                    Installer.InstallProduct(LocalFilePath, Package.CommandArguments);
                    Logger.LogInformation($"Completed installing {Package.Name}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"An error has occured.");

                return Task.FromResult(IMorphicResult<bool, InstallError>.ErrorResult(InstallError.MiscFailure));
            }
            finally
            {
                if (!_localPackage)
                {
                    Logger.LogInformation($"Cleaning up {Package.Name}.");
                    Cleanup();
                }
            }

            return Task.FromResult(IMorphicResult<bool, InstallError>.SuccessResult(true));
        }

        public override string GetFileExtension()
        {
            return FileExtension;
        }

        public override Task<IMorphicResult<bool, InstallError>> Uninstall()
        {
            try
            {
                Download();

                if (Validate())
                {
                    Installer.SetInternalUI(InstallUIOptions.Silent);
                    Installer.SetExternalUI(_handler,
                        InstallLogModes.ActionData |
                        InstallLogModes.ActionStart |
                        InstallLogModes.CommonData |
                        InstallLogModes.Error |
                        InstallLogModes.ExtraDebug |
                        InstallLogModes.FatalExit |
                        InstallLogModes.FilesInUse |
                        InstallLogModes.Info |
                        InstallLogModes.Initialize |
                        InstallLogModes.OutOfDiskSpace |
                        InstallLogModes.Progress |
                        InstallLogModes.PropertyDump |
                        InstallLogModes.ResolveSource |
                        InstallLogModes.RMFilesInUse |
                        InstallLogModes.ShowDialog |
                        InstallLogModes.Terminate |
                        InstallLogModes.User |
                        InstallLogModes.Verbose |
                        InstallLogModes.Warning
                        );
                    Installer.InstallProduct(LocalFilePath, $"{Package.CommandArguments} REMOVE=ALL");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"An error has occured.");
            }
            finally
            {
                if(!_localPackage)
                    Cleanup();
            }

            return Task.FromResult(IMorphicResult<bool, InstallError>.SuccessResult(true));
        }

        private void TrackProgress(Record messageRecord)
        {
            if (messageRecord == null || messageRecord.FieldCount == 0)
                return;

            switch (messageRecord.GetInteger(1))
            {
                case 0:
                    //field 1 = 0, field 2 = total number of ticks, field 3 = direction, field 4 = in progress
                    _progressTotal = messageRecord.GetInteger(2);

                    /* determine direction */
                    if (messageRecord.GetInteger(3) == 0)
                        _forwardProgress = true;
                    else // iField[2] == 1
                        _forwardProgress = false;

                    /* get current position of progress bar, depends on direction */
                    // if Forward direction, current position is 0
                    // if Backward direction, current position is Total # ticks
                    _progress = _forwardProgress ? 0 : _progressTotal;

                    _curPos = 0;

                    /* determine new state */
                    // if new state = 1 (script in progress), could send a "Please wait..." msg
                    // new state = 1 means the total # of progress ticks is an estimate, and may not add up correctly
                    _scriptInProgress = (messageRecord.GetInteger(4) == 1) ? true : false;
                    break;
                case 1:
                    //field 1 = 1, field 2 will contain the number of ticks to increment the bar
                    //ignore if field 3 is zero
                    if (messageRecord.GetInteger(3) != 0)
                    {
                        // movement direction determined by g_bForwardProgress set by reset progress msg
                        _enableActionData = true;
                    }
                    else
                    {
                        _enableActionData = false;
                    }
                    break;
                case 2:
                    //field 1 = 2,field 2 will contain the number of ticks the bar has moved
                    // movement direction determined by g_bForwardProgress set by reset progress msg
                    if (0 == _progressTotal)
                        break;

                    _curPos += messageRecord.GetInteger(2);

                    ProgressHandler.Invoke(this, new ProgressEventArgs(100.0 * ((double)_curPos / _progressTotal)));
                    break;
                case 3:
                    break;
            }
        }

        MessageResult UIMonitor(InstallMessage messageType, Record messageRecord, MessageButtons buttons, MessageIcon icon, MessageDefaultButton defaultButton)
        {
            var result = new MessageResult();

            switch (messageType)
            {
                case InstallMessage.Progress:
                    TrackProgress(messageRecord);
                    break;
                default:
                    Logger.LogInformation($"{messageRecord}");
                    break;
            }

            return result;
        }
    }
}
