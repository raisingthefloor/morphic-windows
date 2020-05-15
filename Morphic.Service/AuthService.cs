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
using Morphic.Core;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace Morphic.Service
{
    public static class AuthService
    {

        #region Registration

        /// <summary>
        /// Send a request to create a new user with a username/password pair
        /// </summary>
        /// <param name="service"></param>
        /// <param name="user">The user to create</param>
        /// <param name="usernameCredentials">The user's username credentials</param>
        /// <returns>An authentication token and user information, or <code>null</code> if the request failed</returns>
        public static async Task<AuthResponse?> Register(this HttpService service, User user, UsernameCredentials usernameCredentials)
        {
            var registration = new UsernameRegistration(usernameCredentials, user);
            try
            {
                return await service.Session.Send<AuthResponse>(() => HttpRequestMessageExtensions.Create(service.Session, "v1/register/username", HttpMethod.Post, registration));
            }
            catch (Session.BadRequestException e)
            {
                switch (e.Error)
                {
                    case "existing_username":
                        throw new ExistingUsernameException();
                    case "existing_email":
                        throw new ExistingEmailException();
                    case "malformed_email":
                        throw new InvalidEmailException();
                    case "bad_password":
                        throw new BadPasswordException();
                    case "short_password":
                        throw new BadPasswordException();
                }
            }
            return null;
        }

        /// <summary>
        /// Send a request to create a new user with a secret key
        /// </summary>
        /// <param name="service"></param>
        /// <param name="user">The user to create</param>
        /// <param name="usernameCredentials">The user's key credentials</param>
        /// <returns>An authentication token and user information, or <code>null</code> if the request failed</returns>
        public static async Task<AuthResponse?> Register(this HttpService service, User user, KeyCredentials keyCredentials)
        {
            var registration = new KeyRegistration(keyCredentials, user);
            return await service.Session.Send<AuthResponse>(() => HttpRequestMessageExtensions.Create(service.Session, "v1/register/key", HttpMethod.Post, registration));
        }

        /// <summary>
        /// The format of a request body for username based user registration
        /// </summary>
        private class UsernameRegistration
        {

            public UsernameRegistration(UsernameCredentials credentials, User user)
            {
                Username = credentials.Username;
                Password = credentials.Password;
                Email = credentials.Username;
                FirstName = user.FirstName;
                LastName = user.LastName;
            }

            [JsonPropertyName("username")]
            public string Username { get; set; }
            [JsonPropertyName("password")]
            public string Password { get; set; }
            [JsonPropertyName("email")]
            public string Email { get; set; }
            [JsonPropertyName("first_name")]
            public string? FirstName { get; set; }
            [JsonPropertyName("last_name")]
            public string? LastName { get; set; }
        }

        /// <summary>
        /// The format of a request body for key based user registration
        /// </summary>
        private class KeyRegistration
        {

            public KeyRegistration(KeyCredentials credentials, User user)
            {
                Key = credentials.Key;
                FirstName = user.FirstName;
                LastName = user.LastName;
            }

            [JsonPropertyName("key")]
            public string Key { get; set; }
            [JsonPropertyName("first_name")]
            public string? FirstName { get; set; }
            [JsonPropertyName("last_name")]
            public string? LastName { get; set; }
        }

        public class ExistingUsernameException: Exception
        {
        }

        public class ExistingEmailException : Exception
        {
        }

        public class InvalidEmailException : Exception
        {
        }

        public class BadPasswordException : Exception
        {
        }

        #endregion

        #region Authentication

        /// <summary>
        /// Send the appropriate authentication request for the given credentials
        /// </summary>
        /// <param name="service"></param>
        /// <param name="credentials">The username or key credentials to send</param>
        /// <returns>An authentication token and user information, or <code>null</code> if the request failed</returns>
        public static async Task<AuthResponse?> Authenticate(this HttpService service, ICredentials credentials)
        {
            if (credentials is UsernameCredentials usernameCredentials)
            {
                return await service.AuthenticateUsername(usernameCredentials);
            }
            if (credentials is KeyCredentials keyCredentials)
            {
                return await service.AuthenticateKey(keyCredentials);
            }
            return null;
        }

        /// <summary>
        /// Send a reqeust to authenticate with username based credentials
        /// </summary>
        /// <param name="service"></param>
        /// <param name="usernameCredentials">The username credentials</param>
        /// <returns>An authentication token and user information, or <code>null</code> if the request failed</returns>
        public static async Task<AuthResponse?> AuthenticateUsername(this HttpService service, UsernameCredentials usernameCredentials)
        {
            var body = new AuthUsernameRequest(usernameCredentials.Username, usernameCredentials.Password);
            return await service.Session.Send<AuthResponse>(() => HttpRequestMessageExtensions.Create(service.Session, "v1/auth/username", HttpMethod.Post, body));
        }

        private class AuthUsernameRequest
        {
            [JsonPropertyName("username")]
            public string Username { get; set; }
            [JsonPropertyName("password")]
            public string Password { get; set; }

            public AuthUsernameRequest(string username, string password)
            {
                Username = username;
                Password = password;
            }
        }

        /// <summary>
        /// Send a request to authenticate with key based credentials
        /// </summary>
        /// <param name="service"></param>
        /// <param name="keyCredentials">The key credentials</param>
        /// <returns>An authentication token and user information, or <code>null</code> if the request failed</returns>
        public static async Task<AuthResponse?> AuthenticateKey(this HttpService service, KeyCredentials keyCredentials)
        {
            var body = new AuthKeyRequest(keyCredentials.Key);
            return await service.Session.Send<AuthResponse>(() => HttpRequestMessageExtensions.Create(service.Session, "v1/auth/key", HttpMethod.Post, body));
        }

        private class AuthKeyRequest
        {
            [JsonPropertyName("key")]
            public string Key { get; set; } = "";

            public AuthKeyRequest(string key)
            {
                Key = key;
            }
        }

        #endregion
    }

    /// <summary>
    /// The response from an authentication or registration request
    /// </summary>
    public class AuthResponse
    {
#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
        /// <summary>
        /// The auth token to be sent in subsequent requests in the X-Morphic-Auth-Token header
        /// </summary>
        [JsonPropertyName("token")]
        public string Token { get; set; }

        /// <summary>
        /// The authenticated user's information
        /// </summary>
        [JsonPropertyName("user")]
        public User User { get; set; }
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
    }

}
