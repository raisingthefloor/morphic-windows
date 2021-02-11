namespace Morphic.Client.Bar
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Xml;
    using Config;
    using SharpVectors.Converters;
    using SharpVectors.Dom.Svg;
    using SharpVectors.Renderers.Wpf;

    public class BarImages
    {
        /// <summary>
        /// Gets the full path to a bar icon in the assets directory, based on its name (with or without the extension).
        /// </summary>
        /// <param name="name">Name of the icon.</param>
        /// <returns></returns>
        public static string? GetBarIconFile(string name)
        {
            string safe = new Regex(@"\.\.|[^-a-zA-Z0-9./]+", RegexOptions.Compiled)
                .Replace(name, "_")
                .Trim('/')
                .Replace('/', Path.DirectorySeparatorChar);
            string assetFile = AppPaths.GetAssetFile("bar-icons\\" + safe);
            string[] extensions = { "", ".svg", ".png", ".ico", ".jpg", ".jpeg", ".gif" };

            string? foundFile = extensions.Select(extension => assetFile + extension)
                .FirstOrDefault(File.Exists);

            return foundFile;
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

            // Attempt to load an SVG image.
            ImageSource? TrySvg()
            {
                try
                {
                    using FileSvgReader svg = new FileSvgReader(new WpfDrawingSettings());
                    DrawingGroup drawingGroup = svg.Read(imagePath);
                    if (color.HasValue)
                    {
                        ChangeDrawingColor(drawingGroup, color.Value);
                    }

                    return new DrawingImage(drawingGroup);
                }
                catch (Exception e) when (e is NotSupportedException || e is XmlException || e is SvgException)
                {
                    return null;
                }
            }

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
                catch (Exception e) when (e is NotSupportedException || e is XmlException || e is SvgException)
                {
                    return null;
                }
            }

            if (!imagePath.Contains('/'))
            {
                imagePath = GetBarIconFile(imagePath) ?? imagePath;
            }

            if (Path.GetExtension(imagePath) == ".svg")
            {
                result = TrySvg() ?? TryBitmap();
            }
            else
            {
                result = TryBitmap() ?? TrySvg();
            }

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
