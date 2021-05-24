// Copyright 2021 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-windows/blob/master/LICENSE.txt
//
// The R&D leading to these results received funding from the:
// * Rehabilitation Services Administration, US Dept. of Education under
//   grant H421A150006 (APCP)
// * National Institute on Disability, Independent Living, and
//   Rehabilitation Research (NIDILRR)
// * Administration for Independent Living & Dept. of Education under grants
//   H133E080022 (RERC-IT) and H133E130028/90RE5003-01-00 (UIITA-RERC)
// * European Union's Seventh Framework Programme (FP7/2007-2013) grant
//   agreement nos. 289016 (Cloud4all) and 610510 (Prosperity4All)
// * William and Flora Hewlett Foundation
// * Ontario Ministry of Research and Innovation
// * Canadian Foundation for Innovation
// * Adobe Foundation
// * Consumer Electronics Association Foundation

using Morphic.Core;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Morphic.Telemetry
{
    public class MorphicTelemetryClient: IDisposable
    {
        private IMqttClient _mqttClient;
        private IMqttClientOptions _mqttClientOptions;

        private string _clientId;

        private List<MqttEventMessage> _messagesToSend;
        private object _messagesToSendSyncObject;
        private AutoResetEvent _messagesToSendEvent;

        private enum SessionState
        {
            Stopped,
            Starting,
            Started,
            Stopping
        }
        private SessionState _sessionState = SessionState.Stopped;
        private bool _isConnected = false;
        private bool _isDisposed = false;

        private Thread? _sendMessagesThread;

        public string? SiteId = null;

        public MorphicTelemetryClient(string mqttHostname, string clientId, string username, string password)
        {
            _messagesToSend = new List<MqttEventMessage>();
            _messagesToSendSyncObject = new object();
            _messagesToSendEvent = new AutoResetEvent(false);

            // initialize and capture our MQTT client and its configuration options
            // create a new MqttClient instance
            var mqttFactory = new MqttFactory();
            _mqttClient = mqttFactory.CreateMqttClient();

            // set up our MqttClient's options
            _mqttClientOptions = new MqttClientOptionsBuilder()
                .WithClientId(clientId)
                .WithTcpServer(mqttHostname)
                .WithCredentials(username, password)
                .WithCleanSession(true) // we are a write-only client (i.e. no subscriptions), so always start with a clean session
                .WithTls(new MqttClientOptionsBuilderTlsParameters() { UseTls = false })
                .Build();

            // set up connect handler (in case we want to do anything immediately after successful connection)
            _mqttClient.UseConnectedHandler(async e =>
            {
                await this.MqttClientConnected(_mqttClient, e);
            });
            // set up disconnect handler (to handle automatic reconnection)
            _mqttClient.UseDisconnectedHandler(async e =>
            {
                await this.MqttClientDisconnected(_mqttClient, e);
            });

            _clientId = clientId;
        }

        public async Task StartSessionAsync()
        {
            switch (_sessionState)
            {
                case SessionState.Started:
                case SessionState.Starting:
                    // if our session is already started (or is starting), just return
                    return;
                case SessionState.Stopping:
                    // if our session is stopping, wait until the session is stopped
                    while (_sessionState == SessionState.Stopping)
                    {
                        if (_isDisposed == true)
                        {
                            return;
                        }
                        await Task.Delay(100);
                    }
                    // recall this function with the updated state
                    // NOTE: alternatively we could put this sessionstate check in a loop (refactored out into another function); that would avoid potential but extremely unlikely deep recursion
                    await this.StartSessionAsync();
                    return;
                case SessionState.Stopped:
                    // if our session is stopped, continue; this is the appropriate state to call this function
                    break;
            }

            if (_isDisposed == true)
            {
                throw new InvalidOperationException("Cannot re-start a session after the object has been disposed");
            }

            _sessionState = SessionState.Starting;

            // connect to telemetry server in the background
            var initialConnectionResult = await ConnectToMqttServerAsync();
            if (initialConnectionResult.IsError == true)
            {
                // NOTE: if we cannot connect to the server, our Disconnected event will automatically wait and then retry indefinitely
                // TODO: we might want to consider letting our caller know that we are in a "connecting" or "failure" state
            }
            else
            {
                // we are connected
                // TODO: we might want to consider letting our caller know that we are connected
            }

            _sessionState = SessionState.Started;
        }

        private void SendMessages()
        {
            while (true)
            {
                // OBSERVATION: we shouldn't really need to put a timeout on this wait, but we're doing it out of an abundance of caution so we can regularly check if we're disposed/disconnected
                _messagesToSendEvent.WaitOne(2000);

                if (_isDisposed == true)
                {
                    return;
                }
                if (_isConnected == false)
                {
                    return;
                }

                MqttEventMessage messageToSend;
                lock (_messagesToSendSyncObject)
                {
                    var numberOfMessagesToSend = _messagesToSend.Count;
                    if (numberOfMessagesToSend > 0)
                    {
                        messageToSend = _messagesToSend[0];
                        _messagesToSend.RemoveAt(0);
                    }
                    else
                    {
                        continue;
                    }
                }

                // convert the message to JSON
                var payload = JsonSerializer.Serialize(messageToSend);

                // assemble the mqtt message
                var mqttMessage = new MqttApplicationMessageBuilder()
                    .WithTopic("telemetry")
                    .WithPayload(payload)
                    .WithAtLeastOnceQoS() // NOTE: what's the default if we don't specify this?
                    .WithRetainFlag() // NOTE: what's the default if we don't specify this?
                    .Build();

                // send the mqtt message
                var publishSuccess = false;
                try
                {
                    var publishResult = _mqttClient.PublishAsync(mqttMessage, CancellationToken.None).GetAwaiter().GetResult(); // Since 3.0.5 with CancellationToken
                    if (publishResult.ReasonCode == MQTTnet.Client.Publishing.MqttClientPublishReasonCode.Success)
                    {
                        publishSuccess = true;
                    }
                    else
                    {
                        publishSuccess = false;

	                    // if publishing the message was not successful, wait 2 seconds before trying again
                        Thread.Sleep(2000);
                        _messagesToSendEvent.Set();
                    }
                }
                catch
                {
                    // TODO: we may want to consider parsing the exception to determine what it means; it may just indicate that we're offline...but there are other exceptions as well
                }

                if (publishSuccess == false)
                {
                    // if we could not send the message, return it to the front of the queue
                    lock (_messagesToSendSyncObject)
                    {
                        _messagesToSend.Insert(0, messageToSend);
                    }
                }
            }
        }

        public async Task StopSessionAsync()
        {
            // TODO: dequeue and write out any messages to disk which haven't been fully sent yet; be sure to FIRST save out any message currently being sent (and resend it later)

            // disconnect from the MQTT server
            try
            {
                await _mqttClient.DisconnectAsync();
            }
            catch
            {
                // TODO: consider logging any disconnect errors
            }

            // manually set "isConnected" to false just in case our handler didn't get called in the event of failure...and to prevent any timing issues in regards to "_messagesToSendEvent.Set()" getting called before the event handler
            _isConnected = false;

            // set our "messages to send" event handle to make sure that it auto-disconnects
            _messagesToSendEvent.Set();

            // TODO: secure a lock to the _messagesToSendSyncObject and save any remaining queued events to disk

            // finally, dispose of our object
            this.Dispose();
        }

        public void Dispose()
        {
            _isDisposed = true;

            // tear down event handlers
            _mqttClient.UseConnectedHandler(async e => { });
            _mqttClient.UseDisconnectedHandler(async e => { });

            // TODO: consider checking to make sure we've saved out all remaining queued events to disk (in a "managed" version of Dispose)
        }

        // TODO: consider a property which lets us ping the server every X number of minutes so it knows that our session is still active

        // NOTE: when we connect, we start up the message-sending background thread
        private async Task MqttClientConnected(IMqttClient mqttClient, MQTTnet.Client.Connecting.MqttClientConnectedEventArgs e)
        {
            _isConnected = true;

            // start our message-sending thread
            // NOTE: we capture a reference to this thread because we want to be able to re-join/terminate it (although the CLR does not GC active threads...so it's not necessary otherwise)
            _sendMessagesThread = new Thread(SendMessages);
            _sendMessagesThread.Name = "MorphicTelemetry";
            _sendMessagesThread.IsBackground = true;
            _sendMessagesThread.Start();
        }

        // NOTE: when we disconnect, we shut down the message-sending background thread (and then we re-attempt to reconnect, if appropriate)
        private async Task MqttClientDisconnected(IMqttClient sender, MQTTnet.Client.Disconnecting.MqttClientDisconnectedEventArgs e)
        {
            _isConnected = false;

            // NOTE: this is a simple reconnection algorithm; in the future, we may want to enhance this further

            // NOTE: we always wait thirty seconds between retries, just to make sure that we don't flood the server with requests
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed.TotalSeconds < 30)
            {
                if (_isDisposed == true)
                {
                    // if we are already disposed, do nothing
                    _messagesToSendEvent.Set(); // let the messagesToSend thread know we're disposed
                    return;
                }

                switch (_sessionState)
                {
                    case SessionState.Started:
                    case SessionState.Starting:
                        // if the session is starting/started, then we will attempt to reconnect
                        break;
                    case SessionState.Stopping:
                    case SessionState.Stopped:
                        // if the session is stopped, do not attempt to reconnect
                        _messagesToSendEvent.Set(); // let the messagesToSend thread know we're stopped
                        return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(250));
            }

            try
            {
                // NOTE: the MQTT Client library should re-call this function if our connection fails
                await _mqttClient.ConnectAsync(_mqttClientOptions, CancellationToken.None); // Since 3.0.5 with CancellationToken
            }
            catch
            {
                // NOTE: could not reconnect to server; try again.  [This event handler is automatically called again by the MQTTClient library if a connection attempt failed...so we should not manually try again.]
            }
        }

        private async Task<IMorphicResult> ConnectToMqttServerAsync()
        {
            try
            {
                var connectResult = await _mqttClient.ConnectAsync(_mqttClientOptions);
                switch (connectResult.ResultCode)
                {
                    case MQTTnet.Client.Connecting.MqttClientConnectResultCode.Success:
                        return IMorphicResult.SuccessResult;
                    default:
                        return IMorphicResult.ErrorResult;
                }
            }
            catch
            {
                return IMorphicResult.ErrorResult;
            }
        }

        private struct MqttEventMessage
        {
            [JsonPropertyName("id")]
            public Guid Id { get; set; }
            //
            [JsonPropertyName("record_type")]
            public string RecordType { get; set; }
            //
            [JsonPropertyName("record_version")]
            public int RecordVersion { get; set; }
            //
            [JsonPropertyName("sent_at")]
            public DateTimeOffset SentAt { get; set; }
            //
            [JsonPropertyName("site_id")]
            public string? SiteId { get; set; }
            //
            [JsonPropertyName("device_id")]
            public string DeviceId { get; set; }
            //
            [JsonPropertyName("software_version")]
            public string SoftwareVersion { get; set; }
            //
            [JsonPropertyName("os_name")]
            public string OsName { get; set; }
            //
            [JsonPropertyName("os_version")]
            public string OsVersion { get; set; }
            //
            [JsonPropertyName("event_name")]
            public string EventName { get; set; }
        }

        private Lazy<string> CachedOsVersion = new Lazy<string>(() =>
        {
            return System.Environment.OSVersion.Version.ToString();
        });

        private Lazy<string> CachedSoftwareVersion = new Lazy<string>(() =>
        {
            return Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? "0.0.0.0";
        });

        public void EnqueueActionMessage(string eventName)
        {
            // NOTE: we capture the timestamp up front just to alleviate any potential for the timestamp to be captured late
            var capturedAtTimestamp = DateTimeOffset.UtcNow;

            var actionMessage = new MqttEventMessage()
            {
                Id = Guid.NewGuid(),
                RecordType = "event",
                RecordVersion = 1,
                SentAt = capturedAtTimestamp,
                SiteId = this.SiteId,
                DeviceId = _clientId,
                SoftwareVersion = this.CachedSoftwareVersion.Value,
                OsName = "Windows",
                OsVersion = this.CachedOsVersion.Value,
                EventName = eventName
            };

            lock (_messagesToSendSyncObject)
            {
                _messagesToSend.Add(actionMessage);
            }
            _messagesToSendEvent.Set();
        }
    }
}
