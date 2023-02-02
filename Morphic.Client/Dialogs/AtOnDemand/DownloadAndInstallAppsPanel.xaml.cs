// Copyright 2020-2023 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-windows/blob/master/LICENSE.txt
//
// The R&D leading to these results received funding from the:
// * Rehabilitation Services Administration, US Dept. of Education under
//   grant H421A150006 (APCP)
// * National Institute on Disability, Independent Living, and
//   Rehabilitation Research (NIDILRR)
// * Administration for Independent Living & Dept. of Education under grants
//   H133E080022 (RERC-IT) and H133E130028/90RE5003-01-00 (UIITA-RERC)
// * European Union's Seventh Framework Programme (FP7/2007-2013) grant
//   agreement nos. 289016 (Cloud4all) and 610510 (Prosperity4All)
// * William and Flora Hewlett Foundation
// * Ontario Ministry of Research and Innovation
// * Canadian Foundation for Innovation
// * Adobe Foundation
// * Consumer Electronics Association Foundation

namespace Morphic.Client.Dialogs.AtOnDemand;

using Morphic.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Elements;
using Service;
using System.Runtime.CompilerServices;
using System.Threading;

public partial class DownloadAndInstallAppsPanel : StackPanel, IStepPanel
{
    private readonly MorphicSession morphicSession;
    private readonly IServiceProvider serviceProvider;
    public bool ApplyPreferencesAfterLogin { get; set; } = false;

    internal List<AtSoftwareDetails> AppsToInstall { get; set; } = new();

    Stopwatch _stopwatch;

    public DownloadAndInstallAppsPanel(MorphicSession morphicSession, IServiceProvider serviceProvider)
    {
        _stopwatch = Stopwatch.StartNew();

        this.morphicSession = morphicSession;
        this.serviceProvider = serviceProvider;
        this.InitializeComponent();
    }

    private void Panel_Loaded(object sender, RoutedEventArgs e)
    {
        // start a task to execute all of the installation steps; do not await the async task
        _ = Task.Run(async () =>
        {
            await DoInstallAsync();
        });
    }

    // NOTE: due to a quirk in the current panel transition implementation, we've set a "minimum" amount of time to wait before transitioning--so that two panels don't overlap
    const long MINIMUM_MILLISECONDS_BEFORE_PANEL_TRANSITION = 2000;

