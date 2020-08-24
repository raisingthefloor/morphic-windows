// ServiceObject.cs: Objects sent and received by the morphic web app.
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
    using System.Collections.Generic;
    using System.Reflection;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public interface IServicePath
    {
        public string RequestPath { get; }
        public bool AuthRequired { get; }
    }
    
    /// <summary>
    /// Something that's sent or received by the service.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public abstract class ServiceObject : IServicePath
    {
        private IServicePath servicePath;
        string IServicePath.RequestPath => this.servicePath.RequestPath;
        bool IServicePath.AuthRequired => this.servicePath.AuthRequired;

        protected ServiceObject()
        {
            this.servicePath = ServiceObject.GetServicePath(this.GetType());
        }

        public static IServicePath GetServicePath(Type type)
        {
            return type.GetCustomAttribute<ServicePathAttribute>() ??
                    throw new InvalidOperationException(
                        $"{type.Name} has no {nameof(ServicePathAttribute)} attribute");
        }
    }

    /// <summary>
    /// Something that has an ID.
    /// </summary>
    public class ServiceRecord
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;
    }

    public class ErrorResponse
    {
        [JsonProperty("error")]
        public string? Error { get; set; }
        
        [JsonProperty("details")]
        public string? Details { get; set; }
    }

    [ServicePath("auth/username", false)]
    public class AuthRequest : ServiceObject
    {
        [JsonProperty("username")]
        public string UserName { get; set; } = string.Empty;

        [JsonProperty("password")]
        public string Password { get; set; } = string.Empty;
    }

    public class AuthResponse
    {
        [JsonProperty("token")]
        public string Token { get; set; } = string.Empty;
        
        [JsonProperty("user")]
        public ServiceRecord? User { get; set; }
    }

    [ServicePath("users/{userId}/communities")]
    public class UserCommunities : ServiceObject
    {
        [JsonProperty("communities")]
        public List<ServiceRecord> Communities { get; set; } = new List<ServiceRecord>();
    }
    
    [ServicePath("users/{userId}/communities/{communityId}")]
    public class UserCommunity : ServiceObject
    {
        [JsonProperty("bar")]
        internal JRaw? BarObject
        {
            set => this.BarJson = value?.ToString();
            get => null;
        }

        public string? BarJson { get; private set; }
        public string? Id { get; private set; }
    }
    
    

    public class ServicePathAttribute : Attribute, IServicePath
    {
        public string RequestPath { get; }
        public bool AuthRequired { get; }

        public ServicePathAttribute(string requestPath, bool authRequired = true)
        {
            this.AuthRequired = authRequired;
            this.RequestPath = requestPath.TrimStart('/').TrimEnd('/');
        }

    }
}
