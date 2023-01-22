// Copyright 2020-2022 Raising the Floor - US, Inc.
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

namespace Morphic.WindowsNative;

using Morphic.Core;

// NOTE: this type is designed to be returned as the Error type in MorphicResult<TResult, Win32ApiError> function results
public record Win32ApiError : MorphicAssociatedValueEnum<Win32ApiError.Values>
{
    // enum members
    public enum Values
    {
        Win32Error/*(uint win32ErrorCode)*/
    }

    // functions to create member instances
    public static Win32ApiError Win32Error(uint win32ErrorCode) => new(Values.Win32Error) { Win32ErrorCode = win32ErrorCode };

    // associated values
    public uint? Win32ErrorCode { get; private set; }

    // verbatim required constructor implementation for MorphicAssociatedValueEnums
    private Win32ApiError(Values value) : base(value) { }
}
