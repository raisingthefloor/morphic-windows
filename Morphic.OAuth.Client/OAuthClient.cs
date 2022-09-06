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
using Morphic.OAuth.Rfc6749;
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
        public string? ClientId { get; private set; } = null;
        public string? ClientSecret { get; private set; } = null;

        private OAuthTokenEndpointAuthMethod _tokenEndpointAuthMethod;
        public OAuthTokenEndpointAuthMethod TokenEndpointAuthMethod
        {
            get
            {
                return _tokenEndpointAuthMethod;
            }
            set
            {
                if (this.ClientSecret is null)
                {
                    switch (value)
                    {
                        case OAuthTokenEndpointAuthMethod.None:
                            // this is fine
                            break;
                        case OAuthTokenEndpointAuthMethod.ClientSecretBasic:
                        case OAuthTokenEndpointAuthMethod.ClientSecretPost:
                            throw new ArgumentOutOfRangeException("OAuth clients without client secrets cannot use this token endpoint auth method");
                        default:
                            throw new Exception("invalid code path");
                    }
                }
                else /* if (this.ClientSecret is not null) */
                {
                    switch (value)
                    {
                        case OAuthTokenEndpointAuthMethod.ClientSecretBasic:
                        case OAuthTokenEndpointAuthMethod.ClientSecretPost:
                            // these are fine
                            break;
                        case OAuthTokenEndpointAuthMethod.None:
                            throw new ArgumentOutOfRangeException("OAuth clients with client secrets cannot use this token endpoint auth method");
                        default:
                            throw new Exception("invalid code path");
                    }
                }

                _tokenEndpointAuthMethod = value;
            }
        }

        public OAuthClient()
        {
            this.TokenEndpointAuthMethod = OAuthTokenEndpointAuthMethod.None;
        }

        public OAuthClient(string clientId)
        {
            this.ClientId = clientId;
            //
            // default token endpoint auth method for a client with an clientId is ClientSecretBasic
            this.TokenEndpointAuthMethod = OAuthTokenEndpointAuthMethod.ClientSecretBasic;
        }

        public OAuthClient(string clientId, string clientSecret)
        {
            this.ClientId = clientId;
            this.ClientSecret = clientSecret;
            //
            // default token endpoint auth method for a client with an clientId is ClientSecretBasic
            this.TokenEndpointAuthMethod = OAuthTokenEndpointAuthMethod.ClientSecretBasic;
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
                InvalidClientInformationResponse,
                NetworkError,
                OAuthClientRegistrationError,
                Timeout,
                Unauthorized,
                UnsupportedOAuthClientRegistrationError
            }

            // functions to create member instances
            public static RegisterClientError HttpError(HttpStatusCode httpStatusCode) => new RegisterClientError(Values.HttpError) { HttpStatusCode = httpStatusCode };
            public static RegisterClientError InvalidClientInformationResponse(string? responseContent) => new RegisterClientError(Values.InvalidClientInformationResponse) { ResponseContent = responseContent };
            public static RegisterClientError NetworkError => new RegisterClientError(Values.NetworkError);
            public static RegisterClientError OAuthClientRegistrationError(Rfc7591ClientRegistrationErrorCodes error, string? errorDescription) => new RegisterClientError(Values.OAuthClientRegistrationError) { Error = error, ErrorDescription = errorDescription };
            public static RegisterClientError Timeout => new RegisterClientError(Values.Timeout);
            public static RegisterClientError UnsupportedOAuthClientRegistrationError(string unsupportedError, string? errorDescription) => new RegisterClientError(Values.UnsupportedOAuthClientRegistrationError) { UnsupportedError = unsupportedError, ErrorDescription = errorDescription };
            public static RegisterClientError Unauthorized => new RegisterClientError(Values.Unauthorized);

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
            Rfc7591.Rfc7591ClientRegistrationRequestContent requestContent = new();
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
                                    clientInformationResponseContent = JsonSerializer.Deserialize<Rfc7591.Rfc7591ClientInformationResponseContent>(responseContent);
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
                    case HttpStatusCode.Unauthorized:
                        {
                            // this would typically occur when an unauthorized initial access token was provided
                            return MorphicResult.ErrorResult(RegisterClientError.Unauthorized);
                        }
                    default:
                        return MorphicResult.ErrorResult(RegisterClientError.HttpError(responseMessage.StatusCode));
                }
            }
        }

        #endregion "Client Registration API"


        #region "Token Auth API"

        public struct RequestAccessTokenResponse
        {
            public string AccessToken;
            public string TokenType;
            public double? ExpiresIn;
            public string? RefreshToken;
            public string? Scope;
        }
        public record RequestAccessTokenError : MorphicAssociatedValueEnum<RequestAccessTokenError.Values>
        {
            // enum members
            public enum Values
            {
                HttpError,
                InvalidAccessTokenResponse,
                NetworkError,
                OAuthAccessTokenRequestError,
                Timeout,
                Unauthorized,
                UnsupportedOAuthAccessTokenErrorResponseError
            }

            // functions to create member instances
            public static RequestAccessTokenError HttpError(HttpStatusCode httpStatusCode) => new RequestAccessTokenError(Values.HttpError) { HttpStatusCode = httpStatusCode };
            public static RequestAccessTokenError InvalidAccessTokenResponse(string? responseContent) => new RequestAccessTokenError(Values.InvalidAccessTokenResponse) { ResponseContent = responseContent };
            public static RequestAccessTokenError NetworkError => new RequestAccessTokenError(Values.NetworkError);
            public static RequestAccessTokenError OAuthAccessTokenRequestError(Rfc6749AccessTokenErrorResponseErrorCodes error, string? errorDescription, string? errorUri) => new RequestAccessTokenError(Values.OAuthAccessTokenRequestError) { Error = error, ErrorDescription = errorDescription, ErrorUri = errorUri };
            public static RequestAccessTokenError Timeout => new RequestAccessTokenError(Values.Timeout);
            public static RequestAccessTokenError Unauthorized => new RequestAccessTokenError(Values.Unauthorized);
            public static RequestAccessTokenError UnsupportedOAuthAccessTokenErrorResponseError(string unsupportedError, string? errorDescription, string? errorUri) => new RequestAccessTokenError(Values.UnsupportedOAuthAccessTokenErrorResponseError) { UnsupportedError = unsupportedError, ErrorDescription = errorDescription, ErrorUri = errorUri };

            // associated values
            public Rfc6749AccessTokenErrorResponseErrorCodes? Error { get; private set; }
            public string? ErrorDescription { get; private set; }
            public string? ErrorUri { get; private set; }
            public HttpStatusCode? HttpStatusCode { get; private set; }
            public string? ResponseContent { get; private set; }
            public string? UnsupportedError { get; private set; }

            // verbatim required constructor implementation for MorphicAssociatedValueEnums
            private RequestAccessTokenError(Values value) : base(value) { }
        }
        public async Task<MorphicResult<RequestAccessTokenResponse, RequestAccessTokenError>> RequestAccessTokenUsingClientCredentialsGrantAsync(Uri tokenEndpointUri, string? scope)
        {
            // per RFC 6749 Section 2.3.1, all token requests using a password (i.e. a client secret) must be secured via TLS
            if (tokenEndpointUri.Scheme.ToLowerInvariant() != "https")
            {
                throw new ArgumentException("Argument \"tokenEndpointUri\" must be secured via https; ClientSecrets may not be transmitted in cleartext", nameof(tokenEndpointUri));
            }

            // assemble our message's content
            var postParameters = new List<KeyValuePair<string?, string?>>();
            postParameters.Add(new KeyValuePair<string?, string?>("grant_type", "client_credentials"));
            if (scope is not null)
            {
                postParameters.Add(new KeyValuePair<string?, string?>("scope", scope));
            }

            string? encodedClientIdAndSecret = null;

            switch (this.TokenEndpointAuthMethod)
            {
                case OAuthTokenEndpointAuthMethod.ClientSecretPost:
                    // if our token endpoint auth method is ClientSecretPost, then encode the client id and client secret (which must be present) as post parameters
                    postParameters.Add(new KeyValuePair<string?, string?>("client_id", this.ClientId!));
                    postParameters.Add(new KeyValuePair<string?, string?>("client_secret", this.ClientSecret!));
                    break;
                case OAuthTokenEndpointAuthMethod.ClientSecretBasic:
                    // if our token endpoint auth method is ClientSecretBasic, then encode the client id and client secret for the Authorization header
                    encodedClientIdAndSecret = Utils.EncodingUtils.EncodeUsernameAndPasswordForOAuthBasicAuthorization(this.ClientId!, this.ClientSecret!);
                    break;
                case OAuthTokenEndpointAuthMethod.None:
                    throw new InvalidOperationException("To use this grant type to request an access token, a client's TokenEndpointAuthMethod must support transmitting the client secret");
                default:
                    throw new Exception("invalid code path");
            }

            // assemble our request message
            //
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, tokenEndpointUri);
            //
            // set the content (along with the content-type header)
            requestMessage.Content = new FormUrlEncodedContent(postParameters);
            //
            // set the authorization header (if we're using the ClientSecretBasic token endpoint auth method)
            if (encodedClientIdAndSecret is not null)
            {
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Basic", encodedClientIdAndSecret);
            }
            //
            // NOTE: although the OAuth spec doesn't specify it as a requirement, we set our accept header to "application/json"; if this causes troubles in production we can remove it
            // set the Accept header
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
                    return MorphicResult.ErrorResult(RequestAccessTokenError.NetworkError);
                }
                catch (TaskCanceledException ex)
                {
                    if (ex.InnerException?.GetType() == typeof(TimeoutException))
                    {
                        // timeout
                        return MorphicResult.ErrorResult(RequestAccessTokenError.Timeout);
                    }
                    else
                    {
                        // we should not have any other TaskCanceledExceptions
                        throw;
                    }
                }

                switch (responseMessage.StatusCode)
                {
                    case HttpStatusCode.OK:
                        {
                            // (successful) response
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
                                        // invalid access token response; return the response content
                                        return MorphicResult.ErrorResult(RequestAccessTokenError.InvalidAccessTokenResponse(responseContent));
                                    }
                                }
                                else
                                {
                                    // invalid access token response; return the response content
                                    return MorphicResult.ErrorResult(RequestAccessTokenError.InvalidAccessTokenResponse(responseContent));
                                }

                                // deserialize the response content
                                Rfc6749AccessTokenSuccessfulResponseContent successfulResponse;
                                try
                                {
                                    successfulResponse = JsonSerializer.Deserialize<Rfc6749AccessTokenSuccessfulResponseContent>(responseContent);
                                }
                                catch
                                {
                                    // invalid access token response; return the response content
                                    return MorphicResult.ErrorResult(RequestAccessTokenError.InvalidAccessTokenResponse(responseContent));
                                }

                                RequestAccessTokenResponse result = new();
                                // AccessToken
                                if (successfulResponse.access_token is not null)
                                {
                                    result.AccessToken = successfulResponse.access_token!;
                                }
                                else
                                {
                                    // invalid access token response; return the response content
                                    return MorphicResult.ErrorResult(RequestAccessTokenError.InvalidAccessTokenResponse(responseContent));
                                }
                                // TokenType
                                if (successfulResponse.token_type is not null)
                                {
                                    result.TokenType = successfulResponse.token_type!;
                                }
                                else
                                {
                                    // invalid access token response; return the response content
                                    return MorphicResult.ErrorResult(RequestAccessTokenError.InvalidAccessTokenResponse(responseContent));
                                }
                                // ExpiresIn
                                result.ExpiresIn = successfulResponse.expires_in;
                                // RefreshToken
                                result.RefreshToken = successfulResponse.refresh_token;
                                // Scope
                                result.Scope = successfulResponse.scope;

                                return MorphicResult.OkResult(result);
                            }
                            else
                            {
                                // invalid access token response; return the response content
                                return MorphicResult.ErrorResult(RequestAccessTokenError.InvalidAccessTokenResponse(null /* responseContent */));
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
                                    // invalid oauth error response; return the http error code
                                    return MorphicResult.ErrorResult(RequestAccessTokenError.HttpError(responseMessage.StatusCode));
                                }
                            }
                            else
                            {
                                // invalid oauth error response; return the http error code
                                return MorphicResult.ErrorResult(RequestAccessTokenError.HttpError(responseMessage.StatusCode));
                            }

                            // deserialize the response content
                            if (responseContent is not null)
                            {
                                Rfc6749AccessTokenErrorResponseContent errorResponse;
                                try
                                {
                                    errorResponse = JsonSerializer.Deserialize<Rfc6749AccessTokenErrorResponseContent>(responseContent);
                                }
                                catch
                                {
                                    // invalid oauth error response; just return the http status code (as it's not an OAuth error)
                                    return MorphicResult.ErrorResult(RequestAccessTokenError.HttpError(responseMessage.StatusCode));
                                }

                                Rfc6749AccessTokenErrorResponseErrorCodes? error = null;
                                if (errorResponse.error is not null)
                                {
                                    error = MorphicEnum<Rfc6749AccessTokenErrorResponseErrorCodes>.FromStringValue(errorResponse.error);
                                    if (error is null)
                                    {
                                        // missing or unknown oauth error code
                                        return MorphicResult.ErrorResult(RequestAccessTokenError.UnsupportedOAuthAccessTokenErrorResponseError(errorResponse.error, errorResponse.error_description, errorResponse.error_uri));
                                    }
                                }
                                else
                                {
                                    // if we did not get a valid response, return the HTTP error (as it's not an OAuth error)
                                    return MorphicResult.ErrorResult(RequestAccessTokenError.HttpError(responseMessage.StatusCode));
                                }

                                return MorphicResult.ErrorResult(RequestAccessTokenError.OAuthAccessTokenRequestError(error.Value, errorResponse.error_description, errorResponse.error_uri));
                            }
                            else
                            {
                                // if we did not get a valid response, return the HTTP error (as it's not an OAuth error)
                                return MorphicResult.ErrorResult(RequestAccessTokenError.HttpError(responseMessage.StatusCode));
                            }
                        }
                    case HttpStatusCode.Unauthorized:
                        {
                            // this would typically occur when an unauthorized client id + secret was provided
                            return MorphicResult.ErrorResult(RequestAccessTokenError.Unauthorized);
                        }
                    default:
                        return MorphicResult.ErrorResult(RequestAccessTokenError.HttpError(responseMessage.StatusCode));
                }
            }
        }

        #endregion "Token Auth API"

    }
}
