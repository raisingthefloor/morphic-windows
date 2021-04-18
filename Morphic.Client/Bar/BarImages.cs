namespace Morphic.Client.Bar
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Resources;
    using System.Xml;
    using Config;

    public class BarImages
    {
        /// <summary>
        /// Gets the full path to a bar icon in the assets directory, based on its name (with or without the extension).
        /// </summary>
        /// <param name="name">Name of the icon.</param>
        /// <returns></returns>
        public static string? GetBarIconFile(string name)
        {
            var translatedName = BarImages.TranslateImageUrlToFileName(name);
            if (translatedName != null)
            {
                name = translatedName;
            }

            string safe = new Regex(@"\.\.|[^-a-zA-Z0-9./]+", RegexOptions.Compiled)
                .Replace(name, "_")
                .Trim('/')
                .Replace('/', Path.DirectorySeparatorChar);
            string assetFile = AppPaths.GetAssetFile("bar-icons\\" + safe);
            string[] extensions = { "", ".xaml", ".png", ".ico", ".jpg", ".jpeg", ".gif" };

            string? foundFile = extensions.Select(extension => assetFile + extension)
                .FirstOrDefault(File.Exists);

            return foundFile;
        }

        // NOTE: the image_url values we get back from the v1 API do not always represent the filename, so we need to map them here
        //       in the (very-near-term) future, we must standardize on URLs or another form via the API; manual mapping is not sustainable
        public static string? TranslateImageUrlToFileName(string? imageUrl) {
            switch (imageUrl) {
                case "abcnews":
                    return "logo_abcNews";
                case "aljazeera":
                    return "logo_alJazeera";
                case "amazon":
                    return "logo_amazon";
                case "amazonmusic":
                    return "logo_amazonMusic";
                case "aolold":
                    return "logo_aolOld";
                case "bbc":
                    return "logo_bbc";
                case "bestbuy":
                    return "logo_bestBuy";
                case "bloomberg":
                    return "logo_bloomberg";
                case "box":
                    return "logo_box";
                case "calculator":
                    return "logo_calculator";
                case "calendar$calendar":
                    return "calendar";
                case "cbsnews":
                    return "logo_cbsNews";
                case "chrome":
                    return "logo_chrome";
                case "cnbc":
                    return "logo_cnbc";
                case "cnn":
                    return "logo_cnn";
                case "craigslist":
                    return "logo_craigslist";
                case "deezer":
                    return "logo_deezer";
                case "disneyplus":
                    return "logo_disneyPlus";
                case "dropbox":
                    return "logo_dropbox";
                case "drudgereport":
                    return "logo_drudgeReport";
                case "ebay":
                    return "logo_ebay";
                case "etsy":
                    return "logo_etsy";
                case "email$envelope":
                    return "envelope";
                case "email$envelopeopen":
                    return "envelope-open";
                case "email$envelopeopentext":
                    return "envelope-open-text";
                case "email$envelopeoutline":
                    return "envelope-outline";
                case "email$envelopeoutlineopen":
                    return "envelope-outline-open";
                case "facebook":
                    return "logo_facebook";
                case "faviconfoxnews":
                    return "favicon_foxNews";
                case "firefox":
                    return "logo_firefox";
                case "forbes":
                    return "logo_forbes";
                case "foxnews":
                    return "logo_foxNews";
                case "gmail":
                    return "logo_gmail";
                case "googledrive":
                    return "logo_googleDrive";
                case "googlenews":
                    return "logo_googleNews";
                case "huffpost":
                    return "logo_huffpost";
                case "hulu":
                    return "logo_hulu";
                case "icloud":
                    return "logo_icloud";
                case "news$newspaper":
                    return "newspaper";
                case "iheartradio":
                    return "logo_iheartRadio";
                case "imgur":
                    return "logo_imgur";
                case "instagram":
                    return "logo_instagram";
                case "itunes":
                    return "logo_itunes";
                case "kohls":
                    return "logo_kohls";
                case "latimes":
                    return "logo_laTimes";
                case "linkedin":
                    return "logo_linkedIn";
                case "macys":
                    return "logo_macys";
                case "mail":
                    return "logo_mail";
                case "msedge":
                    return "logo_msedge";
                case "msquickassist":
                    return "logo_msquickassist";
                case "nbcnews":
                    return "logo_nbcNews";
                case "netflix":
                    return "logo_netflix";
                case "nextdoor":
                    return "logo_nextdoor";
                case "newyorktimes":
                    return "logo_newYorkTimes";
                case "npr":
                    return "logo_npr";
                case "onedrive":
                    return "logo_onedrive";
                case "opera":
                    return "logo_opera";
                case "outlook":
                    return "logo_outlook";
                case "pandora":
                    return "logo_pandora";
                case "pinterest":
                    return "logo_pinterest";
                case "reddit":
                    return "logo_reddit";
                case "reuters":
                    return "logo_reuters";
                case "skype":
                    return "logo_skype";
                case "spotify":
                    return "logo_spotify";
                case "soundcloud":
                    return "logo_soundcloud";
                case "target":
                    return "logo_target";
                case "theguardian":
                    return "logo_theGuardian";
                case "thehill":
                    return "logo_theHill";
                case "tidal":
                    return "logo_tidal";
                case "tumblr":
                    return "logo_tumblr";
                case "twitter":
                    return "logo_twitter";
                case "usatoday":
                    return "logo_usaToday";
                case "vimeo":
                    return "logo_vimeo";
                case "walmart":
                    return "logo_walmart";
                case "washingtonpost":
                    return "logo_washingtonPost";
                case "wayfair":
                    return "logo_wayfair";
                case "windowmaximize":
                    return "window-maximize";
                case "wsj":
                    return "logo_wsj";
                case "yahoo":
                    return "logo_yahoo";
                case "yahoomail":
                    return "logo_yahoomail";
                case "youtube":
                    return "logo_youtube";
                case "youtubemusic":
                    return "logo_youtubeMusic";
                case null:
                default:
                    return imageUrl;
            }
        }


        /// <summary>
        /// Creates an image source from a local image.
        /// </summary>
        /// <param name="imagePath">The path to the image, or the name of the icon in the assets directory.</param>
        /// <param name="color">The color, for monochrome vectors.</param>
        /// <returns>null if the image is not supported.</returns>
        public static ImageSource? CreateImageSource(string imagePath, Color? color = null)
        {
            ImageSource? result;

            // Attempt to load a bitmap image.
            ImageSource? TryBitmap()
            {
                try
                {
                    BitmapImage image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.UriSource = new Uri(imagePath);
                    image.EndInit();
                    return image;
                }
                catch (Exception e) when (e is NotSupportedException || e is XmlException)
                {
                    return null;
                }
            }

            if ((imagePath.Contains('/') == false) && (imagePath.Contains('\\') == false))
            {
                imagePath = GetBarIconFile(imagePath) ?? imagePath;
            }

            result = TryBitmap();

            return result;
        }

        /// <summary>
        /// Replaces the brushes used in a monochrome drawing with a new one, which can be set to a specific colour.
        /// </summary>
        /// <param name="drawing">The drawing to change.</param>
        /// <param name="color">The new colour to set (if brush is null).</param>
        /// <param name="brush">The brush to use.</param>
        /// <returns>The brush used (null if the drawing isn't monochrome).</returns>
        public static SolidColorBrush? ChangeDrawingColor(Drawing drawing, Color color, SolidColorBrush? brush = null)
        {
            List<GeometryDrawing>? geometryDrawings;

            // Get all the geometries in the drawing.
            if (drawing is DrawingGroup drawingGroup)
            {
                geometryDrawings = GetDrawings(drawingGroup).OfType<GeometryDrawing>().ToList();
            }
            else
            {
                geometryDrawings = new List<GeometryDrawing>();
                if (drawing is GeometryDrawing gd)
                {
                    geometryDrawings.Add(gd);
                }
            }

            // If there's only 1 colour, it's mono.
            bool mono = geometryDrawings.Count > 0
                && geometryDrawings
                    .Select(gd => gd.Brush)
                    .OfType<SolidColorBrush>()
                    .Where(b => b.Opacity > 0)
                    .Select(b => b.Color)
                    .Where(c => c.A != 0)
                    .Distinct()
                    .Count() == 1;

            if (!mono)
            {
                return null;
            }
            else
            {
                brush ??= new SolidColorBrush(color);
                geometryDrawings.ForEach(gd =>
                {
                    if (gd.Brush is SolidColorBrush && gd.Brush.Opacity > 0)
                    {
                        gd.Brush = brush;
                    }
                });
                return brush;
            }
        }

        /// <summary>
        /// Gets all drawings within a drawing group.
        /// </summary>
        /// <param name="drawingGroup"></param>
        /// <returns></returns>
        private static IEnumerable<Drawing> GetDrawings(DrawingGroup drawingGroup)
        {
            return drawingGroup.Children.OfType<DrawingGroup>()
                .SelectMany(GetDrawings)
                .Concat(drawingGroup.Children.OfType<GeometryDrawing>());
        }
    }
}
