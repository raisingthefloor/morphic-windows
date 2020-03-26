using MorphicCore;
using System.Threading.Tasks;
using System.Net.Http;

namespace MorphicService
{
    static class UserService
    {

        /// <summary>
        /// Get the user for the given identifier
        /// </summary>
        /// <param name="service"></param>
        /// <param name="identifier">The user identifier</param>
        /// <returns>The user, or <code>null</code> if the request failed</returns>
        public static async Task<User?> FetchUser(this Service service, string identifier)
        {
            return await service.Session.Send<User>(() => HttpRequestMessageExtensions.Create(service.Session, string.Format("users/{0}", identifier), HttpMethod.Get));
        }

        /// <summary>
        /// Save the given user
        /// </summary>
        /// <param name="service"></param>
        /// <param name="user">The user to save</param>
        /// <returns><code>true</code> if the request succeeded, <code>false</code> otherwise</returns>
        public static async Task<bool> Save(this Service service, User user)
        {
            return await service.Session.Send(() => HttpRequestMessageExtensions.Create(service.Session, string.Format("users/{0}", user.Id), HttpMethod.Put, user));
        }
    }
}
