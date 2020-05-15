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
using System.Threading.Tasks;
using System.Net.Http;

namespace Morphic.Service
{
    public static class PreferencesService
    {

        /// <summary>
        /// Get the preferences for the given identifier
        /// </summary>
        /// <param name="service"></param>
        /// <param name="identifier">The preferences identifier, typically found in <code>User.PreferencesId</code></param>
        /// <returns>The preferences, or <code>null</code> if the request failed</returns>
        public static async Task<Preferences?> FetchPreferences(this HttpService service, User user)
        {
            if (user.PreferencesId is string prefsId)
            {
                return await service.Session.Send<Preferences>(() => HttpRequestMessageExtensions.Create(service.Session, string.Format("v1/users/{0}/preferences/{1}", user.Id, prefsId), HttpMethod.Get));
            }
            return null;
        }

        /// <summary>
        /// Save the given preferences
        /// </summary>
        /// <param name="service"></param>
        /// <param name="preferences">The preferences to save</param>
        /// <returns><code>true</code> if the request succeeded, <code>false</code> otherwise</returns>
        public static async Task<bool> Save(this HttpService service, Preferences preferences)
        {
            if (preferences.UserId is string userId)
            {
                return await service.Session.Send(() => HttpRequestMessageExtensions.Create(service.Session, string.Format("v1/users/{0}/preferences/{1}", userId, preferences.Id), HttpMethod.Put, preferences));
            }
            return false;
        }
    }
}
