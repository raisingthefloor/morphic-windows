// BarAction.cs: Actions performed by bar items.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

namespace Morphic.Bar.Bar.Actions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Media;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// An action for a bar item.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    [JsonConverter(typeof(TypedJsonConverter), "kind", "null")]
    public abstract class BarAction
    {
        [JsonProperty("identifier")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Called by <c>Invoke</c> to perform the implementation-specific action invocation.
        /// </summary>
        /// <param name="source">Button ID, for multi-button bar items.</param>
        /// <returns></returns>
        protected abstract Task<bool> InvokeImpl(string? source = null);

        /// <summary>
        /// Invokes the action.
        /// </summary>
        /// <param name="source">Button ID, for multi-button bar items.</param>
        /// <returns></returns>
        public Task<bool> Invoke(string? source = null)
        {
            return this.InvokeImpl(source);
        }

        /// <summary>
        /// Resolves "{identifiers}" in a string with its value.
        /// </summary>
        /// <param name="arg"></param>
        /// <param name="source"></param>
        /// <returns>null if arg is null</returns>
        protected string? ResolveString(string? arg, string? source)
        {
            // Today, there is only "{button}".
            return arg?.Replace("{button}", source ?? string.Empty);
        }

        public virtual Uri? DefaultImageUri { get; }
        public virtual ImageSource? DefaultImageSource { get; }
        public virtual bool IsAvailable { get; protected set; } = true;

    }

    [JsonTypeName("null")]
    public class NoOpAction : BarAction
    {
        protected override Task<bool> InvokeImpl(string? source = null)
        {
            return Task.FromResult(true);
        }
    }

    [JsonTypeName("internal")]
    public class InternalAction : BarAction
    {
        [JsonProperty("function", Required = Required.Always)]
        public string? FunctionName { get; set; }

        [JsonProperty("args")]
        public Dictionary<string, string> Arguments { get; set; } = new Dictionary<string, string>();

        protected override Task<bool> InvokeImpl(string? source = null)
        {
            if (this.FunctionName == null)
            {
                return Task.FromResult(true);
            }

            Dictionary<string, string> resolvedArgs = this.Arguments
                .ToDictionary(kv => kv.Key, kv => this.ResolveString(kv.Value, source) ?? string.Empty);

            return InternalFunctions.Default.InvokeFunction(this.FunctionName, resolvedArgs);
        }
    }

    [JsonTypeName("gpii")]
    public class GpiiAction : BarAction
    {
        [JsonProperty("data", Required = Required.Always)]
        public JObject RequestObject { get; set; } = null!;

        protected override async Task<bool> InvokeImpl(string? source = null)
        {
            ClientWebSocket socket = new ClientWebSocket();
            CancellationTokenSource cancel = new CancellationTokenSource();
            await socket.ConnectAsync(new Uri("ws://localhost:8081/pspChannel"), cancel.Token);

            string requestString = this.RequestObject.ToString();
            byte[] bytes = Encoding.UTF8.GetBytes(requestString);
            
            ArraySegment<byte> sendBuffer = new ArraySegment<byte>(bytes);
            await socket.SendAsync(sendBuffer, WebSocketMessageType.Text, true, cancel.Token);

            return true;
        }
    }
}