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
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Extensions.Logging;
using Morphic.Core;
using Morphic.Core.Community;
using Morphic.Settings;

namespace Morphic.Service
{

    /// <summary>
    /// Manages a user's session with the morphic server
    /// </summary>
    public class CommunitySession : IHttpServiceCredentialsProvider
    {

        #region Creating a Session

        /// <summary>
        /// Create a new session with the given URL
        /// </summary>
        /// <param name="endpoint">The root URL of the Morphic HTTP service</param>
        public CommunitySession(SessionOptions options, SettingsManager settingsManager, Storage storage, Keychain keychain, IUserSettings userSettings, ILogger<CommunitySession> logger, ILogger<HttpService> httpLogger)
        {
            Service = new HttpService(new Uri(options.Endpoint), this, httpLogger);
            Storage = storage;
            SettingsManager = settingsManager;
            this.keychain = keychain;
            this.logger = logger;
            this.userSettings = userSettings;
        }

        /// <summary>
        /// The unerlying Morphic service this session talks to
        /// </summary>
        public HttpService Service { get; private set; }

        public SettingsManager SettingsManager { get; private set; }

        private readonly Keychain keychain;

        public readonly Storage Storage;

        private readonly IUserSettings userSettings;

        /// <summary>
        /// Open the session by trying to login with the saved user information, if any 
        /// </summary>
        /// <returns>A task that completes when the user information has been fetched</returns>
        public async Task Open()
        {
            logger.LogInformation("Opening Session");
            if (CurrentCredentials is UsernameCredentials credentials)
            {
                await Authenticate(credentials);
            }
        }

        #endregion

        #region Logger

        /// <summary>
        /// The logger for this session
        /// </summary>
        private readonly ILogger<CommunitySession> logger;

        #endregion

        #region Authentication

        /// <summary>
        /// The current user's saved credentials, if any
        /// </summary>
        private ICredentials? CurrentCredentials
        {
            get
            {
                if (CurrentUserId is string userId)
                {
                    if (userSettings.GetUsernameForId(userId) is string username)
                    {
                        if (keychain.LoadUsername(Service.Endpoint, username) is ICredentials credentials)
                        {
                            return credentials;
                        }
                    }
                    return keychain.LoadKey(Service.Endpoint, userId);
                }
                return null;
            }
        }

        public ICredentials? CredentialsForHttpService(HttpService service)
        {
            return CurrentCredentials;
        }

        public void HttpServiceAuthenticatedUser(User user)
        {
            User = user;
        }

        public async Task<bool> Authenticate(UsernameCredentials credentials)
        {
            var auth = await Service.Authenticate(credentials);
            if (auth != null)
            {
                keychain.Save(credentials, Service.Endpoint);
                Service.AuthToken = auth.Token;
                userSettings.SetUsernameForId(credentials.Username, auth.User.Id);
                await Signin(auth.User);
                return true;
            }
            return false;
        }

        #endregion

        #region User Info

        /// <summary>
        /// The current user's id
        /// </summary>
        public string? CurrentUserId
        {
            get
            {
                return userSettings.UserId;
            }
            set
            {
                userSettings.UserId = value;
            }
        }

        /// <summary>
        /// The current user's information
        /// </summary>
        public User? User
        {
            get
            {
                return user;
            }
            set
            {
                user = value;
                CurrentUserId = value?.Id;
            }
        }

        /// <summary>
        /// Field backing for the User property
        /// </summary>
        private User? user;


        public event EventHandler? UserChanged;

        public async Task Signin(User user)
        {
            Bar = null;
            Communities = new UserCommunity[] { };
            User = user;
            await Storage.Save(user);
            var communitiesPage = await Service.FetchUserCommunities(user.Id);
            if (communitiesPage != null)
            {
                Communities = communitiesPage.Communities;
            }
            if (Communities.Length == 1)
            {
                var community = await Service.FetchUserCommunity(user.Id, Communities[0].Id);
                if (community != null)
                {
                    Bar = community.Bar;
                }
            }
            UserChanged?.Invoke(this, new EventArgs());
        }

        public Task Signout()
        {
            User = null;
            UserChanged?.Invoke(this, new EventArgs());
            return Task.CompletedTask;
        }

        #endregion

        #region Communities

        public UserCommunity[] Communities = new UserCommunity[] { };

        public UserBar? Bar;

        #endregion
    }
}
