// WebAction.cs: Bar action that opens a website.
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
    using Microsoft.Extensions.Logging;
    using Morphic.Core;
    using Newtonsoft.Json;
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;

    /// <summary>
    /// A web-link action.
    /// </summary>
    [JsonTypeName("link")]
    public class WebAction : BarAction
    {
        private string? urlString;

        [JsonProperty("url", Required = Required.Always)]
        public string UrlString
        {
            get => this.Uri?.ToString() ?? this.urlString ?? string.Empty;
            set
            {
                if (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
                {
                    // validate our uri
                    switch (uri?.Scheme.ToLowerInvariant()) {
                        case "http":
                        case "https":
                            // allowed
                            break;
                        case "skype":
                            // allowed for now, but in the future we may want to launch Skype directly and handle this information seperately
                            break;
                        default:
                            // all other schemes (as well as a null scheme) are disallowed
                            uri = null;
                            break;
                    }

                    // save our validated uri
                    this.Uri = uri;
                }
                else
                {
                    this.urlString = value;
                    App.Current.Logger.LogWarning($"Unable to parse url '{this.urlString}'");
                }
            }
        }

        public Uri? Uri { get; set; }

        /// <summary>
        /// Use the site's favicon as the default.
        /// </summary>
        public override Uri? DefaultImageUri
        {
            get
            {
                return null;
//                this.Uri is not null ? new Uri($"https://icons.duckduckgo.com/ip2/{this.Uri.Host}.ico") : null;
            }
        }

        public string? TelemetryEventName { get; set; }

        protected override Task<MorphicResult<MorphicUnit, MorphicUnit>> InvokeAsyncImpl(string? source = null, bool? toggleState = null)
        {
            try
            {
                bool success = true;
                if (this.Uri is not null)
                {
                    Process? process = Process.Start(new ProcessStartInfo()
                    {
                        FileName = this.ResolveString(this.Uri?.ToString(), source),
                        UseShellExecute = true
                    });
                    success = process is not null;
                }

                MorphicResult<MorphicUnit, MorphicUnit> result = success ? MorphicResult.OkResult() : MorphicResult.ErrorResult();
                return Task.FromResult(result);
            }
            finally
            {
                if (this.TelemetryEventName is not null)
                {
                    // NOTE: ideally we would change this whole function into an async function (instead of using the Task.FromResult pattern)
                    Task.Run(async () =>
                    {
                        await App.Current.Telemetry_RecordEventAsync(this.TelemetryEventName!);
                    });
                }
            }
        }
    }
}
