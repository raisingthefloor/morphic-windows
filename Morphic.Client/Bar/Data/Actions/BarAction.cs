// BarAction.cs: Actions performed by bar items.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

namespace Morphic.Client.Bar.Data.Actions
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using System.Windows.Media;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// An action for a bar item.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    [JsonConverter(typeof(TypedJsonConverter), "kind", "shellExec")]
    public abstract class BarAction : IDeserializable
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
            Task<bool> result;
            try
            {
                try
                {
                    result = this.InvokeImpl(source);
                }
                catch (Exception e) when (!(e is ActionException || e is OutOfMemoryException))
                {
                    throw new ActionException(e.Message, e);
                }
            }
            catch (ActionException e)
            {
                App.Current.Logger.LogError(e, $"Error while invoking action for bar {this.Id} {this}");

                if (e.UserMessage != null)
                {
                    MessageBox.Show($"There was a problem performing the action:\n\n{e.UserMessage}",
                        "Morphic Community Bar", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }

                result = Task.FromResult(false);
            }

            return result;
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

        public virtual void Deserialized()
        {
        }
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

    [JsonTypeName("shellExec")]
    public class ShellExecuteAction : BarAction
    {
        [JsonProperty("run")]
        public string? ShellCommand { get; set; }

        protected override Task<bool> InvokeImpl(string? source = null)
        {
            bool success = true;
            if (!string.IsNullOrEmpty(this.ShellCommand))
            {
                Process? process = Process.Start(new ProcessStartInfo()
                {
                    FileName = this.ResolveString(this.ShellCommand, source),
                    UseShellExecute = true
                });
                success = process != null;
            }

            return Task.FromResult(success);
        }

        public override void Deserialized()
        {
        }
    }

    /// <summary>
    /// Exception that gets thrown by action invokers.
    /// </summary>
    public class ActionException : ApplicationException
    {
        /// <summary>
        /// The message displayed to the user. null to not display a message.
        /// </summary>
        public string? UserMessage { get; set; }

        public ActionException(string? userMessage)
            : this(userMessage, userMessage, null)
        {
        }
        public ActionException(string? userMessage, Exception innerException)
            : this(userMessage, userMessage, innerException)
        {
        }

        public ActionException(string? userMessage, string? internalMessage = null, Exception? innerException = null)
            : base(internalMessage ?? userMessage ?? innerException?.Message, innerException)
        {
            this.UserMessage = userMessage;
        }
    }

}