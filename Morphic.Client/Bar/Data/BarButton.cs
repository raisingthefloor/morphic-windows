// BarButton.cs: Button widget on the bar
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

namespace Morphic.Client.Bar.Data
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Net;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Xml;
    using Config;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using UI.BarControls;

    /// <summary>
    /// Button bar item.
    /// </summary>
    [JsonTypeName("button")]
    [BarControl(typeof(ButtonBarControl))]
    public class BarButton : BarItem, INotifyPropertyChanged
    {
        private string? imagePath;
        private string? imageValue;
        private ImageSource? imageSource;
        private Uri? remoteImage;

        public BarButton(BarData bar) : base(bar)
        {
        }

        [JsonProperty("configuration.image_path")]
        public string? FrontendImagePath { get; set; }

        /// <summary>
        /// The original image, as defined in json.
        /// </summary>
        [JsonProperty("configuration.image_url")]
        public string? ImageValue
        {
            get => this.imageValue;
            set
            {
                this.imageValue = value ?? string.Empty;
                if (string.IsNullOrEmpty(this.imageValue))
                {
                    this.ImagePath = string.Empty;
                }
                else
                {
                    Uri.TryCreate(this.imageValue, UriKind.Absolute, out Uri? uri);
                    string? localPath = null;
                    if (uri == null || uri.IsFile)
                    {
                        localPath = BarImages.GetBarIconFile(this.imageValue);
                        if (localPath == null)
                        {
                            uri = new Uri(this.Bar.FrontEndUri, this.FrontendImagePath);
                        }
                    }

                    if (localPath != null)
                    {
                        this.ImagePath = localPath;
                    }
                    else if (uri != null)
                    {
                        // Download later.
                        this.RemoteImage = uri;
                    }
                }
            }
        }

        /// <summary>
        /// The image to use.
        /// </summary>
        public ImageSource? ImageSource
        {
            get => this.imageSource;
            set
            {
                this.imageSource = value;
                this.OnPropertyChanged();
            }
        }

        private Canvas? _xamlContent;
        public Canvas? XamlContent
        {
            get => _xamlContent;
            set
            {
                _xamlContent = value;
                this.OnPropertyChanged();
            }
        }

        /// <summary>
        /// The real local path of the item's image.
        /// </summary>
        public string ImagePath
        {
            get => this.imagePath ?? string.Empty;
            private set => this.imagePath = value;
        }

        // Limit the concurrent downloads.
        private static SemaphoreSlim downloads = new SemaphoreSlim(8);
        private static HashSet<string> downloading = new HashSet<string>();
        private static HashSet<string> downloadComplete = new HashSet<string>();

        /// <summary>
        /// Loads the image specified by ImagePath.
        /// </summary>
        /// <returns>true on success.</returns>
        public async Task<bool> LoadImage()
        {
            bool success = false;

            // Download the remote image.
            if (this.DownloadRequired && this.RemoteImage != null)
            {
                using WebClient wc = new WebClient();
                string tempFile = this.ImagePath + ".new";
                try
                {
                    try
                    {
                        await downloads.WaitAsync();

                        // Check if the image is being downloaded by another bar item.
                        bool downloadRequired = downloading.Add(this.ImagePath);

                        if (downloadRequired)
                        {
                            // Download it
                            this.Logger.LogDebug("Downloading {remoteImage}", this.RemoteImage);
                            await wc.DownloadFileTaskAsync(this.RemoteImage, tempFile);
                        }
                        else
                        {
                            // wait for the other bar's download to complete
                            while (!downloadComplete.Contains(this.ImagePath))
                            {
                                await Task.Delay(500);
                            }
                        }
                    }
                    finally
                    {
                        downloads.Release();
                    }
                    FileInfo fileInfo = new FileInfo(tempFile);

                    if (fileInfo.Exists && fileInfo.Length > 0)
                    {
                        File.Move(tempFile, this.ImagePath, true);
                    }
                }
                catch (Exception e) when (!(e is OutOfMemoryException))
                {
                    // Ignore
                    this.Logger.LogWarning(e, "Download failed {remoteImage}", this.RemoteImage);
                }
                finally
                {
                    File.Delete(tempFile);
                    downloadComplete.Add(this.ImagePath);
                }
            }

            // Load the local image.
            if (!string.IsNullOrEmpty(this.ImagePath) && File.Exists(this.ImagePath))
            {
                if (Path.GetExtension(imagePath)?.ToLowerInvariant() == ".xaml")
                {
                    var xamlFileStream = new FileStream(this.ImagePath, FileMode.Open);
                    // TODO: when we move to .NET 5, set useRestrictiveXamlReader to true
                    var xamlAsCanvas = (Canvas)System.Windows.Markup.XamlReader.Load(new XmlTextReader(xamlFileStream) /*, true */);
                    this.XamlContent = xamlAsCanvas;

                    success = true;
                }
                else
                {
                    this.ImageSource = BarImages.CreateImageSource(this.ImagePath);
                    
                    success = this.ImageValue != null;
                }
            }

            // Fallback to a default image.
            if (!success)
            {
                ImageSource? source = this.Action?.DefaultImageSource;
                if (source != null)
                {
                    this.ImageSource = source;
                    success = true;
                }
                else
                {
                    Uri? defaultUri = this.Action?.DefaultImageUri;
                    if (defaultUri != null && this.RemoteImage != defaultUri)
                    {
                        this.RemoteImage = defaultUri;
                        success = await this.LoadImage();
                    }
                }
            }

            return success;
        }

        /// <summary>
        /// true if downloading a new copy of a remote image is needed.
        /// </summary>
        public bool DownloadRequired { get; set; }

        /// <summary>
        /// The URL to the remote image.
        /// </summary>
        public Uri? RemoteImage
        {
            get => this.remoteImage;
            private set
            {
                this.remoteImage = value;
                if (this.remoteImage != null)
                {
                    this.ImagePath = AppPaths.GetCacheFile(this.remoteImage, out bool exists);
                    this.DownloadRequired = !exists
                        || (DateTime.Now - File.GetLastWriteTime(this.ImagePath)).TotalDays > 2;
                }
            }
        }

        public override void Deserialized()
        {
            base.Deserialized();

            _ = this.LoadImage();
        }

        public bool ShowIcon => true;// string.IsNullOrEmpty(this.IconPath);

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
