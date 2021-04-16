// Copyright 2021 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt
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
using System.Text;

namespace Morphic.Windows.Native.Process
{
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
        public static IMorphicResult<string, GetPathToExecutableForFileError> GetAssociatedExecutableForFile(string path)
        {
            StringBuilder pathToExecutable = new StringBuilder(ExtendedPInvoke.MAX_PATH + 1);
            var resultCode = ExtendedPInvoke.FindExecutable(path, null, pathToExecutable);
            if (resultCode.ToInt64() > 32)
            {
                // success
                return IMorphicResult<string, GetPathToExecutableForFileError>.SuccessResult(pathToExecutable.ToString());
            }
            else
            {
                // failure
                switch (resultCode.ToInt64())
                {
                    case (Int64)ExtendedPInvoke.ShellExecuteErrorCode.SE_ERR_FNF:
                        return IMorphicResult<string, GetPathToExecutableForFileError>.ErrorResult(GetPathToExecutableForFileError.FileNotFound);
                    case (Int64)ExtendedPInvoke.ShellExecuteErrorCode.SE_ERR_PNF:
                        return IMorphicResult<string, GetPathToExecutableForFileError>.ErrorResult(GetPathToExecutableForFileError.PathIsInvalid);
                    case (Int64)ExtendedPInvoke.ShellExecuteErrorCode.SE_ERR_ACCESSDENIED:
                        return IMorphicResult<string, GetPathToExecutableForFileError>.ErrorResult(GetPathToExecutableForFileError.AccessDenied);
                    case (Int64)ExtendedPInvoke.ShellExecuteErrorCode.SE_ERR_OOM:
                        return IMorphicResult<string, GetPathToExecutableForFileError>.ErrorResult(GetPathToExecutableForFileError.OutOfMemoryOrResources);
                    case (Int64)ExtendedPInvoke.ShellExecuteErrorCode.SE_ERR_NOASSOC:
                        return IMorphicResult<string, GetPathToExecutableForFileError>.ErrorResult(GetPathToExecutableForFileError.NoAssociatedExecutable);
                    default:
                        return IMorphicResult<string, GetPathToExecutableForFileError>.ErrorResult(GetPathToExecutableForFileError.UnknownShellExecuteErrorCode);
                }
            }
        }
    }
}
