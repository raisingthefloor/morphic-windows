// Copyright 2020-2023 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-controls-lib-cs/blob/main/LICENSE.txt
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

namespace Morphic.Controls.TrayButton.Windows11;

// NOTE: this type is designed to be returned as the Error type in MorphicResult<TResult, Win32ApiError> function results
internal record CreateNewError : MorphicAssociatedValueEnum<CreateNewError.Values>
{
     // enum members
     public enum Values
     {
          CouldNotCalculateWindowPosition,
          OtherException/*(Exception exception)*/,
          Win32Error/*(uint win32ErrorCode)*/
     }

     // functions to create member instances
     public static CreateNewError CouldNotCalculateWindowPosition => new(Values.CouldNotCalculateWindowPosition);
     public static CreateNewError OtherException(Exception ex) => new(Values.OtherException) { Exception = ex };
     public static CreateNewError Win32Error(uint win32ErrorCode) => new(Values.Win32Error) { Win32ErrorCode = win32ErrorCode };

     // associated values
     public Exception? Exception { get; private init; }
     public uint? Win32ErrorCode { get; private init; }

     // verbatim required constructor implementation for MorphicAssociatedValueEnums
     private CreateNewError(Values value) : base(value) { }
}
