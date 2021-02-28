﻿// Copyright 2020 Raising the Floor - International
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
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Timers;
    using Microsoft.Extensions.Logging;
    using Morphic.Core;
    using Morphic.Core.Community;
    using Settings.SolutionsRegistry;

    /// <summary>
    /// Manages a user's session with the morphic server
    /// </summary>
    public class MorphicSession: Session
    {

        #region Creating a Session

        /// <summary>
        /// Create a new session with the given URL
        /// </summary>
        public MorphicSession(SessionOptions options, Storage storage,
            Keychain keychain, IUserSettings userSettings, ILogger<MorphicSession> logger,
            ILogger<HttpService> httpLogger, Solutions solutions)
            : base(options.ApiEndpointUri, storage, keychain, userSettings, logger, httpLogger, solutions)
        {
        }

        /// <summary>
        /// Open the session by trying to login with the saved user information, if any 
        /// </summary>
        /// <returns>A task that completes when the user information has been fetched</returns>
        public override async Task OpenAsync()
        {
            this.logger.LogInformation("Opening Session");
			//
            // NOTE: ideally we would not re-authenticate and re-load the MorphicBars before loading the current bar (i.e. ideally we would cache this data, like we do on macOS)
            if (this.CurrentCredentials is UsernameCredentials credentials)
            {
                await this.Authenticate(credentials);
            }
            //
            // cache our user id (so that we don't re-call "user changed" if our user doesn't actually change here)
            var cachedUserId = this.User?.Id;
            //
            if (this.CurrentUserId != null)
            {
                if (this.CurrentUserId != "")
                {
                    this.User = await this.Storage.LoadAsync<User>(this.CurrentUserId);
                }
            }
            string preferencesId = this.User?.PreferencesId ?? "__default__";
            this.Preferences = await this.Storage.LoadAsync<Preferences>(preferencesId);
            if ((this.User != null) && (this.User?.Id != cachedUserId))
            {
                await this.UserChangedAsync?.Invoke(this, new EventArgs());
            }
        }

        #endregion

        /// <summary>
        /// The current user's preferences
        /// </summary>
        public Preferences? Preferences;

        public delegate Task AsyncEventHandler(object sender, EventArgs e);
        public event AsyncEventHandler? UserChangedAsync;

        public override async Task SignInAsync(User user)
        {
            await this.SignInAsync(user, null);
        }

        public async Task SignInAsync(User user, Preferences? preferences)
        {
            this.Communities = new UserCommunity[] { };
            this.User = user;
            if (preferences == null)
            {
                this.Preferences = null;
                preferences = await this.Service.FetchPreferences(user);
            }

            this.Preferences = preferences;
            await this.Storage.Save(user);
            if (this.Preferences != null)
            {
                await this.Storage.Save(this.Preferences);
            }

            UserCommunitiesPage? communitiesPage = await this.Service.FetchUserCommunities(user.Id);
            if (communitiesPage != null)
            {
                this.Communities = communitiesPage.Communities;
            }

            if (this.UserChangedAsync != null)
            {
                await this.UserChangedAsync.Invoke(this, new EventArgs());
            }
        }

        public async Task SignOut()
        {
            // empty our list of communities
            this.Communities = new UserCommunity[0];

            // clear out the user
            this.User = null;

            //            this.Preferences = await this.Storage.Load<Preferences>("__default__");
            //            await this.Solutions.ApplyPreferences(this.Preferences!);

            if (this.UserChangedAsync != null)
            {
                await this.UserChangedAsync.Invoke(this, new EventArgs());
            }
        }

        public async Task<bool> RegisterUserAsync(User user, UsernameCredentials credentials, Preferences preferences)
        {
            AuthResponse? auth = await this.Service.Register(user, credentials);
            bool success = auth != null;
            if (success)
            {
                if (!this.keychain.Save(credentials, this.Service.Endpoint))
                {
                    this.logger.LogError("Failed to save username credentials to keychain");
                }

                this.Service.AuthToken = auth!.Token;
                // The server doesn't currently send email, but we reference it immediately after creating an account,
                // so just fill it in from the input
                auth.User.Email ??= user.Email;
                if (auth.User.PreferencesId != null)
                {
                    preferences.Id = auth.User.PreferencesId;
                    preferences.UserId = auth.User.Id;
                    await this.Service.Save(preferences);
                }

                this.userSettings.SetUsernameForId(credentials.Username, auth.User.Id);
                await this.SignInAsync(auth.User, preferences);
            }

            return success;
        }

        #region Preferences

        /// <summary>
        /// Set the specified preference to the given value
        /// </summary>
        /// <remarks>
        /// Calls <code>SetNeedsPreferencesSave()</code> to queue a save after a timeout.
        /// </remarks>
        /// <param name="key">The preference lookup key</param>
        /// <param name="value">The preference value</param>
        /// <returns>Whether the preference was successfully applied to the system</returns>
        public bool SetPreference(Preferences.Key key, object? value)
        {
            this.logger.LogInformation("Setting {0}={1}", key, value);
            this.Preferences?.Set(key, value);
            this.SetNeedsPreferencesSave();
            return true;
        }

        /// <summary>
        /// Create and run an apply session for the current user's preferences
        /// </summary>
        /// <returns></returns>
        public async Task ApplyAllPreferences()
        {
            if (this.Preferences != null)
            {
                await this.Solutions.ApplyPreferences(this.Preferences, true);
            }
        }

        /// <summary>
        /// Get a string preference
        /// </summary>
        /// <param name="key">The preference key</param>
        /// <returns>The requested string or <code>null</code> if no string is found for the key</returns>
        public string? GetString(Preferences.Key key)
        {
            return this.Preferences?.Get(key) as string;
        }

        /// <summary>
        /// Get a double preference
        /// </summary>
        /// <param name="key">The preference key</param>
        /// <returns>The requested double or <code>null</code> if no double is found for the key</returns>
        public double? GetDouble(Preferences.Key key)
        {
            return this.Preferences?.Get(key) as double?;
        }

        /// <summary>
        /// Get an integer preference
        /// </summary>
        /// <param name="key">The preference key</param>
        /// <returns>The requested integer or <code>null</code> if no integer is found for the key</returns>
        public long? GetInteger(Preferences.Key key)
        {
            return this.Preferences?.Get(key) as long?;
        }

        /// <summary>
        /// Get a boolean preference
        /// </summary>
        /// <param name="key">The preference key</param>
        /// <returns>The requested boolean or <code>null</code> if no boolean is found for the key</returns>
        public bool? GetBool(Preferences.Key key)
        {
            return this.Preferences?.Get(key) as bool?;
        }

        /// <summary>
        /// Get a dictionary preference
        /// </summary>
        /// <param name="key">The preference key</param>
        /// <returns>The requested dictionary or <code>null</code> if no dictionary is found for the key</returns>
        public Dictionary<string, object?>? GetDictionary(Preferences.Key key)
        {
            return this.Preferences?.Get(key) as Dictionary<string, object?>;
        }

        /// <summary>
        /// Get an array preference
        /// </summary>
        /// <param name="key">The preference key</param>
        /// <returns>The requested array or <code>null</code> if no array is found for the key</returns>
        public object?[]? GetArray(Preferences.Key key)
        {
            return this.Preferences?.Get(key) as object?[];
        }

        /// <summary>
        /// The timer for saving preferences to the server
        /// </summary>
        private Timer? preferencesSaveTimer;

        /// <summary>
        /// Indicate that the preferences need to be saved to the server
        /// </summary>
        /// <remarks>
        /// Does not save immediately.  Sets a timer to save in a few seconds so
        /// rapid calls only require a single request to the server.
        /// </remarks>
        private void SetNeedsPreferencesSave()
        {
            this.preferencesSaveTimer?.Stop();
            this.preferencesSaveTimer = new Timer(5000);
            this.preferencesSaveTimer.Elapsed += this.PreferencesSaveTimerElapsed;
            this.preferencesSaveTimer.Start();
        }

        /// <summary>
        /// Called when the preferences save timer fires
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PreferencesSaveTimerElapsed(object sender, ElapsedEventArgs e)
        {
            this.preferencesSaveTimer?.Stop();
            this.preferencesSaveTimer = null;
            _ = this.SavePreferences();

        }

        /// <summary>
        /// Actually save the preferences to the server
        /// </summary>
        /// <returns></returns>
        private async Task SavePreferences()
        {
            if (this.Preferences == null)
            {
                this.logger.LogWarning("SavePreferences called with null preferences");
            }
            else
            {
                this.logger.LogInformation("Saving preferences to disk");
                if (await this.Storage.Save(this.Preferences))
                {
                    this.logger.LogInformation("Saved preferences to disk");
                }
                else
                {
                    this.logger.LogError("Failed to save preferences to disk");
                }

                if (this.User != null)
                {
                    this.logger.LogInformation("Saving preferences to server");
                    if (await this.Service.Save(this.Preferences))
                    {
                        this.logger.LogInformation("Saved preferences to server");
                    }
                    else
                    {
                        this.logger.LogError("Failed to save preferences to server");
                    }
                }
            }
        }

        #endregion

        #region Community

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
            if (community != null)
            {
                return community.Bar;
            } 
            else
            {
                throw new ApplicationException("Unable to retrieve the bar");
            }
        }

        #endregion
    }
}
