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
using System.Collections.Generic;

namespace Morphic.OAuth.Rfc7591
{
    public struct Rfc7591ClientRegistrationRequestContent
    {
        public List<string>? redirect_uris { get; set; }
        public string? token_endpoint_auth_method { get; set; }
        public List<string>? grant_types { get; set; }
        public List<string>? response_types { get; set; }
        public string? software_id { get; set; }
        public string? software_version { get; set; }
    }

    public struct Rfc7591ClientInformationResponseContent
    {
        public string? client_id { get; set; }
        public string? client_secret { get; set; }
        public ulong? client_id_issued_at { get; set; }
        public ulong? client_secret_expires_at { get; set; }

        //

        public List<string>? redirect_uris { get; set; }
        public string? token_endpoint_auth_method { get; set; }
        public List<string>? grant_types { get; set; }
        public List<string>? response_types { get; set; }
        public string? software_id { get; set; }
        public string? software_version { get; set; }
    }

    public enum Rfc7591ClientRegistrationErrorCodes
    {
        [MorphicStringValue("invalid_redirect_uri")]
        InvalidRedirectUri,
        [MorphicStringValue("invalid_client_metadata")]
        InvalidClientMetadata,
        [MorphicStringValue("invalid_software_statement")]
        InvalidSoftwareStatement,
        [MorphicStringValue("unapproved_software_statement")]
        UnapprovedSoftwareStatement,
    }

    //

    public struct Rfc7591ClientRegistrationErrorResponseContent
    {
        public string? error { get; set; }
        public string? error_description { get; set; }
    }
}
