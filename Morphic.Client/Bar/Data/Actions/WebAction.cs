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
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

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
        public override Uri? DefaultImageUri =>
            this.Uri != null ? new Uri($"https://icons.duckduckgo.com/ip2/{this.Uri.Host}.ico") : null;

        protected override Task<bool> InvokeImpl(string? source = null, bool? toggleState = null)
        {
            bool success = true;
            if (this.Uri != null)
            {
                Process? process = Process.Start(new ProcessStartInfo()
                {
                    FileName = this.ResolveString(this.Uri?.ToString(), source),
                    UseShellExecute = true
                });
                success = process != null;
            }

            return Task.FromResult(success);
        }
    }
}
