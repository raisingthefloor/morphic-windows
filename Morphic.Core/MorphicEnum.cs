// Copyright 2021 Raising the Floor - International
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

using System;
using System.Reflection;

public partial class MorphicEnum<TEnum> where TEnum : struct, Enum
{
    public static bool IsMember(TEnum value)
    {
        return (typeof(TEnum).GetEnumName(value) is not null);
    }

    public static TEnum? FromStringValue(string stringValue, StringComparison comparisonType = StringComparison.Ordinal)
    {
        foreach (TEnum member in System.Enum.GetValues(typeof(TEnum)))
        {
            var memberName = typeof(TEnum).GetEnumName(member);
            //
            var fieldInfo = typeof(TEnum).GetField(memberName!);
            //
            var attribute = fieldInfo!.GetCustomAttribute<Morphic.Core.MorphicStringValueAttribute>();
            if (attribute is null)
            {
                // this enum member does not have a string value
                continue;
            }

            if (attribute.StringValue.Equals(stringValue, comparisonType))
            {
                return member;
            }
        }

        // if we could not find the member (i.e. the member with the supplied string value does not exist), return null
        return null;
    }
}