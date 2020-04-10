// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt
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
