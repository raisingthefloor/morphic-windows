using System.Net.Http;
using System.Linq;
using System;
using System.Text.Json;
using System.Threading.Tasks;

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
            var contentType = response.Headers.GetValues("Content-Type").FirstOrDefault();
            if (contentType != "application/json; charset=utf-8")
            {
                return null;
            }
            var json = await response.Content.ReadAsStreamAsync();
            try
            {
                return await JsonSerializer.DeserializeAsync<T>(json);
            }
            catch
            {
                return null;
            }
        }
    }
}

#nullable disable