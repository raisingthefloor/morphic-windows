// Copyright 2025 Raising the Floor - US, Inc.
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

using Morphic.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Morphic.Client.Bar.Data.Actions;

public class ApplicationProcessUtils
{
    public interface IStartApplicationError
    {
        public record CannotFindExecutable : IStartApplicationError;
        public record NotStarted : IStartApplicationError;
        public record Win32Exception(System.ComponentModel.Win32Exception Exception) : IStartApplicationError;
    }
    public static MorphicResult<MorphicUnit, IStartApplicationError> StartApplication(string pathToApplication)
    {
        if (pathToApplication is null || System.IO.File.Exists(pathToApplication!) == false)
        {
            return MorphicResult.ErrorResult<IStartApplicationError>(new IStartApplicationError.CannotFindExecutable());
        }

        var startProcessResult = Morphic.WindowsNative.Process.Process.StartProcess(pathToApplication);
        if (startProcessResult.IsError == true)
        {
            switch (startProcessResult.Error!)
            {
                case Morphic.WindowsNative.Process.Process.IStartProcessError.NotStarted:
                    return MorphicResult.ErrorResult<IStartApplicationError>(new IStartApplicationError.NotStarted());
                case Morphic.WindowsNative.Process.Process.IStartProcessError.Win32Exception(var exception):
                    return MorphicResult.ErrorResult<IStartApplicationError>(new IStartApplicationError.Win32Exception(exception));
                default:
                    throw new MorphicUnhandledErrorException();
            }
        }

        return MorphicResult.OkResult();
    }

    public interface IStopApplicationError
    {
        public record NotStarted : IStopApplicationError;
        public record Win32Exception(System.ComponentModel.Win32Exception Exception) : IStopApplicationError;
    }
    public static async Task<MorphicResult<MorphicUnit, IStopApplicationError>> StopApplicationAsync(string processName, TimeSpan timeout)
    {
        // NOTE: we are stopping the process based on its name, so technically this could refer to a _different_ same-process-name app than our target
        //       [it is possible to check the path of the process, but that would require admin permissions; to keep things simple, we have opted to react simply to the task name itself.]

        // get process ID for application
        var processId = Morphic.WindowsNative.Process.Process.GetProcessIdOrNullByProcessName(processName);
        if (processId is null)
        {
            return MorphicResult.ErrorResult<IStopApplicationError>(new IStopApplicationError.NotStarted());
        }

        // try closing all the windows for the process
        var closeResult = Morphic.WindowsNative.Process.Process.CloseAllWindowsForProcess(processId!.Value);
        if (closeResult.IsError == true)
        {
            switch (closeResult.Error!)
            {
                case Morphic.WindowsNative.Process.Process.ICloseAllWindowsError.NotRunning:
                    return MorphicResult.ErrorResult<IStopApplicationError>(new IStopApplicationError.NotStarted());
                case Morphic.WindowsNative.Process.Process.ICloseAllWindowsError.PartialFailure:
                    System.Diagnostics.Debug.WriteLine("Could not close all windows associated with application's process");
                    break;
                default:
                    throw new MorphicUnhandledErrorException();
            }
        }

        // finally, wait for the process to exit
        var stopProcessResult = await Morphic.WindowsNative.Process.Process.StopProcessWithIdAsync(processId!.Value, timeout, true);
        if (stopProcessResult.IsError == true)
        {
            switch (stopProcessResult.Error!)
            {
                case Morphic.WindowsNative.Process.Process.IStopProcessError.NotRunning:
                    // this is fine (as the process may have terminated when the windows were closed; proceed
                    break;
                case Morphic.WindowsNative.Process.Process.IStopProcessError.Win32Exception(var exception):
                    return MorphicResult.ErrorResult<IStopApplicationError>(new IStopApplicationError.Win32Exception(exception));
                default:
                    throw new MorphicUnhandledErrorException();
            }
        }

        return MorphicResult.OkResult();
    }
}
