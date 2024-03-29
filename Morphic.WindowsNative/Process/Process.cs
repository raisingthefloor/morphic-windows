﻿// Copyright 2021-2023 Raising the Floor - US, Inc.
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
using System.Text;

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
          StringBuilder pathToExecutable = new StringBuilder(ExtendedPInvoke.MAX_PATH + 1);
          var resultCode = ExtendedPInvoke.FindExecutable(path, null, pathToExecutable);
          if (resultCode.ToInt64() > 32)
          {
               // success
               return MorphicResult.OkResult(pathToExecutable.ToString());
          }
          else
          {
               // failure
               switch (resultCode.ToInt64())
               {
                    case (Int64)ExtendedPInvoke.ShellExecuteErrorCode.SE_ERR_FNF:
                         return MorphicResult.ErrorResult(GetPathToExecutableForFileError.FileNotFound);
                    case (Int64)ExtendedPInvoke.ShellExecuteErrorCode.SE_ERR_PNF:
                         return MorphicResult.ErrorResult(GetPathToExecutableForFileError.PathIsInvalid);
                    case (Int64)ExtendedPInvoke.ShellExecuteErrorCode.SE_ERR_ACCESSDENIED:
                         return MorphicResult.ErrorResult(GetPathToExecutableForFileError.AccessDenied);
                    case (Int64)ExtendedPInvoke.ShellExecuteErrorCode.SE_ERR_OOM:
                         return MorphicResult.ErrorResult(GetPathToExecutableForFileError.OutOfMemoryOrResources);
                    case (Int64)ExtendedPInvoke.ShellExecuteErrorCode.SE_ERR_NOASSOC:
                         return MorphicResult.ErrorResult(GetPathToExecutableForFileError.NoAssociatedExecutable);
                    default:
                         return MorphicResult.ErrorResult(GetPathToExecutableForFileError.UnknownShellExecuteErrorCode);
               }
          }
     }

     public static IEnumerable<IntPtr> GetAllWindowHandlesForProcess(int processId)
     {
          var handles = new List<IntPtr>();

          var threadsForProcess = System.Diagnostics.Process.GetProcessById(processId).Threads;
          foreach (var threadAsObject in threadsForProcess)
          {
               ProcessThread? thread = threadAsObject as ProcessThread;
               if (thread is not null)
               {
                    // NOTE: as we control the enumeration callback and it always returns true, we do not capture or analyze any errors returned by this function call;
                    // //    a 'false' response here would theoretically just mean that there were no windows to enumerate (in which case we'd correctly return an empty list)
                    _ = WindowsApi.EnumThreadWindows(thread.Id, (hWnd, lParam) => { handles.Add(hWnd); return true; }, IntPtr.Zero);
               }
          }

          return handles;
     }

     // NOTE: This function returns MorphicResult.ErrorResult() even if some windows were closed; we may want to add granularity (i.e. "CompletelyFailed" vs "PartiallyFailed" vs "Success") in the future
     public static MorphicResult<MorphicUnit, MorphicUnit> CloseAllWindowsForProcess(int processId)
     {
          var success = true;

          var windowHandlesForProcess = GetAllWindowHandlesForProcess(processId);
          foreach (var handle in windowHandlesForProcess)
          {
               var result = WindowsApi.SendNotifyMessage(handle, WindowsApi.WindowMessages.WM_CLOSE, UIntPtr.Zero, IntPtr.Zero);
               if (result != false)
               {
                    success = false;
               }
          }

          return success ? MorphicResult.OkResult() : MorphicResult.ErrorResult();
     }

     public static string[] GetCurrentProcessNames()
     {
          List<string> result = new();

          var processes = System.Diagnostics.Process.GetProcesses();
          foreach (var process in processes)
          {
               // NOTE: we do not check process.HasExited here, as we should never get any exited processes in a fresh process list (and checking the property will consume unnecessary processor cycles)
               var processName = process.ProcessName;
               result.Add(processName);
          }

          return result.ToArray();
     }
}
