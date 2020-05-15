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

using Morphic.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Morphic.Core
{

    /// <summary>
    /// Options for creating a keychain
    /// </summary>
    /// <remarks>
    /// Designed to pass one or more options to a <code>Keychain</code> constructor via dependency injection
    /// </remarks>
    public class KeychainOptions
    {
        /// <summary>
        /// The file path of the keychain
        /// </summary>
        public string Path = "";
    }

    /// <summary>
    /// A keychain that encrypts and saves sensitive user data like passwords
    /// </summary>
    public class Keychain
    {

        /// <summary>
        /// Create a new keychain
        /// </summary>
        /// <param name="options">The creation options</param>
        /// <param name="dataProtection">A object that can encrypt and decrypt data</param>
        /// <param name="logger">A logger for the keychain</param>
        public Keychain(KeychainOptions options, IDataProtection dataProtection, ILogger<Keychain> logger)
        {
            path = options.Path;
            this.logger = logger;
            this.dataProtection = dataProtection;
            if (!ReadEncryptedData())
            {
                logger.LogError("Failed to read keychain");
            }
        }

        /// <summary>
        /// The keychain's logger
        /// </summary>
        private readonly ILogger<Keychain> logger;

        /// <summary>
        /// An object that handles the encryption and decryption for the keychain
        /// </summary>
        /// <remarks>
        /// On Windows, the <code>ProtectedData</code> class provides a way to
        /// encrypt data for the user, but it's not available in a .net core library,
        /// so the keychain delegates its encryption tasks to this object that can
        /// be provided by an application with access to <code>ProtectedData</code>
        /// </remarks>
        private readonly IDataProtection dataProtection;

        /// <summary>
        /// Save the key-based credentials to the keychain
        /// </summary>
        /// <param name="keyCredentials">The credentials to save</param>
        /// <param name="endpoint">The endpoint to which they apply</param>
        /// <param name="userId">The user tied to the credentials</param>
        /// <returns></returns>
        public bool Save(KeyCredentials keyCredentials, Uri endpoint, string userId)
        {
            var key = userId + ';' + endpoint.ToString();
            values[key] = keyCredentials.Key;
            return PersistEncryptedData();
        }

        /// <summary>
        /// Get the key-based credentials for a given user
        /// </summary>
        /// <param name="endpoint">The endpoint where the credentials are used</param>
        /// <param name="userId">The identifier of the user tied to the credentials</param>
        /// <returns>The saved credentials, if found</returns>
        public KeyCredentials? LoadKey(Uri endpoint, string userId)
        {
            var key = userId + ';' + endpoint.ToString();
            if (values.TryGetValue(key, out var secretKey))
            {
                return new KeyCredentials(secretKey);
            }
            return null;
        }

        /// <summary>
        /// Save the username/password-based credentials to the keychain
        /// </summary>
        /// <param name="keyCredentials">The credentials to save</param>
        /// <param name="endpoint">The endpoint to which they apply</param>
        /// <param name="userId">The user tied to the credentials</param>
        /// <returns></returns>
        public bool Save(UsernameCredentials usernameCredentials, Uri endpoint)
        {
            var key = usernameCredentials.Username + ';' + endpoint.ToString();
            values[key] = usernameCredentials.Password;
            return PersistEncryptedData();
        }

        /// <summary>
        /// Get the username/password-based credentials for a given user
        /// </summary>
        /// <param name="endpoint">The endpoint where the credentials are used</param>
        /// <param name="userId">The username in the credentials</param>
        /// <returns>The saved credentials, if found</returns>
        public UsernameCredentials? LoadUsername(Uri endpoint, string username)
        {
            var key = username + ';' + endpoint.ToString();
            if (values.TryGetValue(key, out var password))
            {
                return new UsernameCredentials(username, password);
            }
            return null;
        }

        private Dictionary<string, string> values = new Dictionary<string, string>();

        private readonly string path;

        private bool ReadEncryptedData()
        {
            if (!File.Exists(path))
            {
                return true;
            }
            try
            {
                byte[] encrypted = File.ReadAllBytes(path);
                var json = dataProtection.Unprotect(encrypted);
                values = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                return true;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error reading keychain");
                return false;
            }
        }

        private bool PersistEncryptedData()
        {
            try
            {
                if (Path.GetDirectoryName(path) is string parent)
                {
                    if (!Directory.Exists(parent))
                    {
                        Directory.CreateDirectory(parent);
                    }
                }
                var json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(values));
                var encrypted = dataProtection.Protect(json);
                File.WriteAllBytes(path, encrypted);
                return true;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error writing keychain");
                return false;
            }
        }
    }
}
