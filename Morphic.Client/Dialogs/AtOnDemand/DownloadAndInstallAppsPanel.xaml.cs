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
                case AtSoftwareInstallMethod.Values.MultipleOfflineInstallers:
                    { 
                        var installResult = await this.InstallMultipleOfflineInstallersAsync(appToInstall);
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

    private async Task<MorphicResult<InstallMsiResult, MorphicUnit>> InstallCustom_FusionAsync(AtSoftwareDetails atSoftwareDetails)
    {
        bool rebootRequired = false;

        int currentStep = 0;
        const int TOTAL_STEP_COUNT = 61;

        // Sentinel System Driver Installer 7.6.1.exe
        var installExeResult = await this.InstallExeAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "prerequisites\\Sentinel System Driver Installer 7.6.1.exe"), "/S /v/qn");
        if (installExeResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT).Invoke(1.0);
        currentStep += 1;

        // vcredist2022_x64.exe
        installExeResult = await this.InstallExeAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "prerequisites\\vcredist2022_x64.exe"), "/install /quiet /norestart");
        if (installExeResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT).Invoke(1.0);
        currentStep += 1;

        // vcredist2022_x86.exe
        installExeResult = await this.InstallExeAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "prerequisites\\vcredist2022_x86.exe"), "/install /quiet /norestart");
        if (installExeResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT).Invoke(1.0);
        currentStep += 1;

        // windowsdesktop-runtime-6.0.11-win-x64.exe
        installExeResult = await this.InstallExeAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "prerequisites\\windowsdesktop-runtime-6.0.11-win-x64.exe"), "/install /quiet /norestart");
        if (installExeResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT).Invoke(1.0);
        currentStep += 1;

        // windowsdesktop-runtime-6.0.11-win-x86.exe
        installExeResult = await this.InstallExeAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "prerequisites\\windowsdesktop-runtime-6.0.11-win-x86.exe"), "/install /quiet /norestart");
        if (installExeResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT).Invoke(1.0);
        currentStep += 1;

        // create registry key
        //// NOTE: we should create a "CreateSubKey" function in Morphic.WindowsNative.Registry.RegistryKey
        //var openSubKeyResult = Morphic.WindowsNative.Registry.CurrentUser.OpenSubKey("SOFTWARE", true);
        //if (openSubKeyResult.IsError == true)
        //{
        //    return MorphicResult.ErrorResult();
        //}
        //var hkcuSoftwareKey = openSubKeyResult.Value!;
        ////
        //var createSubKeyResult = hkcuSoftwareKey.CreateSubKey("Freedom Scientific");
        //if (createSubKeyResult.IsError == true)
        //{
        //    return MorphicResult.ErrorResult();
        //}
        //
        try
        {
            var hkcuSubKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE", true);
            if (hkcuSubKey is null)
            {
                return MorphicResult.ErrorResult();
            }
            else
            {
                bool couldOpenExistingKey = false;
                try
                {
                    var freedomScientificSubKey = hkcuSubKey.OpenSubKey("Freedom Scientific", true);
                    couldOpenExistingKey = true;
                }
                catch
                {
                }
                if (couldOpenExistingKey == false)
                {
                    hkcuSubKey.CreateSubKey("Freedom Scientific", true);
                }
            }
        }
        catch
        {
            return MorphicResult.ErrorResult();
        }

        Dictionary<string, string> msiCommandLineArgs = new();
        msiCommandLineArgs.Add("ARPSYSTEMCOMPONENT", "1");

        // Eloquence.msi
        var progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        var installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "Eloquence.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // VideoAccessibility.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "x64\\VideoAccessibility.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // Utilities.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "x64\\Utilities.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // fsElevation.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "x64\\fsElevation.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // fsSynth.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "x64\\FsSynth.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // UIAHooks.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "x64\\UIAHooks.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // FSReader.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "x64\\FSReader.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // TableOfContents.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "x64\\TableOfContents.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // FSOmnipage.msi (unknown architecture)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "FSOmnipage.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // FSOcr.msi (x86)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "x86\\FSOcr.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // FSOcr.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "x64\\FSOcr.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // FSOcrTombstone (x86)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "x86\\FSOcrTombstone.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // FSOcrTombstone.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "x64\\FSOcrTombstone.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // FSSupportTool.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\FSSupportTool.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // ErrorReporting.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "x64\\ErrorReporting.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // TouchServer.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "x64\\TouchServer.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // FusionInterface.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "x64\\FusionInterface.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // Authorization.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "x64\\Authorization.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // RdpSupport.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\RdpSupport.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // FSCam.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "x64\\FSCam.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // HookManager.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "x64\\HookManager.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // AccEventCache.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\AccEventCache.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // GlobalHooksDispatcher.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\GlobalHooksDispatcher.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // Telemetry.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\Telemetry.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // VoiceAssistant (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\VoiceAssistant.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // JAWSBase.msi (x64)
        msiCommandLineArgs.Add("SETUP_LANGUAGES", "enu");
        msiCommandLineArgs.Add("TANDEM", "1");
        //
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "x64\\JAWSBase.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;
        //
        msiCommandLineArgs.Remove("SETUP_LANGUAGES");
        msiCommandLineArgs.Remove("TANDEM");

        // JAWSLanguage.msi (x64)
        msiCommandLineArgs.Add("PRIMARY_LANGUAGE", "1");
        msiCommandLineArgs.Add("TANDEM", "1");
        //
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\JAWSLanguage.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;
        //
        msiCommandLineArgs.Remove("PRIMARY_LANGUAGE");
        msiCommandLineArgs.Remove("TANDEM");

        // JAWSStart.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\JAWSStart.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // ZoomTextLanguage.msi (arb)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "arb\\x64\\ZoomTextLanguage.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // ZoomTextLanguage.msi (csy)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "csy\\x64\\ZoomTextLanguage.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // ZoomTextLanguage.msi (dan)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "dan\\x64\\ZoomTextLanguage.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // ZoomTextLanguage.msi (deu)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "deu\\x64\\ZoomTextLanguage.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // ZoomTextLanguage.msi (eng)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "eng\\x64\\ZoomTextLanguage.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // ZoomTextLanguage.msi (esn)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "esn\\x64\\ZoomTextLanguage.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // ZoomTextLanguage.msi (fin)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "fin\\x64\\ZoomTextLanguage.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // ZoomTextLanguage.msi (fra)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "fra\\x64\\ZoomTextLanguage.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // ZoomTextLanguage.msi (heb)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "heb\\x64\\ZoomTextLanguage.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // ZoomTextLanguage.msi (hun)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "hun\\x64\\ZoomTextLanguage.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // ZoomTextLanguage.msi (isl)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "isl\\x64\\ZoomTextLanguage.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // ZoomTextLanguage.msi (ita)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "ita\\x64\\ZoomTextLanguage.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // ZoomTextLanguage.msi (jpn)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "jpn\\x64\\ZoomTextLanguage.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // ZoomTextLanguage.msi (kor)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "kor\\x64\\ZoomTextLanguage.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // ZoomTextLanguage.msi (nld)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "nld\\x64\\ZoomTextLanguage.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // ZoomTextLanguage.msi (nor)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "nor\\x64\\ZoomTextLanguage.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // ZoomTextLanguage.msi (plk)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "plk\\x64\\ZoomTextLanguage.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // ZoomTextLanguage.msi (ptb)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "ptb\\x64\\ZoomTextLanguage.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // ZoomTextLanguage.msi (rus)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "rus\\x64\\ZoomTextLanguage.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // ZoomTextLanguage.msi (sky)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "sky\\x64\\ZoomTextLanguage.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // ZoomTextLanguage.msi (sve)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "sve\\x64\\ZoomTextLanguage.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // ZoomTextLanguage.msi (trk)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "trk\\x64\\ZoomTextLanguage.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // KeyboardManager.msi (x86)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "x86\\KeyboardManager.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // VocalizerExpressive-2.2.206-enu-Tom-Compact-enu.msi (unknown architecture)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "VocalizerExpressive-2.2.206-enu-Tom-Compact-enu.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // VocalizerExpressive-2.2.206-enu-Zoe-Compact-enu.msi (unknown architecture)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "VocalizerExpressive-2.2.206-enu-Zoe-Compact-enu.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // ZoomText.msi (enu) (x64)
        msiCommandLineArgs.Add("PRODUCT_TYPE", "2");
        msiCommandLineArgs.Add("PRIMARY_LANGUAGE", "1");
        //
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\ZoomText.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;
        //
        msiCommandLineArgs.Remove("PRODUCT_TYPE");
        msiCommandLineArgs.Remove("PRIMARY_LANGUAGE");

        // Fusion.msi (enu) (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\Fusion.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // FusionBundle.exe
        installExeResult = await this.InstallExeAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "FusionBundle.exe"), "/Type silent");
        if (installExeResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT).Invoke(1.0);
        currentStep += 1;

        var result = new InstallMsiResult() { RebootRequired = rebootRequired };
        return MorphicResult.OkResult(result);
    }

    private async Task<MorphicResult<InstallMsiResult, MorphicUnit>> InstallCustom_JawsAsync(AtSoftwareDetails atSoftwareDetails)
    {
        bool rebootRequired = false;

        int currentStep = 0;
        const int TOTAL_STEP_COUNT = 33;

        // Sentinel System Driver Installer 7.6.1.exe
        var installExeResult = await this.InstallExeAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "prerequisites\\Sentinel System Driver Installer 7.6.1.exe"), "/S /v/qn");
        if (installExeResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT).Invoke(1.0);
        currentStep += 1;

        // vcredist2022_x64.exe
        installExeResult = await this.InstallExeAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "prerequisites\\vcredist2022_x64.exe"), "/install /quiet /norestart");
        if (installExeResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT).Invoke(1.0);
        currentStep += 1;

        // windowsdesktop-runtime-6.0.11-win-x64.exe
        installExeResult = await this.InstallExeAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "prerequisites\\windowsdesktop-runtime-6.0.11-win-x64.exe"), "/install /quiet /norestart");
        if (installExeResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT).Invoke(1.0);
        currentStep += 1;

        // create registry key
        //// NOTE: we should create a "CreateSubKey" function in Morphic.WindowsNative.Registry.RegistryKey
        //var openSubKeyResult = Morphic.WindowsNative.Registry.CurrentUser.OpenSubKey("SOFTWARE", true);
        //if (openSubKeyResult.IsError == true)
        //{
        //    return MorphicResult.ErrorResult();
        //}
        //var hkcuSoftwareKey = openSubKeyResult.Value!;
        ////
        //var createSubKeyResult = hkcuSoftwareKey.CreateSubKey("Freedom Scientific");
        //if (createSubKeyResult.IsError == true)
        //{
        //    return MorphicResult.ErrorResult();
        //}
        //
        try
        {
            var hkcuSubKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE", true);
            if (hkcuSubKey is null)
            {
                return MorphicResult.ErrorResult();
            }
            else
            {
                bool couldOpenExistingKey = false;
                try
                {
                    var freedomScientificSubKey = hkcuSubKey.OpenSubKey("Freedom Scientific", true);
                    couldOpenExistingKey = true;
                }
                catch
                {
                }
                if (couldOpenExistingKey == false)
                {
                    hkcuSubKey.CreateSubKey("Freedom Scientific", true);
                }
            }
        }
        catch
        {
            return MorphicResult.ErrorResult();
        }

        Dictionary<string, string> msiCommandLineArgs = new();
        msiCommandLineArgs.Add("ARPSYSTEMCOMPONENT", "1");

        // Eloquence.msi (x86)
        var progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        var installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x86\\Eloquence.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // VideoAccessibility.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\VideoAccessibility.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // Utilities.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\Utilities.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // FSElevation.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\FSElevation.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // FsSynth.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\FsSynth.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // UIAHooks.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\UIAHooks.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // HookManager.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\HookManager.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // AccEventCache.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\AccEventCache.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // GlobalHooksDispatcher.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\GlobalHooksDispatcher.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // FSReader.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\FSReader.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // TableOfContents.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\TableOfContents.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // FSOmnipage.msi (x86)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x86\\FSOmnipage.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // FSOcr.msi (x86)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x86\\FSOcr.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // FSOcr.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\FSOcr.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // FSOcrTombstone (x86)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x86\\FSOcrTombstone.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // FSOcrTombstone.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\FSOcrTombstone.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // FSSupportTool.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\FSSupportTool.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // ErrorReporting.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\ErrorReporting.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // TouchServer.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\TouchServer.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // FusionInterface.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\FusionInterface.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // Authorization.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\Authorization.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // KeyboardManager.msi (x86)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x86\\KeyboardManager.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // FSCam.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\FSCam.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // RdpSupport.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\RdpSupport.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // Telemetry.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\Telemetry.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // VoiceAssistant (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\VoiceAssistant.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // JAWSBase.msi (x64)
        msiCommandLineArgs.Add("ENABLE_UNTRUSTED_FONTS_EXCEPTION", "1");
        msiCommandLineArgs.Add("SETUP_LANGUAGES", "enu");
        msiCommandLineArgs.Add("TANDEM", "1");
        msiCommandLineArgs.Add("REMOTE_ONLY", "0");
        //
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\JAWSBase.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;
        //
        msiCommandLineArgs.Remove("ENABLE_UNTRUSTED_FONTS_EXCEPTION");
        msiCommandLineArgs.Remove("SETUP_LANGUAGES");
        msiCommandLineArgs.Remove("TANDEM");
        msiCommandLineArgs.Remove("REMOTE_ONLY");

        // JAWSLanguage.msi (x64)
        msiCommandLineArgs.Add("PRIMARY_LANGUAGE", "1");
        msiCommandLineArgs.Add("TANDEM", "1");
        //
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\JAWSLanguage.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;
        //
        msiCommandLineArgs.Remove("PRIMARY_LANGUAGE");
        msiCommandLineArgs.Remove("TANDEM");

        // JAWSStart.msi
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\JAWSStart.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // JAWS setup package.exe
        installExeResult = await this.InstallExeAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "JAWS setup package.exe"), "/Type silent");
        if (installExeResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT).Invoke(1.0);
        currentStep += 1;

        var result = new InstallMsiResult() { RebootRequired = rebootRequired };
        return MorphicResult.OkResult(result);
    }

    private async Task<MorphicResult<InstallMsiResult, MorphicUnit>> InstallCustom_ZoomTextAsync(AtSoftwareDetails atSoftwareDetails)
    {
        bool rebootRequired = false;

        int currentStep = 0;
        const int TOTAL_STEP_COUNT = 22;

        // Sentinel System Driver Installer 7.6.1.exe
        var installExeResult = await this.InstallExeAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "prerequisites\\Sentinel System Driver Installer 7.6.1.exe"), "/S /v/qn");
        if (installExeResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT).Invoke(1.0);
        currentStep += 1;

        // vcredist2022_x64.exe
        installExeResult = await this.InstallExeAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "prerequisites\\vcredist2022_x64.exe"), "/install /quiet /norestart");
        if (installExeResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT).Invoke(1.0);
        currentStep += 1;

        // vcredist2022_x86.exe
        installExeResult = await this.InstallExeAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "prerequisites\\vcredist2022_x86.exe"), "/install /quiet /norestart");
        if (installExeResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT).Invoke(1.0);
        currentStep += 1;

        // windowsdesktop-runtime-6.0.11-win-x64.exe
        installExeResult = await this.InstallExeAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "prerequisites\\windowsdesktop-runtime-6.0.11-win-x64.exe"), "/install /quiet /norestart");
        if (installExeResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT).Invoke(1.0);
        currentStep += 1;

        // windowsdesktop-runtime-6.0.11-win-x86.exe
        installExeResult = await this.InstallExeAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "prerequisites\\windowsdesktop-runtime-6.0.11-win-x86.exe"), "/install /quiet /norestart");
        if (installExeResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT).Invoke(1.0);
        currentStep += 1;

        // create registry key
        //// NOTE: we should create a "CreateSubKey" function in Morphic.WindowsNative.Registry.RegistryKey
        //var openSubKeyResult = Morphic.WindowsNative.Registry.CurrentUser.OpenSubKey("SOFTWARE", true);
        //if (openSubKeyResult.IsError == true)
        //{
        //    return MorphicResult.ErrorResult();
        //}
        //var hkcuSoftwareKey = openSubKeyResult.Value!;
        ////
        //var createSubKeyResult = hkcuSoftwareKey.CreateSubKey("Freedom Scientific");
        //if (createSubKeyResult.IsError == true)
        //{
        //    return MorphicResult.ErrorResult();
        //}
        //
        try
        {
            var hkcuSubKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE", true);
            if (hkcuSubKey is null)
            {
                return MorphicResult.ErrorResult();
            }
            else
            {
                bool couldOpenExistingKey = false;
                try
                {
                    var freedomScientificSubKey = hkcuSubKey.OpenSubKey("Freedom Scientific", true);
                    couldOpenExistingKey = true;
                }
                catch
                {
                }
                if (couldOpenExistingKey == false)
                {
                    hkcuSubKey.CreateSubKey("Freedom Scientific", true);
                }
            }
        }
        catch
        {
            return MorphicResult.ErrorResult();
        }

        Dictionary<string, string> msiCommandLineArgs = new();
        msiCommandLineArgs.Add("ARPSYSTEMCOMPONENT", "1");

        // fsElevation.msi (x64)
        var progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        var installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "x64\\fsElevation.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // FSSupportTool.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\FSSupportTool.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // ErrorReporting.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\ErrorReporting.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // Authorization.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "x64\\Authorization.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // KeyboardManager.msi (unknown architecture)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "KeyboardManager.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // FsSynth.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "x64\\FsSynth.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // RdpSupport.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "x64\\RdpSupport.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // VocalizerExpressive-2.2.206-enu-Tom-Compact-enu.msi (unknown architecture)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "VocalizerExpressive-2.2.206-enu-Tom-Compact-enu.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // VocalizerExpressive-2.2.206-enu-Zoe-Compact-enu.msi (unknown architecture)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "VocalizerExpressive-2.2.206-enu-Zoe-Compact-enu.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // HookManager.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\HookManager.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // Telemetry.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\Telemetry.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // AccEventCache.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\AccEventCache.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // GlobalHooksDispatcher.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\GlobalHooksDispatcher.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // UIAHooks.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\UIAHooks.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // VoiceAssistant.msi (x64)
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\VoiceAssistant.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;

        // ZoomText.msi (enu) (x64)
        msiCommandLineArgs.Add("PRODUCT_TYPE", "2");
        msiCommandLineArgs.Add("PRIMARY_LANGUAGE", "1");
        msiCommandLineArgs.Add("SETUP_LANGUAGES", "enu");
        //
        progressFunction = this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT);
        installMsiResult = await this.InstallMsiAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "enu\\x64\\ZoomText.msi"), msiCommandLineArgs, progressFunction);
        if (installMsiResult.IsError == true) { return MorphicResult.ErrorResult(); }
        if (installMsiResult.Value!.RebootRequired == true) { rebootRequired = true; }
        currentStep += 1;
        //
        msiCommandLineArgs.Remove("PRODUCT_TYPE");
        msiCommandLineArgs.Remove("PRIMARY_LANGUAGE");
        msiCommandLineArgs.Remove("SETUP_LANGUAGES");

        // ZoomTextSetupPackage.exe
        installExeResult = await this.InstallExeAsync(System.IO.Path.Combine(atSoftwareDetails.DownloadUri.LocalPath, "ZoomTextSetupPackage.exe"), "/Type silent");
        if (installExeResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        this.CreateProgressFunction(currentStep, TOTAL_STEP_COUNT).Invoke(1.0);
        currentStep += 1;

        var result = new InstallMsiResult() { RebootRequired = rebootRequired };
        return MorphicResult.OkResult(result);
    }

    private async Task<MorphicResult<InstallMsiResult, MorphicUnit>> InstallMultipleOfflineInstallersAsync(AtSoftwareDetails atSoftwareDetails)
    {
        bool rebootRequired = false;

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            this.InstallStatusLabel.Content = "Installing " + atSoftwareDetails.ProductName + "...";
            this.ProgressBar.Value = 0.0;
            this.ProgressBar.IsIndeterminate = false;
        });

        switch (atSoftwareDetails.ShortName)
        {
            case "fusion":
                {
                    var installResult = await InstallCustom_FusionAsync(atSoftwareDetails);
                    if (installResult.IsError == true) { return MorphicResult.ErrorResult(); }
                    if (installResult.Value!.RebootRequired == true) { rebootRequired = true; }
                }
                break;
            case "jaws":
                {
                    var installResult = await InstallCustom_JawsAsync(atSoftwareDetails);
                    if (installResult.IsError == true) { return MorphicResult.ErrorResult(); }
                    if (installResult.Value!.RebootRequired == true) { rebootRequired = true; }
                }
                break;
            case "zoomtext":
                {
                    var installResult = await InstallCustom_ZoomTextAsync(atSoftwareDetails);
                    if (installResult.IsError == true) { return MorphicResult.ErrorResult(); }
                    if (installResult.Value!.RebootRequired == true) { rebootRequired = true; }
                }
                break;
            default:
                throw new Exception("invalid case");
        }

        var result = new InstallMsiResult() { RebootRequired = rebootRequired };
        return MorphicResult.OkResult(result);
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

