using MorphicCore;
using System.Threading.Tasks;
using System.Net.Http;

namespace MorphicService
{
    static class PreferencesService
    {

        /// <summary>
        /// Get the preferences for the given identifier
        /// </summary>
        /// <param name="service"></param>
        /// <param name="identifier">The preferences identifier, typically found in <code>User.PreferencesId</code></param>
        /// <returns>The preferences, or <code>null</code> if the request failed</returns>
        public static async Task<Preferences?> FetchPreferences(this Service service, string identifier)
        {
            return await service.Session.Send<Preferences>(() => HttpRequestMessageExtensions.Create(service.Session, string.Format("preferences/{0}", identifier), HttpMethod.Get));
        }

        /// <summary>
        /// Save the given preferences
        /// </summary>
        /// <param name="service"></param>
        /// <param name="preferences">The preferences to save</param>
        /// <returns><code>true</code> if the request succeeded, <code>false</code> otherwise</returns>
        public static async Task<bool> Save(this Service service, Preferences preferences)
        {
            return await service.Session.Send(() => HttpRequestMessageExtensions.Create(service.Session, string.Format("preferences/{0}", preferences.Id), HttpMethod.Put, preferences));
        }
    }
}
