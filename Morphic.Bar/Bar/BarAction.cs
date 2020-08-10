// BarAction.cs: Actions performed by bar items.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

namespace Morphic.Bar.Bar
{
    using System;
    using System.Diagnostics;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Media;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// An action for a bar item.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public abstract class BarAction
    {
        public abstract Task<bool> Invoke();

        public virtual Uri? DefaultImageUri { get; }
        public virtual ImageSource? DefaultImageSource { get; }

    }

    [JsonTypeName("null")]
    public class BarNoAction : BarAction
    {
        public override Task<bool> Invoke()
        {
            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// A web-link action.
    /// </summary>
    [JsonTypeName("link")]
    public class BarWebAction : BarAction
    {
        [JsonProperty("url", Required = Required.Always)]
        public string UrlString
        {
            // Wrapping a Uri means the URL is validated during load.
            get => this.Uri.ToString();
            set => this.Uri = new Uri(value);
        }

        public Uri Uri { get; set; } = null!;

        /// <summary>
        /// Use the site's favicon as the default.
        /// </summary>
        public override Uri? DefaultImageUri => new Uri($"https://icons.duckduckgo.com/ip2/{this.Uri.Host}.ico");

        public override Task<bool> Invoke()
        {
            Process.Start(new ProcessStartInfo()
            {
                FileName = this.Uri.ToString(),
                UseShellExecute = true
            });

            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// Action to start an application.
    /// </summary>
    [JsonTypeName("application")]
    public class BarAppAction : BarAction
    {
        [JsonProperty("exe", Required = Required.Always)]
        public string AppName { get; set; } = null!;

        public override Task<bool> Invoke()
        {
            MessageBox.Show($"Opens the application {this.AppName}");
            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// Action to start an application.
    /// </summary>
    [JsonTypeName("action")]
    public class BarBuiltinAction : BarAction
    {
        [JsonProperty("identifier", Required = Required.Always)]
        public string Identifier { get; set; } = null!;

        public override Task<bool> Invoke()
        {
            MessageBox.Show($"Run the action {this.Identifier}");
            return Task.FromResult(true);
        }
    }

    [JsonTypeName("gpii")]
    public class BarGpiiAction : BarAction
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