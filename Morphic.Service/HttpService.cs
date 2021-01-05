// Copyright 2020 Raising the Floor - International
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
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Morphic.Core;
using System.Text.Json.Serialization;
using System.Collections.Generic;

#nullable enable

namespace Morphic.Service
{
    /// <summary>
    /// An interface to the Morphic HTTP API
    /// </summary>
    /// <remarks>
    /// The service is implemented via extensions across multiple files
    /// </remarks>
    public class HttpService
    {

        /// <summary>
        /// Create a new service with the given endpoint and session
        /// </summary>
        /// <param name="endpoint">The root URL of the morphic server</param>
        /// <param name="session">The session in which to make requests</param>
        public HttpService(Uri endpoint, IHttpServiceCredentialsProvider credentialsProvider, ILogger<HttpService> logger, HttpClient? client = null)
        {
            Endpoint = endpoint;
            this.credentialsProvider = credentialsProvider;
            this.client = client ?? new HttpClient();
            this.logger = logger;
        }

        /// <summary>
        /// The root URL of the morphic server
        /// </summary>
        public Uri Endpoint { get; }

        private IHttpServiceCredentialsProvider credentialsProvider;

        private ILogger<HttpService> logger;

        #region Requests

        /// <summary>
        /// The underlying HTTP client that makes requests
        /// </summary>
        private readonly HttpClient client;

        /// <summary>
        /// Send a request, re-authenticating if needed
        /// </summary>
        /// <typeparam name="ResponseBody">The type of response expected to be decoded from JSON</typeparam>
        /// <param name="requestFactory">A function that creates the request to send</param>
        /// <remarks>
        /// We use a request factory function in case we need to re-send the request after re-authenticating.
        /// Unfortunately, an HttpRequestMessage can only be sent once.
        /// </remarks>
        /// <returns>The response decoded from JSON, or <code>null</code> if no valid response was provided</returns>
        public async Task<ResponseBody?> Send<ResponseBody>(Func<HttpRequestMessage> requestFactory) where ResponseBody : class
        {
            try
            {
                var request = requestFactory.Invoke();
                logger.LogInformation("{0} {1}", request.Method, request.RequestUri);
                var response = await client.SendAsync(request);
                logger.LogInformation("{0} {1}", response.StatusCode.ToString(), request.RequestUri.AbsolutePath);
                if (response.RequiresMorphicAuthentication())
                {
                    var success = await Authenticate();
                    if (!success)
                    {
                        logger.LogInformation("Could not authenticate user");
                        return null;
                    }
                    request = requestFactory.Invoke();
                    logger.LogInformation("{0} {1}", request.Method, request.RequestUri.AbsolutePath);
                    response = await client.SendAsync(request);
                    logger.LogInformation("{0} {1}", response.StatusCode.ToString(), request.RequestUri.AbsolutePath);
                }
                return await response.GetObject<ResponseBody>();
            }
            catch (BadRequestException e)
            {
                throw e;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Request failed");
                return null;
            }
        }

        /// <summary>
        /// Send a request that expects no response body, re-authenticating if needed
        /// </summary>
        /// <param name="requestFactory">A function that creates the request to send</param>
        /// <remarks>
        /// We use a request factory function in case we need to re-send the request after re-authenticating.
        /// Unfortunately, an HttpRequestMessage can only be sent once.
        /// </remarks>
        /// <returns><code>true</code> if the request succeeds, <code>false</code> otherwise</returns>
        public async Task<bool> Send(Func<HttpRequestMessage> requestFactory)
        {
            try
            {
                var request = requestFactory.Invoke();
                logger.LogInformation("{0} {1}", request.Method, request.RequestUri.AbsolutePath);
                var response = await client.SendAsync(request);
                logger.LogInformation("{0} {1}", response.StatusCode.ToString(), request.RequestUri.AbsolutePath);
                if (response.RequiresMorphicAuthentication())
                {
                    var success = await Authenticate();
                    if (!success)
                    {
                        logger.LogInformation("Could not authenticate user");
                        return false;
                    }
                    request = requestFactory.Invoke();
                    logger.LogInformation("{0} {1}", request.Method, request.RequestUri.AbsolutePath);
                    response = await client.SendAsync(request);
                    logger.LogInformation("{0} {1}", response.StatusCode.ToString(), request.RequestUri.AbsolutePath);
                }
                return response.StatusCode == System.Net.HttpStatusCode.OK;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Request failed");
                return false;
            }
        }

        public class BadRequestException : Exception
        {

            [JsonPropertyName("error")]
            public string Error { get; set; } = null!;

            [JsonPropertyName("details")]
            public Dictionary<string, object?>? Details { get; set; }

        }

        /// <summary>
        /// The service's auth token
        /// </summary>
        public string? AuthToken { get; set; }

        /// <summary>
        /// Authenticate using the current credentials
        /// </summary>
        /// <returns></returns>
        private async Task<bool> Authenticate()
        {
            if (credentialsProvider.CredentialsForHttpService(this) is ICredentials creds)
            {
                var auth = await this.Authenticate(creds);
                if (auth != null)
                {
                    AuthToken = auth.Token;
                    credentialsProvider.HttpServiceAuthenticatedUser(auth.User);
                    return true;
                }
            }
            return false;
        }

        #endregion


    }
}

#nullable disable