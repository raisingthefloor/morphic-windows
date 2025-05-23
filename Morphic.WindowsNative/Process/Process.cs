﻿// Copyright 2021-2025 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-windowsnative-lib-cs/blob/main/LICENSE
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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Morphic.WindowsNative.Process;

public class Process
{
     public enum GetPathToExecutableForFileError
     {
          AccessDenied,
          FileNotFound,
          NoAssociatedExecutable,
          OutOfMemoryOrResources,
          PathIsInvalid,
          UnknownShellExecuteErrorCode,
     }
    //
    public static MorphicResult<string, GetPathToExecutableForFileError> GetAssociatedExecutableForFile(string path)
    {
        var pathToExecutableLength = Windows.Win32.PInvoke.MAX_PATH + 1;

        Span<char> pathToExecutableAsChars = new char[(int)pathToExecutableLength];
        nint findExecutableResultAsNint;
        //
        // see: https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-findexecutablew
        var findExecutableResult = Windows.Win32.PInvoke.FindExecutable(path, null, pathToExecutableAsChars);
        if (findExecutableResult is not null && findExecutableResult!.DangerousGetHandle() > 32)
        {
            // success
            // NOTE: we are relying on FindExecutable to return a valid null-terminated string
            var indexOfNullTerminator = pathToExecutableAsChars.IndexOf("\0");
            string pathToExecutable;
            if (indexOfNullTerminator == -1)
            {
                pathToExecutable = new string(pathToExecutableAsChars);
            }
            else
            {
                pathToExecutable = new string(pathToExecutableAsChars.ToArray(), 0, indexOfNullTerminator);
            }
            return MorphicResult.OkResult(pathToExecutable);
        }
        else
        {
            // failure
            // capture failure (result) code
            findExecutableResultAsNint = findExecutableResult!.DangerousGetHandle();
        }
        //
        // if we reach here, we have a failure result
        //
        // failure
        switch (findExecutableResultAsNint)
        {
            case (nint)Windows.Win32.PInvoke.SE_ERR_FNF:
                return MorphicResult.ErrorResult(GetPathToExecutableForFileError.FileNotFound);
            case (nint)Windows.Win32.PInvoke.SE_ERR_PNF:
                return MorphicResult.ErrorResult(GetPathToExecutableForFileError.PathIsInvalid);
            case (nint)Windows.Win32.PInvoke.SE_ERR_ACCESSDENIED:
                return MorphicResult.ErrorResult(GetPathToExecutableForFileError.AccessDenied);
            case (nint)Windows.Win32.PInvoke.SE_ERR_OOM:
                return MorphicResult.ErrorResult(GetPathToExecutableForFileError.OutOfMemoryOrResources);
            case (nint)Windows.Win32.PInvoke.SE_ERR_NOASSOC:
                return MorphicResult.ErrorResult(GetPathToExecutableForFileError.NoAssociatedExecutable);
            default:
                return MorphicResult.ErrorResult(GetPathToExecutableForFileError.UnknownShellExecuteErrorCode);
        }
    }

    //