    private async Task DoInstallAsync()
    {
        List<AtSoftwareDetails> listOfInstalledApps = new();

        var rebootRequired = false;

        foreach (var appToInstall in this.AppsToInstall)
        {
            var installWasSuccess = false;

            switch (appToInstall.InstallMethod.Value)
            {
                case AtSoftwareInstallMethod.Values.ZipFileWithEmbeddedMsi:
                    {
                        var installResult = await this.InstallZipFileWithEmbeddedMsiAsync(appToInstall);
                        if (installResult.IsSuccess == true)
                        {
                            installWasSuccess = true;

                            if (installResult.Value!.RebootRequired == true)
                            {
                                rebootRequired = true;
                            }
                        }
                    }
                    break;
                default:
                    throw new Exception("invalid case");
            }

            if (installWasSuccess == true)
            {
                listOfInstalledApps.Add(appToInstall);
            }
        }

        // once the installations are complete, wait a moment (in case installation didn't require enough time) and then proceed to the "atod complete" panel
        var waitBeforeTransitionTime = Math.Max(0, MINIMUM_MILLISECONDS_BEFORE_PANEL_TRANSITION - _stopwatch.ElapsedMilliseconds);
        if (waitBeforeTransitionTime > 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds((double)waitBeforeTransitionTime));
        }

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            // transition to the "atod complete" panel
            var atOnDemandCompletePanel = this.StepFrame.PushPanel<Morphic.Client.Dialogs.AtOnDemand.AtOnDemandCompletePanel>();
            atOnDemandCompletePanel.ApplyPreferencesAfterLogin = this.ApplyPreferencesAfterLogin;
            atOnDemandCompletePanel.ListOfInstalledApps = listOfInstalledApps;
            atOnDemandCompletePanel.RebootRequired = rebootRequired;
            atOnDemandCompletePanel.Completed += (o, args) => this.Completed?.Invoke(this, EventArgs.Empty);
        });
    }

    private Action<double> CreateProgressFunction()
    {
        return this.CreateProgressFunction(0, 1);
    }
    //
    private Action<double> CreateProgressFunction(int index, int count)
    {
        SemaphoreSlim inFunctionSemaphore = new SemaphoreSlim(1, 1);

        var result = new Action<double>(async (percentageComplete) =>
        {
            if (percentageComplete == 1.0)
            {
                await inFunctionSemaphore.WaitAsync();
            }
            else
            {
                var acquiredSemaphore = await inFunctionSemaphore.WaitAsync(0);
                if (acquiredSemaphore == false)
                {
                    // for efficiency and to avoid rendering call overflows: if the progress function is already executing, return (unless this is the 100% completion mark)
                    return;
                }
            }
            try
            {
                double relativePercentComplete;
                if (index >= count)
                {
                    relativePercentComplete = 1.0;
                }
                else
                {
                    var percentPerEntry = (double)1.0 / (double)count;
                    //
                    relativePercentComplete = (double)index / (double)count;
                    relativePercentComplete += percentageComplete * percentPerEntry;
                }

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.ProgressBar.Value = relativePercentComplete * 100;
                });
            }
            finally
            {
                inFunctionSemaphore.Release();
            }
        });

        return result;
    }

    private async Task<MorphicResult<InstallMsiResult, MorphicUnit>> InstallZipFileWithEmbeddedMsiAsync(AtSoftwareDetails atSoftwareDetails)
    {
        bool rebootRequired;

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            this.InstallStatusLabel.Content = "Downloading " + atSoftwareDetails.ProductName + "...";
            this.ProgressBar.Value = 0.0;
            this.ProgressBar.IsIndeterminate = false;
        });

        var progressFunction = this.CreateProgressFunction();

        // download the ZIP file
        var downloadFileResult = await AtOnDemandHelpers.DownloadFileAsync(atSoftwareDetails.DownloadUri, progressFunction);
        if (downloadFileResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        var pathToDownloadedFile = downloadFileResult.Value!;

        // unzip the downloaded file into a temporary folder and then delete the file
        var unzipResult = await this.UnzipFileIntoTemporaryFolder(pathToDownloadedFile, true);
        if (unzipResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        var pathToUnzippedContents = unzipResult.Value!;

        // now run the installer; once we're done (successful or not), try to delete the unzipped content folder
        try
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                this.InstallStatusLabel.Content = "Installing " + atSoftwareDetails.ProductName + "...";
                this.ProgressBar.Value = 0.0;
                this.ProgressBar.IsIndeterminate = false;
            });

            var pathToMsi = System.IO.Path.Combine(pathToUnzippedContents, atSoftwareDetails.InstallMethod.PathToMsi!);

            var installResult = await this.InstallMsiAsync(pathToMsi, progressFunction);
            if (installResult.IsError == true)
            {
                return MorphicResult.ErrorResult();
            }
            rebootRequired = installResult.Value!.RebootRequired;
        }
        finally
        {
            try
            {
                System.IO.Directory.Delete(pathToUnzippedContents, true);
            }
            catch
            {
                Debug.Assert(false, "Could not delete the unzipped folder which we created from the downloaded ZIP file");
            }
        }

        // if we reach here, the install was successful
        var result = new InstallMsiResult()
        {
            RebootRequired = rebootRequired
        };
        return MorphicResult.OkResult(result);
    }

    private async Task<MorphicResult<InstallMsiResult, MorphicUnit>> InstallMsiAsync(string pathToMsi, Action<double>? progressFunction)
    {
        // set up the command line settings (i.e. installer properties, etc.)
        var commandLineSettings = new Dictionary<string, string>();

        var result = await this.InstallMsiAsync(pathToMsi, commandLineSettings, progressFunction);
        return result;
    }

    private struct InstallMsiResult
    {
        public bool RebootRequired;
    }
    private async Task<MorphicResult<InstallMsiResult, MorphicUnit>> InstallMsiAsync(string pathToMsi, Dictionary<string, string> commandLineSettings, Action<double>? progressFunction)
    {
        // suppress all reboot prompts and the actual reboots; this will cause the operation to return ERROR_SUCCESS_REBOOT_REQUIRED instead of ERROR_SUCCESS if a reboot is required
        var commandLineSettingsWithRebootSuppression = new Dictionary<string, string>(commandLineSettings);
        commandLineSettingsWithRebootSuppression.Add("REBOOT", "ReallySuppress");

        var windowsInstaller = new AToD.Deployment.MSI.WindowsInstaller();

        // NOTE: for now, we only call the progressComplete callback if progress has increased at least 0.1% since the last callback
        const double MINIMUM_PERCENTAGE_INCREASE_BETWEEN_PROGRESS_CALLBACKS = 0.001;

        double lastPercentageComplete = 0;
        windowsInstaller.ProgressUpdate += (sender, args) =>
        {
            var percentageComplete = args.Percent;
            if (percentageComplete != 0)
            {
                if (percentageComplete > lastPercentageComplete + MINIMUM_PERCENTAGE_INCREASE_BETWEEN_PROGRESS_CALLBACKS)
                {
                    lastPercentageComplete = percentageComplete;

                    _ = Task.Run(() =>
                    {
                        progressFunction?.Invoke(percentageComplete);
                    });
                }
            }
        };

        var installResult = await windowsInstaller.InstallAsync(pathToMsi, commandLineSettingsWithRebootSuppression);
        if (installResult.IsError == true)
        {
            Debug.Assert(false, "AT on Demand was unable to install the application.");
            return MorphicResult.ErrorResult();
        }
        var installResultValue = installResult.Value!;
        var rebootRequired = installResultValue.RebootRequired;

        // otherwise, we succeeded.
        await Task.Run(() =>
        {
            progressFunction?.Invoke(1.0);
        });

        var result = new InstallMsiResult() 
        { 
            RebootRequired = rebootRequired 
        };
        return MorphicResult.OkResult(result);
    }

    private async Task<MorphicResult<MorphicUnit, int?>> InstallExeAsync(string pathToExe, string arguments)
    {
        var startInfo = new ProcessStartInfo(pathToExe, arguments);
        startInfo.Verb = "runas";
        startInfo.UseShellExecute = true;
        Process? process;
        try
        {
            process = System.Diagnostics.Process.Start(startInfo);
        }
        catch
        {
            return MorphicResult.ErrorResult<int?>(null);
        }
        if (process is null)
        {
            return MorphicResult.ErrorResult<int?>(null);
        }
        await process.WaitForExitAsync();

        var exitCode = process.ExitCode;
        if (exitCode != 0)
        {
            Debug.Assert(false, "EXE exited with non-zero status code; make sure this is not a status code indicating a reboot requirement, etc.");
            return MorphicResult.ErrorResult<int?>(exitCode);
        }

        // otherwise, we succeeded.
        return MorphicResult.OkResult();
    }

    // NOTE: this function returns the path to where the designated file was unzipped
    private async Task<MorphicResult<string, MorphicUnit>> UnzipFileIntoTemporaryFolder(string path, bool deleteAfterUnzip)
    {
        // create an empty folder into which we will unzip the installer
        string? unzipToDirectoryPath = null;

        try
        {
            for (var i = 0; i < 100; i++)
            {
                var randomGuid = Guid.NewGuid().ToString("B");
                unzipToDirectoryPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), randomGuid);
                if (System.IO.Directory.Exists(unzipToDirectoryPath) == true)
                {
                    unzipToDirectoryPath = null;
                    continue;
                }
            }
            // if we couldn't generate a unique directory path in a reasonable number of attempts, return an error now
            if (unzipToDirectoryPath is null)
            {
                return MorphicResult.ErrorResult();
            }

            var extractResult = await Task.Run(MorphicResult<MorphicUnit, MorphicUnit> () =>
            {
                try
                {
                    // NOTE: this takes a moment, so we await; it would be ideal to show an "indeterminate" state temporarily
                    System.IO.Compression.ZipFile.ExtractToDirectory(path, unzipToDirectoryPath);
                }
                catch
                {
                    return MorphicResult.ErrorResult();
                }

                return MorphicResult.OkResult();
            });
            if (extractResult.IsError == true)
            {
                return MorphicResult.ErrorResult();
            }
        }
        finally
        {
            // whether or not we were able to extract the ZIP file, delete the ZIP file if requested
            if (deleteAfterUnzip == true)
            {
                try
                {
                    System.IO.File.Delete(path);
                }
                catch
                {
                    Debug.Assert(false, "Could not delete the downloaded ZIP file");
                }
            }
        }

        return MorphicResult.OkResult(unzipToDirectoryPath!);
    }

    #region IStepPanel

    public StepFrame StepFrame { get; set; } = null!;
    public event EventHandler? Completed;
    #endregion

}

