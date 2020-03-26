using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using MorphicCore;
using MorphicSettings;

namespace MorphicService
{

    public class SessionOptions
    {
        public string Endpoint = "";
    }

    /// <summary>
    /// Manages a user's session with the morphic server
    /// </summary>
    public class Session
    {

        #region Creating a Session

        /// <summary>
        /// Create a new session with the given URL
        /// </summary>
        /// <param name="endpoint">The root URL of the Morphic HTTP service</param>
        public Session(SessionOptions options, Settings settings, ILogger<Session> logger)
        {
            Service = new Service(new Uri(options.Endpoint), this);
            client = new HttpClient();
            this.logger = logger;
            this.settings = settings;
        }

        /// <summary>
        /// The unerlying Morphic service this session talks to
        /// </summary>
        internal Service Service;

        /// <summary>
        /// Open the session by trying to login with the saved user information, if any 
        /// </summary>
        /// <returns>A task that completes when the user information has been fetched</returns>
        public async Task Open(string userId)
        {
            if (userId != "")
            {
                logger.LogInformation("Opening Session");
                CurrentUserId = userId;
                User = await Service.FetchUser(userId);
                if (User != null && User.PreferencesId != null)
                {
                    Preferences = await Service.FetchPreferences(User.PreferencesId);
                }
            }
        }

        #endregion

        #region

        /// <summary>
        /// The logger for this session
        /// </summary>
        private readonly ILogger<Session> logger;

        #endregion

        #region Requests

        /// <summary>
        /// The underlying HTTP client that makes requests
        /// </summary>
        private readonly HttpClient client;

        /// <summary>
        /// Send a request, re-authenticating if needed
        /// </summary>
        /// <typeparam name="ResponseBody">The type of response expected to be decoded from JSON</typeparam>
        /// <param name="request">The request to send</param>
        /// <returns>The response decoded from JSON, or <code>null</code> if no valid response was provided</returns>
        public async Task<ResponseBody?> Send<ResponseBody>(HttpRequestMessage request) where ResponseBody: class
        {
            try
            {
                logger.LogInformation("{0} {1}", request.Method, request.RequestUri.AbsolutePath);
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
                    logger.LogInformation("{0} {1}", request.Method, request.RequestUri.AbsolutePath);
                    response = await client.SendAsync(request);
                    logger.LogInformation("{0} {1}", response.StatusCode.ToString(), request.RequestUri.AbsolutePath);
                }
                return await response.GetObject<ResponseBody>();
            }catch (Exception e)
            {
                logger.LogError(e, "Request failed");
                return null;
            }
        }

        /// <summary>
        /// Send a request that expects no response body, re-authenticating if needed
        /// </summary>
        /// <param name="request">The request to send</param>
        /// <returns><code>true</code> if the request succeeds, <code>false</code> otherwise</returns>
        public async Task<bool> Send(HttpRequestMessage request)
        {
            try
            {
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

        #endregion

        #region Authentication

        private string? authToken;

        /// <summary>
        /// The session's auth token
        /// </summary>
        public string? AuthToken
        {
            get
            {
                // FIXME: fetch from Credentials Manager
                return authToken;
            }
            set
            {
                // FIXME: store in Credentials Manager
                authToken = value;
            }
        }

        /// <summary>
        /// The current user's saved credentials, if any
        /// </summary>
        private ICredentials? CurrentCredentials
        {
            get
            {
                if (CurrentUserId is string userId)
                {
                    // FIXME: fetch from Credentials Manager
                }
                return null;
            }
        }

        /// <summary>
        /// Authenticate using the current credentials
        /// </summary>
        /// <returns></returns>
        private async Task<bool> Authenticate()
        {
            if (CurrentCredentials is ICredentials creds)
            {
                var auth = await Service.Authenticate(creds);
                if (auth != null)
                {
                    AuthToken = auth.Token;
                    User = auth.User;
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region User Info

        /// <summary>
        /// The current user's id
        /// </summary>
        public string? CurrentUserId;

        /// <summary>
        /// The current user's information
        /// </summary>
        public User? User;

        /// <summary>
        /// The current user's preferences
        /// </summary>
        public Preferences? Preferences;

        #endregion

        #region Preferences

        /// <summary>
        /// The Settings object that applies settings to the system
        /// </summary>
        private readonly Settings settings;

        /// <summary>
        /// Set the specified preference to the given value
        /// </summary>
        /// <remarks>
        /// Calls <code>SetNeedsPreferencesSave()</code> to queue a save after a timeout.
        /// </remarks>
        /// <param name="solution">The solution name</param>
        /// <param name="preference">The preference name</param>
        /// <param name="value">The preference value</param>
        /// <returns>Whether the preference was successfully applied to the system</returns>
        public bool SetPreference(string solution, string preference, object? value)
        {
            if (Preferences is Preferences preferences)
            {
                preferences.Set(solution, preference, value);
                SetNeedsPreferencesSave();
                return settings.Apply(solution, preference, value);
            }
            return false;
        }

        /// <summary>
        /// Applies all of the user's preferences to the system
        /// </summary>
        /// <remarks>
        /// Used after the user logs in
        /// </remarks>
        public void ApplyAllPreferences()
        {
            if (Preferences is Preferences preferences)
            {
                if (preferences.Default != null)
                {
                    foreach (var solution in preferences.Default)
                    {
                        foreach (var preference in solution.Value.Values)
                        {
                            _ = settings.Apply(solution.Key, preference.Key, preference.Value);
                        }
                    }
                }
            }
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
                var success = await Service.Save(preferences);
                if (!success)
                {
                    logger.LogError("Failed to save preferences");
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
