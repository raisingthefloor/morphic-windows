// Copyright 2021 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-windows/blob/main/LICENSE
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
using Morphic.OAuth.Rfc7591;
using Morphic.OAuth.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace Morphic.OAuth
{
    public class OAuthClient
    {
        public OAuthClient()
        {
        }

        #region "Client Registration API"

        public struct RegisterClientResponse
        {
            public string ClientId { get; set; }
            public string? ClientSecret { get; set; }
            public DateTimeOffset? ClientIdIssuedAt { get; set; }
            public DateTimeOffset? ClientSecretExpiresAt { get; set; }

            //

            public OAuthClientRegistrationMetadata Metadata { get; set; }
        }
        public record RegisterClientError : MorphicAssociatedValueEnum<RegisterClientError.Values>
        {
            // enum members
            public enum Values
            {
                HttpError,
                InvalidSuccessResponse,
                NetworkError,
                OAuthClientRegistrationError,
                Timeout,
                UnsupportedOAuthClientRegistrationError
            }

            // functions to create member instances
            public static RegisterClientError HttpError(HttpStatusCode httpStatusCode) => new RegisterClientError(Values.HttpError) { HttpStatusCode = httpStatusCode };
            public static RegisterClientError InvalidClientInformationResponse(string? responseContent) => new RegisterClientError(Values.InvalidSuccessResponse) { ResponseContent = responseContent };
            public static RegisterClientError NetworkError => new RegisterClientError(Values.NetworkError);
            public static RegisterClientError OAuthClientRegistrationError(Rfc7591ClientRegistrationErrorCodes error, string? errorDescription) => new RegisterClientError(Values.OAuthClientRegistrationError) { Error = error, ErrorDescription = errorDescription };
            public static RegisterClientError Timeout => new RegisterClientError(Values.Timeout);
            public static RegisterClientError UnsupportedOAuthClientRegistrationError(string unsupportedError, string? errorDescription) => new RegisterClientError(Values.UnsupportedOAuthClientRegistrationError) { UnsupportedError = unsupportedError, ErrorDescription = errorDescription };

            // associated values
            public Rfc7591ClientRegistrationErrorCodes? Error { get; private set; }
            public string? ErrorDescription { get; private set; }
            public HttpStatusCode? HttpStatusCode { get; private set; }
            public string? ResponseContent { get; private set; }
            public string? UnsupportedError { get; private set; }

            // verbatim required constructor implementation for MorphicAssociatedValueEnums
            private RegisterClientError(Values value) : base(value) { }
        }
        public static async Task<MorphicResult<RegisterClientResponse, RegisterClientError>> RegisterClientAsync(Uri clientRegistrationEndpointUri, OAuthClientRegistrationMetadata metadata, string? initialAccessToken = null)
        {
            // validate the provided metadata
            // [nothing to validate]

            // assemble our message content
            Rfc7591ClientRegistrationRequestContent requestContent = new();
            //
            // redirect_uris
            requestContent.redirect_uris = metadata.RedirectUris;
            //
            // token_endpoint_auth_method
            requestContent.token_endpoint_auth_method = metadata.TokenEndpointAuthMethod?.ToStringValue()!;
            //
            // grant_types
            if (metadata.GrantTypes is not null)
            {
                requestContent.grant_types = metadata.GrantTypes.Select(x => x.ToStringValue()!).ToList();
            }
            else
            {
                requestContent.grant_types = null;
            }
            //
            // response_types
            if (metadata.ResponseTypes is not null)
            {
                requestContent.response_types = metadata.ResponseTypes.Select(x => x.ToStringValue()!).ToList();
            }
            else
            {
                requestContent.response_types = null;
            }
            // 
            // software_id
            requestContent.software_id = metadata.SoftwareId;
            //
            // software_version
            requestContent.software_version = metadata.SoftwareVersion;

            // convert our request content into JSON
            var requestContentAsJson = JsonSerializer.Serialize(requestContent, new JsonSerializerOptions() { IgnoreNullValues = true });

            // assemble our request message
            //
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, clientRegistrationEndpointUri);
            //
            // set the content (along with the content-type header)
            requestMessage.Content = new StringContent(requestContentAsJson, System.Text.Encoding.UTF8, EncodingUtils.CONTENT_TYPE_APPLICATION_JSON);
            //
            // set the authorization header (if we're using an initial access token)
            if (initialAccessToken is not null)
            {
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Basic", initialAccessToken);
            }
            //
            // NOTE: although the OAuth spec doesn't specify it as a requirement, we set our accept header to "application/json"; its use is illustrated in RFC 7591's examples (including in section 3.1)
            requestMessage.Headers.Accept.Clear();
            requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(EncodingUtils.CONTENT_TYPE_APPLICATION_JSON));

            // send our request (and capture the response)
            using (var httpClient = new HttpClient())
            {
                HttpResponseMessage responseMessage;
                try
                {
                    responseMessage = await httpClient.SendAsync(requestMessage);
                }
                catch (HttpRequestException)
                {
                    // network/http error (connectivity, dns, tls)
                    return MorphicResult.ErrorResult(RegisterClientError.NetworkError);
                }
                catch (TaskCanceledException ex)
                {
                    if (ex.InnerException?.GetType() == typeof(TimeoutException))
                    {
                        // timeout
                        return MorphicResult.ErrorResult(RegisterClientError.Timeout);
                    }
                    else
                    {
                        // we should not have any other TaskCanceledExceptions
                        throw;
                    }
                }

                switch (responseMessage.StatusCode)
                {
                    case HttpStatusCode.Created:
                        {
                            // (successful) client information response
                            var responseContent = await responseMessage.Content.ReadAsStringAsync();

                            if (responseContent is not null)
                            {
                                // verify that the response has a content-type of application/json
                                // NOTE: we do not parse the optional character set; we assume the default character set
                                var responseContentType = responseMessage.Content.Headers.ContentType?.MediaType;
                                if (responseContentType is not null)
                                {
                                    var contentTypeIsApplicationJson = EncodingUtils.VerifyContentTypeIsApplicationJson(responseContentType);
                                    if (contentTypeIsApplicationJson == false)
                                    {
                                        // invalid client information response; return the response content
                                        return MorphicResult.ErrorResult(RegisterClientError.InvalidClientInformationResponse(responseContent));
                                    }
                                }
                                else
                                {
                                    // invalid client information response; return the response content
                                    return MorphicResult.ErrorResult(RegisterClientError.InvalidClientInformationResponse(responseContent));
                                }

                                // deserialize the response content
                                Rfc7591ClientInformationResponseContent clientInformationResponseContent;
                                try
                                {
                                    clientInformationResponseContent = JsonSerializer.Deserialize<Rfc7591ClientInformationResponseContent>(responseContent);
                                }
                                catch
                                {
                                    // invalid client information response; return the response content
                                    return MorphicResult.ErrorResult(RegisterClientError.InvalidClientInformationResponse(responseContent));
                                }

                                RegisterClientResponse result = new();
                                //
                                // ClientId
                                if (clientInformationResponseContent.client_id is not null)
                                {
                                    result.ClientId = clientInformationResponseContent.client_id;
                                }
                                else
                                {
                                    // invalid client information response; return the response content
                                    return MorphicResult.ErrorResult(RegisterClientError.InvalidClientInformationResponse(responseContent));
                                }
                                //
                                // ClientSecret
                                result.ClientSecret = clientInformationResponseContent.client_secret;
                                //
                                // ClientIdIssuedAt
                                if (clientInformationResponseContent.client_id_issued_at is not null)
                                {
                                    if (clientInformationResponseContent.client_id_issued_at! > long.MaxValue)
                                    {
                                        Debug.Assert(false, "Metadata value 'client_id_issued_at' is out of the range of long.MinValue and long.MaxValue");
                                        result.ClientIdIssuedAt = null;
                                    }
                                    else
                                    {
                                        result.ClientIdIssuedAt = DateTimeOffset.FromUnixTimeSeconds((long)clientInformationResponseContent.client_id_issued_at!);
                                    }
                                }
                                else
                                {
                                    result.ClientIdIssuedAt = null;
                                }
                                //
                                // ClientSecretExpiresAt
                                if (clientInformationResponseContent.client_secret_expires_at is not null)
                                {
                                    if (clientInformationResponseContent.client_secret_expires_at! > long.MaxValue)
                                    {
                                        Debug.Assert(false, "Metadata value 'client_secret_expires_at' is out of the range of long.MinValue and long.MaxValue");
                                        result.ClientSecretExpiresAt = null;
                                    }
                                    else
                                    {
                                        result.ClientSecretExpiresAt = clientInformationResponseContent.client_secret_expires_at! != 0 ? DateTimeOffset.FromUnixTimeSeconds((long)clientInformationResponseContent.client_secret_expires_at!) : null;
                                    }
                                }
                                else
                                {
                                    result.ClientSecretExpiresAt = null;
                                }
                                //
                                // Metadata
                                // NOTE: this implementation omits all unknown metadata fields; it also omits all unknown values used in known metadata fields
                                result.Metadata = new()
                                {
                                    // RedirectUris
                                    RedirectUris = clientInformationResponseContent.redirect_uris,
                                    //
                                    // TokenEndpointAuthMethod
                                    // NOTE: if the response indicates a token endpoint auth method which we don't understand, this will be set to null
                                    TokenEndpointAuthMethod = clientInformationResponseContent.token_endpoint_auth_method is not null ?
                                        MorphicEnum<OAuthTokenEndpointAuthMethod>.FromStringValue(clientInformationResponseContent.token_endpoint_auth_method!) :
                                        null,
                                    //
                                    // GrantTypes
                                    // NOTE: if the response indicates grant type(s) which we don't understand, we will not include them in our list; additionally, if there are entries but we don't understand any of them then the list will be an empty array (rather than null)
                                    GrantTypes = clientInformationResponseContent.grant_types is not null ?
                                        clientInformationResponseContent.grant_types!.Select(x => MorphicEnum<OAuthGrantType>.FromStringValue(x)).Where(x => x is not null).Select(x => x!.Value).ToList() :
                                        null,
                                    //
                                    // ResponseTypes
                                    // NOTE: if the response indicates response type(s) which we don't understand, we will not include them in our list; additionally, if there are entries but we don't understand any of them then the list will be an empty array (rather than null)
                                    ResponseTypes = clientInformationResponseContent.response_types is not null ?
                                        clientInformationResponseContent.response_types!.Select(x => MorphicEnum<OAuthResponseType>.FromStringValue(x)).Where(x => x is not null).Select(x => x!.Value).ToList() :
                                        null,
                                    //
                                    // SoftwareId
                                    SoftwareId = clientInformationResponseContent.software_id,
                                    //
                                    // SoftwareVersion
                                    SoftwareVersion = clientInformationResponseContent.software_version
                                };

                                return MorphicResult.OkResult(result);
                            }
                            else
                            {
                                // invalid oauth client information response; return the response content
                                return MorphicResult.ErrorResult(RegisterClientError.InvalidClientInformationResponse(null /* responseContent */));
                            }
                        }
                    case HttpStatusCode.BadRequest:
                        {
                            var responseContent = await responseMessage.Content.ReadAsStringAsync();

                            // verify that the response has a content-type of application/json
                            // NOTE: we do not parse the optional character set; we assume the default character set
                            var responseContentType = responseMessage.Content.Headers.ContentType?.MediaType;
                            if (responseContentType is not null)
                            {
                                var contentTypeIsApplicationJson = EncodingUtils.VerifyContentTypeIsApplicationJson(responseContentType);
                                if (contentTypeIsApplicationJson == false)
                                {
                                    // invalid client registration error response; return the http error code
                                    return MorphicResult.ErrorResult(RegisterClientError.HttpError(responseMessage.StatusCode));
                                }
                            }
                            else
                            {
                                // invalid client registration error response; return the http error code
                                return MorphicResult.ErrorResult(RegisterClientError.HttpError(responseMessage.StatusCode));
                            }

                            // deserialize the response content
                            if (responseContent is not null)
                            {
                                Rfc7591ClientRegistrationErrorResponseContent errorResponseContent;
                                try
                                {
                                    errorResponseContent = JsonSerializer.Deserialize<Rfc7591ClientRegistrationErrorResponseContent>(responseContent);
                                }
                                catch
                                {
                                    // invalid client registration error response; just return the http status code (as it's not an OAuth error)
                                    return MorphicResult.ErrorResult(RegisterClientError.HttpError(responseMessage.StatusCode));
                                }

                                Rfc7591ClientRegistrationErrorCodes? error = null;
                                if (errorResponseContent.error is not null)
                                {
                                    error = MorphicEnum<Rfc7591ClientRegistrationErrorCodes>.FromStringValue(errorResponseContent.error);
                                    if (error is null)
                                    {
                                        // missing or unknown client registration error code
                                        return MorphicResult.ErrorResult(RegisterClientError.UnsupportedOAuthClientRegistrationError(errorResponseContent.error, errorResponseContent.error_description));
                                    }
                                }
                                else
                                {
                                    // if we did not get a valid response, return the HTTP error (as it's not an OAuth error)
                                    return MorphicResult.ErrorResult(RegisterClientError.HttpError(responseMessage.StatusCode));
                                }

                                return MorphicResult.ErrorResult(RegisterClientError.OAuthClientRegistrationError(error.Value, errorResponseContent.error_description));
                            }
                            else
                            {
                                // if we did not get a valid response, return the HTTP error (as it's not an OAuth error)
                                return MorphicResult.ErrorResult(RegisterClientError.HttpError(responseMessage.StatusCode));
                            }
                        }
                    default:
                        return MorphicResult.ErrorResult(RegisterClientError.HttpError(responseMessage.StatusCode));
                }
            }
        }

        #endregion "Client Registration API"

    }
}
