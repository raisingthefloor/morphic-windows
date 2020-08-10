// BarItem.cs: An item on a bar.
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
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Xml;
    using Newtonsoft.Json;
    using SharpVectors.Converters;
    using SharpVectors.Dom.Svg;
    using SharpVectors.Renderers.Wpf;
    using UI;

    /// <summary>
    /// A bar item.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    [JsonConverter(typeof(TypedJsonConverter), "widget", "button")]
    public class BarItem
    {
        /// <summary>
        /// true if the item is to be displayed on the pull-out bar.
        /// </summary>
        [JsonProperty("is_primary")]
        public bool IsPrimary { get; set; }
        
        /// <summary>
        /// The text displayed on the item.
        /// </summary>
        [JsonProperty("configuration.label")]
        public string? Text { get; set; }
        
        /// <summary>
        /// Tooltip main text (default is the this.Text).
        /// </summary>
        [JsonProperty("configuration.toolTipHeader")]
        public string? ToolTip { get; set; }

        /// <summary>
        /// Tooltip smaller text.
        /// </summary>
        [JsonProperty("configuration.toolTipInfo")]
        public string? ToolTipInfo { get; set; }

        /// <summary>
        /// The background colour (setter from json to allow empty strings).
        /// </summary>
        [JsonProperty("configuration.color")]
        public string ColorValue
        {
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    if (ColorConverter.ConvertFromString(value) is Color color)
                    {
                        this.Color = color;
                    }
                }
            }
            get => "";
        }

        /// <summary>
        /// The background colour.
        /// </summary>
        public Color Color
        {
            get => this.Theme.Background ?? Colors.Transparent;
            set
            {
                this.Theme.Background = value;
                this.Theme.InferStateThemes(true);
            }
        }

        /// <summary>
        /// Don't display this item.
        /// </summary>
        [JsonProperty("hidden")]
        public bool Hidden { get; set; }
        
        /// <summary>
        /// Theme for the item.
        /// </summary>
        [JsonProperty("theme", DefaultValueHandling = DefaultValueHandling.Populate)]
        public BarItemTheme Theme { get; set; } = new BarItemTheme();

        /// <summary>
        /// Items are sorted by this.
        /// </summary>
        [JsonProperty("priority")]
        public int Priority { get; set; }

        /// <summary>
        /// The type of control used. This is specified by using BarControl attribute in a subclass of this.
        /// </summary>
        public Type ControlType => this.GetType().GetCustomAttribute<BarControlAttribute>()?.Type!;

        /// <summary>
        /// Called when the bar has loaded.
        /// </summary>
        /// <param name="bar"></param>
        public virtual void Deserialized(BarData bar)
        {
            // Inherit the default theme
            this.Theme.Inherit(bar.DefaultTheme);
            this.Theme.InferStateThemes();
        }
    }

    /// <summary>
    /// Button bar item.
    /// </summary>
    [JsonTypeName("button")]
    [BarControl(typeof(BarButtonControl))]
    public class BarButton : BarItem, INotifyPropertyChanged
    {
        private string? imagePath;
        private string? imageValue;
        private ImageSource? imageSource;
        private Uri? remoteImage;

        [JsonProperty("configuration", ObjectCreationHandling = ObjectCreationHandling.Replace)]
        [JsonConverter(typeof(TypedJsonConverter), "kind", "null")]
        public BarAction Action { get; set; } = new BarNoAction();

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

                Uri.TryCreate(this.imageValue, UriKind.Absolute, out Uri? uri);

                if (uri != null && !uri.IsFile)
                {
                    // Download later.
                    this.RemoteImage = new Uri(this.imageValue);
                }
                else
                {
                    // Check if it points to a file within the assets directory.
                    string safe = new Regex("[^-a-zA-Z0-9.]+", RegexOptions.Compiled).Replace(this.imageValue, "_");
                    string assetFile = AppPaths.GetAssetFile("bar-icons\\" + safe);
                    string[] extensions = {"", ".svg", ".png", ".ico", ".jpg", ".jpeg", ".gif"};
                    
                    string? foundFile = extensions.Select(extension => assetFile + extension)
                        .FirstOrDefault(File.Exists);

                    if (foundFile == null)
                    {
                        this.ImagePath = string.Empty;
                    }
                    else
                    {
                        this.ImagePath = foundFile;
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
                        Console.WriteLine(this.RemoteImage);
                        await wc.DownloadFileTaskAsync(this.RemoteImage, tempFile);
                    }
                    finally
                    {
                        Console.WriteLine(this.RemoteImage + " done");
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
                }
                finally
                {
                    File.Delete(tempFile);
                }
            }

            // Load the local image.
            if (!string.IsNullOrEmpty(this.ImagePath) && File.Exists(this.ImagePath))
            {
                try
                {
                    BitmapImage image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.UriSource = new Uri(this.ImagePath);
                    image.EndInit();
                    
                    this.ImageSource = image;
                    success = true;
                }
                catch (NotSupportedException)
                {
                    try
                    {
                        using FileSvgReader svg = new FileSvgReader(new WpfDrawingSettings());
                        this.ImageSource = new DrawingImage(svg.Read(this.ImagePath));
                        success = true;
                    }
                    catch (Exception e) when (e is XmlException || e is SvgException)
                    {
                        // Nothing
                    }
                }
            }

            // Fallback to a default image.
            if (!success)
            {
                ImageSource? source = this.Action.DefaultImageSource;
                if (source != null)
                {
                    this.ImageSource = source;
                    success = true;
                }
                else
                {
                    Uri? defaultUri = this.Action.DefaultImageUri;
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

        public override async void Deserialized(BarData bar)
        {
            base.Deserialized(bar);
            _ = this.LoadImage();
        }

        public bool ShowIcon => true;// string.IsNullOrEmpty(this.IconPath);

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }

    /// <summary>
    /// Image bar item.
    /// </summary>
    [JsonTypeName("image")]
    public class BarImage : BarButton
    {
    }
    
    /// <summary>
    /// Used by a BarItem subclass to identify the control used to display the item.
    /// </summary>
    public class BarControlAttribute : Attribute
    {
        public Type Type { get; }

        public BarControlAttribute(Type type)
        {
            this.Type = type;
        }
    }
}