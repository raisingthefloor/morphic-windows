using System;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using MorphicCore;

namespace MorphicService
{
    internal static class HttpRequestMessageExtensions
    {
        internal static HttpRequestMessage Create(Session session, string path, HttpMethod method)
        {
            var uri = new Uri(session.Service.Endpoint, path);
            var request = new HttpRequestMessage(method, uri);
            if (session.AuthToken is string token)
            {
                request.Headers.Add("X-Morphic-Auth-Token", token);
            }
            return request;
        }

        internal static HttpRequestMessage Create<RequestBody>(Session session, string path, HttpMethod method, RequestBody body)
        {
            var request = Create(session, path, method);
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonElementInferredTypeConverter());
            var json = JsonSerializer.Serialize(body, options);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return request;
        }
    }
}
