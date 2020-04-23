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
            PossibleSettings = FindPossibleSettings();
            try
            {
                NormalSettings = PossibleSettings.Last();
            }
            catch
            {
                // oh well
            }
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

        private readonly List<Native.Display.DisplaySettings> PossibleSettings;

        public Native.Display.DisplaySettings? NormalSettings;

        public Native.Display.DisplaySettings? CurrentSettings
        {
            get
            {
                return Native.Display.GetCurrentDisplaySettingsForDisplayAdapter(Name);
            }
        }

        #endregion

        #region Zoom Level

        public double PercentageForZoomingIn
        {
            get
            {
                if (NormalSettings is Native.Display.DisplaySettings normal)
                {
                    if (CurrentSettings is Native.Display.DisplaySettings current)
                    {
                        Native.Display.DisplaySettings target;
                        try
                        {
                            target = PossibleSettings.Last(setting => setting.widthInPixels < current.widthInPixels);
                        }
                        catch
                        {
                            target = current;
                        }
                        return (double)target.widthInPixels / (double)normal.widthInPixels;
                    }
                }
                return 1.0;
            }
        }

        public double PercentageForZoomingOut
        {
            get
            {
                if (NormalSettings is Native.Display.DisplaySettings normal)
                {
                    if (CurrentSettings is Native.Display.DisplaySettings current)
                    {
                        Native.Display.DisplaySettings target;
                        try
                        {
                            target = PossibleSettings.First(setting => setting.widthInPixels > current.widthInPixels);
                        }
                        catch
                        {
                            target = current;
                        }
                        return (double)target.widthInPixels / (double)normal.widthInPixels;
                    }
                }
                return 1.0;
            }
        }

        public bool Zoom(double percentage)
        {
            if (GetDisplaySettingsForZoomPercentage(percentage) is Native.Display.DisplaySettings settings)
            {
                Native.Display.SetCurrentDisplaySettings(Name, settings);
                return true;
            }
            return false;
        }

        private List<Native.Display.DisplaySettings> FindPossibleSettings()
        {
            var settings = Native.Display.GetAllDisplaySettingsForDisplayAdapter(Name);
            if (Native.Display.GetCurrentDisplaySettingsForDisplayAdapter(Name) is Native.Display.DisplaySettings current)
            {
                settings = settings.Where(setting => setting.refreshRateInHertz == current.refreshRateInHertz && setting.orientation == current.orientation && (setting.fixedOutputOption ?? Native.Display.DisplaySettings.FixedResolutionOutputOption.Default) == Native.Display.DisplaySettings.FixedResolutionOutputOption.Default && setting.MatchesAspectRatio(current)).ToList();
            }
            settings.Sort((a, b) => (int)a.widthInPixels - (int)b.widthInPixels);
            return settings;
        }

        private Native.Display.DisplaySettings? GetDisplaySettingsForZoomPercentage(double percentage)
        {
            if (NormalSettings is Native.Display.DisplaySettings normal)
            {
                var targetWidth = (uint)((double)normal.widthInPixels * percentage);
                var settings = PossibleSettings.Select(setting => (diff: Math.Abs(setting.widthInPixels - targetWidth), setting));
                settings.OrderBy(pair => pair.diff);
                try
                {
                    return settings.First().setting;
                }
                catch
                {
                    return normal;
                }
            }
            return null;
        }

        #endregion

    }

    public static class NativeDisplayExtensions
    {

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

    public class DisplayZoomHandler: SettingsHandler
    {
        public override bool Apply(object? value)
        {
            if (value is double percentage)
            {
                return Display.Primary.Zoom(percentage);
            }
            else
            {
                return false;
            }
        }
    }
}
