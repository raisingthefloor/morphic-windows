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

using System;

namespace Morphic.Core
{
    internal interface IMorphicResult<TValue, TError>
    {
        public bool IsSuccess { get; }
        public bool IsError { get; }

        public TValue? Value { get; }
        public TError? Error { get; }
    }

    public class MorphicSuccess<TValue, TError> : IMorphicResult<TValue, TError>
    {
        public bool IsSuccess
        {
            get
            {
                return true;
            }
        }
        public bool IsError
        {
            get
            {
                return false;
            }
        }

        public TValue Value { get; private set; }
        public TError Error
        {
            get
            {
                // this property should not be retrieved on MorphicSuccess
                throw new NotSupportedException();
            }
        }

        public MorphicSuccess(TValue value)
        {
            this.Value = value;
        }
    }

    public class MorphicError<TValue, TError> : IMorphicResult<TValue, TError>
    {
        public bool IsSuccess
        {
            get
            {
                return false;
            }
        }
        public bool IsError
        {
            get
            {
                return true;
            }
        }

        public TValue Value
        {
            get
            {
                // this property should not be retrieved on MorphicError
                throw new NotSupportedException();
            }
        }
        public TError Error { get; private set; }

        public MorphicError(TError error)
        {
            this.Error = error;
        }
    }
}