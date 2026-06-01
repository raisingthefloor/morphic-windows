// Copyright 2026 Raising the Floor - US, Inc.
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

using System;
using System.Diagnostics;

namespace Morphic.WindowsNative;

internal class TypeValidationUtils
{
    public const string ASSERT_MESSAGE_API_RESULT_TYPE_MISMATCH = "API result type does not match";

    public static void AssertIfNotEnumOfType<T>(T value, Type underlyingType, string? message = null) where T : Enum
    {
        Debug.Assert(TypeValidationUtils.IsEnumOfUnderlyingType(value, underlyingType), message);
    }

    public static bool IsEnumOfUnderlyingType<T>(T value, Type underlyingType) where T: Enum
    {
        // NOTE: if we needed to check the type dynamically (to make sure it's an enum), <Type>.IsEnum can do that.
        // return (value.GetType().IsEnum) && (Enum.GetUnderlyingType(value.GetType()) == underlyingType);
        return Enum.GetUnderlyingType(value.GetType()) == underlyingType;
    }
}
