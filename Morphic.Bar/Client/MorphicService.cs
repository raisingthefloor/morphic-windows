// MorphicService.cs: Communication with the web app.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

namespace Morphic.Bar.Client
{
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Flurl;
    using Flurl.Http;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// A session with the morphic web app.
    /// </summary>
    public class MorphicService
    {
        public const string ApiVersion = "v1";
        private readonly CredentialsProvider credentialsProvider;
        private string? authToken;

        public string Endpoint { get; set; }

        [PathValue("userId")]
        public string UserId { get; private set; } = string.Empty;

        private static readonly ILogger DefaultLogger = LogUtil.LoggerFactory.CreateLogger<MorphicService>();
        public ILogger Logger { get; }

        static MorphicService()
        {
            FlurlHttp.Configure(settings =>
            {
                settings.BeforeCall = call =>
                    MorphicService.DefaultLogger.LogInformation("Calling {method} {call}", call.Request.Method,
                        call.Request.RequestUri);
                settings.AfterCall = call =>
                    MorphicService.DefaultLogger.LogInformation("Received from {method} {call}: {status} {reason}",
                        call.Request.Method, call.Request.RequestUri, call.Response.StatusCode, call.Response.ReasonPhrase);
                settings.OnError = call => MorphicService.DefaultLogger.LogError("Failed {exception}", call.Exception);
            });
        }

        public MorphicService(string endpoint, CredentialsProvider? credentialsProvider = null, ILogger? logger = null)
        {
            this.Logger = logger ?? MorphicService.DefaultLogger;
            this.credentialsProvider = credentialsProvider ?? new CredentialsProvider();
            this.Endpoint = endpoint;
        }

        /// <summary>
        /// Perform a GET request. The path is taken from the ServicePath attribute of TResponse.
        /// </summary>
        /// <param name="pathValues">The values used for the path.</param>
        /// <typeparam name="TResponse">The object type to get.</typeparam>
        /// <returns></returns>
        public async Task<TResponse> Get<TResponse>(object? pathValues = null)
        {
            this.Logger.LogDebug("get {responseType}", typeof(TResponse).Name);
            IFlurlRequest req = await this.CreateRequest(ServiceObject.GetServicePath(typeof(TResponse)), pathValues);
            return await req.GetAsync().ReceiveJson<TResponse>();
        }

        /// <summary>
        /// Perform a POST request.
        /// </summary>
        /// <param name="requestObject"></param>
        /// <param name="pathValues">The values used for the path.</param>
        /// <typeparam name="TResponse">The object type to get.</typeparam>
        /// <returns></returns>
        public async Task<TResponse> Post<TResponse>(IServicePath requestObject, object? pathValues = null)
        {
            this.Logger.LogDebug("post {requestType} {responseType}", requestObject.GetType(), typeof(TResponse).Name);
            IFlurlRequest req = await this.CreateRequest(requestObject, pathValues);
            return await req.PostJsonAsync(requestObject).ReceiveJson<TResponse>();
        }

        private async Task<IFlurlRequest> CreateRequest(IServicePath requestObject, object? pathValues)
        {
            if (requestObject.AuthRequired)
            {
                if (this.authToken == null)
                {
                    await this.Authenticate();
                }
            }

            string path = this.ResolvePath(requestObject.RequestPath, pathValues);
            Url url = new Url(this.Endpoint).AppendPathSegments(MorphicService.ApiVersion, path);
            IFlurlRequest req = new FlurlRequest(url);

            if (requestObject.AuthRequired && this.authToken != null)
            {
                req.WithOAuthBearerToken(this.authToken);
            }

            return req;
        }

        /// <summary>
        /// Authenticate the user.
        /// </summary>
        /// <returns></returns>
        public async Task<ServiceRecord?> Authenticate()
        {
            bool success = false;
            Credentials? credentials = null;
            AuthResponse? authResponse = null;
            do
            {
                try
                {
                    credentials = await this.credentialsProvider.GetCredentials(this.Endpoint, credentials);
                    if (credentials.ServerHost != null)
                    {
                        this.Endpoint = credentials.ServerHost;
                    }
                    this.Logger.LogInformation("Authenticating for {endpoint} with {credentials}", this.Endpoint, credentials);
                    authResponse = await this.Post<AuthResponse>(credentials.AuthRequest);
                    success = true;
                    credentials.OnSuccess();
                }
                catch (FlurlHttpException e)
                {
                    ErrorResponse? errorResponse = await e.GetResponseJsonAsync<ErrorResponse>();
                    credentials?.OnFailure(errorResponse?.Error ?? e.Message);
                    success = false;
                }
            } while (!success);

            if (authResponse != null)
            {
                this.authToken = authResponse.Token;
                this.UserId = authResponse.User?.Id ?? string.Empty;
                this.Logger.LogDebug("Authenticated for {userId}", this.UserId);
                return authResponse.User;
            }

            return null;
        }

        /// <summary>
        /// Resolves the identifiers in a "path/{like}/{this}" with properties from an object, or properties from
        /// the session (this) with a PathValue attribute.
        /// </summary>
        /// <param name="path">The original path string.</param>
        /// <param name="pathValues">The object from which values are taken.</param>
        /// <returns>The final path.</returns>
        public string ResolvePath(string path, object? pathValues = null)
        {
            string result = path;
            
            if (pathValues != null)
            {
                foreach (PropertyInfo property in pathValues.GetType().GetProperties())
                {
                    string value = Url.Encode(property.GetValue(pathValues)?.ToString());
                    result = result.Replace($"{{{property.Name}}}", value);
                }
            }
            
            // Apply values from this object.
            this.pathProperties ??= this.GetType().GetProperties()
                .Select(p => (p, p.GetCustomAttribute<PathValueAttribute>()?.Name))
                .Where(p => p.Name != null)
                .ToArray();
            
            foreach ((var property, string? name) in this.pathProperties)
            {
                string value = Url.Encode(property.GetValue(this)?.ToString());
                result = result.Replace($"{{{name}}}", value);
            }

            return result;
        }

        private (PropertyInfo property, string? name)[]? pathProperties;
        
        public class PathValueAttribute : Attribute
        {
            public string Name { get; }
            public PathValueAttribute(string name)
            {
                this.Name = name;
            }
        }
    }

}
