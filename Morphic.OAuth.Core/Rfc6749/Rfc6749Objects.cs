// Copyright 2021-2022 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-oauthcore-lib-cs/blob/main/LICENSE
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
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Morphic.OAuth.Rfc6749
{
    public struct Rfc6749AccessTokenSuccessfulResponseContent
    {
        public string? access_token { get; set; }
        public string? token_type { get; set; }
        public double? expires_in { get; set; }
        public string? refresh_token { get; set; }
        public string? scope { get; set; }
    }

    public enum Rfc6749AccessTokenErrorResponseErrorCodes
    {
        [MorphicStringValue("invalid_request")]
        InvalidRequest,
        //
        [MorphicStringValue("invalid_client")]
        InvalidClient,
        //
        [MorphicStringValue("invalid_grant")]
        InvalidGrant,
        //
        [MorphicStringValue("unauthorized_client")]
        UnauthorizedClient,
        //
        [MorphicStringValue("unsupported_grant_type")]
        UnsupportedGrantType,
        //
        [MorphicStringValue("invalid_scope")]
        InvalidScope
    }

    //

    public struct Rfc6749AccessTokenErrorResponseContent
    {
        public string? error { get; set; }
        public string? error_description { get; set; }
        public string? error_uri { get; set; }
    }

}