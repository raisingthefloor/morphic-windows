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
using Morphic.Settings;

namespace Morphic.Service
{

    public class SessionOptions
    {
        public string Endpoint { get; set; } = "";

        public string FrontEnd { get; set; } = "";

        public Uri FontEndUri
        {
            get
            {
                return new Uri(FrontEnd);
            }
        }
    }

    /// <summary>
    /// Manages a user's session with the morphic server
    /// </summary>
    public class Session: IHttpServiceCredentialsProvider
    {

        #region Creating a Session

        /// <summary>
        /// Create a new session with the given URL
        /// </summary>
        /// <param name="endpoint">The root URL of the Morphic HTTP service</param>
        public Session(SessionOptions options, SettingsManager settingsManager, Storage storage, Keychain keychain, IUserSettings userSettings, ILogger<Session> logger, ILogger<HttpService> httpLogger)
        {
            Service = new HttpService(new Uri(options.Endpoint), this, httpLogger);
            this.SettingsManager = settingsManager;
            Storage = storage;
            this.keychain = keychain;
            this.logger = logger;
            this.userSettings = userSettings;
        }

        /// <summary>
        /// The unerlying Morphic service this session talks to
        /// </summary>
        public HttpService Service { get; private set; }

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
            if (CurrentUserId is string userId)
            {
                if (userId != "")
                {
                    User = await Storage.Load<User>(userId);
                }
            }
            var preferencesId = User?.PreferencesId ?? "__default__";
            Preferences = await Storage.Load<Preferences>(preferencesId);
            if (User != null)
            {
                UserChanged?.Invoke(this, new EventArgs());
            }
        }

        #endregion

        #region Logger

        /// <summary>
        /// The logger for this session
        /// </summary>
        private readonly ILogger<Session> logger;

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

        /// <summary>
        /// The current user's preferences
        /// </summary>
        public Preferences? Preferences;

        public event EventHandler? UserChanged;

        public async Task Signin(User user, Preferences? preferences = null)
        {
            if (User == null)
            {
                // If we are going from no user to a logged in user, capture the computer's current settings as the
                // default preferences that will be applied back when the user logs out.
                //
                // If we are going from one user to another user, we don't want to do anything because the computer's
                // current settings are the first user's rather than whatever the computer was before that user logged in
                if (Preferences is Preferences defaultPreferences)
                {
                    if (defaultPreferences.Id == "__default__")
                    {
                        var capture = new CaptureSession(SettingsManager, defaultPreferences);
                        capture.AddAllSolutions();
                        await capture.Run();
                        await Storage.Save(defaultPreferences);
                    }
                    else
                    {
                        logger.LogError("User is null, but Preferences.Id != '__default__'; not capturing default settings on signin");
                    }
                }
            }
            User = user;
            if (preferences == null)
            {
                Preferences = null;
                preferences = await Service.FetchPreferences(user);
            }
            Preferences = preferences;
            await Storage.Save(user);
            if (Preferences != null)
            {
                await Storage.Save(Preferences);
            }
            UserChanged?.Invoke(this, new EventArgs());
        }

        public async Task Signout()
        {
            User = null;
            Preferences = await Storage.Load<Preferences>("__default__");
            var apply = new ApplySession(SettingsManager, Preferences!);
            await apply.Run();
            UserChanged?.Invoke(this, new EventArgs());
        }

        public async Task<bool> RegisterUser(User user, UsernameCredentials credentials, Preferences preferences)
        {
            var auth = await Service.Register(user, credentials);
            if (auth != null)
            {
                if (!keychain.Save(credentials, Service.Endpoint))
                {
                    logger.LogError("Failed to save username creds to keychain");
                }
                Service.AuthToken = auth.Token;
                // The server doesn't currently send email, but we reference it immedately after creating an account,
                // so just fill it in from the input
                auth.User.Email = auth.User.Email ?? user.Email;
                if (auth.User.PreferencesId is string preferencesId)
                {
                    preferences.Id = preferencesId;
                    preferences.UserId = auth.User.Id;
                    await Service.Save(preferences);
                }
                userSettings.SetUsernameForId(credentials.Username, auth.User.Id);
                await Signin(auth.User, preferences);
                return true;
            }
            return false;
        }

        #endregion

        #region Preferences

        /// <summary>
        /// The Settings object that applies settings to the system
        /// </summary>
        public readonly SettingsManager SettingsManager;

