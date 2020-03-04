using MorphicCore;
using System.Net.Http;
using System;
using System.Threading.Tasks;

#nullable enable

namespace MorphicService
{
    public class PreferencesService
    {

        public PreferencesService(Uri endpoint)
        {
            Endpoint = endpoint;
            Client = new HttpClient();
            PreferencesRootUri = new Uri(Endpoint, "preferences/");
        }

        public Uri Endpoint { get; }
        public HttpClient Client { get; }

        public async Task<Preferences?> FetchPrefernces(User user)
        {
            var uri = GetPreferencesUri(user);
            var response = await Client.GetAsync(uri);
            return await response.GetObject<Preferences>();
        }

        private Uri PreferencesRootUri;

        private Uri GetPreferencesUri(User user)
        {
            return new Uri(PreferencesRootUri, user.identifier);
        }
         
    }
}

#nullable disable