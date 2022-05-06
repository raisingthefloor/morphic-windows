// Copyright 2021 Raising the Floor - US, Inc.
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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Morphic.Core
{
    // NOTE: MorphicResult is a construct lovingly borrowed from Rust and other languages; in Morphic we use it extensively to return success or failure from 
    //       function calls; we reserve exceptions for unexpected/unhandled conditions (and generally let those exceptions result in a crashlog).

    // NOTE: we declare both a MorphicResult with two generic types (for the return type of functions) and a non-generic MorphicResult with static factory methods
    //       which create an Ok or Error response.  The non-generic MorphicResult actually creates a boxed value which is then implicitly cast to the MorphicResult
    //       with generic types.  This is a bit of compiler magic to keep code simple while preserving full static typing

    // MorphicResult<TValue, TError> is the actual result type returned by functions; instances are to be created via the non-generic MorphicResult type (below)
    public struct MorphicResult<TValue, TError>
    {
        // properties which the caller of a function will check to see if the function succeeded or failed (and the corresponding success/error value)
        public TValue? Value { get; internal set; }
        public TError? Error { get; internal set; }
        public bool IsSuccess { get; internal set; }
        public bool IsError { get; internal set; }

        // implicit conversions from MorphicResultOkValue<TValue> or MorphicResultErrorValue<TError>; by doing this, our caller's code doesn't need to specify
        // either of the return types when the result is returned to the calling code

        // NOTE: we use separate intermediate types (MorphicResultOkValue vs. MorphicResultErrorValue); this is critical to preserve our ability to have both
        //       TValue and TError set to the same type (e.g. MorphicUnit).  The compiler uses these two separate structs to distinguish success from failure results.

        public static implicit operator MorphicResult<TValue, TError>(MorphicResultOkValue<TValue> value)
        {
            return new MorphicResult<TValue, TError>()
            {
                Value = value.Value,
                IsSuccess = true,
                IsError = false
            };
        }

        public static implicit operator MorphicResult<TValue, TError>(MorphicResultErrorValue<TError> error)
        {
            return new MorphicResult<TValue, TError>()
            {
                Error = error.Error,
                IsSuccess = false,
                IsError = true
            };
        }
    }

    // MorphicResult is is the non-generic helper type used when functions want to return a result
    // NOTE: MorphicResult is intentionally declared as a static class (rather than a type) so that nobody tries to create an instance of it accidentally
    public static class MorphicResult
    {
        public static MorphicResultOkValue<TValue> OkResult<TValue>(TValue value)
        {
            return new MorphicResultOkValue<TValue>(value);
        }

        // for Ok results of type MorphicUnit, the overload with no parameters may be called
        public static MorphicResultOkValue<MorphicUnit> OkResult()
        {
            return new MorphicResultOkValue<MorphicUnit>(new MorphicUnit());
        }

        public static MorphicResultErrorValue<TError> ErrorResult<TError>(TError error)
        {
            return new MorphicResultErrorValue<TError>(error);
        }

        // for Error results of type MorphicUnit, the overload with no parameters may be called
        public static MorphicResultErrorValue<MorphicUnit> ErrorResult()
        {
            return new MorphicResultErrorValue<MorphicUnit>(new MorphicUnit());
        }
    }

    // the MorphicResultOkValue generic type is provided so that we implicitly cast Ok return values into dual-generic-typed MorphicResult<,> result types
    public struct MorphicResultOkValue<T>
    {
        public T Value { get; private set; }

        public MorphicResultOkValue(T value)
        {
            this.Value = value;
        }
    }

    // the MorphicResultErrorValue generic type is provided so that we implicitly cast Error return values into dual-generic-typed MorphicResult<,> result types
    public struct MorphicResultErrorValue<T>
    {
        public T Error { get; private set; }

        public MorphicResultErrorValue(T error)
        {
            this.Error = error;
        }
    }
}
