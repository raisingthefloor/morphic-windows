// WebAction.cs: Bar action that opens a website.
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
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    /// <summary>
    /// A web-link action.
    /// </summary>
    [JsonTypeName("link")]
    public class WebAction : BarAction
    {

        public WebAction()
        {
        }

        public WebAction(Uri uri)
        {
            this.Uri = uri;
        }

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
}