        /// <summary>
        /// Set the specified preference to the given value
        /// </summary>
        /// <remarks>
        /// Calls <code>SetNeedsPreferencesSave()</code> to queue a save after a timeout.
        /// </remarks>
        /// <param name="key">The preference lookup key</param>
        /// <param name="value">The preference value</param>
        /// <returns>Whether the preference was successfully applied to the system</returns>
        public async Task<bool> Apply(Preferences.Key key, object? value)
        {
            return await SettingsManager.Apply(key, value);
        }

        public async Task<Dictionary<Preferences.Key, bool>> Apply(Dictionary<Preferences.Key, object?> valuesByKey)
        {
            return await SettingsManager.Apply(valuesByKey);
        }

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
            logger.LogInformation("Setting {0}={1}", key, value);
            Preferences?.Set(key, value);
            SetNeedsPreferencesSave();
            return true;
        }

        /// <summary>
        /// Create and run an apply session for the current user's preferences
        /// </summary>
        /// <returns></returns>
        public async Task ApplyAllPreferences()
        {
            if (Preferences is Preferences preferences)
            {
                var applySession = new ApplySession(SettingsManager, preferences);
                await applySession.Run();
            }
        }

        /// <summary>
        /// Get a string preference
        /// </summary>
        /// <param name="key">The preference key</param>
        /// <returns>The requested string or <code>null</code> if no string is found for the key</returns>
        public string? GetString(Preferences.Key key)
        {
            return Preferences?.Get(key) as string;
        }

        /// <summary>
        /// Get a double preference
        /// </summary>
        /// <param name="key">The preference key</param>
        /// <returns>The requested double or <code>null</code> if no double is found for the key</returns>
        public double? GetDouble(Preferences.Key key)
        {
            return Preferences?.Get(key) as double?;
        }

        /// <summary>
        /// Get an integer preference
        /// </summary>
        /// <param name="key">The preference key</param>
        /// <returns>The requested integer or <code>null</code> if no integer is found for the key</returns>
        public long? GetInteger(Preferences.Key key)
        {
            return Preferences?.Get(key) as long?;
        }

        /// <summary>
        /// Get a boolean preference
        /// </summary>
        /// <param name="key">The preference key</param>
        /// <returns>The requested boolean or <code>null</code> if no boolea  is found for the key</returns>
        public bool? GetBool(Preferences.Key key)
        {
            return Preferences?.Get(key) as bool?;
        }

        /// <summary>
        /// Get a dictionary preference
        /// </summary>
        /// <param name="key">The preference key</param>
        /// <returns>The requested dictionary or <code>null</code> if no dictionary is found for the key</returns>
        public Dictionary<string, object?>? GetDictionary(Preferences.Key key)
        {
            return Preferences?.Get(key) as Dictionary<string, object?>;
        }

        /// <summary>
        /// Get an array preference
        /// </summary>
        /// <param name="key">The preference key</param>
        /// <returns>The requested array or <code>null</code> if no array is found for the key</returns>
        public object?[]? GetArray(Preferences.Key key)
        {
            return Preferences?.Get(key) as object?[];
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
            if (preferencesSaveTimer is Timer timer)
            {
                timer.Stop();
            }
            preferencesSaveTimer = new Timer(5000);
            preferencesSaveTimer.Elapsed += PreferencesSaveTimerElapsed;
            preferencesSaveTimer.Start();
        }

        /// <summary>
        /// Called when the preferences save timer fires
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PreferencesSaveTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (preferencesSaveTimer is Timer timer)
            {
                timer.Stop();
            }
            preferencesSaveTimer = null;
            _ = SavePreferences();

        }

        /// <summary>
        /// Actually save the preferences to the server
        /// </summary>
        /// <returns></returns>
        private async Task SavePreferences()
        {
            if (Preferences is Preferences preferences)
            {
                logger.LogInformation("Saving preferences to disk");
                if (await Storage.Save(preferences))
                {
                    logger.LogInformation("Saved preferences to disk");
                }
                else
                {
                    logger.LogError("Failed to save preferences to disk");
                }
                if (User != null)
                {
                    logger.LogInformation("Saving preferences to server");
                    if (await Service.Save(preferences))
                    {
                        logger.LogInformation("Saved preferences to server");
                    }
                    else
                    {
                        logger.LogError("Failed to save preferences to server");
                    }

                }
            }
            else
            {
                logger.LogWarning("SavePreferences called with null preferences");
            }
        }

        #endregion
    }
}
