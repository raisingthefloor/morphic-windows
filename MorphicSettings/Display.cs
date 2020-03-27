using System;
using Native = Morphic.Windows.Native;
using System.Collections.Generic;
using System.Linq;

namespace MorphicSettings
{

    /// <summary>
    /// A system display 
    /// </summary>
    public class Display
    {

        #region Creating a Display

        public Display(string name)
        {
            Name = name;
        }

        private static Display? primary;

        public static Display Primary
        {
            get
            {
                if (primary == null)
                {
                    if (Native.Display.GetPrimaryDisplayAdapterName() is string name)
                    {
                        primary = new Display(name);
                    }
                    else
                    {
                        throw new Exception("no primary display");
                    }
                }
                return primary;
            }
        }

        #endregion

        #region Identification

        public string Name { get; private set; }

        #endregion

        #region Zoom Level

        public enum ZoomLevel
        {
            Normal,
            Percent125,
            Percent150,
            Percent200
        }

        public bool SetZoomLevel(ZoomLevel zoomLevel)
        {
            var settings = GetDisplaySettingsForZoomLevel(zoomLevel);
            Native.Display.SetCurrentDisplaySettings(Name, settings);
            return true;
        }

        public IEnumerable<string> PossibleSettingsStrings()
        {
            return PossibleSettings().Select(setting => NativeDisplayExtensions.ToString(setting));
        }

        private List<Native.Display.DisplaySettings> PossibleSettings()
        {
            var settings = Native.Display.GetAllDisplaySettingsForDisplayAdapter(Name);
            if (Native.Display.GetCurrentDisplaySettingsForDisplayAdapter(Name) is Native.Display.DisplaySettings current)
            {
                settings = settings.Where(setting => setting.refreshRateInHertz == current.refreshRateInHertz && setting.orientation == current.orientation && (setting.fixedOutputOption ?? Native.Display.DisplaySettings.FixedResolutionOutputOption.Default) == Native.Display.DisplaySettings.FixedResolutionOutputOption.Default && setting.MatchesAspectRatio(current)).ToList();
            }
            settings.Sort((a, b) => (int)a.widthInPixels - (int)b.widthInPixels);
            return settings;
        }

        private Native.Display.DisplaySettings GetDisplaySettingsForZoomLevel(ZoomLevel zoomLevel)
        {
            var possibleSettings = PossibleSettings();
            var normalSettings = possibleSettings.First(settings => settings.IsDefault());
            possibleSettings.Reverse();
            switch (zoomLevel)
            {
                case ZoomLevel.Normal:
                    return normalSettings;
                case ZoomLevel.Percent125:
                    try
                    {
                        return possibleSettings.FirstOrDefault(settings => settings.widthInPixels <= (int)((double)normalSettings.widthInPixels * 4.0 / 5.0));
                    }
                    catch
                    {
                        return possibleSettings.Last();
                    }
                case ZoomLevel.Percent150:
                    try
                    {
                        return possibleSettings.FirstOrDefault(settings => settings.widthInPixels <= (int)((double)normalSettings.widthInPixels * 2.0 / 3.0));
                    }
                    catch
                    {
                        return possibleSettings.Last();
                    }
                case ZoomLevel.Percent200:
                    try
                    {
                        return possibleSettings.FirstOrDefault(settings => settings.widthInPixels <= (int)((double)normalSettings.widthInPixels / 2.0));
                    }
                    catch
                    {
                        return possibleSettings.Last();
                    }
                default:
                    throw new ArgumentException("Invalid ZoomLevel");
            }
        }

        #endregion

    }

    public static class NativeDisplayExtensions
    {
        public static bool IsDefault(this Native.Display.DisplaySettings settings)
        {
            // FIXME: not sure how to tell this
            return settings.widthInPixels == 3840;
        }

        public static string ToString(this Native.Display.DisplaySettings settings)
        {
            return $"{settings.widthInPixels}x{settings.heightInPixels} @{settings.refreshRateInHertz} {settings.bitsPerPixel}bpp {settings.fixedOutputOption?.ToString() ?? "null"}";
        }

        public static double GetAspectRatio(this Native.Display.DisplaySettings settings)
        {
            return (double)settings.widthInPixels / (double)settings.heightInPixels;
        }

        public static bool MatchesAspectRatio(this Native.Display.DisplaySettings settings, Native.Display.DisplaySettings other)
        {
            return Math.Abs(settings.GetAspectRatio() - other.GetAspectRatio()) < 0.1;
        }
    }
}
