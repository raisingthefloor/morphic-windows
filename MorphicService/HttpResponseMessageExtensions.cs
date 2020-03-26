using System.Net.Http;
using System.Linq;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using MorphicCore;

#nullable enable

namespace MorphicService
{
    internal static class HttpResponseMessageExtensions
    {
        internal static async Task<T?> GetObject<T>(this HttpResponseMessage response) where T : class
        {
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            if (response.Content.Headers.ContentType.MediaType != "application/json")
            {
                return null;
            }
            if (response.Content.Headers.ContentType.CharSet != "utf-8")
            {
                return null;
            }
            var json = await response.Content.ReadAsStreamAsync();
            try
            {
                var options = new JsonSerializerOptions();
                options.Converters.Add(new JsonElementInferredTypeConverter());
                return await JsonSerializer.DeserializeAsync<T>(json, options);
            }
            catch
            {
                return null;
            }
        }

        internal static bool RequiresMorphicAuthentication(this HttpResponseMessage response)
        {
            return response.StatusCode == System.Net.HttpStatusCode.Unauthorized;
        }
    }
}

#nullable disable