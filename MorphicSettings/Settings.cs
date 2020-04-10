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
using Microsoft.Extensions.Logging;

namespace MorphicSettings
{
    public class Settings
    {

        public Settings(ILogger<Settings> logger)
        {
            this.logger = logger;
        }

        private readonly ILogger<Settings> logger;

        public bool Apply(string solution, string preference, object? value)
        {
            if (solution == "com.microsoft.windows.display")
            {
                if (preference == "zoom")
                {
                    if (value is string stringValue)
                    {
                        if (Enum.TryParse<Display.ZoomLevel>(stringValue, true, out var level))
                        {
                            var success = Display.Primary.SetZoomLevel(level);
                            if (!success)
                            {
                                logger.LogError("Failed to set display zoom level");
                            }
                            return success;
                        }
                        else
                        {
                            logger.LogError("Provided zoom level is invalid");
                        }
                    }
                    else
                    {
                        logger.LogError("Provided zoom level is not a string");
                    }
                }
            }
            return false;
        }
    }
}