    public struct StartProcessResult
    {
        public int ProcessId;
    }
    public interface IStartProcessError
    {
        public record NotStarted : IStartProcessError;
        public record Win32Exception(System.ComponentModel.Win32Exception Exception) : IStartProcessError;
    }
    public static MorphicResult<StartProcessResult, IStartProcessError> StartProcess(string path)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo(path)
        {
            UseShellExecute = true
        };
        System.Diagnostics.Process? startedProcess;
        try
        {
            startedProcess = System.Diagnostics.Process.Start(startInfo);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return MorphicResult.ErrorResult<IStartProcessError>(new IStartProcessError.Win32Exception(ex));
        }
        if (startedProcess is not null)
        {
            var result = new StartProcessResult()
            {
                ProcessId = startedProcess!.Id,
            };
            return MorphicResult.OkResult(result);
        }
        else
        {
            return MorphicResult.ErrorResult<IStartProcessError>(new IStartProcessError.NotStarted());
        }
    }

    public interface IStopProcessError
    {
        public record NotRunning : IStopProcessError;
        public record Timeout : IStopProcessError;
        public record Win32Exception(System.ComponentModel.Win32Exception Exception) : IStopProcessError;
    }
    public static async Task<MorphicResult<MorphicUnit, IStopProcessError>> StopProcessWithIdAsync(int processId, TimeSpan maximumWaitBeforeForceStop, bool forceKillAfterMaximumWait)
    {
        System.Diagnostics.Process? runningProcess;
        // see: https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.getprocessbyid
        try
        {
            runningProcess = System.Diagnostics.Process.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
            return MorphicResult.ErrorResult<IStopProcessError>(new IStopProcessError.NotRunning());
        }

        // try to exit the process; if it doesn't exit after 'maximum' time, then force-stop the process
        try
        {
#if DEBUG
            // in debug, we throw OperationCanceledException (since we can't call WAitForExitAsync without elevated (uiAccess or admin) permissions; this will simply kill the process instead (i.e. gracefully degrade)
            throw new OperationCanceledException();
#else
            await runningProcess.WaitForExitAsync(new CancellationTokenSource(maximumWaitBeforeForceStop).Token);
#endif
        }
        catch (OperationCanceledException)
        {
            if (forceKillAfterMaximumWait == false)
            {
                return MorphicResult.ErrorResult<IStopProcessError>(new IStopProcessError.Timeout());
            }

            // if the process doesn't exit after maximumWaitBeforeForceStop, kill the process and all of its subchildren
            // see: https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.kill
            try
            {
                runningProcess.Kill(true);
            }
            catch (InvalidOperationException)
            {
                return MorphicResult.ErrorResult<IStopProcessError>(new IStopProcessError.NotRunning());
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                return MorphicResult.ErrorResult<IStopProcessError>(new IStopProcessError.Win32Exception(ex));
            }
        }

        return MorphicResult.OkResult();
    }

    public interface IGetAllWindowHandlesForProcessError
    {
        public record NotRunning : IGetAllWindowHandlesForProcessError;
    }
    public static MorphicResult<IEnumerable<IntPtr>, IGetAllWindowHandlesForProcessError> GetAllWindowHandlesForProcessWithId(int processId)
    {
        List<IntPtr> handles = [];
        List<int> failedProcessThreads = [];

        System.Diagnostics.ProcessThreadCollection? threadsForProcess;
        try
        {
            threadsForProcess = System.Diagnostics.Process.GetProcessById(processId).Threads;
        }
        catch (ArgumentException)
        {
            return MorphicResult.ErrorResult<IGetAllWindowHandlesForProcessError>(new IGetAllWindowHandlesForProcessError.NotRunning());
        }
        //
        foreach (var threadAsObject in threadsForProcess)
        {
            ProcessThread? thread = threadAsObject as ProcessThread;
            if (thread is not null)
            {
                // NOTE: as we control the enumeration callback and it always returns true, we do not capture or analyze any errors returned by this function call;
                //       a 'false' response here just means that there were no windows to enumerate (in which case we'd correctly return an empty list)
                // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumthreadwindows
                _ = Windows.Win32.PInvoke.EnumThreadWindows((uint)thread.Id, (hWnd, lParam) => { handles.Add(hWnd); return true; }, IntPtr.Zero);
            }
        }

        return MorphicResult.OkResult<IEnumerable<IntPtr>>(handles);
    }

    public interface ICloseAllWindowsError
    {
        public record NotRunning: ICloseAllWindowsError;
        public record PartialFailure(List<IntPtr> SuccessWindowHandles, List<KeyValuePair<IntPtr, IWin32ApiError>> FailureWindows) : ICloseAllWindowsError;
    }
    // NOTE: This function returns a list of closed window handles upon success (or a list of successfully/unsuccessfully closed window handles on partial failure)
    public static MorphicResult<List<IntPtr>, ICloseAllWindowsError> CloseAllWindowsForProcess(int processId)
    {
        IEnumerable<IntPtr> windowHandlesForProcess = [];
        //
        var windowHandlesForProcessResult = GetAllWindowHandlesForProcessWithId(processId);
        if (windowHandlesForProcessResult.IsError)
        {
            switch (windowHandlesForProcessResult.Error!)
            {
                case IGetAllWindowHandlesForProcessError.NotRunning:
                    break;
                default:
                    throw new MorphicUnhandledErrorException();
            }
        }
        windowHandlesForProcess = windowHandlesForProcessResult.Value!;
        //
        List<IntPtr> closedWindowHandles = [];
        List<KeyValuePair<IntPtr, IWin32ApiError>> failureWindowHandles = [];
        foreach (var handle in windowHandlesForProcess)
        {
            var hwnd = new Windows.Win32.Foundation.HWND(handle);
            //
            // see: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendnotifymessagew
            var sendNotifyMessageResult = Windows.Win32.PInvoke.SendNotifyMessage(hwnd, Windows.Win32.PInvoke.WM_CLOSE, 0, 0);
            if (sendNotifyMessageResult != 0)
            {
                closedWindowHandles.Add(handle);
            }
            else
            {
                var win32Error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                failureWindowHandles.Add(new KeyValuePair<nint, IWin32ApiError>(handle, new IWin32ApiError.Win32Error((uint)win32Error)));
            }
        }

        if (failureWindowHandles.Count > 0)
        {
            return MorphicResult.ErrorResult<ICloseAllWindowsError>(new ICloseAllWindowsError.PartialFailure(closedWindowHandles, failureWindowHandles));
        }
        else
        {
            return MorphicResult.OkResult(closedWindowHandles);
        }
    }

    public static bool GetProcessIsRunningByProcessName(string processName)
    {
        var runningProcessNames = Process.GetCurrentProcessNames();
        return runningProcessNames.Contains(processName);
    }

    public static int? GetProcessIdOrNullByProcessName(string processName)
    {
        var allProcessIds = Process.GetProcessIdsByProcessName(processName);

        if (allProcessIds.Count > 0)
        {
            return allProcessIds[0];
        }
        else
        {
            // could not get process id
            return null;
        }
    }

    public static List<int> GetProcessIdsByProcessName(string processName)
    {
        List<int> result = [];

        var processes = System.Diagnostics.Process.GetProcesses();
        foreach (var process in processes)
        {
            // NOTE: we do not check process.HasExited here, as we should never get any exited processes in a fresh process list (i.e. checking the property would consume unnecessary processor cycles)
            if (process.ProcessName == processName)
            {
                result.Add(process.Id);
            }
        }

        return result;
    }

    public static List<string> GetCurrentProcessNames()
    {
        List<string> result = new();

        var processes = System.Diagnostics.Process.GetProcesses();
        foreach (var process in processes)
        {
            // NOTE: we do not check process.HasExited here, as we should never get any exited processes in a fresh process list (i.e. checking the property would consume unnecessary processor cycles)
            var processName = process.ProcessName;
            result.Add(processName);
        }

        return result;
    }
}
