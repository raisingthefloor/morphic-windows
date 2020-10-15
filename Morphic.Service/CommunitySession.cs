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

namespace Morphic.Service
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Morphic.Core;
    using Morphic.Core.Community;
    using System.Linq;

    /// <summary>
    /// Manages a user's session with the morphic server
    /// </summary>
    public class CommunitySession : Session
    {

        #region Creating a Session

        /// <summary>
        /// Create a new session with the given URL
        /// </summary>
        public CommunitySession(SessionOptions options, Storage storage, Keychain keychain, IUserSettings userSettings,
            ILogger<CommunitySession> logger, ILogger<HttpService> httpLogger)
            : base(options, storage, keychain, userSettings, logger, httpLogger)
        {
        }


        /// <summary>
        /// Open the session by trying to login with the saved user information, if any 
        /// </summary>
        /// <returns>A task that completes when the user information has been fetched</returns>
        public override async Task Open()
        {
            this.logger.LogInformation("Opening Session");
            if (this.CurrentCredentials is UsernameCredentials credentials)
            {
                await this.Authenticate(credentials);
            }
        }

        #endregion

        public event EventHandler? UserChanged;

        public override async Task Signin(User user)
        {
            this.Communities = new UserCommunity[] { };
            this.User = user;
            await this.Storage.Save(user);
            UserCommunitiesPage? communitiesPage = await this.Service.FetchUserCommunities(user.Id);
            if (communitiesPage != null)
            {
                this.Communities = communitiesPage.Communities;
            }

            this.UserChanged?.Invoke(this, new EventArgs());
        }

        public Task SignOut()
        {
            this.User = null;
            this.UserChanged?.Invoke(this, new EventArgs());
            return Task.CompletedTask;
        }


        #region Communities

        public UserCommunity[] Communities = { };

        /// <summary>
        /// Gets a bar for a community.
        /// </summary>
        /// <param name="communityId"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="ApplicationException"></exception>
        public async Task<UserBar> GetBar(string communityId)
        {
            this.logger.LogInformation($"Getting bar for {communityId}");

            bool knownCommunity = this.Communities.Any(c => c.Id == communityId);
            if (!knownCommunity)
            {
                throw new ArgumentOutOfRangeException(nameof(communityId), "Unknown community ID for the current session");
            }

            if (this.User == null)
            {
                throw new InvalidOperationException("Unable to get a bar while logged out");
            }

            if (this.Communities.Length == 0)
            {
                throw new ApplicationException("Unable to get a bar for a user in no communities");
            }

            UserCommunityDetail? community = await this.Service.FetchUserCommunity(this.User.Id, communityId);
            return community?.Bar ?? throw new ApplicationException("Unable to retrieve the bar");
        }

        #endregion
    }
}
