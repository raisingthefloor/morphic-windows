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
using System.Threading.Tasks;
using Native = Morphic.Windows.Native;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Morphic.Settings
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

        public int NumberOfZoomLevels
        {
            get
            {
                return PossibleSettings.Count;
            }
        }

        public int CurrentZoomLevel
        {
            get
            {
                if (CurrentSettings is Native.Display.DisplaySettings current)
                {
                    return PossibleSettings.FindIndex(setting => setting.widthInPixels == current.widthInPixels);
                }
                return -1;
            }
        }

        public double CurrentZoomPercentage
        {
            get
            {
                if (NormalSettings is Native.Display.DisplaySettings normal)
                {
                    if (CurrentSettings is Native.Display.DisplaySettings current)
                    {
                        return (double)normal.widthInPixels / (double)current.widthInPixels;
                    }
                }
                return 1.0;

            }
        }

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
                        return (double)normal.widthInPixels / (double)target.widthInPixels;
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
                        return (double)normal.widthInPixels / (double)target.widthInPixels;
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
            IEnumerable<Native.Display.DisplaySettings> settings =
                Native.Display.GetAllDisplaySettingsForDisplayAdapter(Name);

            if (Native.Display.GetCurrentDisplaySettingsForDisplayAdapter(Name) is Native.Display.DisplaySettings current)
            {
                settings = settings.Where(setting => setting.refreshRateInHertz == current.refreshRateInHertz && setting.orientation == current.orientation && (setting.fixedOutputOption ?? Native.Display.DisplaySettings.FixedResolutionOutputOption.Default) == Native.Display.DisplaySettings.FixedResolutionOutputOption.Default && setting.MatchesAspectRatio(current)).ToList();

                // remove the very similar resolutions (favouring the one closest to the current ratio)
                double currentRatio = current.GetAspectRatio();
                settings = settings.GroupBy(s => s.widthInPixels)
                    .Select(g => g.OrderBy(s => Math.Abs(currentRatio - s.GetAspectRatio())).First());
            }

            return settings.OrderBy(s => s.heightInPixels).ToList();
        }

        private Native.Display.DisplaySettings? GetDisplaySettingsForZoomPercentage(double percentage)
        {
            if (NormalSettings is Native.Display.DisplaySettings normal)
            {
                var targetWidth = (uint)((double)normal.widthInPixels / percentage);
                var settings = PossibleSettings.Select(setting => (diff: Math.Abs((int)setting.widthInPixels - (int)targetWidth), setting)).ToList();
                settings.Sort((a, b) => a.diff - b.diff);
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

        public bool CanZoomOut
        {
            get
            {
                if (CurrentSettings is Native.Display.DisplaySettings current)
                {
                    return PossibleSettings.Count > 0 && PossibleSettings.Last().widthInPixels != current.widthInPixels;
                }
                return false;
            }
        }

        public bool CanZoomIn
        {
            get
            {
                if (CurrentSettings is Native.Display.DisplaySettings current)
                {
                    return PossibleSettings.Count > 0 && PossibleSettings[0].widthInPixels != current.widthInPixels;
                }
                return false;
            }
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

    public class DisplayZoomHandler: SettingHandler
    {

        public DisplayZoomHandler(ILogger<DisplayZoomHandler> logger)
        {
            this.logger = logger;
        }

        private readonly ILogger<DisplayZoomHandler> logger;

        public override Task<bool> Apply(object? value)
        {
            if (value is double percentage)
            {
                var result = Display.Primary.Zoom(percentage);
                return Task.FromResult(result);
            }
            else
            {
                if (value is object obj)
                {
                    logger.LogError("Invalid data type for display zoom: {0}", obj.GetType().Name);
                }
                else
                {
                    logger.LogError("Invalid data type for display zoom: null");
                }
                return Task.FromResult(false);
            }
        }

        public override Task<CaptureResult> Capture()
        {
            var result = new CaptureResult();
            result.Value = Display.Primary.CurrentZoomPercentage;
            result.Success = true;
            return Task.FromResult(result);
        }
    }
}
