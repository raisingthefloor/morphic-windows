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
