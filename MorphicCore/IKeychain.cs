using System;

namespace MorphicCore
{
    /// <summary>
    /// A keychain interface for morphic credentials
    /// </summary>
    public interface IKeychain
    {

        /// <summary>
        /// Save the key-based credentials to the keychain
        /// </summary>
        /// <param name="keyCredentials">The credentials to save</param>
        /// <param name="endpoint">The endpoint to which they apply</param>
        /// <param name="userId">The user tied to the credentials</param>
        /// <returns></returns>
        public bool Save(KeyCredentials keyCredentials, Uri endpoint, string userId);

        /// <summary>
        /// Get the key-based credentials for a given user
        /// </summary>
        /// <param name="endpoint">The endpoint where the credentials are used</param>
        /// <param name="userId">The identifier of the user tied to the credentials</param>
        /// <returns>The saved credentials, if found</returns>
        public KeyCredentials? LoadKey(Uri endpoint, string userId);

        /// <summary>
        /// Save the username/password-based credentials to the keychain
        /// </summary>
        /// <param name="keyCredentials">The credentials to save</param>
        /// <param name="endpoint">The endpoint to which they apply</param>
        /// <param name="userId">The user tied to the credentials</param>
        /// <returns></returns>
        public bool Save(UsernameCredentials usernameCredentials, Uri endpoint);

        /// <summary>
        /// Get the username/password-based credentials for a given user
        /// </summary>
        /// <param name="endpoint">The endpoint where the credentials are used</param>
        /// <param name="userId">The username in the credentials</param>
        /// <returns>The saved credentials, if found</returns>
        public UsernameCredentials? LoadUsername(Uri endpoint, string username);

    }
}
