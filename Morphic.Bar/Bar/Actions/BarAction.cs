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
        public string Id { get; set; }

        public abstract Task<bool> Invoke();

        public virtual Uri? DefaultImageUri { get; }
        public virtual ImageSource? DefaultImageSource { get; }
        public virtual bool IsAvailable { get; protected set; } = true;

    }

    [JsonTypeName("null")]
    public class NoOpAction : BarAction
    {
        public override Task<bool> Invoke()
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
        public string[]? Arguments { get; set; }

        public override Task<bool> Invoke()
        {
            if (this.FunctionName == null)
            {
                return Task.FromResult(true);
            }

            return ActionFunctions.Default.InvokeFunction(this.FunctionName, this.Arguments ?? new string[0]);
        }
    }

    [JsonTypeName("gpii")]
    public class GpiiAction : BarAction
    {
        [JsonProperty("data", Required = Required.Always)]
        public JObject RequestObject { get; set; } = null!;

        public override async Task<bool> Invoke()
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