// Copyright 2021 Raising the Floor - US, Inc.
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

namespace Morphic.OAuth
{
    public enum OAuthGrantType
    {
        [MorphicStringValue("authorization_code")]
        AuthorizationCode, // (default)
        [MorphicStringValue("implicit")]
        Implicit,          // (for web browsers, etc.)
        [MorphicStringValue("password")]
        Password,          // resource owner password credentials (for trusted first-party clients, etc.)
        [MorphicStringValue("client_credentials")]
        ClientCredentials, // (for server to server flow, manually-entered OAuth credentials, etc.)
        [MorphicStringValue("refresh_token")]
        RefreshToken,      // (for refreshing tokens)
        [MorphicStringValue("urn:ietf:params:oauth:grant-type:jwt-bearer")]
        JwtBearer,         // see RFC7523
        [MorphicStringValue("urn:ietf:params:oauth:grant-type:saml2-bearer")]
        Saml2Bearer,        // see RFC7522
    }
}