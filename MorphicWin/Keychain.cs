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

using MorphicCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Logging;

namespace MorphicWin
{
    class Keychain : IKeychain
    {
        public Keychain(ILogger<Keychain> logger)
        {
            path = Path.Combine(new string[] { Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MorphicLite", "keychain" });
            this.logger = logger;
            if (!ReadEncryptedData())
            {
                logger.LogError("Failed to read keychain");
            }
        }

        private readonly ILogger<Keychain> logger;

        public bool Save(KeyCredentials keyCredentials, Uri endpoint, string userId)
        {
            var key = userId + ';' + endpoint.ToString();
            values[key] = keyCredentials.Key;
            return PersistEncryptedData();
        }

        public KeyCredentials? LoadKey(Uri endpoint, string userId)
        {
            var key = userId + ';' + endpoint.ToString();
            if (values.TryGetValue(key, out var secretKey))
            {
                return new KeyCredentials(secretKey);
            }
            return null;
        }

        public bool Save(UsernameCredentials usernameCredentials, Uri endpoint)
        {
            var key = usernameCredentials.Username + ';' + endpoint.ToString();
            values[key] = usernameCredentials.Password;
            return PersistEncryptedData();
        }

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
                var json = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
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
                var encrypted = ProtectedData.Protect(json, null, DataProtectionScope.CurrentUser);
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
