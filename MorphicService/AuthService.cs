using MorphicCore;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json.Serialization;

namespace MorphicService
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
        public static async Task<AuthResponse?> Register(this Service service, User user, UsernameCredentials usernameCredentials)
        {
            var registration = new UsernameRegistration(usernameCredentials, user);
            var request = HttpRequestMessageExtensions.Create(service.Session, "register/username", HttpMethod.Post, registration);
            return await service.Session.Send<AuthResponse>(request);
        }

        /// <summary>
        /// Send a request to create a new user with a secret key
        /// </summary>
        /// <param name="service"></param>
        /// <param name="user">The user to create</param>
        /// <param name="usernameCredentials">The user's key credentials</param>
        /// <returns>An authentication token and user information, or <code>null</code> if the request failed</returns>
        public static async Task<AuthResponse?> Register(this Service service, User user, KeyCredentials keyCredentials)
        {
            var registration = new KeyRegistration(keyCredentials, user);
            var request = HttpRequestMessageExtensions.Create(service.Session, "register/key", HttpMethod.Post, registration);
            return await service.Session.Send<AuthResponse>(request);
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
                FirstName = user.FirstName;
                LastName = user.LastName;
            }

            [JsonPropertyName("username")]
            public string Username { get; set; }
            [JsonPropertyName("password")]
            public string Password { get; set; }
            [JsonPropertyName("firstName")]
            public string? FirstName { get; set; }
            [JsonPropertyName("lastName")]
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
            [JsonPropertyName("firstName")]
            public string? FirstName { get; set; }
            [JsonPropertyName("lastName")]
            public string? LastName { get; set; }
        }

        #endregion

        #region Authentication

        /// <summary>
        /// Send the appropriate authentication request for the given credentials
        /// </summary>
        /// <param name="service"></param>
        /// <param name="credentials">The username or key credentials to send</param>
        /// <returns>An authentication token and user information, or <code>null</code> if the request failed</returns>
        public static async Task<AuthResponse?> Authenticate(this Service service, ICredentials credentials)
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
        public static async Task<AuthResponse?> AuthenticateUsername(this Service service, UsernameCredentials usernameCredentials)
        {
            var request = HttpRequestMessageExtensions.Create(service.Session, "auth/username", HttpMethod.Post, usernameCredentials);
            return await service.Session.Send<AuthResponse>(request);
        }

        /// <summary>
        /// Send a request to authenticate with key based credentials
        /// </summary>
        /// <param name="service"></param>
        /// <param name="keyCredentials">The key credentials</param>
        /// <returns>An authentication token and user information, or <code>null</code> if the request failed</returns>
        public static async Task<AuthResponse?> AuthenticateKey(this Service service, KeyCredentials keyCredentials)
        {
            var request = HttpRequestMessageExtensions.Create(service.Session, "auth/username", HttpMethod.Post, keyCredentials);
            return await service.Session.Send<AuthResponse>(request);
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
